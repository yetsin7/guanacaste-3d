using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;
using Guanacaste3D.Desktop.Controls;

internal static partial class WorkspaceChecks
{
    /// <summary>Exercises production flight, wall traversal, collision opt-in, and camera history without OS key injection.</summary>
    private static void FlightNavigationChecks()
    {
        var camera = new NavigationCamera { Position = new(0, 0, 1.6f), YawDegrees = 0, PitchDegrees = 60 };
        var horizontal = CameraTravel.Displacement(camera, Vector3.UnitX, 2, 0.1f, true);
        var flying = CameraTravel.Displacement(camera, Vector3.UnitX, 2, 0.1f, false);
        Check(Math.Abs(horizontal.Z) < 0.00001 && Math.Abs(horizontal.X - 0.2f) < 0.00001 && flying.Z > 0.1f,
            "Horizontal travel preserves eye height while free flight follows the pitched camera");
        var diagonal = CameraTravel.Displacement(camera, new(1, 1, 1), 2, 0.1f, true);
        Check(Math.Abs(diagonal.Length() - 0.2f) < 0.00001,
            "Combined forward, strafe, and elevation input cannot accelerate diagonal travel");
        Check(CameraTravel.Displacement(camera, Vector3.UnitX, float.NaN, 1, false) == Vector3.Zero &&
              CameraTravel.Displacement(camera, Vector3.UnitX, 2, -1, false) == Vector3.Zero,
            "Invalid speed and negative frame time cannot corrupt camera coordinates");
        var precision = CameraTravel.Displacement(camera, Vector3.UnitX, 2, 0.1f, true, precise: true);
        Check(Math.Abs(precision.Length() - 0.04f) < 0.00001, "Precision travel uses one fifth of the selected metric speed");

        var surface = new Border();
        surface.Measure(new Size(1000, 700));
        surface.Arrange(new Rect(0, 0, 1000, 700));
        camera.PitchDegrees = 0;
        var type = Field<object>("_modelNavigator").GetType();
        var navigator = Activator.CreateInstance(type, surface, camera)!;
        type.GetProperty("TravelSpeed")!.SetValue(navigator, 2f);
        type.GetProperty("FreeTravel")!.SetValue(navigator, true);
        type.GetProperty("Pick")!.SetValue(navigator, (Func<Vector3, Vector3, float?>)((_, _) => 0.1f));
        var mesh = new SceneMesh();
        mesh.AddQuad(new(0.2f, -5, 0), new(0.2f, 5, 0), new(0.2f, 5, 5), new(0.2f, -5, 5), SurfaceKind.Wall);
        type.GetProperty("Collision")!.SetValue(navigator, SceneQuery.Build(new RenderSceneBuilder().AddBuilding(mesh).Build()).Collision(0.05f));
        var wheel = type.GetMethod("ProcessWheel", PrivateInstance)!;
        wheel.Invoke(navigator, [new Point(500, 350), 120, ModifierKeys.None]);
        Check(Math.Abs(camera.Position.X - 0.5f) < 0.0001, "Render wheel crosses a nearby wall when collisions are disabled");
        camera.Position = new(0, 0, 1.6f);
        type.GetProperty("CollisionEnabled")!.SetValue(navigator, true);
        wheel.Invoke(navigator, [new Point(500, 350), 120, ModifierKeys.None]);
        Check(camera.Position.X < 0.2f, "The same free-travel wheel respects explicitly enabled wall collisions");
        type.GetProperty("CollisionEnabled")!.SetValue(navigator, false);
        camera.Position = new(0, 0, 1.6f);
        type.GetMethod("ResetHistory")!.Invoke(navigator, null);
        var flight = type.GetMethod("ApplyFlight", PrivateInstance)!;
        for (var frame = 0; frame < 30; frame++) flight.Invoke(navigator, [Vector3.UnitX, 0.1f, false, false]);
        flight.Invoke(navigator, [Vector3.Zero, 0.1f, false, false]);
        Check(Math.Abs(camera.Position.X - 6f) < 0.001, "Metric flight traverses walls independently of project size");
        type.GetMethod("RestoreView")!.Invoke(navigator, [false]);
        Check(camera.Position == new Vector3(0, 0, 1.6f), "One previous-view operation restores the start of a continuous flight");

        var panel = (RenderPanel)_window.FindName("RenderPanelControl");
        var collision = (CheckBox)panel.FindName("CollisionCheck");
        var card = (StackPanel)collision.Parent;
        var controls = card.Children.OfType<StackPanel>().Single();
        var slider = controls.Children.OfType<DockPanel>().Single().Children.OfType<Slider>().Single();
        var renderNavigator = panel.GetType().GetProperty("Navigator", PrivateInstance)!.GetValue(panel)!;
        slider.Value = 0.6;
        Check(Math.Abs((float)type.GetProperty("TravelSpeed")!.GetValue(renderNavigator)! - 0.6f) < 0.001,
            "The actual Render speed slider changes navigation speed");
        controls.Children.OfType<CheckBox>().Single().IsChecked = true;
        Check((bool)type.GetProperty("HorizontalTravel")!.GetValue(renderNavigator)!,
            "The actual Render horizontal-travel toggle controls interior navigation");
        slider.Value = 2;
        controls.Children.OfType<CheckBox>().Single().IsChecked = false;
        RenderFlightPreviewChecks(panel);
    }

    /// <summary>Verifies that camera movement updates the interactive scene without starting a ray-traced preview.</summary>
    private static void RenderFlightPreviewChecks(RenderPanel panel)
    {
        UseDocument((session, level) =>
        {
            session.AddWall(level, new(-4, -4), new(4, -4));
            session.AddWall(level, new(4, -4), new(4, 4));
            session.AddWall(level, new(4, 4), new(-4, 4));
            session.AddFloor(level, [new(-4, -4), new(4, -4), new(4, 4), new(-4, 4)]);
        });
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 6;
        ((TabControl)panel.FindName("SectionTabs")).SelectedIndex = 2;
        Pump();
        var viewport = (System.Windows.Controls.Viewport3D)panel.FindName("InteractiveViewport");
        WaitFor(() => viewport.Children.Count > 2 && viewport.Camera is not null);
        var previousPosition = ((System.Windows.Media.Media3D.PerspectiveCamera)viewport.Camera!).Position;
        var camera = Field<NavigationCamera>("_navigation");
        camera.Position = new(0, 0, 1.6f);
        camera.LookAt(new(4, 2, 1.6f));
        panel.GetType().GetMethod("OnCameraChanged", PrivateInstance)!.Invoke(panel, null);
        Check(((System.Windows.Media.Media3D.PerspectiveCamera)viewport.Camera!).Position != previousPosition &&
              panel.GetType().GetField("_preview", PrivateInstance)!.GetValue(panel) is null,
            "Render navigation moves the 3D camera without starting the preview worker");
        Check(Vector3.Distance(camera.Position, new(0, 0, 1.6f)) < 0.001,
            "The interactive interior view retains the requested eye position");
        SaveBitmap("render-interior-navigation.png");
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 1;
        Pump();
    }
}
