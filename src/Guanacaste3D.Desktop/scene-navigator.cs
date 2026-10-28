using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Walk-through navigation for a surface that shows the model (the 3D view or the render preview), in the manner of
/// Revit: the wheel zooms toward the point under the cursor, the middle button pans, Shift plus the middle button
/// orbits the model, the right button looks around, the left button only selects, W A S D fly, Q and E go down and up, Shift is fast and Ctrl is slow. Both views share one
/// <see cref="NavigationCamera"/>, so moving in either keeps the other in step.
/// </summary>
internal sealed class SceneNavigator
{
    private readonly FrameworkElement _surface;
    private Point _last;
    private MouseButton? _dragButton;
    private Vector3 _orbitPivot;
    private TimeSpan _lastTick;
    private bool _ticking;

    public SceneNavigator(FrameworkElement surface, NavigationCamera camera)
    {
        _surface = surface;
        Camera = camera;
        surface.Focusable = true;
        surface.MouseDown += OnMouseDown;
        surface.MouseMove += OnMouseMove;
        surface.MouseUp += OnMouseUp;
        surface.MouseWheel += OnMouseWheel;
        surface.LostMouseCapture += (_, _) => _dragButton = null;
        surface.IsVisibleChanged += (_, _) => UpdateTicking();
        surface.Loaded += (_, _) => UpdateTicking();
        surface.Unloaded += (_, _) => StopTicking();
    }

    /// <summary>The camera shared with the other views. Replace it to switch the navigated camera.</summary>
    public NavigationCamera Camera { get; set; }

    /// <summary>Raised after every change of the camera so the owner can redraw.</summary>
    public event Action? Changed;

    /// <summary>A left click that did not turn into a drag, at the pointer position in the surface; used to select.</summary>
    public event Action<Point>? Clicked;

    /// <summary>A right click that did not turn into a look-around, at the pointer position; used to open the context menu.</summary>
    public event Action<Point>? ContextRequested;

    /// <summary>A left double-click on the surface.</summary>
    public event Action<Point>? DoubleClicked;

    private Point _pressPoint;
    private bool _dragged;
    private bool _orbiting;

    /// <summary>Scene bounds, used for movement speed and for framing.</summary>
    public Func<(Vector3 Min, Vector3 Max)> Bounds { get; set; } = () => (new Vector3(-5, -5, 0), new Vector3(5, 5, 3));

    /// <summary>Distance along a ray to the nearest surface, or null; drives cursor-centered zoom and orbit pivots.</summary>
    public Func<Vector3, Vector3, float?>? Pick { get; set; }

    /// <summary>Stops the camera at walls when set; null or <see cref="CollisionEnabled"/> false lets it pass through.</summary>
    public CameraCollision? Collision { get; set; }

    public bool CollisionEnabled { get; set; }

    /// <summary>Whether keys currently belong to navigation (the pointer is over the surface and no text box has focus).</summary>
    public bool IsActive => _surface.IsVisible && (_surface.IsMouseOver || _surface.IsKeyboardFocusWithin) &&
                            Keyboard.FocusedElement is not TextBox;

    /// <summary>Whether a key press is a navigation key that the window must not treat as a command shortcut.</summary>
    public bool OwnsKey(Key key) => IsActive && key is Key.W or Key.A or Key.S or Key.D or Key.Q or Key.E or Key.Up or Key.Down
        or Key.Left or Key.Right or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl;

    private float SceneRadius()
    {
        var (min, max) = Bounds();
        return MathF.Max(3, (max - min).Length() / 2);
    }

    private float PickDistance(Vector3 direction) =>
        Pick?.Invoke(Camera.Position, direction) is { } distance && distance > 0.01f ? distance : SceneRadius();

    private (float Width, float Height) Size() => ((float)Math.Max(1, _surface.ActualWidth), (float)Math.Max(1, _surface.ActualHeight));

    // ---------------------------------------------------------------- mouse

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _surface.Focus();
        if (_dragButton is not null) return;
        if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2)
        {
            // A middle double-click brings a lost user back to the model; it does not start a pan.
            CenterAll();
            e.Handled = true;
            return;
        }
        _dragButton = e.ChangedButton;
        _last = _pressPoint = e.GetPosition(_surface);
        _dragged = false;
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) DoubleClicked?.Invoke(_pressPoint);
        _orbiting = e.ChangedButton == MouseButton.Middle && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (_orbiting)
        {
            var (width, height) = Size();
            var ray = Camera.PixelRay((float)_last.X, (float)_last.Y, width, height);
            _orbitPivot = Camera.Position + ray * PickDistance(ray);
        }
        if (e.ChangedButton is MouseButton.Right or MouseButton.Middle or MouseButton.Left)
        {
            _surface.CaptureMouse();
            _surface.Cursor = e.ChangedButton switch
            {
                MouseButton.Middle => _orbiting ? Cursors.Hand : Cursors.SizeAll,
                MouseButton.Right => Cursors.Cross,
                _ => Cursors.Arrow
            };
        }
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragButton is not { } button) return;
        var position = e.GetPosition(_surface);
        if (!_dragged && (position - _pressPoint).Length > 4) _dragged = true;
        var dx = (float)(position.X - _last.X);
        var dy = (float)(position.Y - _last.Y);
        _last = position;
        switch (button)
        {
            case MouseButton.Right:
                Camera.Look(dx, dy);
                break;
            case MouseButton.Middle when _orbiting:
                Camera.OrbitAround(_orbitPivot, dx, dy);
                break;
            case MouseButton.Middle:
            {
                // Pan so the point under the cursor follows the cursor: world units per pixel at that depth.
                var (width, height) = Size();
                var distance = PickDistance(Camera.PixelRay((float)position.X, (float)position.Y, width, height));
                var unitsPerPixel = 2 * distance * MathF.Tan(Camera.FieldOfViewDegrees * MathF.PI / 360) / height;
                MoveCamera(Camera.PanDelta(dx, dy, unitsPerPixel));
                break;
            }
        }
        if (button != MouseButton.Left) Changed?.Invoke();
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragButton != e.ChangedButton) return;
        _dragButton = null;
        _surface.ReleaseMouseCapture();
        _surface.Cursor = Cursors.Arrow;
        e.Handled = true;
        if (e.ChangedButton == MouseButton.Left && !_dragged && e.ClickCount < 2) Clicked?.Invoke(e.GetPosition(_surface));
        // A right button released without a drag is a click, not a look-around, so it asks for the context menu.
        if (e.ChangedButton == MouseButton.Right && !_dragged) ContextRequested?.Invoke(e.GetPosition(_surface));
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var position = e.GetPosition(_surface);
        var (width, height) = Size();
        var ray = Camera.PixelRay((float)position.X, (float)position.Y, width, height);
        var distance = PickDistance(ray);
        // Each notch covers a fifth of the way to whatever is under the cursor, so close-ups slow down and a distant
        // view crosses the model quickly. Holding Shift doubles the step.
        var step = distance * 0.2f * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 2 : 1) * Math.Sign(e.Delta);
        step = Math.Clamp(step, -SceneRadius() * 0.6f, distance - 0.05f);
        MoveCamera(ray * step);
        Changed?.Invoke();
        e.Handled = true;
    }

    // ---------------------------------------------------------------- keyboard flight

    private void UpdateTicking()
    {
        if (_surface.IsVisible && _surface.IsLoaded) StartTicking();
        else StopTicking();
    }

    private void StartTicking()
    {
        if (_ticking) return;
        _ticking = true;
        _lastTick = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    private void StopTicking()
    {
        if (!_ticking) return;
        _ticking = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = (e as RenderingEventArgs)?.RenderingTime ?? TimeSpan.Zero;
        var dt = _lastTick == TimeSpan.Zero ? 0 : Math.Min(0.1, (now - _lastTick).TotalSeconds);
        _lastTick = now;
        if (dt <= 0 || !IsActive) return;

        float Key(Key key) => Keyboard.IsKeyDown(key) ? 1 : 0;
        var forward = Key(System.Windows.Input.Key.W) + Key(System.Windows.Input.Key.Up) - Key(System.Windows.Input.Key.S) - Key(System.Windows.Input.Key.Down);
        var right = Key(System.Windows.Input.Key.D) + Key(System.Windows.Input.Key.Right) - Key(System.Windows.Input.Key.A) - Key(System.Windows.Input.Key.Left);
        var up = Key(System.Windows.Input.Key.E) - Key(System.Windows.Input.Key.Q);
        if (forward == 0 && right == 0 && up == 0) return;

        var speed = Math.Clamp(SceneRadius() * 0.25f, 1.5f, 60f);
        if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift)) speed *= 3;
        if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl)) speed *= 0.25f;
        MoveCamera(Camera.FlyDelta(new Vector3(forward, right, up), speed * (float)dt));
        Changed?.Invoke();
    }

    /// <summary>Applies a displacement, stopping at surfaces when collisions are on.</summary>
    private void MoveCamera(Vector3 delta)
    {
        Camera.Position = CollisionEnabled && Collision is { } collision
            ? collision.Move(Camera.Position, delta)
            : Camera.Position + delta;
    }

    /// <summary>Places the camera so the whole model is in view, from the usual three-quarter angle.</summary>
    public void FrameAll()
    {
        var (min, max) = Bounds();
        var framed = NavigationCamera.Frame(min, max);
        Camera.Position = framed.Position;
        Camera.YawDegrees = framed.YawDegrees;
        Camera.PitchDegrees = framed.PitchDegrees;
        Camera.FieldOfViewDegrees = framed.FieldOfViewDegrees;
        Changed?.Invoke();
    }

    /// <summary>Looks at the model from the side the given direction points to, keeping the whole model in view.</summary>
    public void LookFrom(Vector3 direction)
    {
        var (min, max) = Bounds();
        var center = (min + max) / 2;
        var radius = MathF.Max(3, (max - min).Length() / 2);
        var distance = radius / MathF.Tan(Camera.FieldOfViewDegrees * MathF.PI / 360) * 1.1f;
        Camera.Position = center + Vector3.Normalize(direction) * distance;
        Camera.LookAt(center);
        Changed?.Invoke();
    }

    /// <summary>Moves the camera toward (positive) or away from (negative) the model by a step scaled to its size.</summary>
    public void ZoomBy(float sign)
    {
        MoveCamera(Camera.Forward * SceneRadius() * 0.2f * Math.Sign(sign));
        Changed?.Invoke();
    }

    /// <summary>Recenters the view on the whole model, for when the user has lost it on screen.</summary>
    public void CenterAll()
    {
        var (min, max) = Bounds();
        var (width, height) = Size();
        Camera.FrameBounds(min, max, width / height);
        Changed?.Invoke();
    }
}
