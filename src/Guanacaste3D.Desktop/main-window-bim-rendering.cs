using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Renders floor and room overlays behind wall geometry in plan.</summary>
    private void RenderExtendedPlanElements(BuildingDocument document, PlanViewRange range)
    {
        foreach (var floor in document.Floors.Where(floor => floor.LevelId == _activeLevelId &&
            range.Intersects(floor.ElevationOffset - floor.Thickness, floor.ElevationOffset)))
        {
            if (floor.IsToposolid)
            {
                AddToposolidPlan(floor);
                continue;
            }
            var hatch = FloorPlanHatchRules.For(floor);
            var polygon = new Polygon
            {
                Points = new PointCollection(floor.Boundary.Select(WorldToCanvas)),
                Fill = WallAppearanceRendering.CreateBrush(hatch.Color, hatch.Pattern, 0.85),
                Stroke = floor.Id == _selectedFloorId ? Brushes.DarkOrange : Brushes.SeaGreen,
                StrokeThickness = floor.Id == _selectedFloorId ? 2.5 : 1,
                Tag = floor.Id
            };
            PlanCanvas.Children.Add(polygon);
            // The overhang reads as a dashed outline beyond the walls, as Revit draws a roof's eaves in plan.
            var built = RoofGeometry.WithOverhang(floor);
            if (!ReferenceEquals(built, floor))
                PlanCanvas.Children.Add(new Polygon
                {
                    Points = new PointCollection(built.Boundary.Select(WorldToCanvas)),
                    Stroke = floor.Id == _selectedFloorId ? Brushes.DarkOrange : Brushes.SeaGreen,
                    StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 6, 3 }, IsHitTestVisible = false
                });
            // A pitched roof shows its ridge and hips so its shape reads in plan.
            foreach (var (from, to) in RoofGeometry.CreaseLines(built))
            {
                var start = WorldToCanvas(from);
                var end = WorldToCanvas(to);
                PlanCanvas.Children.Add(new Line
                {
                    X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = Brushes.SeaGreen,
                    StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 6, 3 }, Tag = floor.Id
                });
            }
        }
        foreach (var column in document.Columns.Where(column => column.LevelId == _activeLevelId &&
            range.Intersects(0, column.Height)))
        {
            PlanCanvas.Children.Add(new Polygon
            {
                Points = new PointCollection(column.Outline.Select(WorldToCanvas)),
                Fill = new SolidColorBrush(Color.FromArgb(170, 96, 102, 110)),
                Stroke = column.Id == _selectedColumnId ? Brushes.DarkOrange : Brushes.DimGray,
                StrokeThickness = column.Id == _selectedColumnId ? 2.5 : 1,
                Tag = column.Id
            });
        }
        foreach (var room in document.Rooms.Where(room => room.LevelId == _activeLevelId))
        {
            // Draw the exact footprint, rather than its bounding box, for concave rooms.
            var outline = RoomRegionGeometry.Outline(room);
            PlanCanvas.Children.Add(new Polygon
            {
                Points = new PointCollection(outline.Select(WorldToCanvas)),
                Fill = new SolidColorBrush(Color.FromArgb(45, 61, 132, 181)),
                Stroke = room.Id == _selectedRoomId ? Brushes.DarkOrange : Brushes.SteelBlue,
                StrokeThickness = room.Id == _selectedRoomId ? 2.5 : 1,
                Tag = room.Id
            });
            var center = WorldToCanvas(room.Center);
            var label = new TextBlock
            {
                Text = $"{room.Number}\n{room.Name}\n{room.Area:0.00} m²",
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Foreground = Brushes.MidnightBlue,
                Tag = room.Id
            };
            Canvas.SetLeft(label, center.X - 45);
            Canvas.SetTop(label, center.Y - 24);
            PlanCanvas.Children.Add(label);
        }
        RenderStructuralPlanElements();
    }

}
