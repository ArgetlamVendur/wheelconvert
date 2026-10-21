using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Triggers;

/// <summary>
/// STUB: WP-02 (docs/modules/WP-02-shift-drag-trigger.md). Global low-level mouse hook that detects
/// Shift + left-drag that started over Explorer or the desktop.
/// </summary>
public sealed class ShiftDragTrigger : ITrigger
{
    private readonly int _dragThresholdPx;

    public ShiftDragTrigger(int dragThresholdPx)
    {
        _dragThresholdPx = dragThresholdPx;
    }

    public string Name => "Shift-drag";

    public string? LastError { get; private set; }

    /// <summary>Last time the hook callback ran (UTC). WP-11 uses it to detect a hook Windows silently removed.</summary>
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

    // Stub never raises these; the pragmas go away when WP-02 implements the hook.
#pragma warning disable CS0067
    public event EventHandler<WheelRequestedEventArgs>? WheelRequested;

    public event EventHandler? WheelDismissRequested;
#pragma warning restore CS0067

    // TODO(WP-02): SetWindowsHookEx(WH_MOUSE_LL) on the UI thread; keep the delegate in a field.
    public bool Start()
    {
        _ = _dragThresholdPx;
        LastError = "Shift-drag is not implemented yet.";
        return false; // Stub: report failure so the integration layer falls back to the hotkey (ADR-006).
    }

    // TODO(WP-02): UnhookWindowsHookEx.
    public void Stop()
    {
    }

    public void Dispose() => Stop();
}
