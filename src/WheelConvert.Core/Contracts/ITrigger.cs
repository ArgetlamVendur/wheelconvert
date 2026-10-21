namespace WheelConvert.Core.Contracts;

/// <summary>Screen position in PHYSICAL pixels (as Win32 reports them). The wheel converts to DIPs.</summary>
public readonly record struct ScreenPoint(int X, int Y);

public sealed class WheelRequestedEventArgs(ScreenPoint point) : EventArgs
{
    public ScreenPoint Point { get; } = point;
}

/// <summary>
/// Something that decides when the wheel should open: Shift-drag hook (WP-02) or hotkey (WP-03).
/// RULES: events are raised on the UI thread. Start/Stop are idempotent. Dispose unhooks everything.
/// </summary>
public interface ITrigger : IDisposable
{
    string Name { get; }

    /// <summary>Why the last <see cref="Start"/> failed, in plain language; null when running fine.</summary>
    string? LastError { get; }

    /// <summary>Open the wheel centred at the point.</summary>
    event EventHandler<WheelRequestedEventArgs>? WheelRequested;

    /// <summary>Close the wheel without converting (mouse released elsewhere, hotkey pressed again, …).</summary>
    event EventHandler? WheelDismissRequested;

    /// <summary>Installs the hook / registers the hotkey. Never throws. Returns false (and sets <see cref="LastError"/>) on failure.</summary>
    bool Start();

    void Stop();
}
