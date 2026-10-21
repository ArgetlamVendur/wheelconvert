using System.Globalization;
using WheelConvert.Core.Contracts;

namespace WheelConvert.Core.Infrastructure;

/// <summary>
/// Plain-text log, one line per event, ISO timestamp first. Rolls to "&lt;name&gt;.1" at 1 MB.
/// Never throws: logging failures are swallowed so they can't break conversions.
/// </summary>
public sealed class FileLog : ILog
{
    public const long MaxBytes = 1024 * 1024;

    private readonly string _path;
    private readonly object _gate = new();

    public FileLog(string path)
    {
        _path = Path.GetFullPath(path);
    }

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WheelConvert", "wheelconvert.log");

    public string FilePath => _path;

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz} {level} {message}");
        if (exception is not null)
        {
            // Full detail (type, message, stack, inner exceptions) on one line.
            line += " | " + exception;
        }

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                RollIfNeeded();
                // FileShare.ReadWrite so a second instance or an open log viewer doesn't block writes.
                using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(line.ReplaceLineEndings(" ⏎ "));
            }
            catch
            {
                // Logging must never throw.
            }
        }
    }

    private void RollIfNeeded()
    {
        var info = new FileInfo(_path);
        if (info.Exists && info.Length > MaxBytes)
        {
            File.Move(_path, _path + ".1", overwrite: true);
        }
    }
}
