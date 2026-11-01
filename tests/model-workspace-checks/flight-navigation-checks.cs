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

    /// <summary>Verifies navigation resolution and settled recovery against the real live-render worker and WPF image.</summary>
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
        var image = (Image)panel.FindName("PreviewImage");
        WaitFor(() => image.Source is not null);
        /// <summary>Copies the displayed pixels to distinguish a newly rendered view from a reused bitmap.</summary>
        byte[] CapturePixels()
        {
            var bitmap = (System.Windows.Media.Imaging.BitmapSource)image.Source;
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }
        var previousPixels = CapturePixels();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var camera = Field<NavigationCamera>("_navigation");
        camera.Position = new(0, 0, 1.6f);
        camera.LookAt(new(4, 2, 1.6f));
        panel.GetType().GetMethod("OnCameraChanged", PrivateInstance)!.Invoke(panel, null);
        var size = ((int Width, int Height))panel.GetType().GetMethod("PreviewSize", PrivateInstance)!.Invoke(panel, null)!;
        Check(size.Height <= 144, "Camera motion reduces live preview cost immediately");
        WaitFor(() => !(bool)panel.GetType().GetField("_interactivePreview", PrivateInstance)!.GetValue(panel)!);
        var settled = ((int Width, int Height))panel.GetType().GetMethod("PreviewSize", PrivateInstance)!.Invoke(panel, null)!;
        WaitFor(() => image.Source is System.Windows.Media.Imaging.BitmapSource bitmap && bitmap.PixelHeight == settled.Height &&
                      !CapturePixels().SequenceEqual(previousPixels));
        Check(settled.Height >= size.Height, "Settling restores the hardware-selected preview resolution");
        var status = (TextBlock)panel.FindName("StatusText");
        var ready = string.Format((string)Application.Current.FindResource("RenderPanel.PreviewReady"), 40);
        WaitFor(() => elapsed.Elapsed.TotalSeconds >= 1 && status.Text == ready);
        Check(Vector3.Distance(camera.Position, new(0, 0, 1.6f)) < 0.001,
            "The settled interior render retains the requested eye position");
        SaveBitmap("render-interior-navigation.png");
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 1;
        Pump();
    }
}
