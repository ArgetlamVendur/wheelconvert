namespace WheelConvert.Core.Wheel;

/// <summary>
/// Pure maths for the wheel so it can be unit-tested without WPF.
/// Convention: segment 0 is centred at 12 o'clock; indices increase clockwise.
/// Coordinates are offsets from the wheel centre, y pointing DOWN (screen convention), any consistent unit.
/// </summary>
public static class WheelGeometry
{
    /// <summary>Segment index under the point, or -1 if in the centre hole, outside the wheel, or no segments.</summary>
    public static int HitTest(double dx, double dy, int segmentCount, double innerRadius, double outerRadius)
    {
        if (segmentCount <= 0 || double.IsNaN(dx) || double.IsNaN(dy))
        {
            return -1;
        }

        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance < innerRadius || distance > outerRadius)
        {
            return -1;
        }

        var span = 360.0 / segmentCount;
        var angle = AngleFromTop(dx, dy);
        var index = (int)Math.Floor(((angle + (span / 2)) % 360.0) / span);
        return Math.Clamp(index, 0, segmentCount - 1);
    }

    /// <summary>Start angle (degrees clockwise from 12 o'clock, may be negative) and sweep for drawing a segment.</summary>
    public static (double StartDegrees, double SweepDegrees) GetSegmentArc(int index, int segmentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, segmentCount);
        var span = 360.0 / segmentCount;
        return ((index * span) - (span / 2), span);
    }

    /// <summary>Point on a circle at the given clockwise-from-top angle, as an offset from centre (y down).</summary>
    public static (double X, double Y) PointOnCircle(double degreesFromTop, double radius)
    {
        var radians = degreesFromTop * Math.PI / 180.0;
        return (radius * Math.Sin(radians), -radius * Math.Cos(radians));
    }

    /// <summary>Clockwise angle from 12 o'clock in [0, 360).</summary>
    internal static double AngleFromTop(double dx, double dy)
    {
        var degrees = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }
}
