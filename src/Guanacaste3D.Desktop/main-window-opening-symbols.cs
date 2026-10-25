using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Plan symbols for hosted doors and windows, drawn in the host wall's own coordinates. A door shows
/// its reveal lines, its leaf open at 90° and the swing arc; a window shows its reveal lines and the
/// two glazing lines inside the frame. The same drawing previews an opening while it is being moved.
/// </summary>
public partial class MainWindow
{
    private static readonly SolidColorBrush OpeningFrameBrush = Frozen(Color.FromRgb(61, 71, 79));
    private static readonly SolidColorBrush DoorLeafFill = Frozen(Color.FromRgb(236, 222, 196));

    /// <summary>Draws a placed opening with its selection-aware colour and a tag that makes it selectable.</summary>
    private void RenderOpeningSymbol(Wall wall, WallOpening opening)
    {
        var brush = opening.Id == _selectedOpeningId ? Brushes.DarkOrange
            : opening.Kind == OpeningKind.Door ? Brushes.SaddleBrown : Brushes.SteelBlue;
        DrawOpeningSymbol(wall, opening, brush, DoorLeafFill, opening.Id);
    }

    /// <summary>
    /// Draws one opening inside its host wall: the white cut through the wall, reveal lines at both
    /// jambs, then either glazing lines or a door leaf with its swing arc. A null tag makes the symbol a
    /// non-selectable preview, which is how an opening in motion is shown.
    /// </summary>
    private void DrawOpeningSymbol(Wall wall, WallOpening opening, Brush brush, Brush leafFill, Guid? tag)
    {
        var axisX = (wall.End.X - wall.Start.X) / wall.Length;
        var axisY = (wall.End.Y - wall.Start.Y) / wall.Length;
        // u runs along the wall from its start; v runs across it, toward the left-hand normal.
        Point2 At(double u, double v) =>
            new(wall.Start.X + axisX * u - axisY * v, wall.Start.Y + axisY * u + axisX * v);
        var half = wall.Thickness / 2;

        AddLine(At(opening.Begin, 0), At(opening.End, 0), Math.Max(4, wall.Thickness * _scale + 2), Brushes.White, tag);
        AddLine(At(opening.Begin, -half), At(opening.Begin, half), 2, OpeningFrameBrush, tag);
        AddLine(At(opening.End, -half), At(opening.End, half), 2, OpeningFrameBrush, tag);

        if (opening.Kind == OpeningKind.Window)
        {
            // Two glazing lines sit a third of the way from the centreline to each face of the frame.
            var pane = half / 3;
            AddLine(At(opening.Begin, -pane), At(opening.End, -pane), 1.5, brush, tag);
            AddLine(At(opening.Begin, pane), At(opening.End, pane), 1.5, brush, tag);
            return;
        }

        // The leaf is drawn open at 90°, hinged at the begin jamb; its plan width stays visible at any zoom.
        var leafHalf = Math.Max(0.02, 2 / _scale);
        PlanCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                WorldToCanvas(At(opening.Begin - leafHalf, 0)), WorldToCanvas(At(opening.Begin + leafHalf, 0)),
                WorldToCanvas(At(opening.Begin + leafHalf, opening.Width)),
                WorldToCanvas(At(opening.Begin - leafHalf, opening.Width))
            },
            Fill = leafFill,
            Stroke = brush,
            StrokeThickness = 1.5,
            Tag = tag
        });

        // The swing arc runs from the latch jamb (angle 0) to the leaf tip (90°), dashed as in drafting convention.
        var swing = new List<Point2>();
        for (var step = 0; step <= 24; step++)
        {
            var angle = step * Math.PI / 48;
            swing.Add(At(opening.Begin + opening.Width * Math.Cos(angle), opening.Width * Math.Sin(angle)));
        }
        AddPolyline(swing, 1, brush, tag, dashed: true);
    }

    /// <summary>Adds a model-space polyline, optionally dashed, to the plan canvas.</summary>
    private void AddPolyline(IEnumerable<Point2> points, double thickness, Brush color, Guid? tag, bool dashed)
    {
        PlanCanvas.Children.Add(new Polyline
        {
            Points = new PointCollection(points.Select(WorldToCanvas)),
            Stroke = color,
            StrokeThickness = thickness,
            StrokeDashArray = dashed ? new DoubleCollection { 4, 3 } : null,
            Tag = tag
        });
    }

    /// <summary>Selects the hosted instance instead of its wall when its plan symbol is clicked.</summary>
    private bool TrySelectOpeningSymbol(MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.Tag is not Guid id ||
            _session.Document.Openings.FirstOrDefault(opening => opening.Id == id) is not { } opening) return false;
        _selectedOpeningId = id;
        _selectedWallId = opening.WallId;
        _selectedWallOffset = opening.Offset;
        _selectedFloorId = _selectedRoomId = _selectedViewId = null;
        RefreshProperties();
        RenderPlan();
        return true;
    }

    /// <summary>Returns a frozen brush so static symbol colours can be shared across renders.</summary>
    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
