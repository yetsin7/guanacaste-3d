using System.Numerics;
using System.Reflection;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Desktop.Controls;

internal static partial class WorkspaceChecks
{
    /// <summary>Verifies selection, full-project fallback, edit invalidation, and shared render orbit policy in WPF.</summary>
    private static void OrbitInteractionChecks()
    {
        Guid selected = default;
        Guid far = default;
        var session = UseDocument((document, level) =>
        {
            selected = document.AddWall(level, new(0, 0), new(4, 0));
            far = document.AddWall(level, new(35, 0), new(45, 0), height: 8);
        });
        var navigator = Field<object>("_modelNavigator");
        var pivotProperty = navigator.GetType().GetProperty("OrbitPivot")!;
        var resolve = (Func<Vector3>)pivotProperty.GetValue(navigator)!;
        var whole = BuildingMesher.Build(session.Document).Bounds();
        var center = (whole.Min + whole.Max) / 2;
        Check(Vector3.Distance(resolve(), center) < 0.0001f, "No selection orbits about the entire project center");
        SetField("_selectedWallId", selected);
        var selectionCenter = new Vector3(2, 0, 1.5f);
        Check(Vector3.Distance(resolve(), selectionCenter) < 0.0001f,
            "A selected wall overrides the project orbit center");
        SetField("_viewSelection", new HashSet<Guid> { selected, far });
        Check(Vector3.Distance(resolve(), center) < 0.0001f,
            "A multiple-element selection orbits about the combined selection bounds");
        SetField("_viewSelection", null);
        var panel = (RenderPanel)_window.FindName("RenderPanelControl");
        var renderNavigator = typeof(RenderPanel).GetField("_navigator", PrivateInstance)!.GetValue(panel)!;
        var renderResolve = (Func<Vector3>)pivotProperty.GetValue(renderNavigator)!;
        Check(renderResolve() == resolve(), "The render workspace uses the same selection pivot as the 3D view");

        Aim(new(0, 0, 1), new(8, -12, 8));
        var camera = Field<NavigationCamera>("_navigation");
        var originalPixel = Pixel(selectionCenter);
        var orbitMode = navigator.GetType().GetMethod("UpdateOrbitMode", PrivateInstance)!;
        var activePivot = navigator.GetType().GetField("_orbitPivot", PrivateInstance)!;
        orbitMode.Invoke(navigator, [true]);
        Check((Vector3)activePivot.GetValue(navigator)! == selectionCenter,
            "Shift beginning during a middle-button pan captures the selected element center");
        SetField("_selectedWallId", far);
        orbitMode.Invoke(navigator, [true]);
        Check((Vector3)activePivot.GetValue(navigator)! == selectionCenter,
            "Selection changes do not move the pivot during an active orbit gesture");
        orbitMode.Invoke(navigator, [false]);
        Check(!(bool)navigator.GetType().GetField("_orbiting", PrivateInstance)!.GetValue(navigator)!,
            "Releasing Shift switches the existing middle-button gesture back to pan");
        SetField("_selectedWallId", selected);
        navigator.GetType().GetMethod("OrbitBy")!.Invoke(navigator, [25f, 10f]);
        Check((Pixel(selectionCenter) - originalPixel).Length < 0.01,
            "The production orbit command preserves an off-center selection's pixel");

        session.MoveWall(selected, 3, 4);
        Check(Vector3.Distance(resolve(), new(5, 4, 1.5f)) < 0.0001f,
            "Editing the selected element invalidates the cached pivot immediately");
        session.Undo();
        Check(Vector3.Distance(resolve(), selectionCenter) < 0.0001f, "Undo restores the original selection pivot");
        SetField("_selectedWallId", null);
        var modelView = session.Document.Views.Single(view => view.Kind == ProjectViewKind.Model3D);
        session.SetViewVisibility(modelView.Id, [far], []);
        Invoke("RefreshAll");
        WaitFor(() => !Field<bool>("_modelDirty"));
        Check(Vector3.Distance(resolve(), center) < 0.0001f,
            "Hiding a distant element does not replace the full-project orbit center with visible bounds");
        SetField("_selectedWallId", selected);
        session.RemoveWall(selected);
        whole = BuildingMesher.Build(session.Document).Bounds();
        Check(Vector3.Distance(resolve(), (whole.Min + whole.Max) / 2) < 0.0001f,
            "A deleted selection safely falls back to the current project center");
        SetField("_selectedWallId", null);
        UseDocument((_, _) => { });
        Check(Vector3.Distance(resolve(), new(0, 0, 1.5f)) < 0.0001f,
            "An empty project has a finite stable orbit center");
    }
}
