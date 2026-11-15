using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;
using Guanacaste3D.Desktop.Controls;

internal static partial class WorkspaceChecks
{
    /// <summary>Verifies all 26 orientations, aspect-aware fitting, cube controls, and compact viewport containment.</summary>
    private static void NavigationChecks()
    {
        UseDocument((session, level) => session.AddWall(level, new(-9, 0), new(9, 0), height: 7));
        var widget = (ModelNavigation)_window.FindName("ViewNavigation");
        var navigator = Field<object>("_modelNavigator");
        var lookFrom = navigator.GetType().GetMethod("LookFrom")!;
        var bounds = ((Func<(Vector3 Min, Vector3 Max)>)navigator.GetType().GetProperty("Bounds")!.GetValue(navigator)!)();
        var camera = Field<NavigationCamera>("_navigation");
        var surface = (FrameworkElement)_window.FindName("ModelSurface");
        var directions = new HashSet<Vector3>();
        foreach (var x in new[] { -1, 0, 1 })
        foreach (var y in new[] { -1, 0, 1 })
        foreach (var z in new[] { -1, 0, 1 })
        {
            var direction = new Vector3(x, y, z);
            if (direction == Vector3.Zero) continue;
            directions.Add(direction);
            lookFrom.Invoke(navigator, [direction]);
            Check(Vector3.Dot(camera.Forward, -Vector3.Normalize(direction)) > 0.99999f,
                $"Cube direction {direction} aligns the shared camera accurately");
            foreach (var horizontal in new[] { bounds.Min.X, bounds.Max.X })
            foreach (var depth in new[] { bounds.Min.Y, bounds.Max.Y })
            foreach (var vertical in new[] { bounds.Min.Z, bounds.Max.Z })
            {
                var pixel = Pixel(new(horizontal, depth, vertical));
                Check(pixel.X >= 0 && pixel.X <= surface.ActualWidth && pixel.Y >= 0 && pixel.Y <= surface.ActualHeight,
                    "Orientation framing keeps the entire bounding box within the viewport");
            }
        }
        Check(directions.Count == 26, "Faces, edges, and corners expose 26 distinct orientations");
        var faceCenter = ViewCubeDirections.FromFace(-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, new(0.5f, 0.5f));
        var corner = ViewCubeDirections.FromFace(-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, new(0.9f, 0.1f));
        Check(faceCenter == -Vector3.UnitY && corner == new Vector3(1, -1, 1),
            "Face hit regions distinguish a face center from its adjacent corner");
        // The cube glides between orientations but must land on exactly the pose the instant snap produces.
        var animated = navigator.GetType().GetMethod("LookFromAnimated")!;
        var transitioning = navigator.GetType().GetProperty("IsTransitioning")!;
        lookFrom.Invoke(navigator, [new Vector3(1, -1, 1)]);
        var expected = camera.Clone();
        lookFrom.Invoke(navigator, [new Vector3(-1, 1, 0.3f)]);
        animated.Invoke(navigator, [new Vector3(1, -1, 1)]);
        Check((bool)transitioning.GetValue(navigator)!, "A cube click starts a camera glide instead of cutting");
        WaitFor(() => !(bool)transitioning.GetValue(navigator)!);
        Check(Vector3.Distance(camera.Position, expected.Position) < 1e-3f &&
              Math.Abs(camera.YawDegrees - expected.YawDegrees) < 1e-3f && Math.Abs(camera.PitchDegrees - expected.PitchDegrees) < 1e-3f,
            "The cube glide ends exactly on the snapped orientation");
        lookFrom.Invoke(navigator, [new Vector3(-1, 1, 0.3f)]);
        animated.Invoke(navigator, [new Vector3(1, -1, 1)]);
        camera.YawDegrees += 7;
        Pump();
        Pump();
        Check(!(bool)transitioning.GetValue(navigator)! && Math.Abs(camera.YawDegrees - expected.YawDegrees) > 1,
            "Moving the camera during a glide cancels it instead of fighting the user");
        lookFrom.Invoke(navigator, [new Vector3(1, -1, 1)]);
        var pitchBefore = camera.PitchDegrees;
        Invoke("ExecuteViewNavigation", "TiltDown");
        var pitchDown = camera.PitchDegrees;
        Invoke("ExecuteViewNavigation", "TiltUp");
        Check(pitchDown > pitchBefore && Math.Abs(camera.PitchDegrees - pitchBefore) < 0.01f,
            "The cube's tilt arrows raise and lower the viewpoint symmetrically");
        var home = Descendants(widget).OfType<Button>().Single(button => button.Tag as string == "Home");
        home.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        widget.Redraw();
        var cube = (Canvas)widget.FindName("CubeCanvas");
        Check(cube.Children.OfType<Polygon>().Count() == 3 && cube.Children.OfType<Border>().Count(label => !label.RenderTransform.Value.IsIdentity) == 3,
            "The home view projects three cube faces with matching transformed labels");
        SaveBitmap("navigation-home.png");
        SaveWidgetBitmap(widget, "cube-home.png");
        var faces = cube.Children.OfType<Polygon>().ToArray();
        var hovered = faces.Select(face => face.Points.Aggregate(new Point(), (sum, point) => new Point(sum.X + point.X / 4, sum.Y + point.Y / 4))).ToArray();
        var cornerPoint = new Point(faces[0].Points[0].X * 0.85 + hovered[0].X * 0.15, faces[0].Points[0].Y * 0.85 + hovered[0].Y * 0.15);
        widget.HoverAt(faces[0], cornerPoint);
        var caption = cube.Children.OfType<Border>().Select(border => border.Child).OfType<TextBlock>().FirstOrDefault(text => text.Text.Contains('·'));
        Check(caption is not null && caption.Text.Split('·').Length == 3,
            "Hovering a cube corner names its three orientations in a caption");
        SaveWidgetBitmap(widget, "cube-hover-corner.png");
        widget.HoverAt(faces[0], hovered[0]);
        caption = cube.Children.OfType<Border>().Select(border => border.Child).OfType<TextBlock>().FirstOrDefault(text => text.Foreground is SolidColorBrush { Color.R: 255 } && text.FontSize == 10);
        Check(caption is { Text.Length: > 0 } && !caption.Text.Contains('·'), "Hovering a face center names just that face");
        SaveWidgetBitmap(widget, "cube-hover-face.png");
        widget.Redraw();
        foreach (var (label, view) in new[] { ("top", new Vector3(0, 0, 1)), ("bottom", new Vector3(0, 0, -1)),
                     ("back", new Vector3(0, 1, 0)), ("corner", new Vector3(-1, 1, 1)), ("flat", new Vector3(1, -1, 0.05f)) })
        {
            lookFrom.Invoke(navigator, [view]);
            widget.Redraw();
            SaveWidgetBitmap(widget, $"cube-{label}.png");
        }
        lookFrom.Invoke(navigator, [new Vector3(1, -1, 1)]);
        widget.Redraw();
        Check(cube.Children.OfType<TextBlock>().Any(text => text.Text.Split('·').Length == 3),
            "The cube names the current standard view when the camera sits on a corner orientation");
        lookFrom.Invoke(navigator, [new Vector3(0.3f, -1, 0.5f)]);
        widget.Redraw();
        Check(!cube.Children.OfType<TextBlock>().Any(),
            "The cube shows no view name while the camera is between standard orientations");
        home.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        widget.Redraw();
        _window.Width = 960;
        _window.Height = 640;
        Pump();
        var origin = widget.TransformToAncestor(surface.Parent as Visual ?? _window).Transform(new Point());
        Check(origin.X >= 0 && origin.Y >= 0 && origin.X + widget.ActualWidth <= surface.ActualWidth + 1 &&
              origin.Y + widget.ActualHeight <= surface.ActualHeight + 1,
            "The cube and navigation toolbar remain inside a 960 by 640 workspace");
        SaveBitmap("navigation-compact.png");
        _window.Width = 1440;
        _window.Height = 950;
        Pump();
    }

    /// <summary>Checks render-only neutral materials, preserved source assignments, and colored model visuals.</summary>
    private static void MaterialChecks()
    {
        var session = UseDocument((document, level) =>
        {
            var wall = document.AddWall(level, new(-4, 0), new(4, 0));
            document.AddOpening(wall, OpeningKind.Door, 2);
            document.AddOpening(wall, OpeningKind.Window, 5, sill: 1, height: 1);
            document.AddFloor(level, [new(-4, 0), new(4, 0), new(4, 4), new(-4, 4)]);
        });
        var mesh = BuildingMesher.Build(session.Document);
        var source = new RenderSceneBuilder().AddBuilding(mesh).Build();
        var previous = source.Materials.Select(material => material.BaseColor).ToArray();
        var styled = RenderSceneStyles.Clay(source);
        Check(styled.Materials.All(material => material.BaseColor.X == material.BaseColor.Y && material.BaseColor.Y == material.BaseColor.Z),
            "The render study style uses neutral materials across all categories");
        Check(source.Materials.Select(material => material.BaseColor).SequenceEqual(previous) &&
              styled.TriangleCount == source.TriangleCount && styled.Materials.All(material => material.Texture is null),
            "Clay rendering preserves source materials and geometry while removing texture overrides");
        Check(_window.FindName("ClayCheck") is null, "The modeling workspace does not expose the render-only gray style");
        var panel = (RenderPanel)_window.FindName("RenderPanelControl");
        var clay = (CheckBox)panel.FindName("RenderClayCheck");
        clay.IsChecked = true;
        Check(Field<bool>("_renderClay"), "The Render panel controls the actual render scene override");
        Invoke("RebuildModelVisual");
        WaitFor(() => !Field<bool>("_modelDirty"));
        var viewport = (Viewport3D)_window.FindName("ModelViewport");
        var colored = viewport.Children.OfType<ModelVisual3D>().Select(visual => visual.Content).OfType<GeometryModel3D>()
            .Any(model => model.Material is DiffuseMaterial { Brush: SolidColorBrush brush } &&
                          brush.Color.R > brush.Color.G + 20 && brush.Color.G > brush.Color.B + 15);
        Check(colored, "Door wood remains colored in the model while the render uses gray materials");
        clay.IsChecked = false;
        Check(!Field<bool>("_renderClay"), "The Render panel restores assigned materials when the override is disabled");
        Aim(new(0, 1, 1), new(10, -13, 10));
        SaveBitmap("colored-model.png");
    }

    /// <summary>Walks visual descendants to exercise the actual navigation buttons without OS input injection.</summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
