using Microsoft.Win32;

namespace WheelConvert.App.Tray;

/// <summary>
/// WP-05 req. 4: "Start with Windows" via HKCU\Software\Microsoft\Windows\CurrentVersion\Run, value "WheelConvert".
/// Per-user only: never touches HKLM, so it never needs elevation (AGENTS §4.7).
/// </summary>
public sealed class StartupRegistration
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "WheelConvert";

    private readonly string _keyPath;
    private readonly string _valueName;
    private readonly string? _exePath;

    /// <summary>The real Run key and the running exe.</summary>
    public StartupRegistration()
        : this(RunKeyPath, DefaultValueName, Environment.ProcessPath)
    {
    }

    /// <summary>Tests point this at a scratch key under HKCU so the real Run key is never touched.</summary>
    public StartupRegistration(string keyPath, string valueName, string? exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        _keyPath = keyPath;
        _valueName = valueName;
        _exePath = exePath;
    }

    /// <summary>The Run value: the full exe path in quotes (paths with spaces must be quoted for Windows to start them).</summary>
    public string? ExpectedValue => string.IsNullOrWhiteSpace(_exePath) ? null : $"\"{_exePath}\"";

    /// <summary>
    /// True only when the value points at this exe. A value left by an older copy in another folder reads as off,
    /// so ticking the item rewrites it to the current path.
    /// </summary>
    public bool IsEnabled()
    {
        var expected = ExpectedValue;
        if (expected is null)
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
        return key?.GetValue(_valueName) is string value
            && string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <exception cref="InvalidOperationException">The exe path is unknown.</exception>
    public void Enable()
    {
        var expected = ExpectedValue ?? throw new InvalidOperationException("The app's own path is unknown.");
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
        key.SetValue(_valueName, expected, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
