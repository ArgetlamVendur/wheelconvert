using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WheelConvert.Core.Contracts;

namespace WheelConvert.Core.Settings;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON, tolerantly:
///  - missing file → defaults (and the file is written);
///  - one bad or unknown field → that field falls back to its default, the rest is kept (logged);
///  - property names are matched case-insensitively;
///  - unparseable file → moved aside to "settings.corrupt-&lt;timestamp&gt;-&lt;id&gt;.json", defaults used;
///  - file locked/unreadable (antivirus, sync client) → defaults IN MEMORY, the file is NOT touched and saves are
///    blocked until a later Load succeeds, so a transient lock can never wipe the user's settings.
/// Saves are atomic and flushed to disk.
/// </summary>
public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly PropertyInfo[] SettableProperties = typeof(AppSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite && p.Name != nameof(AppSettings.SchemaVersion))
        .ToArray();

    private readonly string _path;
    private readonly ILog _log;
    private readonly object _gate = new();
    private bool _savesBlocked;

    public JsonSettingsStore(string path, ILog log)
    {
        _path = Path.GetFullPath(path);
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>%LOCALAPPDATA%\WheelConvert\settings.json (or the platform equivalent).</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WheelConvert", "settings.json");

    public string FilePath => _path;

    /// <summary>True when the last Load couldn't read the file; Save will refuse until a Load succeeds.</summary>
    public bool SavesBlocked
    {
        get
        {
            lock (_gate)
            {
                return _savesBlocked;
            }
        }
    }

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                _savesBlocked = false;
                var defaults = new AppSettings();
                TrySave(defaults);
                return defaults;
            }

            string json;
            try
            {
                json = ReadWithRetry();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn($"Settings file could not be read; using defaults without touching it: {_path}", ex);
                _savesBlocked = true;
                return new AppSettings();
            }

            _savesBlocked = false;
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json, documentOptions: DocumentOptions);
            }
            catch (JsonException ex)
            {
                _log.Warn($"Settings file is not valid JSON; moved aside, using defaults: {_path}", ex);
                QuarantineCorruptFile();
                return new AppSettings();
            }

            if (root is not JsonObject obj)
            {
                _log.Warn("Settings file is not a JSON object; moved aside, using defaults.");
                QuarantineCorruptFile();
                return new AppSettings();
            }

            return ReadTolerant(obj).Normalize();
        }
    }

    /// <returns>False if saving is blocked because the file couldn't be read earlier.</returns>
    /// <exception cref="IOException">Disk error while writing.</exception>
    /// <exception cref="UnauthorizedAccessException">Settings folder not writable.</exception>
    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            if (_savesBlocked)
            {
                _log.Warn("Settings save skipped: the existing file couldn't be read, so it is protected from overwrite.");
                return false;
            }

            SaveCore(settings.Normalize());
            return true;
        }
    }

    private AppSettings ReadTolerant(JsonObject obj)
    {
        var settings = new AppSettings();
        // Case-insensitive; if a key appears twice in different casing ("hotkey" and "Hotkey"), the last one wins.
        var byName = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in obj)
        {
            byName[key] = value;
        }
        foreach (var property in SettableProperties)
        {
            var jsonName = JsonOptions.PropertyNamingPolicy!.ConvertName(property.Name);
            if (!byName.TryGetValue(jsonName, out var node) || node is null)
            {
                continue;
            }

            try
            {
                var value = node.Deserialize(property.PropertyType, JsonOptions);
                if (value is not null || Nullable.GetUnderlyingType(property.PropertyType) is not null || !property.PropertyType.IsValueType)
                {
                    // Init-only setters are settable via reflection; the record is not yet shared.
                    property.SetValue(settings, value);
                }
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
            {
                _log.Warn($"Settings field '{jsonName}' is invalid; using its default.", ex);
            }
        }

        return settings;
    }

    private string ReadWithRetry()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }

    private void TrySave(AppSettings settings)
    {
        try
        {
            SaveCore(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Could not write default settings to {_path}", ex);
        }
    }

    private void SaveCore(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonSerializer.Serialize(settings, JsonOptions));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
            {
                File.Replace(temp, _path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, _path, overwrite: false);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp); // Only our own uniquely named temp file.
            }
        }
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            var target = Path.Combine(
                Path.GetDirectoryName(_path)!,
                $"{Path.GetFileNameWithoutExtension(_path)}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json");
            File.Move(_path, target, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Could not move the corrupt settings file aside.", ex);
            _savesBlocked = true; // Don't overwrite what we couldn't preserve.
        }
    }
}
