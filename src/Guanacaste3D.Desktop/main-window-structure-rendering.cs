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
            if (element.IsDatumLine)
            {
                AddDatumLine(element, selected);
                continue;
            }
            if (element.Kind == StructuralElementKind.Area)
            {
                AddAreaOutline(element, selected);
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
            if (HasDetailedPlanParts(element))
            {
                foreach (var part in ElementGeometry.Parts(element))
                    PlanCanvas.Children.Add(new Polygon { Points = new PointCollection(part.Footprint.Select(WorldToCanvas)),
                        Fill = PartPlanFill(element, part), Stroke = selected ? Brushes.DarkOrange : Brushes.DimGray,
                        StrokeThickness = selected ? 1.5 : 1, Tag = element.Id });
                if (element.Kind == StructuralElementKind.Ramp) AddRampArrow(element, selected);
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

    /// <summary>Kinds and styles whose plan symbol is more than one footprint.</summary>
    private static bool HasDetailedPlanParts(StructuralElement element) =>
        element.Kind is StructuralElementKind.Stair or StructuralElementKind.Railing or StructuralElementKind.Ramp
            or StructuralElementKind.Gutter or StructuralElementKind.CurtainWall or StructuralElementKind.Skylight ||
        (element.HasSolid && element.Style != ElementStyle.Default);

    /// <summary>Glass parts read as pale blue; metal drainage as grey; everything else keeps the neutral part fill.</summary>
    private static Brush PartPlanFill(StructuralElement element, ElementPart part) =>
        part.Kind == Core.Geometry.SurfaceKind.Glass ? new SolidColorBrush(Color.FromArgb(120, 150, 205, 230))
        : element.Kind is StructuralElementKind.Gutter or StructuralElementKind.Downspout ? Brushes.LightSlateGray
        : element.Kind == StructuralElementKind.Ramp ? new SolidColorBrush(Color.FromArgb(170, 205, 190, 150))
        : Brushes.WhiteSmoke;

    /// <summary>Draws the slope direction of a ramp: an arrow from its foot to its head.</summary>
    private void AddRampArrow(StructuralElement ramp, bool selected)
    {
        var brush = selected ? Brushes.DarkOrange : Brushes.DimGray;
        AddLine(ramp.Start, ramp.End, 1, brush, ramp.Id);
        var length = ramp.Length;
        if (length < 0.2) return;
        var direction = new Point2((ramp.End.X - ramp.Start.X) / length, (ramp.End.Y - ramp.Start.Y) / length);
        var head = Math.Min(0.3, length / 4);
        foreach (var side in new[] { -1, 1 })
            AddLine(ramp.End, new Point2(ramp.End.X - direction.X * head - side * direction.Y * head * 0.5,
                ramp.End.Y - direction.Y * head + side * direction.X * head * 0.5), 1, brush, ramp.Id);
    }

    /// <summary>Draws a reference plane, room separator, area boundary or model line in its own drafting style.</summary>
    private void AddDatumLine(StructuralElement element, bool selected)
    {
        var (brush, thickness, dashes) = element.Kind switch
        {
            StructuralElementKind.ReferencePlane => (Brushes.ForestGreen, 1.0, new DoubleCollection { 10, 3, 2, 3 }),
            StructuralElementKind.RoomSeparator => (Brushes.MediumVioletRed, 1.5, new DoubleCollection { 6, 2 }),
            StructuralElementKind.AreaBoundary => (Brushes.Firebrick, 1.5, new DoubleCollection { 2, 2 }),
            _ => (Brushes.Black, 1.5, (DoubleCollection?)null)
        };
        var start = WorldToCanvas(element.Start);
        var end = WorldToCanvas(element.End);
        PlanCanvas.Children.Add(new Line
        {
            X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y,
            Stroke = selected ? Brushes.DarkOrange : brush,
            StrokeThickness = selected || element.Id == _activeWorkPlaneId ? thickness + 1.5 : thickness,
            StrokeDashArray = dashes,
            Tag = element.Id
        });
        if (element.Kind != StructuralElementKind.ReferencePlane || string.IsNullOrWhiteSpace(element.Name)) return;
        var label = new TextBlock { Text = element.Name, FontSize = 10, Foreground = Brushes.ForestGreen, Tag = element.Id };
        Canvas.SetLeft(label, start.X + 3);
        Canvas.SetTop(label, start.Y - 14);
        PlanCanvas.Children.Add(label);
    }

    /// <summary>Draws an area of the area scheme: a dashed rectangle labelled with its name and measured area.</summary>
    private void AddAreaOutline(StructuralElement area, bool selected)
    {
        PlanCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection(area.Footprint.Select(WorldToCanvas)),
            Fill = new SolidColorBrush(Color.FromArgb(40, 205, 120, 60)),
            Stroke = selected ? Brushes.DarkOrange : Brushes.Chocolate,
            StrokeThickness = selected ? 2.5 : 1.2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Tag = area.Id
        });
        var center = WorldToCanvas(new Point2((area.Start.X + area.End.X) / 2, (area.Start.Y + area.End.Y) / 2));
        var label = new TextBlock
        {
            Text = $"{area.Name}\n{area.PlanArea:0.00} m²",
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            Foreground = Brushes.Chocolate,
            Tag = area.Id
        };
        Canvas.SetLeft(label, center.X - 40);
        Canvas.SetTop(label, center.Y - 14);
        PlanCanvas.Children.Add(label);
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
        var style = ElementStyle.Default;
        if (_structuralPreset is { } preset && preset.Kind == tool.Kind)
            (width, height, offset, style) = (preset.Width, preset.Height, preset.Offset, preset.Style);
        var preview = new StructuralElement(Guid.Empty, _activeLevelId, tool.Kind, string.Empty,
            origin, pointer, width, height, offset) { Style = style };
        if (tool.Kind is StructuralElementKind.ReferencePlane or StructuralElementKind.RoomSeparator
            or StructuralElementKind.AreaBoundary or StructuralElementKind.ModelLine)
            AddStructuralPreviewOutline([origin, pointer]);
        else if (HasDetailedPlanParts(preview))
            foreach (var part in ElementGeometry.Parts(preview).Take(400)) AddStructuralPreviewOutline(part.Footprint);
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
        StructuralElementKind.Skylight => Color.FromArgb(150, 150, 200, 225),
        _ => Color.FromArgb(200, 200, 40, 40)
    };
}
