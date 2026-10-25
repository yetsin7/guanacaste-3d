using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Temporary witness dimensions for the selected wall, door, or window. They show the clear
/// distance from the element to the nearest wall on each side, and for a hosted opening its
/// distances to the host wall's ends. They follow a drag in progress and are never saved.
/// Elements are measured by their plan bounding boxes, which is exact for orthogonal walls.
/// </summary>
public partial class MainWindow
{
    /// <summary>Distance from the wall face to the dimension line, in meters, so lines never touch the geometry.</summary>
    private const double DimensionOffset = 0.45;

    /// <summary>Gaps smaller than this are not dimensioned, which hides dimensions between touching walls.</summary>
    private const double DimensionMinimum = 0.01;

    /// <summary>Axis-aligned plan bounds of a model element, in meters.</summary>
    private readonly record struct ElementBounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public ElementBounds Shifted(double deltaX, double deltaY) =>
            new(MinX + deltaX, MinY + deltaY, MaxX + deltaX, MaxY + deltaY);
    }

    /// <summary>Draws the temporary dimensions of the selected wall or hosted opening on the active level.</summary>
    private void RenderTemporaryDimensions()
    {
        if (_drawingWall || _selectedWallId is not Guid wallId) return;
        var host = _session.Document.Walls.FirstOrDefault(wall => wall.Id == wallId);
        if (host is null || host.LevelId != _activeLevelId) return;
        if (_selectedOpeningId is Guid openingId &&
            _session.Document.Openings.FirstOrDefault(opening => opening.Id == openingId) is { } opening)
            RenderOpeningDimensions(host, opening);
        else if (_selectedOpeningId is null)
            RenderWallDimensions(host);
    }

    /// <summary>Dimensions a wall's length and the clear gaps around its moved footprint.</summary>
    private void RenderWallDimensions(Wall wall)
    {
        var (deltaX, deltaY) = CurrentMoveDelta();
        AddGapDimensions(WallBounds(wall).Shifted(deltaX, deltaY), OtherWallBounds(wall.Id));

        var (axisX, axisY) = WallAxis(wall);
        var origin = new Point2(wall.Start.X + deltaX, wall.Start.Y + deltaY);
        var across = wall.Thickness / 2 + DimensionOffset;
        AddDimensionLine(
            WallPoint(origin, axisX, axisY, 0, across),
            WallPoint(origin, axisX, axisY, wall.Length, across),
            wall.Length);
    }

    /// <summary>
    /// Dimensions a hosted opening's position along its host and its width, plus the clear
    /// gaps around it. A drag slides the opening along the wall, so only the along-wall part
    /// of the delta moves it, the same rule the move tool applies.
    /// </summary>
    private void RenderOpeningDimensions(Wall host, WallOpening opening)
    {
        var (deltaX, deltaY) = CurrentMoveDelta();
        var (axisX, axisY) = WallAxis(host);
        var offset = opening.Offset + deltaX * axisX + deltaY * axisY;
        var begin = offset - opening.Width / 2;
        var end = offset + opening.Width / 2;
        var halfThickness = host.Thickness / 2;

        var corners = new[]
        {
            WallPoint(host.Start, axisX, axisY, begin, -halfThickness),
            WallPoint(host.Start, axisX, axisY, end, -halfThickness),
            WallPoint(host.Start, axisX, axisY, end, halfThickness),
            WallPoint(host.Start, axisX, axisY, begin, halfThickness)
        };
        AddGapDimensions(PlanBoundsOf(corners), OtherWallBounds(host.Id));

        var across = halfThickness + DimensionOffset;
        if (begin > DimensionMinimum)
            AddDimensionLine(WallPoint(host.Start, axisX, axisY, 0, across),
                WallPoint(host.Start, axisX, axisY, begin, across), begin);
        var remainder = host.Length - end;
        if (remainder > DimensionMinimum)
            AddDimensionLine(WallPoint(host.Start, axisX, axisY, end, across),
                WallPoint(host.Start, axisX, axisY, host.Length, across), remainder);
        AddDimensionLine(WallPoint(host.Start, axisX, axisY, begin, -across),
            WallPoint(host.Start, axisX, axisY, end, -across), opening.Width);
    }

    /// <summary>Adds the nearest clear gap in each of the four directions from the element.</summary>
    private void AddGapDimensions(ElementBounds element, IReadOnlyList<ElementBounds> others)
    {
        AddGapDimension(element, others, horizontal: true, positive: true);
        AddGapDimension(element, others, horizontal: true, positive: false);
        AddGapDimension(element, others, horizontal: false, positive: true);
        AddGapDimension(element, others, horizontal: false, positive: false);
    }

    /// <summary>
    /// Finds the nearest wall that faces the element in one direction and overlaps it on the
    /// perpendicular axis, then dimensions the gap between the two facing edges.
    /// </summary>
    private void AddGapDimension(ElementBounds element, IReadOnlyList<ElementBounds> others, bool horizontal, bool positive)
    {
        ElementBounds? nearest = null;
        var nearestGap = double.MaxValue;
        foreach (var other in others)
        {
            var overlaps = horizontal
                ? other.MaxY > element.MinY && other.MinY < element.MaxY
                : other.MaxX > element.MinX && other.MinX < element.MaxX;
            var gap = horizontal
                ? positive ? other.MinX - element.MaxX : element.MinX - other.MaxX
                : positive ? other.MinY - element.MaxY : element.MinY - other.MaxY;
            if (!overlaps || gap < DimensionMinimum || gap >= nearestGap) continue;
            nearestGap = gap;
            nearest = other;
        }
        if (nearest is not ElementBounds target) return;

        if (horizontal)
        {
            var y = (element.MinY + element.MaxY) / 2;
            AddDimensionLine(
                new Point2(positive ? element.MaxX : element.MinX, y),
                new Point2(positive ? target.MinX : target.MaxX, y),
                nearestGap);
        }
        else
        {
            var x = (element.MinX + element.MaxX) / 2;
            AddDimensionLine(
                new Point2(x, positive ? element.MaxY : element.MinY),
                new Point2(x, positive ? target.MinY : target.MaxY),
                nearestGap);
        }
    }

    /// <summary>Plan bounds of every other wall on the active level, used as gap targets.</summary>
    private IReadOnlyList<ElementBounds> OtherWallBounds(Guid excludedWallId) =>
        _session.Document.Walls
            .Where(wall => wall.LevelId == _activeLevelId && wall.Id != excludedWallId)
            .Select(WallBounds).ToArray();

    /// <summary>Plan bounds of a wall's footprint, using its drawn centerline and thickness.</summary>
    private static ElementBounds WallBounds(Wall wall) =>
        PlanBoundsOf(WallPlacementGeometry.Footprint(wall.Start, wall.End, wall.Thickness, 0,
            WallLocationReference.Centerline).Points);

    /// <summary>Returns the unit direction of a wall's centerline.</summary>
    private static (double X, double Y) WallAxis(Wall wall) =>
        ((wall.End.X - wall.Start.X) / wall.Length, (wall.End.Y - wall.Start.Y) / wall.Length);

    /// <summary>Returns the model point at a distance along a wall axis and across it, positive toward the normal.</summary>
    private static Point2 WallPoint(Point2 origin, double axisX, double axisY, double along, double across) =>
        new(origin.X + axisX * along - axisY * across, origin.Y + axisY * along + axisX * across);

    /// <summary>Returns the bounding box of a set of plan points.</summary>
    private static ElementBounds PlanBoundsOf(IEnumerable<Point2> points)
    {
        var list = points.ToArray();
        return new ElementBounds(
            list.Min(point => point.X), list.Min(point => point.Y),
            list.Max(point => point.X), list.Max(point => point.Y));
    }

    /// <summary>Draws one dimension: a line between two model points, end ticks, and its length as a label.</summary>
    private void AddDimensionLine(Point2 from, Point2 to, double value)
    {
        var brush = new SolidColorBrush(Color.FromRgb(0, 112, 192));
        var start = WorldToCanvas(from);
        var end = WorldToCanvas(to);
        PlanCanvas.Children.Add(new Line
        {
            X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y,
            Stroke = brush, StrokeThickness = 1, IsHitTestVisible = false
        });
        AddDimensionTick(start, brush);
        AddDimensionTick(end, brush);

        var label = new TextBlock
        {
            Text = value.ToString("0.00", CultureInfo.CurrentCulture) + " m",
            Foreground = brush,
            Background = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, (start.X + end.X) / 2 + 4);
        Canvas.SetTop(label, (start.Y + end.Y) / 2 - 18);
        PlanCanvas.Children.Add(label);
    }

    /// <summary>Adds the 45-degree architectural tick that marks a dimension endpoint.</summary>
    private void AddDimensionTick(Point center, Brush brush) =>
        PlanCanvas.Children.Add(new Line
        {
            X1 = center.X - 4, Y1 = center.Y + 4, X2 = center.X + 4, Y2 = center.Y - 4,
            Stroke = brush, StrokeThickness = 1.5, IsHitTestVisible = false
        });
}
