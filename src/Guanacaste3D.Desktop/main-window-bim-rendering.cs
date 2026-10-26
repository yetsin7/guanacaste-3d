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
    private void RenderExtendedPlanElements()
    {
        foreach (var floor in _session.Document.Floors.Where(floor => floor.LevelId == _activeLevelId))
        {
            if (floor.IsToposolid)
            {
                AddToposolidPlan(floor);
                continue;
            }
            var polygon = new Polygon
            {
                Points = new PointCollection(floor.Boundary.Select(WorldToCanvas)),
                Fill = new SolidColorBrush(Color.FromArgb(70, 111, 145, 125)),
                Stroke = floor.Id == _selectedFloorId ? Brushes.DarkOrange : Brushes.SeaGreen,
                StrokeThickness = floor.Id == _selectedFloorId ? 2.5 : 1,
                Tag = floor.Id
            };
            PlanCanvas.Children.Add(polygon);
            // A pitched roof shows its ridge and hips so its shape reads in plan.
            foreach (var (from, to) in RoofGeometry.CreaseLines(floor))
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
        foreach (var column in _session.Document.Columns.Where(column => column.LevelId == _activeLevelId))
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
        foreach (var room in _session.Document.Rooms.Where(room => room.LevelId == _activeLevelId))
        {
            var first = WorldToCanvas(room.FirstCorner);
            var second = WorldToCanvas(room.SecondCorner);
            var rectangle = new Rectangle
            {
                Width = Math.Abs(second.X - first.X),
                Height = Math.Abs(second.Y - first.Y),
                Fill = new SolidColorBrush(Color.FromArgb(45, 61, 132, 181)),
                Stroke = room.Id == _selectedRoomId ? Brushes.DarkOrange : Brushes.SteelBlue,
                StrokeThickness = room.Id == _selectedRoomId ? 2.5 : 1,
                Tag = room.Id
            };
            Canvas.SetLeft(rectangle, Math.Min(first.X, second.X));
            Canvas.SetTop(rectangle, Math.Min(first.Y, second.Y));
            PlanCanvas.Children.Add(rectangle);
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

    /// <summary>Renders the selected sheet with a live plan viewport and title block.</summary>
    private void RenderSheet()
    {
        if (SheetCanvas is null || _session is null) return;
        SheetCanvas.Children.Clear();
        var sheet = _session.Document.Sheets.FirstOrDefault(candidate => candidate.Id == _selectedSheetId)
            ?? _session.Document.Sheets.FirstOrDefault();
        if (sheet is null) return;
        _selectedSheetId ??= sheet.Id;
        var border = new Rectangle { Width = 1090, Height = 762, Stroke = Brushes.Black, StrokeThickness = 2 };
        Canvas.SetLeft(border, 15);
        Canvas.SetTop(border, 15);
        SheetCanvas.Children.Add(border);
        var viewport = new Rectangle { Width = 780, Height = 610, Stroke = Brushes.Gray, StrokeThickness = 1 };
        Canvas.SetLeft(viewport, 45);
        Canvas.SetTop(viewport, 55);
        SheetCanvas.Children.Add(viewport);
        RenderPlanOnSheet(45, 55, 780, 610);
        AddSheetText(GetText("Sheet.ViewTitle"), 55, 675, 14, FontWeights.SemiBold);
        AddSheetText($"{GetText("Sheet.Project")}: {_session.Document.Name}", 850, 590, 12, FontWeights.Normal);
        AddSheetText($"{GetText("Sheet.Number")}: {sheet.Number}", 850, 625, 18, FontWeights.Bold);
        AddSheetText($"{GetText("Sheet.Title")}: {sheet.Name}", 850, 665, 12, FontWeights.Normal);
    }

    /// <summary>Draws active-level wall centerlines into the sheet viewport.</summary>
    private void RenderPlanOnSheet(double left, double top, double width, double height)
    {
        var walls = _session.Document.Walls.Where(wall => wall.LevelId == _activeLevelId).ToArray();
        if (walls.Length == 0) return;
        var points = walls.SelectMany(wall => new[] { wall.Start, wall.End }).ToArray();
        var minimumX = points.Min(point => point.X);
        var maximumX = points.Max(point => point.X);
        var minimumY = points.Min(point => point.Y);
        var maximumY = points.Max(point => point.Y);
        var scale = Math.Min(
            (width - 80) / Math.Max(1, maximumX - minimumX),
            (height - 80) / Math.Max(1, maximumY - minimumY));
        foreach (var wall in walls)
        {
            var line = new Line
            {
                X1 = left + 40 + (wall.Start.X - minimumX) * scale,
                Y1 = top + height - 40 - (wall.Start.Y - minimumY) * scale,
                X2 = left + 40 + (wall.End.X - minimumX) * scale,
                Y2 = top + height - 40 - (wall.End.Y - minimumY) * scale,
                Stroke = Brushes.Black,
                StrokeThickness = Math.Max(2, wall.Thickness * scale)
            };
            SheetCanvas.Children.Add(line);
        }
    }

    /// <summary>Adds a text label to the drawing sheet.</summary>
    private void AddSheetText(string text, double left, double top, double fontSize, FontWeight fontWeight)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = Brushes.Black
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        SheetCanvas.Children.Add(label);
    }
}
