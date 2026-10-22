using System.IO;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Output;

namespace WheelConvert.App.Composition;

/// <summary>
/// WP-11 req. 10 (ADR-002): remembers which folders conversions wrote to, so the next start-up can remove stale
/// ".~wc-" temp files a crash left there. Only those folders are scanned (top level only), never whole drives,
/// and only exact temp names older than an hour are deleted (<see cref="SafeOutputWriter.TryDeleteStaleTemp"/>).
/// Thread-safe: <see cref="Record"/> is called from queue worker threads.
/// </summary>
public sealed class RecentFolders(string filePath, ILog log)
{
    private const int MaxFolders = 50;
    private static readonly TimeSpan StaleAge = TimeSpan.FromHours(1);

    private readonly object _gate = new();

    // Most recent first.
    private readonly List<string> _folders = [];

    /// <summary>%LOCALAPPDATA%\WheelConvert\recent-folders.txt.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WheelConvert", "recent-folders.txt");

    /// <summary>
    /// Remembers the folder of <paramref name="sourcePath"/>. Call before the converter runs (temps are created there),
    /// so the folder is on disk even if the app crashes mid-conversion. Never throws.
    /// </summary>
    public void Record(string sourcePath)
    {
        string? folder;
        try
        {
            folder = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        if (folder is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_folders.Count > 0 && string.Equals(_folders[0], folder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _folders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
            _folders.Insert(0, folder);
            TrimAndSaveLocked();
        }
    }

    /// <summary>
    /// Start-up clean-up. Run in the background (folders may be on slow or offline network drives). Never throws.
    /// Folders from earlier runs that still hold a (younger) temp file stay on the list for the next start-up.
    /// </summary>
    public void CleanUpStaleTemps()
    {
        var keep = new List<string>();
        var deleted = 0;
        foreach (var folder in ReadPreviousFolders())
        {
            var (removed, remaining) = CleanFolder(folder);
            deleted += removed;
            if (remaining)
            {
                keep.Add(folder);
            }
        }

        if (deleted > 0)
        {
            log.Info($"Removed {deleted} stale temp file(s) left by an earlier run.");
        }

        lock (_gate)
        {
            // Folders recorded this session come first; previous ones fill the remaining places.
            foreach (var folder in keep)
            {
                if (!_folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    _folders.Add(folder);
                }
            }

            TrimAndSaveLocked();
        }
    }

    private List<string> ReadPreviousFolders()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            return File.ReadAllLines(filePath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && Path.IsPathFullyQualified(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxFolders)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warn($"Could not read {filePath}; skipping temp clean-up.", ex);
            return [];
        }
    }

    private (int Removed, bool Remaining) CleanFolder(string folder)
    {
        var removed = 0;
        var remaining = false;
        try
        {
            if (!Directory.Exists(folder))
            {
                return (0, false);
            }

            foreach (var file in Directory.EnumerateFiles(folder, SafeOutputWriter.TempPrefix + "*", SearchOption.TopDirectoryOnly))
            {
                if (!SafeOutputWriter.IsTempFile(file))
                {
                    continue; // A user's own file that merely starts with ".~wc-".
                }

                if (SafeOutputWriter.TryDeleteStaleTemp(file, StaleAge))
                {
                    removed++;
                }
                else
                {
                    remaining = true; // Too young (maybe still in use) or locked: try again next start-up.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log.Warn($"Could not scan {folder} for stale temp files.", ex);
            remaining = true;
        }

        return (removed, remaining);
    }

    private void TrimAndSaveLocked()
    {
        if (_folders.Count > MaxFolders)
        {
            _folders.RemoveRange(MaxFolders, _folders.Count - MaxFolders);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!); // filePath is always a full path under %LOCALAPPDATA%.
            File.WriteAllLines(filePath, _folders);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warn($"Could not write {filePath}.", ex);
        }
    }
}
