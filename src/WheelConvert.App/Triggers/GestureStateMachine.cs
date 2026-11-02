using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Triggers;

/// <summary>Mouse events the Shift-drag gesture cares about (left button only).</summary>
public enum MouseEvent
{
    LeftDown,
    Move,
    LeftUp,
}

/// <summary>What the trigger should do after an event.</summary>
public enum GestureAction
{
    None,
    OpenWheel,
    Dismiss,
}

/// <summary>Result of one <see cref="GestureStateMachine.Process"/> call. <see cref="Point"/> is set for OpenWheel.</summary>
public readonly record struct GestureOutput(GestureAction Action, ScreenPoint Point)
{
    public static GestureOutput None => default;
}

/// <summary>
/// Shift-drag gesture logic (WP-02 req. 3). Pure: no Win32 calls, no clock, no allocation, so it can be unit tested
/// and is cheap enough to run inside the low-level mouse hook. Not thread-safe; the hook calls it on one thread.
/// </summary>
public sealed class GestureStateMachine
{
    private readonly long _thresholdSquared;
    private ScreenPoint _downPoint;
    private bool _armed;
    private bool _fired;

    /// <param name="dragThresholdPx">The wheel opens once the cursor is MORE than this far (physical px) from the mouse-down point.</param>
    public GestureStateMachine(int dragThresholdPx)
    {
        long threshold = Math.Max(0, dragThresholdPx);
        _thresholdSquared = threshold * threshold;
    }

    /// <summary>
    /// True while a Move could still open the wheel. The hook uses it to skip the Shift-key query on ordinary moves.
    /// </summary>
    public bool IsWaitingForDrag => _armed && !_fired;

    /// <param name="startedOverExplorer">Only read on <see cref="MouseEvent.LeftDown"/> (req. 4: checked at mouse-down only).</param>
    public GestureOutput Process(MouseEvent kind, ScreenPoint point, bool shiftDown, bool startedOverExplorer)
    {
        switch (kind)
        {
            case MouseEvent.LeftDown:
                _downPoint = point;
                _armed = startedOverExplorer;
                _fired = false;
                return GestureOutput.None;

            case MouseEvent.Move:
                if (!IsWaitingForDrag || !shiftDown || !IsBeyondThreshold(point))
                {
                    return GestureOutput.None;
                }

                _fired = true;
                return new GestureOutput(GestureAction.OpenWheel, point);

            case MouseEvent.LeftUp:
                var fired = _fired;
                _armed = false;
                _fired = false;
                return fired ? new GestureOutput(GestureAction.Dismiss, point) : GestureOutput.None;

            default:
                return GestureOutput.None;
        }
    }

    private bool IsBeyondThreshold(ScreenPoint point)
    {
        // long: squared screen distances across a large virtual desktop can overflow int.
        long dx = point.X - (long)_downPoint.X;
        long dy = point.Y - (long)_downPoint.Y;
        return dx * dx + dy * dy > _thresholdSquared;
    }
}
