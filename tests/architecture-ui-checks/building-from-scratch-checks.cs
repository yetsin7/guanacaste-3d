using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// A six-storey apartment building made from an empty project the way a person does it: the levels in Manage Levels,
/// one typical floor (perimeter, corridor, four apartments a side, doors, windows, floor slab, columns, rooms), then
/// Ctrl+A and Paste to Levels onto the five storeys above, a roof slab, elevation, 3D, budget, save and reopen. Every
/// step must also stay fast. Opt-in with G3D_BUILDING=1.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    private const int BuildingStoreys = 6;

    public static void RunBuildingFromScratch(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("Building from scratch", "artifacts/interaction-evidence/edificio", BuildingSteps(), check,
            "Edificio de 6 pisos desde cero — resultado de la prueba automática");
    }

    private static IEnumerable<(string Name, Func<string> Action)> BuildingSteps()
    {
        Guid first = Guid.Empty;
        yield return ("E.1 Proyecto nuevo", () =>
        {
            App("NewProject");
            Zoom(30, 10, 6);
            return $"{Doc.Levels.Count} niveles";
        });
        yield return ("E.2 Seis pisos de 3.00 m en Gestionar niveles", () =>
        {
            ExpectDialog("LevelsWindow", dialog =>
            {
                var list = (ListBox)dialog.FindName("LevelsList");
                var levels = Doc.Levels.OrderBy(level => level.Elevation).ToList();
                for (var index = 0; index < BuildingStoreys + 1; index++)
                {
                    if (index >= levels.Count)
                    {
                        Call(dialog, "Add_Click");
                        levels = Doc.Levels.OrderBy(level => level.Elevation).ToList();
                    }
                    var name = index < BuildingStoreys ? $"Piso {index + 1}" : "Azotea";
                    EditLevel(dialog, list, levels[index].Id, name, (index * 3.0).ToString("0.00", System.Globalization.CultureInfo.CurrentCulture));
                }
            });
            Arch(ArchitectureCommand.Levels);
            Expect(Doc.Levels.Count == BuildingStoreys + 1 && Doc.Levels.Any(level => level.Name == "Azotea" && Math.Abs(level.Elevation - 18) < 1e-6),
                "niveles: " + string.Join(", ", Doc.Levels.OrderBy(level => level.Elevation).Select(level => $"{level.Name} {level.Elevation:0.00}")));
            first = Doc.Levels.Single(level => level.Name == "Piso 1").Id;
            return string.Join(", ", Doc.Levels.OrderBy(level => level.Elevation).Select(level => level.Name));
        });
        yield return ("E.3 Planta tipo: fachadas, pasillo y 8 apartamentos", () =>
        {
            ActiveLevel("Piso 1");
            Arch(ArchitectureCommand.Wall);
            SetCheck("WallChainCheck", true);
            foreach (var (x, y) in new[] { (0.0, 0.0), (20.0, 0.0), (20.0, 12.0), (0.0, 12.0), (0.0, 0.0) }) Click(x, y);
            Escape();
            SetCheck("WallChainCheck", false);
            var interior = new List<((double, double), (double, double))> { ((0, 5), (20, 5)), ((0, 7), (20, 7)) };
            foreach (var x in new[] { 5.0, 10.0, 15.0 }) { interior.Add(((x, 0), (x, 5))); interior.Add(((x, 7), (x, 12))); }
            foreach (var (a, b) in interior)
            {
                Arch(ArchitectureCommand.Wall);
                Click(a.Item1, a.Item2);
                Click(b.Item1, b.Item2);
                Escape();
            }
            var walls = Doc.Walls.Count(wall => wall.LevelId == first);
            Expect(walls == 12, $"hay {walls} muros en el piso tipo, se esperaban 12");
            return $"{walls} muros";
        });
        yield return ("E.4 Puertas de los apartamentos y ventanas de fachada", () =>
        {
            foreach (var x in new[] { 2.5, 7.5, 12.5, 17.5 })
            {
                foreach (var y in new[] { 5.0, 7.0 })
                {
                    Arch(ArchitectureCommand.Door);
                    PickMenuIfOpen(0);
                    Move(x, y);
                    Click(x, y);
                }
                foreach (var y in new[] { 0.0, 12.0 })
                {
                    Arch(ArchitectureCommand.Window);
                    PickMenuIfOpen(0);
                    Move(x, y);
                    Click(x, y);
                }
            }
            var doors = Doc.Openings.Count(opening => opening.Kind == OpeningKind.Door);
            var windows = Doc.Openings.Count(opening => opening.Kind == OpeningKind.Window);
            Expect(doors == 8 && windows == 8, $"{doors} puertas y {windows} ventanas, se esperaban 8 y 8");
            return "8 puertas y 8 ventanas";
        });
        yield return ("E.5 Losa, columnas y habitaciones del piso tipo", () =>
        {
            Arch(ArchitectureCommand.Floor);
            PickMenu(0);
            foreach (var (x, y) in new[] { (0.0, 0.0), (20.0, 0.0), (20.0, 12.0), (0.0, 12.0), (0.0, 0.0) }) Click(x, y);
            foreach (var x in new[] { 0.0, 10.0, 20.0 })
            foreach (var y in new[] { 0.0, 12.0 })
            {
                Arch(ArchitectureCommand.Column);
                PickMenuIfOpen(0);
                Click(x, y);
                Escape();
            }
            Arch(ArchitectureCommand.RoomAutomatic);
            var rooms = Doc.Rooms.Count(room => room.LevelId == first);
            Expect(Doc.Floors.Count(floor => floor.LevelId == first) == 1 && Doc.Columns.Count == 6 && rooms == 9,
                $"{Doc.Floors.Count(floor => floor.LevelId == first)} losas, {Doc.Columns.Count} columnas, {rooms} habitaciones (8 apartamentos y el pasillo)");
            return $"losa, 6 columnas, {rooms} habitaciones";
        });
        yield return ("E.6 Seleccionar todo el piso (Ctrl+A) y Pegar en niveles (5 pisos)", () =>
        {
            Invoke("SelectAllInView");
            var picked = 0;
            ExpectDialog("Window", dialog =>
            {
                var all = Descendants<Button>(dialog).First(button => button.Content as string == (Application.Current.TryFindResource("PasteToLevels.All") as string));
                all.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                foreach (var box in Descendants<CheckBox>(dialog))
                    if (Doc.Levels.First(level => level.Id == (Guid)box.Tag).Name == "Azotea") box.IsChecked = false;
                picked = Descendants<CheckBox>(dialog).Count(box => box.IsChecked == true);
                Descendants<Button>(dialog).First(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            });
            var clock = Stopwatch.StartNew();
            App("PasteToLevels");
            var elapsed = clock.Elapsed.TotalMilliseconds;
            var perLevel = Doc.Levels.Where(level => level.Name.StartsWith("Piso", StringComparison.Ordinal))
                .Select(level => (level.Name, Walls: Doc.Walls.Count(wall => wall.LevelId == level.Id), Rooms: Doc.Rooms.Count(room => room.LevelId == level.Id))).ToList();
            Expect(picked == BuildingStoreys - 1, $"el diálogo marcó {picked} niveles");
            Expect(perLevel.All(level => level.Walls == 12 && level.Rooms == 9), "por piso: " + string.Join(", ", perLevel.Select(level => $"{level.Name} {level.Walls} muros {level.Rooms} hab.")));
            Expect(Doc.Openings.Count == 16 * BuildingStoreys && Doc.Columns.Count == 6 * BuildingStoreys, $"{Doc.Openings.Count} vanos y {Doc.Columns.Count} columnas en total");
            var numbers = Doc.Rooms.Select(room => room.Number).ToList();
            Expect(numbers.Distinct().Count() == numbers.Count, "hay números de habitación repetidos entre pisos");
            Expect(elapsed < 4000, $"pegar tardó {elapsed:0} ms");
            return $"{Doc.Walls.Count} muros, {Doc.Openings.Count} vanos, {Doc.Rooms.Count} habitaciones en {elapsed:0} ms";
        });
        yield return ("E.7 Cada piso deshace en un paso (Ctrl+Z) y se rehace", () =>
        {
            var walls = Doc.Walls.Count;
            App("Undo");
            var undone = Doc.Walls.Count;
            App("Redo");
            Expect(undone == 12 && Doc.Walls.Count == walls, $"deshacer dejó {undone} muros, rehacer {Doc.Walls.Count}");
            return "un solo paso";
        });
        yield return ("E.8 Losa de azotea", () =>
        {
            ActiveLevel("Azotea");
            Arch(ArchitectureCommand.Floor);
            PickMenu(0);
            foreach (var (x, y) in new[] { (0.0, 0.0), (20.0, 0.0), (20.0, 12.0), (0.0, 12.0), (0.0, 0.0) }) Click(x, y);
            Expect(Doc.Floors.Any(floor => floor.LevelId == Doc.Levels.Single(level => level.Name == "Azotea").Id), "no se creó la losa de azotea");
            return "azotea cerrada";
        });
        yield return ("E.9 Cambiar de piso es rápido", () =>
        {
            var times = new List<double>();
            foreach (var level in Doc.Levels.OrderBy(level => level.Elevation))
            {
                var clock = Stopwatch.StartNew();
                ActiveLevel(level.Name);
                times.Add(clock.Elapsed.TotalMilliseconds);
            }
            Expect(times.Max() < 700, $"el piso más lento tardó {times.Max():0} ms");
            return $"promedio {times.Average():0} ms, peor {times.Max():0} ms";
        });
        yield return ("E.10 Alzado Norte con los seis pisos y vista 3D", () =>
        {
            Invoke("SelectProjectView", BuiltInProjectViews.NorthElevationId);
            _pump();
            var elevation = ((Canvas)_window.FindName("ElevationCanvas")).Children.Count;
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            var clock = Stopwatch.StartNew();
            App("Model3D");
            var task = (Task<Guanacaste3D.Core.Rendering.RenderScene?>)Invoke("GetSceneAsync")!;
            while (!task.IsCompleted && clock.Elapsed.TotalSeconds < 30) _pump();
            SaveWindow($"{Evidence}/edificio-3d.png");
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            Expect(task.IsCompletedSuccessfully && task.Result is { TriangleCount: > 0 } && elevation > 50, $"alzado {elevation} figuras, 3D {(task.IsCompleted ? "listo" : "sin terminar")}");
            return $"alzado {elevation} figuras, 3D en {clock.Elapsed.TotalMilliseconds:0} ms";
        });
        yield return ("E.11 Presupuesto del edificio", () =>
        {
            var budget = ConstructionBudget.Build(Doc, Session.GetBudgetSettings());
            var doors = budget.Takeoff.Find(MaterialCatalog.Door)?.NetQuantity ?? 0;
            Expect(doors == 8 * BuildingStoreys, $"el presupuesto cuenta {doors} puertas, se esperaban {8 * BuildingStoreys}");
            return $"₡{budget.Total:#,0}";
        });
        yield return ("E.12 Guardar y volver a abrir el edificio", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "edificio-" + Guid.NewGuid().ToString("N") + ProjectFile.NativeExtension);
            typeof(MainWindow).GetField("_filePath", Private)!.SetValue(_window, path);
            Expect((bool)Invoke("SaveProject", false)!, "no se guardó");
            var counts = Counts(Doc);
            Invoke("OpenProjectPath", path);
            for (var wait = 0; wait < 60 && Counts(Doc) != counts; wait++) _pump();
            File.Delete(path);
            Expect(Counts(Doc) == counts, $"al reabrir cambió: {counts} → {Counts(Doc)}");
            return counts;
        });
    }
}
