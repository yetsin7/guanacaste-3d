using System.Numerics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

internal static partial class WorkspaceChecks
{
    /// <summary>Checks persistent graphics, normalized crops, polar camera bases, and direct-light visibility.</summary>
    private static void ReferenceGraphicsStateChecks()
    {
        var session = UseDocument((document, level) => document.AddWall(level, new(-4, 0), new(4, 0)));
        Invoke("ExecuteModelViewCommand", "Style.HiddenLine");
        var reopened = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(session.Document));
        Check(reopened.Views.Single(view => view.Id == BuiltInProjectViews.Model3DId).Graphics!.Style == ModelDisplayStyle.HiddenLine,
            "Native save/reopen preserves the actual model visual style");
        session.Undo();
        Invoke("RefreshAll");
        Check(Field<object>("_modelDisplayStyle").ToString() == "Textures", "Undo restores the previous graphics style and refreshes its controls");
        var surface = (FrameworkElement)_window.FindName("ModelSurface");
        var region = new Rect(surface.ActualWidth * 0.2, surface.ActualHeight * 0.2, surface.ActualWidth * 0.6, surface.ActualHeight * 0.6);
        Invoke("ApplyModelCrop", region);
        Check(surface.Clip is RectangleGeometry clip && Math.Abs(clip.Rect.Width - region.Width) < 0.01,
            "The crop clips the actual 3D surface at the selected screen region");
        Invoke("ExecuteViewNavigation", "SheetSize");
        var framingNavigator = Field<object>("_modelNavigator");
        var framingBounds = ((Func<(Vector3 Min, Vector3 Max)>)framingNavigator.GetType().GetProperty("Bounds")!.GetValue(framingNavigator)!)();
        foreach (var x in new[] { framingBounds.Min.X, framingBounds.Max.X })
        foreach (var y in new[] { framingBounds.Min.Y, framingBounds.Max.Y })
        foreach (var z in new[] { framingBounds.Min.Z, framingBounds.Max.Z })
            Check(region.Contains(Pixel(new(x, y, z))), "Fit print frame keeps each bounding-box corner inside its crop");
        reopened = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(session.Document));
        var graphics = reopened.Views.Single(view => view.Id == BuiltInProjectViews.Model3DId).Graphics!;
        Check(graphics.CropActive && graphics.Crop is { } crop && Math.Abs(crop.Width - 0.6) < 1e-6,
            "Native save/reopen preserves normalized crop coordinates and their enabled state");
        _window.Width = 1080;
        Pump();
        Check(surface.Clip is RectangleGeometry resized && Math.Abs(resized.Rect.Width / surface.ActualWidth - 0.6) < 1e-6,
            "Resizing retains the crop fraction without distorting the camera");
        Invoke("ExecuteModelViewCommand", "Crop.Toggle");
        Check(surface.Clip is null, "Disabling crop restores the whole model surface");
        Invoke("ExecuteModelViewCommand", "Crop.Reset");
        _window.Width = 1440;
        Pump();
        Invoke("ExecuteModelViewCommand", "Sun.Morning");
        Invoke("ExecuteModelViewCommand", "Shadows");
        WaitFor(() => !Field<bool>("_modelDirty"));
        var viewport = (Viewport3D)_window.FindName("ModelViewport");
        Check(viewport.Children.OfType<ModelVisual3D>().Select(visual => visual.Content).OfType<GeometryModel3D>()
            .Any(model => EmissiveColors(model.Material).Contains(Color.FromRgb(113, 119, 110))),
            "The shadow switch adds real projected ground-shadow geometry");
        reopened = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(session.Document));
        graphics = reopened.Views.Single(view => view.Id == BuiltInProjectViews.Model3DId).Graphics!;
        Check(graphics.Shadows && graphics.SunPreset == "Morning", "Solar preset and shadow settings survive native persistence");
        var navigator = Field<object>("_modelNavigator");
        foreach (var direction in new[] { Vector3.UnitZ, -Vector3.UnitZ })
        {
            navigator.GetType().GetMethod("LookFrom")!.Invoke(navigator, [direction]);
            var camera = (PerspectiveCamera)viewport.Camera;
            Check(Math.Abs(Vector3D.DotProduct(camera.LookDirection, camera.UpDirection)) < 1e-5 && camera.UpDirection.Length > 0.99,
                "Top and bottom cube targets retain a valid orthogonal WPF camera basis");
        }
        var source = session.Document;
        try { session.SetViewGraphics(BuiltInProjectViews.Model3DId, graphics with { FieldOfView = float.NaN }); }
        catch (System.IO.InvalidDataException) { }
        Check(ReferenceEquals(source, session.Document), "Invalid graphics cannot mutate the model or its history");
        var mesh = BuildingMesher.Build(session.Document, includeGround: false);
        var detailedSession = new DocumentSession(BuildingDocument.Create());
        var detailWall = detailedSession.AddWall(detailedSession.Document.Levels[0].Id, new(0, 0), new(6, 0));
        detailedSession.AddOpening(detailWall, OpeningKind.Window, 2);
        var detailedMesh = BuildingMesher.Build(detailedSession.Document, includeGround: false);
        var coarseMesh = SceneDetailFilter.Coarse(detailedMesh);
        Check(detailedMesh.TriangleKinds.Contains(SurfaceKind.OpeningFrame) && !coarseMesh.TriangleKinds.Contains(SurfaceKind.OpeningFrame) &&
            coarseMesh.TriangleKinds.Contains(SurfaceKind.Glass), "Coarse detail removes opening trim while retaining glazing and source geometry");
        Invoke("ExecuteModelViewCommand", "Detail.Coarse");
        Check(session.Document.Views.Single(view => view.Id == BuiltInProjectViews.Model3DId).Graphics!.Detail == ModelDetailLevel.Coarse,
            "The detail selector updates persistent model-view settings");
        Invoke("ExecuteModelViewCommand", "Detail.Medium");
        var tracer = new PathTracer(mesh, 64, 48, new(new(0, -5, 1), new(0, 0, 1), 45));
        var visibility = typeof(PathTracer).GetMethod("Visibility", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var shadowed = (Vector3)visibility.Invoke(tracer, [new Vector3(0, -1, 1), Vector3.UnitY, 10f])!;
        tracer.UpdateSettings(new RenderSettings { CastShadows = false });
        var unshadowed = (Vector3)visibility.Invoke(tracer, [new Vector3(0, -1, 1), Vector3.UnitY, 10f])!;
        Check(shadowed == Vector3.Zero && unshadowed == Vector3.One,
            "The realistic renderer's shadow setting controls actual light visibility through an occluder");
        Invoke("ExecuteModelViewCommand", "Lock");
        var savedCamera = Field<Guanacaste3D.Core.Navigation.NavigationCamera>("_navigation").Clone();
        var lockedDocument = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(session.Document));
        Invoke("AttachDocument", lockedDocument, null);
        Check(Vector3.Distance(Field<Guanacaste3D.Core.Navigation.NavigationCamera>("_navigation").Position, savedCamera.Position) < 1e-5,
            "Reopening a locked model view restores its saved camera before framing can change it");
        Invoke("ExecuteModelViewCommand", "Lock");
        Invoke("ExecuteModelViewCommand", "Shadows");
        Invoke("ExecuteModelViewCommand", "Sun.Noon");
        Invoke("ExecuteViewNavigation", "Home");
        WaitFor(() => !Field<bool>("_modelDirty"));
        SaveBitmap("reference-graphics-state.png");
    }
}
