using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Numerics;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Desktop.Controls;

public partial class ModelNavigation
{
    private System.Windows.Shapes.Path? _hoverRegion;
    private Border? _hoverLabel;

    /// <summary>Lights exactly the face, edge or corner zone a click would select, so the target is never a surprise.</summary>
    private void UpdateHover(MouseEventArgs e) => HoverAt(e.OriginalSource, e.GetPosition(CubeCanvas));

    /// <summary>Highlights the zone of the face under a canvas point; separated from the mouse event so it can be exercised directly.</summary>
    public void HoverAt(object? source, Point position)
    {
        if (source is not Polygon { Tag: ValueTuple<Vector3, Vector3, Vector3, Matrix> face } || !face.Item4.HasInverse)
        {
            ClearHover();
            return;
        }
        var inverse = face.Item4;
        inverse.Invert();
        var local = inverse.Transform(position);
        var (x0, x1) = Zone(local.X / 70);
        var (y0, y1) = Zone(local.Y / 70);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(face.Item4.Transform(new Point(x0 * 70, y0 * 70)), true, true);
            context.LineTo(face.Item4.Transform(new Point(x1 * 70, y0 * 70)), true, false);
            context.LineTo(face.Item4.Transform(new Point(x1 * 70, y1 * 70)), true, false);
            context.LineTo(face.Item4.Transform(new Point(x0 * 70, y1 * 70)), true, false);
        }
        geometry.Freeze();
        if (_hoverRegion is null)
        {
            _hoverRegion = new System.Windows.Shapes.Path
            {
                IsHitTestVisible = false, StrokeThickness = 1, StrokeLineJoin = PenLineJoin.Round,
                Fill = new SolidColorBrush(Color.FromArgb(95, Accent.R, Accent.G, Accent.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(235, Accent.R, Accent.G, Accent.B))
            };
        }
        _hoverRegion.Data = geometry;
        if (!CubeCanvas.Children.Contains(_hoverRegion)) CubeCanvas.Children.Add(_hoverRegion);
        ShowHoverName(ViewCubeDirections.FromFace(face.Item1, face.Item2, face.Item3, new Vector2((float)local.X / 70, (float)local.Y / 70)));
    }

    /// <summary>Names the orientation under the pointer ("Superior · Frontal · Derecha") in a small caption below the cube.</summary>
    private void ShowHoverName(Vector3 direction)
    {
        if (_hoverLabel is null)
        {
            _hoverLabel = new Border
            {
                IsHitTestVisible = false, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 1, 7, 2),
                Background = new SolidColorBrush(Color.FromArgb(235, 52, 64, 76)),
                Child = new TextBlock { FontSize = 10, FontFamily = new FontFamily("Segoe UI Semibold"), Foreground = Brushes.White }
            };
        }
        var text = (TextBlock)_hoverLabel.Child;
        text.Text = DirectionName(direction);
        if (!CubeCanvas.Children.Contains(_hoverLabel)) CubeCanvas.Children.Add(_hoverLabel);
        _hoverLabel.Measure(new Size(CubeCanvas.Width, double.PositiveInfinity));
        Canvas.SetLeft(_hoverLabel, Math.Max(0, (CubeCanvas.Width - _hoverLabel.DesiredSize.Width) / 2));
        Canvas.SetTop(_hoverLabel, CubeCanvas.Height - _hoverLabel.DesiredSize.Height - 1);
    }

    /// <summary>Names a face, edge or corner direction, e.g. "Superior · Frontal · Derecha".</summary>
    private static string DirectionName(Vector3 direction)
    {
        var parts = new List<string>();
        if (direction.Z != 0) parts.Add(UiText.Get(direction.Z > 0 ? "ViewCube.Top" : "ViewCube.Bottom"));
        if (direction.Y != 0) parts.Add(UiText.Get(direction.Y < 0 ? "ViewCube.Front" : "ViewCube.Back"));
        if (direction.X != 0) parts.Add(UiText.Get(direction.X < 0 ? "ViewCube.Left" : "ViewCube.Right"));
        return string.Join(" · ", parts);
    }

    /// <summary>Writes the current standard view above the cube, only while the camera sits on one of the 26 cube orientations.</summary>
    private void DrawCurrentViewName()
    {
        var toCamera = -Camera!.Forward;
        foreach (var x in new[] { -1, 0, 1 })
        foreach (var y in new[] { -1, 0, 1 })
        foreach (var z in new[] { -1, 0, 1 })
        {
            var direction = new Vector3(x, y, z);
            if (direction == Vector3.Zero || Vector3.Dot(Vector3.Normalize(direction), toCamera) < 0.9995f) continue;
            var name = new TextBlock
            {
                Text = DirectionName(direction), FontSize = 9.5, FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = new SolidColorBrush(Color.FromRgb(104, 116, 128)), IsHitTestVisible = false
            };
            name.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(name, Math.Max(0, (CubeCanvas.Width - name.DesiredSize.Width) / 2));
            Canvas.SetTop(name, 1);
            CubeCanvas.Children.Add(name);
            return;
        }
    }

    /// <summary>Returns the face band (outer quarter or central half) containing a normalized coordinate.</summary>
    private static (double Start, double End) Zone(double coordinate) =>
        coordinate < 0.25 ? (0, 0.25) : coordinate > 0.75 ? (0.75, 1) : (0.25, 0.75);

    /// <summary>Removes the zone highlight when the pointer leaves the cube or begins a drag.</summary>
    private void ClearHover()
    {
        if (_hoverRegion is not null) CubeCanvas.Children.Remove(_hoverRegion);
        if (_hoverLabel is not null) CubeCanvas.Children.Remove(_hoverLabel);
    }

    /// <summary>Clears the highlight when the pointer leaves, unless a drag still owns the pointer.</summary>
    private void Cube_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_pressPoint is null) ClearHover();
    }

    /// <summary>Zooms from the cube, so the camera can be driven without leaving the widget.</summary>
    private void Cube_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0) return;
        CommandRequested?.Invoke(e.Delta > 0 ? "ZoomIn" : "ZoomOut");
        e.Handled = true;
    }

    /// <summary>Offers history and framing shortcuts next to the cube using the shared command route.</summary>
    private void Cube_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var command in new[] { "Home", "Fit", "FitAll", "Previous", "Next" })
        {
            var requested = command;
            var key = command == "Home" ? "Navigation.Home" : "Navigation." + command;
            var item = new MenuItem { Header = UiText.Get(key) };
            item.Click += (_, _) => CommandRequested?.Invoke(requested);
            menu.Items.Add(item);
            if (command == "Home") menu.Items.Add(new Separator());
        }
        CubeCanvas.ContextMenu = menu;
    }
}
