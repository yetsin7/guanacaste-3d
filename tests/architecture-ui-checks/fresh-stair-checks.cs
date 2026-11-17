using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// A two-storey stair explored from an empty project with a picture of the whole window after each step, including the
/// 3D view: what a person sees is what is judged, not only what the model holds. Opt-in: G3D_FRESH=stair.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    public static void RunFreshStair(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("Fresh stair", "artifacts/interaction-evidence/fresh-stair", FreshStairSteps(), check,
            "Escalera desde un proyecto vacío — resultado de la prueba automática");
    }

    private static void Show3D(string name)
    {
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 1;
        _pump();
        _pump();
        SaveWindow($"{Evidence}/{name}.png");
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
        _pump();
    }

    private static IEnumerable<(string Name, Func<string> Action)> FreshStairSteps()
    {
        yield return ("S1 Proyecto nuevo, muros de 6 × 5 m y piso", () =>
        {
            App("NewProject");
            Zoom(50, 3, 2.5);
            Arch(ArchitectureCommand.Wall);
            foreach (var (x, y) in new[] { (0.0, 0.0), (6.0, 0.0), (6.0, 5.0), (0.0, 5.0), (0.0, 0.0) }) Click(x, y);
            Escape();
            Arch(ArchitectureCommand.Floor);
            PickMenu(0);
            foreach (var (x, y) in new[] { (0.0, 0.0), (6.0, 0.0), (6.0, 5.0), (0.0, 5.0), (0.0, 0.0) }) Click(x, y);
            Expect(Doc.Walls.Count == 4 && Doc.Floors.Count >= 1, $"{Doc.Walls.Count} muros, {Doc.Floors.Count} pisos (estado: {Status})");
            return "4 muros y piso";
        });
        yield return ("S2 Escalera recta con dos clics", () =>
        {
            Arch(ArchitectureCommand.Stair);
            Click(1.0, 1.0);
            Click(1.0, 4.4);
            var stair = Doc.StructuralElements.LastOrDefault(element => element.Kind == StructuralElementKind.Stair);
            Expect(stair is not null, $"no se creó la escalera (estado: {Status})");
            var levels = string.Join(", ", Doc.Levels.OrderBy(level => level.Elevation).Select(level => $"{level.Name} {level.Elevation:0.00}"));
            var fields = (System.Collections.IDictionary)typeof(MainWindow).GetField("_instancePropertyInputs", Private)!.GetValue(_window)!;
            Expect(!fields.Contains("Thickness"), "Propiedades de la escalera muestra el espesor del piso dibujado antes");
            SaveWindow($"{Evidence}/S2-ventana.png");
            return $"escalera de {stair!.Length:0.00} m, sube {stair.Height:0.00} m; niveles: {levels}";
        });
        yield return ("S3 La escalera en 3D y la planta del nivel superior", () =>
        {
            Show3D("S3-3d");
            var upper = Doc.Levels.OrderBy(level => level.Elevation).Last();
            ActiveLevel(upper.Name);
            SaveWindow($"{Evidence}/S3-planta-superior.png");
            var stair = Doc.StructuralElements.Last(element => element.Kind == StructuralElementKind.Stair);
            var rise = upper.Elevation;
            Expect(Math.Abs(stair.Height - rise) < 0.01, $"la escalera sube {stair.Height:0.00} m pero el nivel superior está a {rise:0.00} m");
            return $"sube {stair.Height:0.00} m hasta {upper.Name}";
        });
    }
}
