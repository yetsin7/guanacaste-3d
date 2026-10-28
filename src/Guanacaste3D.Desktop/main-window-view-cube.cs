using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Guanacaste3D.Desktop;

/// <summary>
/// The view cube and the navigation wheel drawn over the 3D view. The cube shows which way the model is turned and
/// snaps the view to a side when one of its faces is clicked; the wheel holds the common navigation commands.
/// </summary>
public partial class MainWindow
{
    private const double ViewCubeHalfSize = 34;

    /// <summary>Each face of the cube, with the key of its label and the direction it points to.</summary>
    private static readonly (string LabelKey, Vector3 Normal)[] CubeFaces =
    [
        ("ViewCube.Front", -Vector3.UnitY), ("ViewCube.Back", Vector3.UnitY),
        ("ViewCube.Left", -Vector3.UnitX), ("ViewCube.Right", Vector3.UnitX),
        ("ViewCube.Top", Vector3.UnitZ), ("ViewCube.Bottom", -Vector3.UnitZ)
    ];

    /// <summary>Connects the cube and the wheel to the 3D navigator; called once from the constructor.</summary>
    private void InitializeViewCube()
    {
        _modelNavigator?.Changed += RedrawViewCube;
        RedrawViewCube();
    }

    /// <summary>Draws the faces that look toward the viewer, projected with the current view direction.</summary>
    private void RedrawViewCube()
    {
        if (ViewCubeCanvas is null) return;
        ViewCubeCanvas.Children.Clear();
        var camera = _navigation;
        var center = new Point(ViewCubeCanvas.Width / 2, ViewCubeCanvas.Height / 2);
        foreach (var (labelKey, normal) in CubeFaces)
        {
            // A face is visible when its normal points back toward the camera; the others are hidden behind the cube.
            if (Vector3.Dot(normal, camera.Forward) >= 0) continue;
            var corners = FaceCorners(normal).Select(corner => new Point(
                center.X + Vector3.Dot(corner, camera.Right) * ViewCubeHalfSize,
                center.Y - Vector3.Dot(corner, camera.Up) * ViewCubeHalfSize)).ToList();

            var face = new Polygon
            {
                Points = new PointCollection(corners),
                Fill = new SolidColorBrush(Color.FromRgb(246, 246, 246)),
                Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                StrokeThickness = 0.8,
                Cursor = Cursors.Hand,
                ToolTip = GetText(labelKey)
            };
            face.MouseLeftButtonDown += (_, e) =>
            {
                _modelNavigator?.LookFrom(normal);
                e.Handled = true;
            };
            ViewCubeCanvas.Children.Add(face);

            var label = new TextBlock
            {
                Text = GetText(labelKey),
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                IsHitTestVisible = false
            };
            var centroid = new Point(corners.Average(point => point.X), corners.Average(point => point.Y));
            Canvas.SetLeft(label, centroid.X - 16);
            Canvas.SetTop(label, centroid.Y - 6);
            ViewCubeCanvas.Children.Add(label);
        }
    }

    /// <summary>The four corners of the cube face with the given normal, in order around the face.</summary>
    private static Vector3[] FaceCorners(Vector3 normal)
    {
        var across = MathF.Abs(normal.X) > 0.5f ? Vector3.UnitY : Vector3.UnitX;
        var down = Vector3.Cross(normal, across);
        return
        [
            normal + across + down, normal + across - down,
            normal - across - down, normal - across + down
        ];
    }

    private void WheelFrameAll_Click(object sender, RoutedEventArgs e) => FrameCameraToScene();

    private void WheelZoomIn_Click(object sender, RoutedEventArgs e) => _modelNavigator?.ZoomBy(1);

    private void WheelZoomOut_Click(object sender, RoutedEventArgs e) => _modelNavigator?.ZoomBy(-1);

    private void WheelCenter_Click(object sender, RoutedEventArgs e) => _modelNavigator?.CenterAll();
}
