using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Core.Output;

/// <summary>
/// Implements the never-overwrite, never-touch-the-source output policy (ADR-002).
/// Temp files live next to the source so the final rename is a same-volume atomic move.
/// Only paths this instance issued can be committed or discarded.
/// </summary>
public sealed partial class SafeOutputWriter : IOutputWriter
{
    public const string TempPrefix = ".~wc-";
    private const int MaxNameAttempts = 999;
    private const int SharingRetries = 5;
    private static readonly TimeSpan SharingRetryDelay = TimeSpan.FromMilliseconds(150);

    // Serialises "pick a free name + move" per folder within this process. Windows' MoveFileEx is atomic on its own;
    // .NET on Unix checks-then-renames, so without this two concurrent commits could pick the same name.
    private static readonly ConcurrentDictionary<string, object> FolderLocks = new(StringComparer.OrdinalIgnoreCase);

    // temp path → source directory it was issued for.
    private readonly ConcurrentDictionary<string, string> _issued = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True only for names of the exact form ".~wc-&lt;32 hex&gt;&lt;ext&gt;" (never a user's own ".~wc-notes.txt").</summary>
    public static bool IsTempFile(string path) => TempNamePattern().IsMatch(Path.GetFileName(path ?? string.Empty));

    /// <summary>
    /// Start-up clean-up (WP-11): deletes one stale temp file left by a crashed run.
    /// Only exact temp names, only if older than <paramref name="minimumAge"/>. Never throws.
    /// </summary>
    public static bool TryDeleteStaleTemp(string path, TimeSpan minimumAge)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!IsTempFile(full))
            {
                return false;
            }

            var info = new FileInfo(full);
            if (!info.Exists || DateTime.UtcNow - info.LastWriteTimeUtc < minimumAge)
            {
                return false;
            }

            info.Delete();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public string CreateTempPath(string sourcePath, string outputExtension)
    {
        var directory = SourceDirectory(sourcePath);
        var ext = RequireExtension(outputExtension);
        var temp = Path.Combine(directory, $"{TempPrefix}{Guid.NewGuid():N}{ext}");
        _issued[temp] = directory;
        return temp;
    }

    public string Commit(string tempPath, string sourcePath, string outputExtension, string label)
    {
        var temp = Path.GetFullPath(tempPath);
        var source = Path.GetFullPath(sourcePath);
        var directory = SourceDirectory(source);

        if (!_issued.TryGetValue(temp, out var issuedFor))
        {
            throw new InvalidOperationException($"Refusing to commit a file this writer did not issue: {temp}");
        }

        if (!string.Equals(issuedFor, directory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Temp file and source are in different folders; the rename would not be atomic.");
        }

        if (!File.Exists(temp))
        {
            throw new FileNotFoundException("Converter did not produce the temp file.", temp);
        }

        var ext = RequireExtension(outputExtension);
        var stem = Path.GetFileNameWithoutExtension(source);
        var cleanLabel = SanitizeLabel(label);

        lock (FolderLocks.GetOrAdd(directory, _ => new object()))
        {
            return CommitLocked(temp, source, directory, ext, stem, cleanLabel);
        }
    }

    private string CommitLocked(string temp, string source, string directory, string ext, string stem, string cleanLabel)
    {
        for (var attempt = 1; attempt <= MaxNameAttempts; attempt++)
        {
            var suffix = attempt == 1 ? cleanLabel : $"{cleanLabel} {attempt}";
            var candidate = Path.Combine(directory, $"{stem} ({suffix}){ext}");

            if (PathsEqual(candidate, source) || File.Exists(candidate) || Directory.Exists(candidate))
            {
                continue;
            }

            try
            {
                // overwrite:false is the guarantee; Exists() above is only a fast path.
                MoveWithSharingRetry(temp, candidate);
                _issued.TryRemove(temp, out _);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate) || Directory.Exists(candidate))
            {
                // Lost a race with another writer; try the next name.
            }
        }

        throw new IOException($"No free output name for '{stem}' after {MaxNameAttempts} attempts.");
    }

    public bool Discard(string tempPath)
    {
        try
        {
            var temp = Path.GetFullPath(tempPath);
            if (!_issued.TryRemove(temp, out _))
            {
                return false;
            }

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    if (!File.Exists(temp))
                    {
                        return false;
                    }

                    File.Delete(temp);
                    return true;
                }
                catch (IOException) when (attempt < SharingRetries)
                {
                    Thread.Sleep(SharingRetryDelay); // Antivirus or indexer briefly holding the file.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    internal static string SanitizeLabel(string label)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).ToHashSet();
        var cleaned = new string((label ?? string.Empty).Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length == 0 ? OptionKeys.DefaultOutputLabel : cleaned;
    }

    private static void MoveWithSharingRetry(string from, string to)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(from, to, overwrite: false);
                return;
            }
            catch (IOException) when (attempt < SharingRetries && !File.Exists(to) && File.Exists(from))
            {
                Thread.Sleep(SharingRetryDelay); // Sharing violation from antivirus/indexer; target still free.
            }
        }
    }

    private static string SourceDirectory(string sourcePath) =>
        Path.GetDirectoryName(Path.GetFullPath(sourcePath))
        ?? throw new ArgumentException("Source has no parent directory.", nameof(sourcePath));

    private static string RequireExtension(string outputExtension)
    {
        var ext = FileClassifier.NormalizeExtension(outputExtension);
        return ext.Length == 0 ? throw new ArgumentException("Output extension is required.", nameof(outputExtension)) : ext;
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^\.~wc-[0-9a-f]{32}\.[A-Za-z0-9]{1,8}$", RegexOptions.CultureInvariant)]
    private static partial Regex TempNamePattern();
}
