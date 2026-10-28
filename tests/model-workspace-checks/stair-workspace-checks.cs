using System.IO;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;

internal static partial class WorkspaceChecks
{
    /// <summary>Loads a stair study in the real model view and verifies properties, selection bounds, and native persistence.</summary>
    private static void StairWorkspaceChecks()
    {
        var session = UseDocument((document, level) =>
        {
            var station = 0;
            foreach (var preset in ArchitecturalLibrary.PresetsFor(StructuralElementKind.Stair))
            {
                var stair = document.AddElementFromPreset(level, preset, new(station * 8, 0), new(station * 8 + 4, 0));
                document.AddRailingsOnHost(stair);
                station++;
            }
        });
        var stairs = session.Document.StructuralElements.Where(element => element.Kind == StructuralElementKind.Stair).ToArray();
        foreach (var stair in stairs)
        {
            Invoke("SelectExtendedElement", null, null, null, null, stair.Id);
            Invoke("RefreshProperties");
            var summary = ((TextBlock)_window.FindName("PropertiesText")).Text;
            Check(summary.Contains(StairGeometry.RiserCount(stair.Height).ToString()) &&
                  !summary.Contains("Properties.StairSummary") && !summary.Contains("Properties.SpiralStairSummary"),
                $"{stair.Style} stair properties report calculated dimensions through localization");
            var bounds = new ElementPicker(session.Document).Bounds(PickedKind.Structural, stair.Id)!.Value;
            var pivot = (System.Numerics.Vector3)Invoke("ResolveModelOrbitPivot")!;
            Check(System.Numerics.Vector3.Distance(pivot, (bounds.Min + bounds.Max) / 2) < 0.0001f,
                $"{stair.Style} stair orbit includes the entire selected stair at its level elevation");
        }
        Invoke("SelectExtendedElement");
        var modelBounds = BuildingMesher.Build(session.Document).Bounds();
        Aim((modelBounds.Min + modelBounds.Max) / 2, new(46, -39, 31));
        Field<object>("_modelNavigator").GetType().GetMethod("CenterAll")!.Invoke(Field<object>("_modelNavigator"), null);
        SaveBitmap("stair-study.png");
        var path = Path.Combine(Evidence, "stair-study.g3d");
        ProjectFile.Save(path, session.Document);
        var restored = ProjectFile.Load(path);
        Check(restored.StructuralElements.Count == session.Document.StructuralElements.Count &&
              restored.StructuralElements.Where(element => element.RailingHostId is not null).All(element =>
                  restored.StructuralElements.Any(host => host.Id == element.RailingHostId)),
            "The editable stair study reopens with every generated guard attached to its host");
    }
}
