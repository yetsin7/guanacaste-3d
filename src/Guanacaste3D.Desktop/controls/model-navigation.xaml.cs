using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>An orientation cube with face, edge, corner, orbit, home, and fit navigation.</summary>
public partial class ModelNavigation : UserControl
{
    private const double HalfSize = 30;
    private Point? _pressPoint;
    private Point _lastPointer;
    private bool _dragged;
    private Vector3? _requestedDirection;
    private static readonly (string Key, Vector3 Normal)[] Faces =
    [
        ("ViewCube.Front", -Vector3.UnitY), ("ViewCube.Back", Vector3.UnitY),
        ("ViewCube.Left", -Vector3.UnitX), ("ViewCube.Right", Vector3.UnitX),
        ("ViewCube.Top", Vector3.UnitZ), ("ViewCube.Bottom", -Vector3.UnitZ)
    ];

    /// <summary>Creates the widget without owning or resetting its host's camera.</summary>
    public ModelNavigation()
    {
        InitializeComponent();
        NavigationBar.CommandRequested += ForwardCommand;
    }

    /// <summary>Applies the user's ViewCube visibility without disabling other navigation controls.</summary>
    public void SetCubeVisible(bool visible)
    {
        CubeCanvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Forwards navigation selectors through the same host command route as the cube.</summary>
    private void ForwardCommand(string command) => CommandRequested?.Invoke(command);

    public NavigationCamera? Camera { get; set; }
    public event Action<Vector3>? DirectionRequested;
    public event Action<float, float>? OrbitRequested;
    public event Action<string>? CommandRequested;
    public event Action? GestureStarted;
    public event Action? GestureCompleted;

    /// <summary>Projects visible faces and their text into the camera's current orientation.</summary>
    public void Redraw()
    {
        if (Camera is null) return;
        CubeCanvas.Children.Clear();
        _hoverRegion = null;
        _hoverLabel = null;
        DrawCompass(behind: true);
        foreach (var (key, normal) in Faces)
        {
            if (Vector3.Dot(normal, Camera.Forward) >= -0.001f) continue;
            var (across, up) = FaceFrame(normal);
            var origin = Project(normal - across + up);
            var horizontal = Project(normal + across + up) - origin;
            var vertical = Project(normal - across - up) - origin;
            var matrix = new Matrix(horizontal.X / 70, horizontal.Y / 70, vertical.X / 70, vertical.Y / 70, origin.X, origin.Y);
            var face = new Polygon
            {
                Points = new PointCollection { origin, origin + horizontal, origin + horizontal + vertical, origin + vertical },
                Fill = FaceBrush(normal), Stroke = EdgeBrush, StrokeThickness = 0.9, StrokeLineJoin = PenLineJoin.Round,
                Cursor = Cursors.Hand, Tag = (normal, across, up, matrix)
            };
            CubeCanvas.Children.Add(face);
            DrawFaceCore(normal, across, up);
            CubeCanvas.Children.Add(new Border
            {
                Width = 70, Height = 70, IsHitTestVisible = false, RenderTransform = new MatrixTransform(matrix),
                Child = new TextBlock
                {
                    Text = (TryFindResource(key) as string)?.ToUpperInvariant(), FontSize = 10.5,
                    FontFamily = new FontFamily("Segoe UI Semibold"), Foreground = LabelBrush,
                    TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            });
        }
        DrawCompass(behind: false);
        DrawCurrentViewName();
    }

    /// <summary>
    /// Chooses the reading frame of a face. Side faces keep world-up text; the top and bottom faces pick whichever of
    /// their four right-handed rotations reads most upright from the current viewpoint, never mirrored or upside down.
    /// </summary>
    private (Vector3 Across, Vector3 Up) FaceFrame(Vector3 normal)
    {
        if (Math.Abs(normal.Z) <= 0.5) return (Vector3.Cross(Vector3.UnitZ, normal), Vector3.UnitZ);
        var best = (Across: Vector3.UnitX, Up: Vector3.Cross(normal, Vector3.UnitX));
        var bestScore = float.NegativeInfinity;
        foreach (var candidate in new[] { Vector3.UnitX, Vector3.UnitY, -Vector3.UnitX, -Vector3.UnitY })
        {
            var up = Vector3.Cross(normal, candidate);
            var score = Vector3.Dot(up, Camera!.Up) * 1.5f + Vector3.Dot(candidate, Camera.Right);
            if (score > bestScore) { bestScore = score; best = (candidate, up); }
        }
        return best;
    }

    private static readonly Brush EdgeBrush = Frozen(new SolidColorBrush(Color.FromArgb(215, 138, 150, 162)));
    private static readonly Brush LabelBrush = Frozen(new SolidColorBrush(Color.FromRgb(88, 99, 110)));

    /// <summary>Freezes a brush so repeated redraws during orbiting allocate no change tracking.</summary>
    private static Brush Frozen(Brush brush) { brush.Freeze(); return brush; }

    /// <summary>Shades a face from a soft key light at the upper left, with a cool tint in the shadows.</summary>
    private static Brush FaceBrush(Vector3 normal)
    {
        var light = Math.Clamp(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-0.35f, -0.55f, 0.75f))), 0, 1);
        var tone = 222 + light * 30;
        Color Tint(double offset) => Color.FromRgb((byte)Math.Clamp(tone + offset - 4, 0, 255),
            (byte)Math.Clamp(tone + offset - 1, 0, 255), (byte)Math.Clamp(tone + offset + 3, 0, 255));
        var brush = new LinearGradientBrush(Tint(6), Tint(-7), new Point(0.15, 0), new Point(0.85, 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>Maps an orientation-space corner to the widget's orthographic projection.</summary>
    private Point Project(Vector3 corner) => new(78 + Vector3.Dot(corner, Camera!.Right) * HalfSize,
        60 - Vector3.Dot(corner, Camera.Up) * HalfSize);

    /// <summary>Classifies the clicked face region into one of the 26 supported viewing directions.</summary>
    private void Cube_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = _lastPointer = e.GetPosition(CubeCanvas);
        GestureStarted?.Invoke();
        _dragged = false;
        _requestedDirection = null;
        ClearHover();
        if (e.OriginalSource is Polygon { Tag: ValueTuple<Vector3, Vector3, Vector3, Matrix> face })
        {
            var inverse = face.Item4;
            if (inverse.HasInverse)
            {
                inverse.Invert();
                var local = inverse.Transform(_lastPointer);
                _requestedDirection = ViewCubeDirections.FromFace(face.Item1, face.Item2, face.Item3,
                    new Vector2((float)local.X / 70, (float)local.Y / 70));
            }
        }
        CubeCanvas.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>Orbits only after the pointer crosses the normal click threshold.</summary>
    private void Cube_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressPoint is not Point press) { UpdateHover(e); return; }
        var position = e.GetPosition(CubeCanvas);
        if (!_dragged && (position - press).Length <= 4) return;
        var previous = _dragged ? _lastPointer : press;
        _dragged = true;
        _lastPointer = position;
        OrbitRequested?.Invoke((float)(position.X - previous.X), (float)(position.Y - previous.Y));
        e.Handled = true;
    }

    /// <summary>Snaps a click to its selected orientation, while releasing a drag preserves its final angle.</summary>
    private void Cube_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var direction = !_dragged ? _requestedDirection : null;
        _pressPoint = null;
        GestureCompleted?.Invoke();
        CubeCanvas.ReleaseMouseCapture();
        if (direction is Vector3 requested) DirectionRequested?.Invoke(requested);
        e.Handled = true;
    }

    /// <summary>Abandons an interrupted orbit without applying a pending face click.</summary>
    private void Cube_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_pressPoint is not null) GestureCompleted?.Invoke();
        _pressPoint = null;
    }

    /// <summary>Routes localized toolbar actions without coupling this control to a document window.</summary>
    private void Command_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string command }) CommandRequested?.Invoke(command);
    }
}
