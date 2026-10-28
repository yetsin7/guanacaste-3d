using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Desktop.Controls;

internal static partial class WorkspaceChecks
{
    /// <summary>Exercises reference-image controls against real cameras and throwaway editable documents.</summary>
    private static void ReferenceNavigationChecks()
    {
        CameraHistoryChecks();
        var session = UseDocument((document, level) =>
        {
            var wall = document.AddWall(level, new(-4, 0), new(4, 0));
            document.AddOpening(wall, OpeningKind.Door, 2);
            document.AddOpening(wall, OpeningKind.Window, 5, sill: 1, height: 1);
            document.AddWall(level, new(4, 0), new(4, 5));
            document.AddFloor(level, [new(-4, -1), new(5, -1), new(5, 5), new(-4, 5)]);
        });
        var widget = (ModelNavigation)_window.FindName("ViewNavigation");
        var toolbar = (ModelViewToolbar)_window.FindName("ModelToolbar");
        var wheelMenu = ((ModelNavigationBar)widget.FindName("NavigationBar")).CreateWheelMenu();
        Check(wheelMenu.Items.OfType<MenuItem>().Count(item => item.IsEnabled) == 6 &&
            wheelMenu.Items.OfType<MenuItem>().Count(item => !item.IsEnabled) == 1,
            "The wheel menu offers all six 3D layouts and marks the plan-only wheel unavailable");
        foreach (var option in wheelMenu.Items.OfType<MenuItem>().Where(item => item.IsEnabled))
        {
            option.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var wheel = Field<ModelNavigationWheel>("_navigationWheel");
            Check(wheel.Visibility == Visibility.Visible && wheel.Mode == option.Tag as string,
                "Selecting a wheel layout opens the corresponding radial navigation control");
            var sectors = wheel.Children.OfType<System.Windows.Shapes.Path>().ToArray();
            Check(sectors.Length == (wheel.Mode.EndsWith("Full") ? 8 : 4), "Object and building wheels expose their correct sector count");
        }
        Invoke("ExecuteViewNavigation", "Wheel.Full");
        SaveBitmap("reference-full-wheel.png");
        Invoke("CancelNavigationOverlay");
        Check(toolbar.CreateMenu("Styles").Items.OfType<MenuItem>().Count() == 7,
            "Graphics settings and all six requested visual styles are available");
        NavigationToolBehaviorChecks();
        TemporaryVisibilityChecks(session);
        VisualStyleBehaviorChecks();
        Invoke("ExecuteModelViewCommand", "Style.Textures");
        WaitFor(() => !Field<bool>("_modelDirty"));
        Invoke("ExecuteViewNavigation", "Home");
        SaveBitmap("reference-view-controls.png");
        _window.Width = 960;
        _window.Height = 640;
        Pump();
        var toolbarViewer = Descendants(toolbar).OfType<ScrollViewer>().Single();
        Check(toolbarViewer.ViewportWidth > 200 && toolbar.ActualWidth <= ((FrameworkElement)_window.FindName("ModelSurface")).ActualWidth + 1,
            "The compact view control bar remains inside the workspace and exposes horizontal overflow");
        SaveBitmap("reference-controls-compact.png");
        _window.Width = 1440;
        _window.Height = 950;
    }

    /// <summary>Checks camera undo/redo branching and snapshot independence without a WPF rendering dependency.</summary>
    private static void CameraHistoryChecks()
    {
        var history = new CameraViewHistory();
        var camera = new NavigationCamera();
        history.Record(camera);
        camera.Position += Vector3.UnitX;
        history.Record(camera);
        Check(history.CanGoBack && !history.CanGoForward, "A completed camera move enables backward view history");
        Check(history.Restore(camera, false) && camera.Position.X == 10 && history.CanGoForward,
            "Previous restores an independent camera snapshot and enables Next");
        Check(history.Restore(camera, true) && camera.Position.X == 11, "Next restores the later view");
        history.Restore(camera, false);
        camera.Position += Vector3.UnitY;
        history.Record(camera);
        Check(!history.CanGoForward, "New navigation after rewind discards the abandoned forward branch");
    }

    /// <summary>Validates region magnification, camera locks, wheel gestures, and reverse navigation.</summary>
    private static void NavigationToolBehaviorChecks()
    {
        var camera = Field<NavigationCamera>("_navigation");
        var navigator = Field<object>("_modelNavigator");
        var surface = (FrameworkElement)_window.FindName("ModelSurface");
        Invoke("ExecuteViewNavigation", "Home");
        var origin = camera.Position;
        navigator.GetType().GetMethod("ZoomRegion")!.Invoke(navigator,
            [new Rect(surface.ActualWidth / 4, surface.ActualHeight / 4, surface.ActualWidth / 2, surface.ActualHeight / 2)]);
        Check(Vector3.Distance(camera.Position, origin) > 0.1 && float.IsFinite(camera.Position.X),
            "A center-half region magnifies the real camera at a finite picked depth");
        Invoke("ExecuteViewNavigation", "Previous");
        Check(Vector3.Distance(camera.Position, origin) < 1e-4f, "Previous restores the exact pre-region camera");
        Invoke("ExecuteViewNavigation", "Next");
        Check(Vector3.Distance(camera.Position, origin) > 0.1, "Next restores the region zoom");
        Invoke("ExecuteViewNavigation", "Previous");
        Invoke("ExecuteModelViewCommand", "Lock");
        Invoke("ExecuteViewNavigation", "Reduce");
        Invoke("ExecuteViewNavigation", "Left");
        Invoke("ExecuteModelViewCommand", "Lens.Wide");
        Check(Vector3.Distance(camera.Position, origin) < 1e-4f && camera.FieldOfViewDegrees == 45,
            "Camera lock blocks dolly, cube orbit, and lens commands");
        Invoke("ExecuteModelViewCommand", "Lock");
        foreach (var action in new[] { "Orbit", "Pan", "Look", "Walk", "UpDown", "Zoom", "Forward" })
        {
            var before = camera.Clone();
            navigator.GetType().GetMethod("ApplyWheelGesture")!.Invoke(navigator, [action, 14f, 12f]);
            Check(Vector3.Distance(camera.Position, before.Position) > 0.001 || Math.Abs(camera.YawDegrees - before.YawDegrees) > 0.1,
                $"The {action} wheel sector changes the shared navigation camera");
        }
        Invoke("ExecuteViewNavigation", "Home");
    }

    /// <summary>Proves temporary hide/isolate never edits saved visibility until explicitly applied.</summary>
    private static void TemporaryVisibilityChecks(DocumentSession session)
    {
        var wall = session.Document.Walls[0];
        var opening = session.Document.Openings[0];
        var original = session.Document;
        SetField("_selectedWallId", wall.Id);
        SetField("_selectedOpeningId", opening.Id);
        Invoke("ExecuteModelViewCommand", "Temporary.Hide");
        var visible = (BuildingDocument)Invoke("ModelDisplayDocument", false)!;
        Check(ReferenceEquals(session.Document, original) && visible.Walls.Count == 2 && !visible.Openings.Any(item => item.Id == opening.Id),
            "Temporarily hiding a door leaves its wall visible and does not create a document transaction");
        Check(session.Document.Views.Single(view => view.Id == BuiltInProjectViews.Model3DId).HiddenElementIds.Count == 0,
            "Temporary filters do not change saved view visibility");
        Invoke("ExecuteModelViewCommand", "Temporary.Reset");
        visible = (BuildingDocument)Invoke("ModelDisplayDocument", false)!;
        Check(visible.Openings.Count == 2, "Reset restores hidden openings");
        SetField("_selectedOpeningId", null);
        Invoke("ExecuteModelViewCommand", "Temporary.Isolate");
        visible = (BuildingDocument)Invoke("ModelDisplayDocument", false)!;
        Check(visible.Walls.Count == 1 && visible.Openings.Count == 2 && visible.Floors.Count == 0,
            "Isolating a wall retains its hosted doors/windows while filtering other architecture");
        Invoke("ExecuteModelViewCommand", "Temporary.Reset");
        Invoke("ExecuteModelViewCommand", "Temporary.HideCategory");
        visible = (BuildingDocument)Invoke("ModelDisplayDocument", false)!;
        Check(visible.Walls.Count == 0 && visible.Floors.Count == 1, "Hide category filters every wall without hiding floors");
        Invoke("ExecuteModelViewCommand", "Temporary.Apply");
        Check(!Field<bool>("_temporaryActive") && session.Document.Views.Single(view => view.Id == BuiltInProjectViews.Model3DId).HiddenElementIds.Count > 0,
            "Apply explicitly promotes transient filtering into one persistent view edit");
        session.Undo();
        Invoke("ApplyViewVisibility");
        visible = (BuildingDocument)Invoke("ModelDisplayDocument", false)!;
        Check(visible.Walls.Count == 2, "One document undo restores promoted visibility");
        Invoke("ExecuteModelViewCommand", "Temporary.Hide");
        Invoke("ExecuteModelViewCommand", "Reveal");
        Check(((BuildingDocument)Invoke("ModelDisplayDocument", true)!).Walls.Count == 2,
            "Reveal mode exposes hidden geometry to the full-document picker");
        Invoke("ExecuteModelViewCommand", "Unhide");
        Check(((BuildingDocument)Invoke("ModelDisplayDocument", false)!).Walls.Count == 2, "Unhide restores selected hidden elements");
        Invoke("ExecuteModelViewCommand", "Reveal");
        Invoke("ExecuteModelViewCommand", "Temporary.Reset");
    }

    /// <summary>Checks real material changes and a progressive path-traced frame in the modeling workspace.</summary>
    private static void VisualStyleBehaviorChecks()
    {
        SetField("_selectedWallId", null);
        SetField("_selectedOpeningId", null);
        Invoke("UpdateModelSelection", true);
        var viewport = (Viewport3D)_window.FindName("ModelViewport");
        foreach (var style in new[] { "Wireframe", "HiddenLine", "Shaded", "ConsistentColors", "Textures" })
        {
            Invoke("ExecuteModelViewCommand", "Style." + style);
            WaitFor(() => !Field<bool>("_modelDirty"));
            var models = viewport.Children.OfType<ModelVisual3D>().Select(visual => visual.Content).OfType<GeometryModel3D>().ToArray();
            Check(style switch
            {
                "Wireframe" => models.Length == 1 && EmissiveColors(models[0].Material).Any(),
                "HiddenLine" => models.Any(model => EmissiveColors(model.Material).Contains(Colors.White)),
                "ConsistentColors" => models.Any(model => EmissiveColors(model.Material).Any()),
                _ => models.Any(model => model.Material is DiffuseMaterial)
            }, $"The {style} selection changes the actual scene representation");
            Check(VisibleModelPixelCount() > 100, $"The {style} model remains visibly rendered against its white background");
            if (style == "HiddenLine") SaveBitmap("reference-hidden-line.png");
        }
        Invoke("ExecuteModelViewCommand", "Style.Realistic");
        WaitFor(() => !Field<bool>("_modelDirty"));
        var image = Field<Image>("_modelRealisticImage");
        WaitFor(() => image.Source is not null && image.Visibility == Visibility.Visible);
        Check(image.Source!.Width >= 64, "Realistic mode publishes a path-traced image over the editable model surface");
        Check(Field<Exception?>("_modelPreviewFailure") is null, "Realistic rendering completes without an active preview failure");
        SaveBitmap("reference-realistic.png");
        Invoke("ExecuteModelViewCommand", "Style.Textures");
    }

    /// <summary>Finds actual emission colors within composite WPF materials without assuming a specific container shape.</summary>
    private static IEnumerable<Color> EmissiveColors(Material material)
    {
        if (material is EmissiveMaterial { Brush: SolidColorBrush brush }) yield return brush.Color;
        if (material is not MaterialGroup group) yield break;
        foreach (var child in group.Children)
        foreach (var color in EmissiveColors(child)) yield return color;
    }

    /// <summary>Samples the real composed model surface so invisible meshes cannot satisfy visual-style assertions.</summary>
    private static int VisibleModelPixelCount()
    {
        var surface = (FrameworkElement)_window.FindName("ModelSurface");
        var width = Math.Max(1, (int)surface.ActualWidth);
        var height = Math.Max(1, (int)surface.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var visible = 0;
        for (var index = 0; index < pixels.Length; index += 4)
            if (pixels[index + 3] > 200 && Math.Min(pixels[index], Math.Min(pixels[index + 1], pixels[index + 2])) < 190) visible++;
        return visible;
    }
}
