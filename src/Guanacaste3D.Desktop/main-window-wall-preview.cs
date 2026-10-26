using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Draws the provisional line or rectangle and its live dimensions over the plan.</summary>
    private void RenderWallSketchOverlay()
    {
        if (!_drawingWall) return;
        if (_activeWallSnap is WallSnapResult snap) AddWallSnapMarker(snap);
        if (_wallPreviewPoint is not Point2 preview) return;
        // Before the first click there is only the crosshair and the snap marker, no wall-shaped ghost.
        if (_pendingWallStart is not Point2 start) return;
        if (WallRectangleMode.IsChecked == true)
        {
            var corners = new[]
            {
                start, new Point2(preview.X, start.Y), preview, new Point2(start.X, preview.Y)
            };
            for (var index = 0; index < corners.Length; index++)
                AddWallPreviewBody(corners[index], corners[(index + 1) % corners.Length]);
            AddWallMeasurement(start, preview, FormatText("WallOptions.RectangleMeasurement",
                Math.Abs(preview.X - start.X), Math.Abs(preview.Y - start.Y)));
            return;
        }

        var end = ResolveWallPreviewEndpoint(start, preview);
        AddWallPreviewBody(start, end);
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X) * 180 / Math.PI;
        var length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        AddWallMeasurement(start, end, FormatText("WallOptions.Measurement", length, angle));
    }

    /// <summary>Draws the active endpoint or midpoint snap marker beside its localized label.</summary>
    private void AddWallSnapMarker(WallSnapResult snap)
    {
        var center = WorldToCanvas(snap.Point);
        Shape marker;
        if (snap.Kind == WallSnapKind.Endpoint)
        {
            marker = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 127, 163)),
                StrokeThickness = 2
            };
        }
        else
        {
            marker = new Polygon
            {
                Points = new PointCollection
                {
                    new(center.X, center.Y - 6), new(center.X + 6, center.Y),
                    new(center.X, center.Y + 6), new(center.X - 6, center.Y)
                },
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 127, 163)),
                StrokeThickness = 2
            };
        }
        if (marker is Rectangle)
        {
            Canvas.SetLeft(marker, center.X - 5);
            Canvas.SetTop(marker, center.Y - 5);
        }
        marker.IsHitTestVisible = false;
        PlanCanvas.Children.Add(marker);

        var label = new TextBlock
        {
            Text = GetText(snap.Kind == WallSnapKind.Endpoint ? "WallSnap.Endpoint" : "WallSnap.Midpoint"),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 96, 124)),
            Background = Brushes.White,
            FontSize = 10,
            Padding = new Thickness(4, 1, 4, 1),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, center.X + 9);
        Canvas.SetTop(label, center.Y + 7);
        PlanCanvas.Children.Add(label);
    }

    /// <summary>Adds a translucent full-thickness wall body to the current plan canvas.</summary>
    private void AddWallPreviewBody(Point2 start, Point2 end)
    {
        if (start.DistanceTo(end) < 0.05 || !TryReadWallPlacementOptions(out _, out var offset, out var typeId))
            return;
        var wallType = _session.Document.WallTypes.Single(type => type.Id == typeId);
        var footprint = WallPlacementGeometry.Footprint(
            start, end, wallType.Thickness, offset, SelectedWallLocationReference());
        PlanCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection(footprint.Points.Select(WorldToCanvas)),
            Fill = WallAppearanceRendering.CreateBrush(
                wallType.PlanFillColor, wallType.PlanFillPattern, 0.72),
            Stroke = new SolidColorBrush(Color.FromRgb(0, 96, 124)),
            StrokeThickness = 1.5,
            IsHitTestVisible = false
        });
    }

    /// <summary>Places a compact dimension callout at the midpoint of a provisional wall sketch.</summary>
    private void AddWallMeasurement(Point2 start, Point2 end, string text)
    {
        var midpoint = WorldToCanvas(new Point2((start.X + end.X) / 2, (start.Y + end.Y) / 2));
        var label = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 127, 163)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(7, 3, 7, 3),
            Child = new TextBlock { Text = text, FontSize = 11, Foreground = Brushes.DarkSlateGray },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, midpoint.X + 8);
        Canvas.SetTop(label, midpoint.Y - 28);
        PlanCanvas.Children.Add(label);
    }

}
