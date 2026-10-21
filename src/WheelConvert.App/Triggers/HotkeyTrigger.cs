using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Triggers;

/// <summary>
/// STUB: WP-03 (docs/modules/WP-03-hotkey-trigger.md). RegisterHotKey on a message-only window; on press,
/// opens the wheel at the cursor. The user then drags a file onto the open wheel.
/// </summary>
public sealed class HotkeyTrigger : ITrigger
{
    private readonly string _hotkey;

    /// <param name="hotkey">AppSettings.Hotkey, e.g. "Ctrl+Alt+Space".</param>
    public HotkeyTrigger(string hotkey)
    {
        _hotkey = hotkey;
    }

    public string Name => "Hotkey";

    public string? LastError { get; private set; }

#pragma warning disable CS0067
    public event EventHandler<WheelRequestedEventArgs>? WheelRequested;

    public event EventHandler? WheelDismissRequested;
#pragma warning restore CS0067

    // TODO(WP-03): parse _hotkey, create HwndSource message-only window, RegisterHotKey, listen for WM_HOTKEY.
    // On failure set LastError to e.g. "Ctrl+Alt+Space is already used by another app." and return false.
    public bool Start()
    {
        _ = _hotkey;
        LastError = "Hotkey is not implemented yet.";
        return false;
    }

    // TODO(WP-03): UnregisterHotKey, dispose HwndSource.
    public void Stop()
    {
    }

    public void Dispose() => Stop();
}
