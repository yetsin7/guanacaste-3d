using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Draws every structural element of the active level, selected ones highlighted.</summary>
    private void RenderStructuralPlanElements()
    {
        foreach (var element in _session.Document.StructuralElements.Where(candidate => candidate.LevelId == _activeLevelId))
        {
            var selected = element.Id == _selectedStructuralId;
            if (element.Kind == StructuralElementKind.ModelLine)
            {
                AddLine(element.Start, element.End, selected ? 3 : 1.5,
                    selected ? Brushes.DarkOrange : Brushes.Black, element.Id);
                continue;
            }
            if (element.Kind == StructuralElementKind.ModelText)
            {
                var origin = WorldToCanvas(element.Start);
                var label = new TextBlock
                {
                    Text = element.Name,
                    FontSize = 12,
                    Foreground = selected ? Brushes.DarkOrange : Brushes.Black,
                    Tag = element.Id
                };
                Canvas.SetLeft(label, origin.X);
                Canvas.SetTop(label, origin.Y);
                PlanCanvas.Children.Add(label);
                continue;
            }
            if (element.Kind is StructuralElementKind.Stair or StructuralElementKind.Railing)
            {
                foreach (var part in CirculationGeometry.Parts(element))
                    PlanCanvas.Children.Add(new Polygon { Points = new PointCollection(part.Footprint.Select(WorldToCanvas)),
                        Fill = Brushes.WhiteSmoke, Stroke = selected ? Brushes.DarkOrange : Brushes.DimGray,
                        StrokeThickness = 1, Tag = element.Id });
                continue;
            }
            var isHole = element.Kind is StructuralElementKind.Opening or StructuralElementKind.Shaft;
            PlanCanvas.Children.Add(new Polygon
            {
                Points = new PointCollection(element.Footprint.Select(WorldToCanvas)),
                Fill = new SolidColorBrush(StructuralPlanFill(element.Kind)),
                Stroke = selected ? Brushes.DarkOrange : Brushes.DimGray,
                StrokeThickness = selected ? 2.5 : 1,
                StrokeDashArray = isHole ? new DoubleCollection { 4, 3 } : null,
                Tag = element.Id
            });
        }
    }

    /// <summary>Shows the armed element as a dashed outline at the pointer, or between the first and second click.</summary>
    private void RenderStructuralPreview()
    {
        if (_structuralTool is not { } tool || _structuralPreviewPoint is not Point2 pointer) return;
        if (tool.Mode == StructuralPlacementMode.Rectangle)
        {
            var corner = _pendingStructuralStart ?? new Point2(pointer.X - 1, pointer.Y - 1);
            var corners = new[] { corner, new Point2(pointer.X, corner.Y), pointer, new Point2(corner.X, pointer.Y) };
            AddStructuralPreviewOutline(corners);
            return;
        }

        var start = tool.Mode == StructuralPlacementMode.Point ? pointer :
            _pendingStructuralStart ?? new Point2(pointer.X - 1, pointer.Y);
        var origin = start;
        var (width, height, offset) = StructuralElementStandards.For(tool.Kind);
        var preview = new StructuralElement(Guid.Empty, _activeLevelId, tool.Kind, string.Empty,
            origin, pointer, width, height, offset);
        if (tool.Kind is StructuralElementKind.Stair or StructuralElementKind.Railing)
            foreach (var part in CirculationGeometry.Parts(preview)) AddStructuralPreviewOutline(part.Footprint);
        else AddStructuralPreviewOutline(preview.Footprint);
    }

    /// <summary>Adds a dashed, non-interactive outline for the provisional structural geometry.</summary>
    private void AddStructuralPreviewOutline(IEnumerable<Point2> outline) =>
        PlanCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection(outline.Select(WorldToCanvas)),
            Stroke = Brushes.DarkOrange,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            IsHitTestVisible = false
        });

    /// <summary>Shows the element's name, section, offset, and length in the properties panel.</summary>
    private bool TryRefreshStructuralProperties()
    {
        var element = _session.Document.StructuralElements.FirstOrDefault(candidate => candidate.Id == _selectedStructuralId);
        if (element is null) return false;
        PropertiesText.Text = FormatText("Properties.StructuralSummary",
            element.Name, element.Width, element.Height, element.Offset, element.Length);
        return true;
    }

    /// <summary>Gives each structural kind a distinct, semi-transparent plan fill.</summary>
    private static Color StructuralPlanFill(StructuralElementKind kind) => kind switch
    {
        StructuralElementKind.Beam => Color.FromArgb(150, 139, 90, 43),
        StructuralElementKind.Truss => Color.FromArgb(150, 160, 120, 40),
        StructuralElementKind.Brace => Color.FromArgb(150, 150, 60, 50),
        StructuralElementKind.WallFoundation => Color.FromArgb(120, 120, 120, 120),
        StructuralElementKind.IsolatedFoundation => Color.FromArgb(120, 150, 150, 150),
        StructuralElementKind.Component => Color.FromArgb(150, 90, 100, 110),
        // Openings and shafts show as holes: the slab underneath is still drawn, so the fill is nearly white.
        StructuralElementKind.Opening or StructuralElementKind.Shaft => Color.FromArgb(235, 255, 255, 255),
        StructuralElementKind.Dormer => Color.FromArgb(160, 150, 96, 70),
        _ => Color.FromArgb(200, 200, 40, 40)
    };
}
