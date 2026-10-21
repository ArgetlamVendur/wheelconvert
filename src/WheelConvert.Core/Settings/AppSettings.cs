namespace WheelConvert.Core.Settings;

public enum TriggerMode
{
    /// <summary>Shift-drag in Explorer/desktop opens the wheel (WP-02). Default.</summary>
    ShiftDrag = 0,

    /// <summary>Global hotkey opens the wheel at the cursor; user then drags onto it (WP-03).</summary>
    Hotkey = 1,

    /// <summary>Both triggers active.</summary>
    Both = 2,
}

/// <summary>
/// User settings, stored as JSON. Adding a property: give it a safe default and clamp it in
/// <see cref="Normalize"/>. Never rename properties (old files would silently lose the value).
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public TriggerMode Trigger { get; init; } = TriggerMode.ShiftDrag;

    /// <summary>Format: modifiers joined by '+', then a key, e.g. "Ctrl+Alt+Space". Parsed by WP-03.</summary>
    public string Hotkey { get; init; } = "Ctrl+Alt+Space";

    /// <summary>Pixels the mouse must move with Shift held before the wheel opens.</summary>
    public int DragThresholdPx { get; init; } = 10;

    public int MaxConcurrentJobs { get; init; } = 2;

    public int MaxWheelSegments { get; init; } = 6;

    /// <summary>Label in output names: "photo (converted).jpg".</summary>
    public string OutputLabel { get; init; } = "converted";

    public bool StartWithWindows { get; init; }

    /// <summary>Optional explicit path to ffmpeg.exe. Null = search PATH and the winget links folder.</summary>
    public string? FfmpegPath { get; init; }

    /// <summary>Target IDs from TargetCatalog the user has hidden from the wheel.</summary>
    public IReadOnlyList<string> DisabledTargetIds { get; init; } = [];

    public bool ShowSuccessNotifications { get; init; } = true;

    /// <summary>Returns a copy with every value forced into its valid range.</summary>
    public AppSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Trigger = Enum.IsDefined(Trigger) ? Trigger : TriggerMode.ShiftDrag,
        Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? "Ctrl+Alt+Space" : Hotkey.Trim(),
        DragThresholdPx = Math.Clamp(DragThresholdPx, 4, 50),
        MaxConcurrentJobs = Math.Clamp(MaxConcurrentJobs, 1, 8),
        MaxWheelSegments = Math.Clamp(MaxWheelSegments, 2, 8),
        OutputLabel = string.IsNullOrWhiteSpace(OutputLabel) ? "converted" : OutputLabel.Trim(),
        FfmpegPath = string.IsNullOrWhiteSpace(FfmpegPath) ? null : FfmpegPath.Trim(),
        DisabledTargetIds = (DisabledTargetIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
    };
}
