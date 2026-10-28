using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core.Navigation;

internal static partial class WorkspaceChecks
{
    /// <summary>Replays input samples through production navigation while checking camera, editor, and history effects.</summary>
    private static void PointerGestureChecks()
    {
        var surface = new Border();
        surface.Measure(new Size(1000, 700));
        surface.Arrange(new Rect(0, 0, 1000, 700));
        var camera = new NavigationCamera { Position = new(8, -12, 7) };
        camera.LookAt(new(0, 0, 1));
        var navigatorType = Field<object>("_modelNavigator").GetType();
        var navigator = Activator.CreateInstance(navigatorType, surface, camera)!;
        var movement = navigatorType.GetMethod("ProcessPointerMove", PrivateInstance)!;
        var wheel = navigatorType.GetMethod("ProcessWheel", PrivateInstance)!;
        var notifications = 0;
        navigatorType.GetEvent("Changed")!.AddEventHandler(navigator, (Action)(() => notifications++));
        var initial = camera.Clone();
        foreach (var button in new[] { MouseButton.Right, MouseButton.Middle })
        {
            PreparePointerGesture(navigator, button);
            movement.Invoke(navigator, [new Point(102, 101), ModifierKeys.None]);
            movement.Invoke(navigator, [new Point(100, 100), ModifierKeys.None]);
            Check(camera.Position == initial.Position && camera.YawDegrees == initial.YawDegrees && notifications == 0,
                $"{button} click jitter does not move the camera or request a redraw");
        }
        movement.Invoke(navigator, [new Point(112, 100), ModifierKeys.None]);
        Check(camera.Position != initial.Position && notifications == 1,
            "Crossing the drag threshold starts one real pan using the full displacement");
        var pivot = new Vector3(0, 0, 1);
        navigatorType.GetProperty("OrbitPivot")!.SetValue(navigator, (Func<Vector3>)(() => pivot));
        var projected = camera.Project(pivot, 1000, 700)!.Value;
        var previousYaw = camera.YawDegrees;
        movement.Invoke(navigator, [new Point(137, 105), ModifierKeys.Shift]);
        var orbited = camera.Project(pivot, 1000, 700)!.Value;
        Check(camera.YawDegrees != previousYaw && Math.Abs(orbited.X - projected.X) < 0.01 &&
              Math.Abs(orbited.Y - projected.Y) < 0.01,
            "Pressing Shift during the sampled middle drag orbits the chosen pivot without reframing");
        previousYaw = camera.YawDegrees;
        movement.Invoke(navigator, [new Point(149, 105), ModifierKeys.None]);
        Check(camera.YawDegrees == previousYaw, "Releasing Shift resumes pan without rotating the camera");
        var beforeLock = camera.Clone();
        navigatorType.GetProperty("IsLocked")!.SetValue(navigator, true);
        movement.Invoke(navigator, [new Point(220, 150), ModifierKeys.Shift]);
        Check(camera.Position == beforeLock.Position && camera.YawDegrees == beforeLock.YawDegrees,
            "Locking the camera also blocks an already-started pointer gesture");
        navigatorType.GetProperty("IsLocked")!.SetValue(navigator, false);
        movement.Invoke(navigator, [new Point(221, 150), ModifierKeys.None]);
        Check(Vector3.Distance(camera.Position, beforeLock.Position) < 0.1f,
            "Unlocking resumes at the latest pointer sample instead of jumping across the locked movement");
        navigatorType.GetField("_dragButton", PrivateInstance)!.SetValue(navigator, null);
        var beforeWheel = camera.Clone();
        var beforeNotifications = notifications;
        wheel.Invoke(navigator, [new Point(500, 350), 0, ModifierKeys.None]);
        Check(camera.Position == beforeWheel.Position && notifications == beforeNotifications,
            "A zero-delta wheel event creates neither movement nor rendering work");
        navigatorType.GetProperty("Pick")!.SetValue(navigator,
            (Func<Vector3, Vector3, float?>)((_, _) => 10));
        wheel.Invoke(navigator, [new Point(500, 350), 15, ModifierKeys.None]);
        var fineDistance = Vector3.Distance(camera.Position, beforeWheel.Position);
        camera.Position = beforeWheel.Position;
        wheel.Invoke(navigator, [new Point(500, 350), 120, ModifierKeys.None]);
        Check(fineDistance > 0 && fineDistance < Vector3.Distance(camera.Position, beforeWheel.Position) / 4,
            "The actual wheel handler preserves high-resolution deltas instead of zooming a full notch");
        EditingDragChecks(navigator, movement);
        WheelPrecisionChecks();
    }

    /// <summary>Resets only pointer state so each sampled gesture begins from a known press location.</summary>
    private static void PreparePointerGesture(object navigator, MouseButton button)
    {
        var type = navigator.GetType();
        type.GetField("_dragButton", PrivateInstance)!.SetValue(navigator, button);
        type.GetField("_pressPoint", PrivateInstance)!.SetValue(navigator, new Point(100, 100));
        type.GetField("_last", PrivateInstance)!.SetValue(navigator, new Point(100, 100));
        type.GetField("_dragged", PrivateInstance)!.SetValue(navigator, false);
        type.GetField("_elementDragging", PrivateInstance)!.SetValue(navigator, false);
        type.GetField("_orbiting", PrivateInstance)!.SetValue(navigator, false);
    }

    /// <summary>Checks that jitter filtering leaves model dragging lazy and capture interruption cancels exactly once.</summary>
    private static void EditingDragChecks(object navigator, System.Reflection.MethodInfo movement)
    {
        var type = navigator.GetType();
        var claimed = 0;
        var moved = 0;
        var cancelled = 0;
        type.GetProperty("BeginElementDrag")!.SetValue(navigator, (Func<Point, bool>)(_ => { claimed++; return true; }));
        type.GetEvent("ElementDragMoved")!.AddEventHandler(navigator, (Action<Point>)(_ => moved++));
        type.GetEvent("ElementDragCancelled")!.AddEventHandler(navigator, (Action)(() => cancelled++));
        PreparePointerGesture(navigator, MouseButton.Left);
        movement.Invoke(navigator, [new Point(103, 101), ModifierKeys.None]);
        Check(claimed == 0 && moved == 0, "Selection click jitter does not begin a model edit");
        movement.Invoke(navigator, [new Point(111, 107), ModifierKeys.None]);
        Check(claimed == 1 && moved == 1, "An intentional selection drag claims and previews the edit once");
        var lostCapture = type.GetMethod("OnLostMouseCapture", PrivateInstance)!;
        lostCapture.Invoke(navigator, [navigator, new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)]);
        lostCapture.Invoke(navigator, [navigator, new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)]);
        movement.Invoke(navigator, [new Point(150, 170), ModifierKeys.None]);
        Check(cancelled == 1 && moved == 1, "Interrupted capture cancels once and stops further edit samples");
    }

    /// <summary>Checks device-independent wheel magnification, reversibility, limits, and malformed focus values.</summary>
    private static void WheelPrecisionChecks()
    {
        var remaining = 10f;
        for (var sample = 0; sample < 8; sample++) remaining -= NavigationZoom.DollyStep(remaining, 15, 100);
        var fullNotch = 10 - NavigationZoom.DollyStep(10, 120, 100);
        Check(Math.Abs(remaining - fullNotch) < 0.0001,
            "Eight fractional wheel samples produce the same magnification as one full notch");
        remaining -= NavigationZoom.DollyStep(remaining, -120, 100);
        Check(Math.Abs(remaining - 10) < 0.0001, "Reversing wheel zoom returns to the original focus distance");
        Check(Math.Abs(NavigationZoom.DollyStep(10, 240, 100) - 3.6f) < 0.0001 &&
              NavigationZoom.DollyStep(10, 120, 100, true) == NavigationZoom.DollyStep(10, 240, 100),
            "Multiple notches and accelerated zoom retain their complete wheel magnitude");
        Check(NavigationZoom.DollyStep(0.02f, 120, 100) == 0 &&
              NavigationZoom.DollyStep(0.02f, -120, 100) < 0 &&
              NavigationZoom.DollyStep(10, int.MinValue, 2) >= -1.2f,
            "Zoom retains minimum surface clearance and bounds extreme outward movement");
        Check(NavigationZoom.DollyStep(float.NaN, 120, 5) == 0 &&
              NavigationZoom.DollyStep(10, 120, float.PositiveInfinity) == 0,
            "Invalid scene depths cannot turn camera coordinates into non-finite values");
    }
}
