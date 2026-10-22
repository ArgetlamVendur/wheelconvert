using System.Windows;
using System.Windows.Media;
using WheelConvert.Core.Wheel;

namespace WheelConvert.App.Wheel;

/// <summary>Builds the drawing for one wheel segment. Angles and offsets come from Core's WheelGeometry.</summary>
internal static class SegmentVisuals
{
    /// <summary>Annular sector for segment <paramref name="index"/>, in coordinates centred on <paramref name="center"/>.</summary>
    public static Geometry CreateSector(int index, int count, Point center, double innerRadius, double outerRadius)
    {
        var (start, sweep) = WheelGeometry.GetSegmentArc(index, count);
        var mid = start + (sweep / 2);
        var end = start + sweep;

        // Each arc is drawn as two halves: a single arc can't describe a full circle (one segment = 360°),
        // and halves are never larger than 180°, so IsLargeArc is always false.
        var outerSize = new Size(outerRadius, outerRadius);
        var innerSize = new Size(innerRadius, innerRadius);
        var figure = new PathFigure { StartPoint = At(center, start, outerRadius), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(At(center, mid, outerRadius), outerSize, 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new ArcSegment(At(center, end, outerRadius), outerSize, 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(At(center, end, innerRadius), true));
        figure.Segments.Add(new ArcSegment(At(center, mid, innerRadius), innerSize, 0, false, SweepDirection.Counterclockwise, true));
        figure.Segments.Add(new ArcSegment(At(center, start, innerRadius), innerSize, 0, false, SweepDirection.Counterclockwise, true));

        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>Centre point of the segment's label, half-way between the inner and outer radius.</summary>
    public static Point LabelCenter(int index, int count, Point center, double innerRadius, double outerRadius)
    {
        var (start, sweep) = WheelGeometry.GetSegmentArc(index, count);
        return At(center, start + (sweep / 2), (innerRadius + outerRadius) / 2);
    }

    private static Point At(Point center, double degreesFromTop, double radius)
    {
        var (x, y) = WheelGeometry.PointOnCircle(degreesFromTop, radius);
        return new Point(center.X + x, center.Y + y);
    }
}
