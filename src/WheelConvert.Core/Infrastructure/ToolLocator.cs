namespace WheelConvert.Core.Infrastructure;

/// <summary>
/// Finds external executables (FFmpeg). Order: explicit configured path → PATH → winget "Links" folder
/// (winget adds that to PATH, but a running app keeps its old PATH until restarted, so we check it directly).
/// </summary>
public static class ToolLocator
{
    public static string? Find(string toolName, string? configuredPath = null, string? pathVariable = null, IEnumerable<string>? extraDirectories = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath) ? Path.GetFullPath(configuredPath) : null;
        }

        var fileNames = OperatingSystem.IsWindows()
            ? new[] { toolName + ".exe", toolName }
            : new[] { toolName };

        var directories = (pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(extraDirectories ?? DefaultExtraDirectories());

        foreach (var dir in directories)
        {
            foreach (var name in fileNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir.Trim('"'), name);
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

    private static IEnumerable<string> DefaultExtraDirectories()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links");
    }
}
