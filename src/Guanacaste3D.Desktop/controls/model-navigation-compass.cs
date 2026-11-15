using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Guanacaste3D.Desktop.Controls;

public partial class ModelNavigation
{
    private const double RingCenterX = 78;
    private const double RingCenterY = 96;
    private static readonly Color Accent = Color.FromRgb(47, 128, 224);

    /// <summary>
    /// Draws the compass base. Called twice per redraw: first with the far cardinal marks (hidden behind the cube),
    /// then with the near marks so they correctly overlap it.
    /// </summary>
    private void DrawCompass(bool behind)
    {
        if (behind) DrawCompassBase();
        foreach (var (key, normal) in new[] { ("North", Vector3.UnitY), ("East", Vector3.UnitX),
                     ("South", -Vector3.UnitY), ("West", -Vector3.UnitX) })
        {
            // A mark is "behind" when its direction points away from the viewer.
            var far = Vector3.Dot(normal, Camera!.Forward) > 0;
            if (far != behind) continue;
            var x = RingCenterX + Vector3.Dot(normal, Camera.Right) * 64;
            var y = RingCenterY - Vector3.Dot(normal, Camera.Up) * 23.5;
            var north = key == "North";
            var label = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, Tag = normal,
                Background = north && !far ? new SolidColorBrush(Color.FromRgb(217, 83, 79)) : Brushes.Transparent,
                ToolTip = UiText.Get("Navigation.Direction." + key), Opacity = far ? 0.55 : 1,
                Child = new TextBlock
                {
                    Text = UiText.Get("Navigation.Compass." + key), FontSize = 9.5, FontFamily = new FontFamily("Segoe UI Semibold"),
                    Foreground = north ? (far ? new SolidColorBrush(Color.FromRgb(217, 83, 79)) : Brushes.White) : new SolidColorBrush(Color.FromRgb(104, 116, 128)),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            label.MouseEnter += (_, _) => { if (!north) label.Background = new SolidColorBrush(Color.FromArgb(60, Accent.R, Accent.G, Accent.B)); };
            label.MouseLeave += (_, _) => { if (!north) label.Background = Brushes.Transparent; };
            label.MouseLeftButtonDown += CompassPressed;
            Canvas.SetLeft(label, x - 8);
            Canvas.SetTop(label, y - 8);
            CubeCanvas.Children.Add(label);
        }
    }

    /// <summary>Draws a soft ground shadow, a slim ring band, and diagonal guide dots that turn with the view.</summary>
    private void DrawCompassBase()
    {
        var shadow = new Ellipse
        {
            Width = 100, Height = 28, IsHitTestVisible = false,
            Fill = new RadialGradientBrush(Color.FromArgb(70, 40, 55, 70), Color.FromArgb(0, 40, 55, 70))
        };
        Canvas.SetLeft(shadow, RingCenterX - 50);
        Canvas.SetTop(shadow, RingCenterY - 13);
        CubeCanvas.Children.Add(shadow);
        var band = new GeometryGroup { FillRule = FillRule.EvenOdd };
        band.Children.Add(new EllipseGeometry(new Point(RingCenterX, RingCenterY), 73, 27));
        band.Children.Add(new EllipseGeometry(new Point(RingCenterX, RingCenterY), 56, 20.5));
        var ring = new System.Windows.Shapes.Path
        {
            Data = band, Cursor = Cursors.Hand, StrokeThickness = 0.8,
            Fill = new LinearGradientBrush(Color.FromRgb(251, 252, 253), Color.FromRgb(232, 236, 240), 90),
            Stroke = new SolidColorBrush(Color.FromArgb(190, 160, 171, 182))
        };
        CubeCanvas.Children.Add(ring);
        foreach (var angle in new[] { 45.0, 135, 225, 315 })
        {
            var radians = angle * Math.PI / 180;
            var direction = new Vector3((float)Math.Cos(radians), (float)Math.Sin(radians), 0);
            var dot = new Ellipse { Width = 3, Height = 3, Fill = new SolidColorBrush(Color.FromRgb(178, 188, 198)), IsHitTestVisible = false };
            Canvas.SetLeft(dot, RingCenterX + Vector3.Dot(direction, Camera!.Right) * 64.5 - 1.5);
            Canvas.SetTop(dot, RingCenterY - Vector3.Dot(direction, Camera.Up) * 24 - 1.5);
            CubeCanvas.Children.Add(dot);
        }
    }

    /// <summary>Marks the central, edge-free square of a face, leaving a darker frame that reads as the edge and corner zones.</summary>
    private void DrawFaceCore(Vector3 normal, Vector3 across, Vector3 up)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(Project(normal - across * 0.5f + up * 0.5f), true, true);
            context.LineTo(Project(normal + across * 0.5f + up * 0.5f), false, false);
            context.LineTo(Project(normal + across * 0.5f - up * 0.5f), false, false);
            context.LineTo(Project(normal - across * 0.5f - up * 0.5f), false, false);
        }
        geometry.Freeze();
        CubeCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = geometry, IsHitTestVisible = false, StrokeThickness = 0.6, StrokeLineJoin = PenLineJoin.Round,
            Fill = new SolidColorBrush(Color.FromArgb(105, 255, 255, 255)),
            Stroke = new SolidColorBrush(Color.FromArgb(120, 150, 162, 174))
        });
    }

    /// <summary>Orients to the selected cardinal direction without starting an orbit gesture.</summary>
    private void CompassPressed(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: Vector3 normal }) DirectionRequested?.Invoke(normal);
        e.Handled = true;
    }
}
