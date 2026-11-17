using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// A roof over a one-room house explored from an empty project: the roof by its footprint, then how it reads in 3D, in the
/// north elevation and in the budget, each with a window picture. Opt-in: G3D_FRESH=roof.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    public static void RunFreshRoof(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("Fresh roof", "artifacts/interaction-evidence/fresh-roof", FreshRoofSteps(), check,
            "Cubierta desde un proyecto vacío — resultado de la prueba automática");
    }

    private static IEnumerable<(string Name, Func<string> Action)> FreshRoofSteps()
    {
        yield return ("R1 Proyecto nuevo y cuatro muros de 8 × 6 m", () =>
        {
            App("NewProject");
            Zoom(45, 4, 3);
            Arch(ArchitectureCommand.Wall);
            foreach (var (x, y) in new[] { (0.0, 0.0), (8.0, 0.0), (8.0, 6.0), (0.0, 6.0), (0.0, 0.0) }) Click(x, y);
            Escape();
            Expect(Doc.Walls.Count == 4, $"hay {Doc.Walls.Count} muros (estado: {Status})");
            return "4 muros";
        });
        yield return ("R2 Cubierta por huella: menú de Cubierta y clics en las esquinas", () =>
        {
            var before = Doc.Floors.Count(floor => floor.Kind == FloorKind.Roof);
            Arch(ArchitectureCommand.Roof);
            PickMenuIfOpen(0);
            var roofs = Doc.Floors.Where(floor => floor.Kind == FloorKind.Roof).ToList();
            SaveWindow($"{Evidence}/R2-ventana.png");
            Expect(roofs.Count > before, $"no se creó la cubierta (estado: {Status})");
            var selected = (Guid?)typeof(MainWindow).GetField("_selectedFloorId", Private)!.GetValue(_window);
            var wall = (Guid?)typeof(MainWindow).GetField("_selectedWallId", Private)!.GetValue(_window);
            Expect(selected == roofs[^1].Id && wall is null, "tras crear la cubierta no queda ella sola seleccionada");
            var roof = roofs[^1];
            return $"cubierta «{roof.Name}» de {roof.CalculateArea():0.0} m², {(roof.IsPitched ? "inclinada" : "plana")}";
        });
        yield return ("R3 La cubierta se ve en 3D y en el alzado Norte", () =>
        {
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 1;
            _pump(); _pump();
            SaveWindow($"{Evidence}/R3-3d.png");
            var north = Doc.Views.First(view => view.Kind == ProjectViewKind.Elevation && view.Orientation == ViewOrientation.North);
            Invoke("SelectProjectView", north.Id);
            _pump(); _pump();
            SaveWindow($"{Evidence}/R3-alzado-norte.png");
            return "capturas 3D y alzado";
        });
        yield return ("R4 El presupuesto cuenta la cubierta", () =>
        {
            var budget = ConstructionBudget.Build(Doc, Session.GetBudgetSettings());
            var roofing = budget.Lines.Any(line => line.Takeoff.Material.Category is MaterialCategory.Roofing ||
                                                   line.Takeoff.Sources.Any(source => source.Contains("ubierta", StringComparison.OrdinalIgnoreCase)));
            Expect(roofing, "el presupuesto no tiene ningún material de cubierta");
            return $"₡{budget.Total:#,0}";
        });
    }
}
