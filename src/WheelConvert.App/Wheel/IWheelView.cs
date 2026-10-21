using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Wheel;

/// <summary>Raised when files are dropped on a segment. Paths are only read, never opened by the wheel.</summary>
public sealed class WheelDropEventArgs(IReadOnlyList<string> paths, ConversionTarget target) : EventArgs
{
    public IReadOnlyList<string> Paths { get; } = paths;

    public ConversionTarget Target { get; } = target;
}

/// <summary>
/// Contract between the wheel (WP-04) and the integration layer (WP-11). All members are UI-thread only.
/// </summary>
public interface IWheelView
{
    /// <summary>Called on DragEnter with ALL dragged paths; returns the segments to show (may be empty). Fast: no file I/O.</summary>
    Func<IReadOnlyList<string>, IReadOnlyList<ConversionTarget>>? TargetsProvider { get; set; }

    /// <summary>Show centred at a point in PHYSICAL pixels. If already visible, move it.</summary>
    void ShowAt(ScreenPoint point);

    /// <summary>Hide without dropping. Safe to call when hidden.</summary>
    void HideWheel();

    bool IsOpen { get; }

    event EventHandler<WheelDropEventArgs>? Dropped;
}
