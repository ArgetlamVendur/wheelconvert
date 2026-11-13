namespace WheelConvert.Core.Infrastructure;

/// <summary>
/// Finds external executables (FFmpeg). Order: explicit configured path → this process's PATH →
/// (Windows) the current user PATH and machine PATH from the registry → winget "Links" folders.
/// A running app keeps the PATH it started with, so a tool installed later (e.g. via winget) is only
/// visible through the registry values or the Links folders; re-reading them avoids an app restart.
/// </summary>
public static class ToolLocator
{
    /// <summary>
    /// Passing <paramref name="pathVariable"/> (even empty) makes the search deterministic: only that string plus
    /// <paramref name="extraDirectories"/> (default: winget Links folders) are searched, never the registry.
    /// </summary>
    public static string? Find(string toolName, string? configuredPath = null, string? pathVariable = null, IEnumerable<string>? extraDirectories = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (pathVariable is not null)
        {
            return Find(toolName, configuredPath, pathVariable, null, null, extraDirectories ?? DefaultExtraDirectories());
        }

        // Only read the registry when we will actually search (a configured path never falls back).
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Find(toolName, configuredPath, null, null, null, []);
        }

        string? userPath = null;
        string? machinePath = null;
        if (OperatingSystem.IsWindows())
        {
            userPath = ReadRegistryPath(EnvironmentVariableTarget.User);
            machinePath = ReadRegistryPath(EnvironmentVariableTarget.Machine);
        }

        return Find(
            toolName,
            configuredPath,
            Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
            userPath,
            machinePath,
            extraDirectories ?? DefaultExtraDirectories());
    }

    /// <summary>Core search with every PATH source injected, so tests can simulate the registry values.</summary>
    internal static string? Find(
        string toolName,
        string? configuredPath,
        string? processPath,
        string? userPath,
        string? machinePath,
        IEnumerable<string> extraDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath) ? Path.GetFullPath(configuredPath) : null;
        }

        var fileNames = OperatingSystem.IsWindows()
            ? new[] { toolName + ".exe", toolName }
            : new[] { toolName };

        foreach (var dir in SearchDirectories(processPath, userPath, machinePath, extraDirectories))
        {
            foreach (var name in fileNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir, name);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    /// <summary>The ordered, trimmed, de-duplicated folders that are probed (internal so tests can assert on it).</summary>
    internal static IEnumerable<string> SearchDirectories(
        string? processPath,
        string? userPath,
        string? machinePath,
        IEnumerable<string> extraDirectories)
    {
        var directories = SplitPath(processPath)
            .Concat(SplitPath(userPath).Select(Environment.ExpandEnvironmentVariables))
            .Concat(SplitPath(machinePath).Select(Environment.ExpandEnvironmentVariables))
            .Concat(extraDirectories);

        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var raw in directories)
        {
            var dir = raw.Trim().Trim('"');
            if (dir.Length == 0)
            {
                continue;
            }

            // Compare without trailing separators so "C:\Tools" and "C:\Tools\" are probed once.
            var key = dir.Length > 1 ? dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : dir;
            if (seen.Add(key.Length == 0 ? dir : key))
            {
                yield return dir;
            }
        }
    }

    private static string[] SplitPath(string? value) =>
        (value ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? ReadRegistryPath(EnvironmentVariableTarget target) =>
        ReadRegistryPath(() => Environment.GetEnvironmentVariable("PATH", target));

    internal static string? ReadRegistryPath(Func<string?> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Locked-down machines may deny registry reads (or a key may vanish mid-read); the process PATH
            // and Links folders still apply, so this must never surface to CheckHealth().
            return null;
        }
    }

    private static IEnumerable<string> DefaultExtraDirectories()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links");
        }

        // Machine-wide winget installs (run elevated / --scope machine) link here.
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            yield return Path.Combine(programFiles, "WinGet", "Links");
        }
    }
}
