using WheelConvert.App.Triggers;
using WheelConvert.Core.Contracts;
using Xunit;

namespace WheelConvert.App.Tests;

public class GestureStateMachineTests
{
    private const int Threshold = 10;
    private static readonly ScreenPoint Origin = new(100, 100);

    private static GestureOutput Down(GestureStateMachine m, ScreenPoint p, bool overExplorer = true)
        => m.Process(MouseEvent.LeftDown, p, shiftDown: true, startedOverExplorer: overExplorer);

    private static GestureOutput Move(GestureStateMachine m, ScreenPoint p, bool shift = true)
        => m.Process(MouseEvent.Move, p, shiftDown: shift, startedOverExplorer: false);

    private static GestureOutput Up(GestureStateMachine m, ScreenPoint p)
        => m.Process(MouseEvent.LeftUp, p, shiftDown: true, startedOverExplorer: false);

    [Fact]
    public void ShiftDrag_BeyondThreshold_OpensWheelAtCurrentPoint()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);

        var output = Move(m, new ScreenPoint(111, 100));

        Assert.Equal(GestureAction.OpenWheel, output.Action);
        Assert.Equal(new ScreenPoint(111, 100), output.Point);
    }

    [Fact]
    public void Drag_WithoutShift_NeverOpensWheel()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);

        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(150, 100), shift: false).Action);
        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(200, 200), shift: false).Action);
        Assert.Equal(GestureAction.None, Up(m, new ScreenPoint(200, 200)).Action);
    }

    [Fact]
    public void ShiftMove_AtOrBelowThreshold_DoesNotOpenWheel()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);

        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(105, 105)).Action);  // ~7 px
        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(110, 100)).Action);  // exactly 10 px: not "more than"
        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(100, 90)).Action);
    }

    [Fact]
    public void ShiftDrag_FiresExactlyOncePerDrag()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);

        var opens = 0;
        for (var x = 101; x <= 300; x++)
        {
            if (Move(m, new ScreenPoint(x, 100)).Action == GestureAction.OpenWheel)
            {
                opens++;
            }
        }

        Assert.Equal(1, opens);
    }

    [Fact]
    public void MouseDown_NotOverExplorer_IsNotArmed()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin, overExplorer: false);

        Assert.False(m.IsWaitingForDrag);
        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(200, 100)).Action);
        Assert.Equal(GestureAction.None, Up(m, new ScreenPoint(200, 100)).Action);
    }

    [Fact]
    public void LeftUp_AfterFiring_Dismisses()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);
        Move(m, new ScreenPoint(150, 100));

        Assert.Equal(GestureAction.Dismiss, Up(m, new ScreenPoint(150, 100)).Action);
    }

    [Fact]
    public void LeftUp_WithoutFiring_DoesNothing()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);
        Move(m, new ScreenPoint(105, 100));

        Assert.Equal(GestureAction.None, Up(m, new ScreenPoint(105, 100)).Action);
    }

    [Fact]
    public void SecondDrag_AfterReset_FiresAgain()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);
        Move(m, new ScreenPoint(150, 100));
        Up(m, new ScreenPoint(150, 100));

        var second = new ScreenPoint(400, 400);
        Down(m, second);
        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(405, 400)).Action); // threshold measured from the new down point
        var output = Move(m, new ScreenPoint(400, 420));

        Assert.Equal(GestureAction.OpenWheel, output.Action);
        Assert.Equal(GestureAction.Dismiss, Up(m, new ScreenPoint(400, 420)).Action);
    }

    [Fact]
    public void ShiftPressedMidDrag_OpensWheelOnNextMove()
    {
        var m = new GestureStateMachine(Threshold);
        Down(m, Origin);
        Assert.Equal(GestureAction.None, Move(m, new ScreenPoint(150, 100), shift: false).Action);

        Assert.Equal(GestureAction.OpenWheel, Move(m, new ScreenPoint(151, 100)).Action);
    }
}
