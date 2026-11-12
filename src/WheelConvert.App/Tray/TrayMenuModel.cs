using WheelConvert.Core.Contracts;
using WheelConvert.Core.Settings;
using Forms = System.Windows.Forms;

namespace WheelConvert.App.Tray;

/// <summary>
/// WP-05: what the tray menu and balloons show, computed from settings and converter health.
/// Pure (no UI, no I/O) so it can be unit-tested; <see cref="TrayIcon"/> only renders it.
/// </summary>
public sealed record TrayMenuModel
{
    /// <summary>NotifyIcon.Text throws above 63 characters (req. 6).</summary>
    public const int MaxTooltipLength = 63;

    /// <summary>Shell limits for balloon title and text (NOTIFYICONDATA szInfoTitle / szInfo, minus the terminator).</summary>
    public const int MaxBalloonTitleLength = 63;
    public const int MaxBalloonTextLength = 255;

    /// <summary>AGENTS §6: user-facing text is at most 120 characters.</summary>
    public const int MaxUserTextLength = 120;

    public const string AppName = "WheelConvert";

    private static readonly string DefaultHotkey = new AppSettings().Hotkey;

    private TrayMenuModel(string header, string howTo, TriggerMode selectedTrigger, IReadOnlyList<string> statusLines, bool allReady)
    {
        AllReady = allReady;
        Header = header;
        HowTo = howTo;
        SelectedTrigger = selectedTrigger;
        StatusLines = statusLines;
    }

    /// <summary>Disabled first menu line, e.g. "WheelConvert 0.1.0".</summary>
    public string Header { get; }

    /// <summary>Text of the "How to use" notification; matches the trigger mode and the configured hotkey.</summary>
    public string HowTo { get; }

    /// <summary>The radio item to check: the saved mode. (The hook-failed fallback isn't visible here; see PR follow-ups.)</summary>
    public TriggerMode SelectedTrigger { get; }

    /// <summary>One line per unavailable converter, or a single "all ready" line.</summary>
    public IReadOnlyList<string> StatusLines { get; }

    /// <summary>True when every converter is available (the status lines are then informational only).</summary>
    public bool AllReady { get; }

    public const string AllReadyLine = "All converters are ready";

    public static TrayMenuModel Create(
        AppSettings settings,
        IReadOnlyList<(string ConverterId, ConverterHealth Health)> unavailable,
        string? version)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(unavailable);
        var normalized = settings.Normalize();

        var lines = unavailable.Select(u => StatusLine(u.ConverterId, u.Health, normalized)).ToList();
        IReadOnlyList<string> statusLines = lines.Count == 0 ? [AllReadyLine] : lines;

        return new TrayMenuModel(HeaderText(version), HowToText(normalized), normalized.Trigger, statusLines, lines.Count == 0);
    }

    public static string HeaderText(string? version)
    {
        // The SDK appends "+<commit hash>" to the informational version; users don't need it.
        var clean = version?.Split('+')[0].Trim();
        return string.IsNullOrEmpty(clean) ? AppName : $"{AppName} {clean}";
    }

    public static string HowToText(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var hotkey = string.IsNullOrWhiteSpace(settings.Hotkey) ? DefaultHotkey : settings.Hotkey.Trim();
        var text = settings.Trigger switch
        {
            TriggerMode.Hotkey => $"Press {hotkey} to open the wheel, then drag a file onto it.",
            TriggerMode.Both => $"Hold Shift and drag a file, or press {hotkey} and drag a file onto the wheel.",
            _ => "Hold Shift and drag a file to open the wheel. Drop it on a segment to convert.",
        };
        return Fit(text, MaxUserTextLength);
    }

    public static string StatusLine(string converterId, ConverterHealth health, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(settings);
        var name = DisplayName(converterId);

        string detail;
        if (string.Equals(converterId, "media", StringComparison.Ordinal) && health.Reason == ConversionError.ToolMissing)
        {
            detail = string.IsNullOrWhiteSpace(settings.FfmpegPath)
                ? "install FFmpeg (winget install Gyan.FFmpeg)"
                : "the configured FFmpeg path wasn't found";
        }
        else if (health.Reason == ConversionError.NotImplemented)
        {
            detail = "not available in this version yet";
        }
        else
        {
            detail = string.IsNullOrWhiteSpace(health.Message) ? "not available" : health.Message.Trim();
        }

        return Fit($"{name}: {detail}", MaxUserTextLength);
    }

    public static string DisplayName(string converterId) => converterId switch
    {
        "image" => "Images",
        "media" => "Audio/video",
        "pdf" => "PDF",
        "qr" => "QR codes",
        _ => converterId,
    };

    /// <summary>Success balloons are optional; warnings and errors always show.</summary>
    public static bool ShouldShow(NotificationKind kind, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return kind != NotificationKind.Success || settings.ShowSuccessNotifications;
    }

    public static Forms.ToolTipIcon BalloonIcon(NotificationKind kind) => kind switch
    {
        NotificationKind.Warning => Forms.ToolTipIcon.Warning,
        NotificationKind.Error => Forms.ToolTipIcon.Error,
        _ => Forms.ToolTipIcon.Info,
    };

    /// <summary>Message when "Open settings file" / "Open log" points at a file that isn't there.</summary>
    public static string MissingFileText(string what) => Fit($"The {what} doesn't exist yet.", MaxUserTextLength);

    /// <summary>Cuts text to <paramref name="max"/> characters, ending with "…" when cut.</summary>
    public static string Fit(string? text, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        var value = text ?? string.Empty;
        return value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");
    }
}
