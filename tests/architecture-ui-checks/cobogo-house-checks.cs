using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// The cobogó house (the program's sample: a white volume over a concrete carport with a breeze-block screen) built
/// from an empty project only with the program's buttons, menus, dialogs and clicks, in the order a person draws it:
/// levels and grids, the ground floor shell and interiors, the upper floor, roofs, structure, plumbing, electrical,
/// lighting, ventilation, documentation and the budget. Each step that fails names what the program lacks; the
/// master plan in docs/casa-cobogo-plan-maestro.md tracks them. Opt-in: set G3D_COBOGO=1 (G3D_SCENARIOS_ONLY=1 alone).
/// Coordinates follow the sample in src/Guanacaste3D.Core/architecture/breeze-block-house-*.cs.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    public static void RunCobogoHouse(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("Cobogó house", "artifacts/interaction-evidence/casa-cobogo", StopAfter(CobogoSteps()), check,
            "Casa del cobogó desde cero — resultado de la prueba automática");
    }

    /// <summary>G3D_STOP_AFTER=2.6 ends the scenario after the step whose name starts with it, to debug one phase in seconds.</summary>
    private static IEnumerable<(string Name, Func<string> Action)> StopAfter(IEnumerable<(string Name, Func<string> Action)> steps)
    {
        var stop = Environment.GetEnvironmentVariable("G3D_STOP_AFTER");
        foreach (var step in steps)
        {
            yield return step;
            if (!string.IsNullOrEmpty(stop) && step.Name.StartsWith(stop + " ", StringComparison.Ordinal)) yield break;
        }
    }

    private const string Ground = "Planta baja";
    private const string Upper = "Planta alta";

    private static IEnumerable<(string Name, Func<string> Action)> CobogoSteps()
    {
        // ---------------------------------------------------------------- 0. Project, levels and grids
        yield return ("0.1 Proyecto nuevo", () =>
        {
            App("NewProject");
            Expect(Doc.Walls.Count == 0, $"el proyecto nuevo trae {Doc.Walls.Count} muros");
            // The house has walls on 7.5 cm lines (0.075, 11.925): a person turns the 10 cm snap off to draw them.
            SetCheck("SnapGridCheck", false);
            Zoom(42, 7.6, 6);
            return $"{Doc.Levels.Count} niveles";
        });
        yield return ("0.2 Niveles: Planta baja 0.00 y Planta alta 3.90 (Gestionar niveles)", () =>
        {
            ExpectDialog("LevelsWindow", dialog =>
            {
                var list = (ListBox)dialog.FindName("LevelsList");
                var levels = Doc.Levels.OrderBy(level => level.Elevation).ToList();
                EditLevel(dialog, list, levels[0].Id, Ground, "0");
                if (levels.Count > 1) EditLevel(dialog, list, levels[1].Id, Upper, "3.90");
                else
                {
                    Call(dialog, "Add_Click");
                    EditLevel(dialog, list, (Guid)list.SelectedValue, Upper, "3.90");
                }
            });
            Arch(ArchitectureCommand.Levels);
            var names = Doc.Levels.OrderBy(level => level.Elevation).Select(level => $"{level.Name} {level.Elevation:0.00}").ToList();
            Expect(Doc.Levels.Any(level => level.Name == Ground && level.Elevation == 0) &&
                   Doc.Levels.Any(level => level.Name == Upper && Math.Abs(level.Elevation - 3.9) < 1e-6),
                "los niveles no quedaron como Planta baja 0.00 y Planta alta 3.90: " + string.Join(", ", names));
            return string.Join(", ", names);
        });
        yield return ("0.3 Ejes A–D y 1–3 con nombre", () =>
        {
            ActiveLevel(Ground);
            // As in the reference application: rename the first grid of a row (F2) and the next ones follow it.
            void Row(IEnumerable<((double, double) A, (double, double) B)> lines, string first)
            {
                var index = 0;
                foreach (var (a, b) in lines)
                {
                    GridLine(a, b);
                    if (index++ > 0) continue;
                    ExpectDialog("PromptWindow", dialog => Answer(dialog, first));
                    typeof(MainWindow).GetField("_selectedGridId", Private)!.SetValue(_window, (Guid?)Doc.Grids[^1].Id);
                    Key(System.Windows.Input.Key.F2);
                }
            }
            Row(new[] { 0.15, 6.55, 10.8, 15.05 }.Select(x => ((x, -2.2), (x, 13.6))), "A");
            Row(new[] { 0.4, 5.2, 11.85 }.Select(y => ((-1.6, y), (16.6, y))), "1");
            Expect(Doc.Grids.Count == 7, $"hay {Doc.Grids.Count} ejes, se esperaban 7");
            var names = Doc.Grids.Select(axis => axis.Name).OrderBy(name => name).ToList();
            Expect(names.SequenceEqual(["1", "2", "3", "A", "B", "C", "D"]), "los ejes se llaman " + string.Join(", ", names));
            return "ejes " + string.Join(" ", names);
        });

        // ---------------------------------------------------------------- 1. Ground floor shell
        yield return ("1.1 Muros de concreto visto 300 mm, 3.60 m (tipo desde la biblioteca)", () =>
        {
            ActiveLevel(Ground);
            WallsOfType("Reinforced Concrete 300 mm", "3.60",
                ((0.15, 0.4), (0.15, 12)), ((0, 0.55), (2.0, 0.55)), ((1.85, 0.7), (1.85, 1.5)),
                ((6.55, 1.5), (6.55, 12)), ((15.05, 5.2), (15.05, 12)));
            return WallSummary(Ground);
        });
        yield return ("1.2 Pórtico de la cochera, 3.10 m", () =>
        {
            WallsOfType("Reinforced Concrete 300 mm", "3.10", ((6.55, -0.4), (6.55, 1.5)), ((15.05, -0.4), (15.05, 5.2)));
            var portal = Doc.Walls.Where(wall => Math.Abs(wall.Height - 3.1) < 1e-6).ToList();
            Expect(portal.Count == 2, $"hay {portal.Count} muros de 3.10 m: la altura escrita en la barra de opciones no se usó");
            return WallSummary(Ground);
        });
        yield return ("1.3 Muro cortina de la sala", () =>
        {
            var before = Count(StructuralElementKind.CurtainWall);
            Arch(ArchitectureCommand.CurtainSystem);
            Click(2.0, 1.5);
            Click(6.4, 1.5);
            Expect(Count(StructuralElementKind.CurtainWall) == before + 1, $"no se creó el muro cortina (estado: {Status})");
            return "muro cortina de 4.40 m";
        });
        yield return ("1.4 Muro de listones de madera y puerta pivotante de 1.60 m", () =>
        {
            WallsOfType("Timber Clad Wall 200 mm", "3.60", ((6.7, 5.2), (14.9, 5.2)));
            var before = Doc.Openings.Count;
            Library(entry => entry.StartsWith("Pivot Door", StringComparison.Ordinal) && entry.Contains("1600"));
            Move(7.7, 5.2);
            Click(7.7, 5.2);
            Expect(Doc.Openings.Count == before + 1, $"no se colocó la puerta pivotante (estado: {Status})");
            var door = Doc.Openings[^1];
            Expect(Math.Abs(door.Width - 1.6) < 1e-6, $"la puerta mide {door.Width:0.00} m, no 1.60");
            return $"puerta {door.Width:0.00} × {door.Height:0.00}";
        });
        yield return ("1.5 Muro posterior de bloque repellado 150 mm", () =>
        {
            WallsOfType("Rendered Block 150 mm", "3.60", ((0.15, 11.85), (15.05, 11.85)));
            return WallSummary(Ground);
        });

        // ---------------------------------------------------------------- 2. Ground floor interiors
        yield return ("2.1 Tabiques de yeso 100 mm", () =>
        {
            WallsOfType("Gypsum Partition 100 mm", "3.00",
                ((10.8, 5.3), (10.8, 11.75)), ((10.8, 7.8), (14.9, 7.8)), ((12.8, 5.3), (12.8, 7.8)));
            return WallSummary(Ground);
        });
        yield return ("2.2 Puertas interiores", () =>
        {
            var before = Doc.Openings.Count(opening => opening.Kind == OpeningKind.Door);
            foreach (var (x, y) in new[] { (10.8, 11.25), (11.8, 7.8), (13.8, 7.8) })
            {
                Arch(ArchitectureCommand.Door);
                PickMenuIfOpen(0);
                Move(x, y);
                Click(x, y);
            }
            var placed = Doc.Openings.Count(opening => opening.Kind == OpeningKind.Door) - before;
            Expect(placed == 3, $"se colocaron {placed} de 3 puertas (estado: {Status})");
            // The door from the hall to the kitchen sits past the stair (which ends at y = 10.8): 0.80 m wide so its swing
            // zone clears the stair and the wall end.
            SizeOpening(Doc.Openings.Where(opening => opening.Kind == OpeningKind.Door).Skip(before).First().Id, 0.8, 2.1, 0);
            return "3 puertas";
        });
        yield return ("2.3 Ventanas con tamaño y antepecho (propiedades)", () =>
        {
            var sized = new List<string>();
            foreach (var (x, y, width, height, sill) in new[] { (3.3, 11.85, 2.4, 1.5, 0.9), (13.0, 11.85, 1.2, 1.2, 0.9), (0.15, 8.0, 2.0, 1.4, 0.9) })
            {
                var before = Doc.Openings.Count;
                Arch(ArchitectureCommand.Window);
                PickMenuIfOpen(0);
                Move(x, y);
                Click(x, y);
                Expect(Doc.Openings.Count == before + 1, $"no se colocó la ventana en ({x}, {y}) (estado: {Status})");
                var window = Doc.Openings[^1];
                SizeOpening(window.Id, width, height, sill);
                var after = Doc.Openings.Single(opening => opening.Id == window.Id);
                Expect(Math.Abs(after.Width - width) < 1e-6 && Math.Abs(after.Height - height) < 1e-6 && Math.Abs(after.Sill - sill) < 1e-6,
                    $"la ventana quedó {after.Width:0.00} × {after.Height:0.00} antepecho {after.Sill:0.00}, no {width} × {height} antepecho {sill}");
                sized.Add($"{width}×{height}");
            }
            return "ventanas " + string.Join(", ", sized);
        });
        yield return ("2.4 Vano ancho entre sala y vestíbulo", () =>
        {
            var existing = Doc.Openings.Select(opening => opening.Id).ToHashSet();
            Arch(ArchitectureCommand.WallOpening);
            Move(6.55, 8.5);
            Click(6.55, 8.5);
            var created = Doc.Openings.Where(opening => !existing.Contains(opening.Id)).ToList();
            Expect(created.Count == 1, $"no se abrió el vano (estado: {Status})");
            SizeOpening(created[0].Id, 2.4, 2.4, 0);
            return "vano de 2.40 × 2.40";
        });
        yield return ("2.5 Pisos por boceto: sala, vestíbulo y cocina, cochera", () =>
        {
            var before = Doc.Floors.Count;
            SketchFloor("Porcelain Tile Floor 200 mm", (0, 0.4), (6.4, 0.4), (6.4, 12), (0, 12));
            SketchFloor("Porcelain Tile Floor 200 mm", (6.4, 5.2), (15.2, 5.2), (15.2, 12), (6.4, 12));
            SketchFloor("Concrete Slab 150 mm", (6.4, -0.4), (15.2, -0.4), (15.2, 5.2), (6.4, 5.2));
            Expect(Doc.Floors.Count == before + 3, $"se crearon {Doc.Floors.Count - before} de 3 pisos (estado: {Status})");
            return $"{Doc.Floors.Count - before} pisos, {Doc.Floors.Skip(before).Sum(floor => floor.CalculateArea()):0.0} m²";
        });
        yield return ("2.6 Habitaciones con nombre", () =>
        {
            var names = new[] { ("Sala y comedor", 3.5, 6.0), ("Cochera", 10.0, 2.5), ("Vestíbulo", 8.5, 8.5), ("Medio baño", 11.8, 6.5),
                ("Lavandería", 13.8, 6.5), ("Cocina", 12.8, 9.8) };
            // The carport is open to the street: as in the reference application, a room separator closes it.
            Arch(ArchitectureCommand.RoomSeparator);
            Click(6.55, -0.4);
            Click(15.05, -0.4);
            Escape();
            Arch(ArchitectureCommand.RoomAutomatic);
            foreach (var (name, x, y) in names)
            {
                if (!Doc.Rooms.Any(room => room.LevelId == LevelId(Ground) && RoomRegionGeometry.Contains(room, new Point2(x, y))))
                    continue;
                ExpectDialog("Window", dialog => Answer(dialog, name));
                Arch(ArchitectureCommand.TagRoom);
                Click(x, y);
                Escape();
            }
            var named = Doc.Rooms.Where(room => room.LevelId == LevelId(Ground)).Select(room => room.Name).ToList();
            var missing = names.Select(item => item.Item1).Except(named).ToList();
            Expect(missing.Count == 0, $"faltan habitaciones: {string.Join(", ", missing)} (hay: {string.Join(", ", named)}) :: {string.Join(" | ", Doc.Rooms.Where(room => room.LevelId == LevelId(Ground)).Select(room => $"{room.Name} [{room.FirstCorner.X:0.0},{room.FirstCorner.Y:0.0}]-[{room.SecondCorner.X:0.0},{room.SecondCorner.Y:0.0}] pts={room.Boundary?.Count}"))}");
            return string.Join(", ", named);
        });
        yield return ("2.7 Muebles y aparatos sanitarios desde la biblioteca", () =>
        {
            var placed = new List<string>();
            // Sanitary ware stands against the side walls of the wet rooms (turned 90° with Space), well away from the door
            // swings on the north wall: the toilet and washer face across the room, the sink faces west.
            const double East = Math.PI / 2, West = 3 * Math.PI / 2;
            foreach (var (name, x, y, turn) in new[] { ("Toilet", 11.2, 6.3, East), ("Pedestal Sink", 12.55, 6.3, West), ("Washing Machine", 14.55, 6.35, West),
                         ("Refrigerator", 12.2, 11.45, 0.0), ("Kitchen Sink Cabinet", 13.25, 11.45, 0.0), ("Cooking Range", 14.4, 11.45, 0.0),
                         ("Three Seat Sofa", 4.3, 4.4, 0.0), ("Dining Table 6 Seats", 4.2, 9.6, 0.0) })
            {
                var before = Doc.StructuralElements.Count;
                Library(entry => entry == name);
                FaceRotation(turn);
                Click(x, y);
                if (Doc.StructuralElements.Count > before) placed.Add(name);
                Escape();
            }
            Expect(placed.Count == 8, $"se colocaron {placed.Count} de 8: faltan {string.Join(", ", new[] { "Toilet", "Pedestal Sink", "Washing Machine", "Refrigerator", "Kitchen Sink Cabinet", "Cooking Range", "Three Seat Sofa", "Dining Table 6 Seats" }.Except(placed))}");
            return string.Join(", ", placed);
        });

        // ---------------------------------------------------------------- 3. Upper floor and roofs
        yield return ("3.1 Losa de entrepiso en Planta alta", () =>
        {
            ActiveLevel(Upper);
            var before = Doc.Floors.Count;
            SketchFloor("Painted Concrete Slab 300 mm", (0, 0), (15.2, 0), (15.2, 12), (0, 12));
            Expect(Doc.Floors.Count == before + 1 && Doc.Floors[^1].LevelId == LevelId(Upper), "no se creó la losa en Planta alta");
            return "losa 15.20 × 12.00";
        });
        yield return ("3.2 Muros blancos de Planta alta, 3.00 m", () =>
        {
            WallsOfType("Rendered Block 150 mm", "3.00",
                ((0, 0.075), (12.8, 0.075)), ((0.075, 0), (0.075, 12)), ((15.125, 0), (15.125, 12)), ((0, 11.925), (15.2, 11.925)));
            return WallSummary(Upper);
        });
        yield return ("3.3 Celosía de cobogó en la fachada", () =>
        {
            var before = Doc.StructuralElements.Count;
            Library(entry => entry.StartsWith("Breeze Block Screen", StringComparison.Ordinal));
            Click(12.8, 0.075);
            Click(15.05, 0.075);
            var screen = Doc.StructuralElements.Skip(before).FirstOrDefault();
            Expect(screen is not null, $"no se colocó la celosía (estado: {Status})");
            return $"celosía «{screen!.Name}» de {screen.Length:0.00} m";
        });
        yield return ("3.4 Balcón acristalado tras el cobogó", () =>
        {
            WallsOfType("Rendered Block 150 mm", "3.00", ((12.875, 0.15), (12.875, 1.6)));
            var before = Count(StructuralElementKind.CurtainWall);
            Arch(ArchitectureCommand.CurtainSystem);
            Click(12.95, 1.6);
            Click(15.05, 1.6);
            Expect(Count(StructuralElementKind.CurtainWall) == before + 1, "no se creó el muro cortina del balcón");
            return "balcón cerrado";
        });
        yield return ("3.5 Tabiques y puertas de Planta alta", () =>
        {
            WallsOfType("Gypsum Partition 100 mm", "3.00",
                ((0.15, 5.0), (12.8, 5.0)), ((0.15, 6.5), (8.9, 6.5)), ((10.3, 6.5), (15.05, 6.5)),
                ((6.5, 6.5), (6.5, 11.85)), ((8.9, 6.5), (8.9, 11.85)), ((10.3, 6.5), (10.3, 11.85)));
            var before = Doc.Openings.Count(opening => opening.Kind == OpeningKind.Door);
            foreach (var (x, y) in new[] { (5.6, 5.0), (3.45, 6.5), (7.65, 6.5), (12.5, 6.5) })
            {
                Arch(ArchitectureCommand.Door);
                PickMenuIfOpen(0);
                Move(x, y);
                Click(x, y);
            }
            var placed = Doc.Openings.Count(opening => opening.Kind == OpeningKind.Door) - before;
            Expect(placed == 4, $"se colocaron {placed} de 4 puertas");
            return WallSummary(Upper) + ", 4 puertas";
        });
        yield return ("3.6 Escalera de Planta baja a Planta alta", () =>
        {
            ActiveLevel(Ground);
            var before = Count(StructuralElementKind.Stair);
            Arch(ArchitectureCommand.Stair);
            Click(9.6, 6.0);
            Click(9.6, 10.8);
            var stair = Doc.StructuralElements.LastOrDefault(element => element.Kind == StructuralElementKind.Stair);
            Expect(Count(StructuralElementKind.Stair) == before + 1 && stair is not null, $"no se creó la escalera (estado: {Status})");
            Expect(Math.Abs(stair!.Height - 3.9) < 0.01, $"la escalera sube {stair.Height:0.00} m, no hasta Planta alta (3.90)");
            return $"escalera de {stair.Length:0.00} m que sube {stair.Height:0.00} m";
        });
        yield return ("3.7 Hueco de escalera en la losa de Planta alta", () =>
        {
            ActiveLevel(Upper);
            var before = Count(StructuralElementKind.Shaft);
            Arch(ArchitectureCommand.ShaftOpening);
            PickMenuIfOpen(0);
            // The shaft opening is drawn by two opposite corners.
            Click(9.0, 6.0);
            Click(10.2, 10.8);
            Expect(Count(StructuralElementKind.Shaft) > before,
                $"la losa de Planta alta no tiene hueco (estado: {Status})");
            return "hueco abierto";
        });
        yield return ("3.8 Barandilla de vidrio alrededor del hueco", () =>
        {
            var before = Count(StructuralElementKind.Railing);
            Library(entry => entry.StartsWith("Glass Guard", StringComparison.Ordinal));
            foreach (var (a, b) in new[] { ((8.95, 6.0), (8.95, 10.8)), ((10.25, 6.0), (10.25, 10.8)), ((9.0, 5.95), (10.2, 5.95)) })
            {
                Click(a.Item1, a.Item2);
                Click(b.Item1, b.Item2);
            }
            Expect(Count(StructuralElementKind.Railing) >= before + 3, $"hay {Count(StructuralElementKind.Railing) - before} de 3 tramos de barandilla");
            return "3 tramos de barandilla de vidrio";
        });
        yield return ("3.9 Losa de cubierta del volumen blanco a 3.30 m (desfase en propiedades)", () =>
        {
            var slab = SketchFloor("Painted Concrete Slab 300 mm", (0, 0), (15.2, 0), (15.2, 12), (0, 12));
            SetInstanceProperty("Elevation", "3.30");
            var placed = Doc.Floors.Single(floor => floor.Id == slab);
            Expect(Math.Abs(placed.ElevationOffset - 3.3) < 1e-6, $"la losa quedó a {placed.ElevationOffset:0.00} m sobre el nivel, no 3.30");
            return "cubierta a 3.30 m sobre Planta alta";
        });
        yield return ("3.10 Marquesina flotante a 3.85 m sobre tres columnas esbeltas", () =>
        {
            var slab = SketchFloor("Exposed Concrete Slab 250 mm", (1.8, -0.6), (13.7, -0.6), (13.7, 9), (1.8, 9));
            SetInstanceProperty("Elevation", "3.85");
            Expect(Math.Abs(Doc.Floors.Single(floor => floor.Id == slab).ElevationOffset - 3.85) < 1e-6, "la marquesina no quedó a 3.85 m");
            var before = Doc.Columns.Count;
            foreach (var x in new[] { 3.5, 7.7, 11.9 })
            {
                Arch(ArchitectureCommand.Column);
                PickMenuIfOpen(0);
                Click(x, 4.5);
                SetInstanceProperty("Width", "0.15");
                SetInstanceProperty("Depth", "0.15");
                SetInstanceProperty("Height", "3.60");
                Escape();
            }
            var columns = Doc.Columns.Skip(before).ToList();
            Expect(columns.Count == 3 && columns.All(column => Math.Abs(column.Width - 0.15) < 1e-6 && Math.Abs(column.Height - 3.6) < 1e-6),
                $"columnas: {string.Join(", ", columns.Select(column => $"{column.Width:0.00}×{column.Depth:0.00}×{column.Height:0.00}"))}");
            return "marquesina y 3 columnas de 15 × 15 cm";
        });
        yield return ("3.11 Losa de la cochera a 3.10 m", () =>
        {
            ActiveLevel(Ground);
            var slab = SketchFloor("Exposed Concrete Slab 250 mm", (6.4, -0.4), (15.2, -0.4), (15.2, 5.2), (6.4, 5.2));
            SetInstanceProperty("Elevation", "3.10");
            Expect(Math.Abs(Doc.Floors.Single(floor => floor.Id == slab).ElevationOffset - 3.1) < 1e-6, "la losa de la cochera no quedó a 3.10 m");
            return "losa de cochera a 3.10 m";
        });
        yield return ("3.12 Habitaciones de Planta alta", () =>
        {
            ActiveLevel(Upper);
            // The study partition stops at 12.80 by the stair; a separator closes it off from the landing.
            Arch(ArchitectureCommand.RoomSeparator);
            Click(12.8, 5.0);
            Click(15.125, 5.0);
            Escape();
            Arch(ArchitectureCommand.RoomAutomatic);
            var rooms = Doc.Rooms.Count(room => room.LevelId == LevelId(Upper));
            Expect(rooms >= 5, $"se detectaron {rooms} habitaciones en Planta alta, se esperaban al menos 5 (estudio, pasillo, 2 dormitorios, baño)");
            return $"{rooms} habitaciones";
        });
        foreach (var step in CobogoDisciplineSteps()) yield return step;
    }

    // ------------------------------------------------------------------------------------------------ helpers

    private static Guid LevelId(string name) => Doc.Levels.Single(level => level.Name == name).Id;

    private static int Count(StructuralElementKind kind) => Doc.StructuralElements.Count(element => element.Kind == kind);

    private static string WallSummary(string level) =>
        $"{Doc.Walls.Count(wall => wall.LevelId == LevelId(level))} muros en {level}";

    /// <summary>Chooses the active level in the level selector of the options bar.</summary>
    private static void ActiveLevel(string name)
    {
        var picker = (ComboBox)_window.FindName("LevelPicker");
        var item = picker.Items.OfType<ComboBoxItem>().FirstOrDefault(candidate => candidate.Content?.ToString()?.Contains(name) == true);
        Expect(item is not null, $"el selector de nivel no ofrece «{name}»");
        picker.SelectedItem = item;
        _pump();
        var active = (Guid)typeof(MainWindow).GetField("_activeLevelId", Private)!.GetValue(_window)!;
        Expect(active == LevelId(name), $"elegir «{name}» en el selector no cambió la planta activa");
    }

    private static void SetCheck(string name, bool value)
    {
        var box = (CheckBox)_window.FindName(name);
        box.IsChecked = value;
        _pump();
    }

    private static void SetText(string name, string text)
    {
        var box = (TextBox)_window.FindName(name);
        box.Text = text;
        box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        _pump();
    }

    /// <summary>Draws separate straight walls of a library type at a height typed in the options bar.</summary>
    private static void WallsOfType(string type, string height, params ((double X, double Y) A, (double X, double Y) B)[] walls)
    {
        var before = Doc.Walls.Count;
        foreach (var (a, b) in walls)
        {
            Library(entry => entry == type);
            SetCheck("WallChainCheck", false);
            // These walls stand on faces of others (6.7 = 6.55 + 0.15); with no face snap a person turns object snap off,
            // zooms in on the start point, clicks it, aims along the wall and types its length, as in the reference
            // application. At a far zoom the 15 cm offsets fall inside the alignment-guide reach and are pulled onto them.
            SetCheck("WallObjectSnapCheck", false);
            SetText("WallHeightInput", height);
            Zoom(220, a.X, a.Y);
            Click(a.X, a.Y);
            var length = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            Move(a.X + (b.X - a.X) / length * 0.8, a.Y + (b.Y - a.Y) / length * 0.8);
            TypeKeys(length.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            Key(System.Windows.Input.Key.Enter);
            Escape();
        }
        Zoom(42, 7.6, 6);
        var drawn = Doc.Walls.Skip(before).ToList();
        Expect(drawn.Count == walls.Length, $"se dibujaron {drawn.Count} de {walls.Length} muros «{type}» (estado: {Status})");
        var expectedType = Doc.WallTypes.FirstOrDefault(candidate => candidate.Name == type);
        Expect(expectedType is not null && drawn.All(wall => wall.WallTypeId == expectedType.Id),
            $"los muros no son del tipo «{type}»: " + string.Join(", ", drawn.Select(wall => Doc.WallTypes.Single(candidate => candidate.Id == wall.WallTypeId).Name).Distinct()));
        var wanted = double.Parse(height, System.Globalization.CultureInfo.InvariantCulture);
        Expect(drawn.All(wall => Math.Abs(wall.Height - wanted) < 1e-6),
            $"alturas {string.Join(", ", drawn.Select(wall => wall.Height.ToString("0.00")).Distinct())}, no {height}");
        foreach (var ((a, b), wall) in walls.Zip(drawn))
            Expect(wall.Start.DistanceTo(new Point2(a.X, a.Y)) < 0.005 && wall.End.DistanceTo(new Point2(b.X, b.Y)) < 0.005,
                $"el muro quedó de ({wall.Start.X:0.###}, {wall.Start.Y:0.###}) a ({wall.End.X:0.###}, {wall.End.Y:0.###}), se pidió ({a.X}, {a.Y})–({b.X}, {b.Y})");
    }

    private static void GridLine((double X, double Y) a, (double X, double Y) b)
    {
        Arch(ArchitectureCommand.Grid);
        Click(a.X, a.Y);
        Click(b.X, b.Y);
        Escape();
    }

    /// <summary>Picks a floor type from the Floor menu and sketches its corners, closing on the first one.</summary>
    /// <summary>Turns the component about to be placed with Space (90° each press) until it has the given rotation.</summary>
    private static void FaceRotation(double target)
    {
        var field = typeof(MainWindow).GetField("_componentRotation", Private)!;
        for (var press = 0; press < 4; press++)
        {
            var turn = ((double)field.GetValue(_window)! % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            if (Math.Abs(turn - target) < 1e-6) return;
            Key(System.Windows.Input.Key.Space);
        }
    }

    private static Guid SketchFloor(string type, params (double X, double Y)[] corners)
    {
        var before = Doc.Floors.Count;
        Arch(ArchitectureCommand.Floor);
        PickMenu(item => item.Tag as string == type);
        foreach (var (x, y) in corners.Append(corners[0])) Click(x, y);
        Expect(Doc.Floors.Count == before + 1, $"el boceto de «{type}» no creó el piso (estado: {Status})");
        return Doc.Floors[^1].Id;
    }

    /// <summary>Types new values in the selected door or window's properties and presses Apply.</summary>
    private static void SizeOpening(Guid id, double width, double height, double sill)
    {
        var opening = Doc.Openings.Single(candidate => candidate.Id == id);
        var host = Doc.Walls.Single(wall => wall.Id == opening.WallId);
        var along = opening.Offset / host.Length;
        Escape();
        var (cx, cy) = (host.Start.X + (host.End.X - host.Start.X) * along, host.Start.Y + (host.End.Y - host.Start.Y) * along);
        var countBefore = Doc.Openings.Count;
        var hit = HitElement(Canvas(cx, cy));
        Click(cx, cy);
        var picked = (Guid?)typeof(MainWindow).GetField("_selectedOpeningId", Private)!.GetValue(_window);
        string Describe(object element) => element is FrameworkElement { Tag: Guid tag } framework
            ? $"{framework.GetType().Name} de {(Doc.Openings.Any(o => o.Id == tag) ? (tag == id ? "este vano" : "otro vano") : Doc.Walls.Any(w => w.Id == tag) ? "un muro" : Doc.Floors.Any(f => f.Id == tag) ? "una losa" : "otro elemento")}"
            : element.GetType().Name;
        Expect(picked == id, $"el clic sobre la ventana no la seleccionó (bajo el puntero: {Describe(hit)}; aberturas {countBefore}→{Doc.Openings.Count}; " +
            $"herramienta de abertura armada: {typeof(MainWindow).GetField("_placingOpening", Private)!.GetValue(_window) ?? "no"}; " +
            $"vano: antepecho {opening.Sill:0.00}, alto {opening.Height:0.00}, ancho {opening.Width:0.00}; estado: {Status})");
        string Metres(double value) => value.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture);
        ((TextBox)_window.FindName("OpeningWidthInput")).Text = Metres(width);
        ((TextBox)_window.FindName("OpeningHeightInput")).Text = Metres(height);
        ((TextBox)_window.FindName("OpeningSillInput")).Text = Metres(sill);
        Invoke("ApplyOpening_Click", _window, new RoutedEventArgs());
        _pump();
    }

    /// <summary>Types a value in the instance properties of the selected element and presses Apply.</summary>
    private static void SetInstanceProperty(string property, string value)
    {
        Invoke("RefreshProperties");
        var inputs = (Dictionary<string, TextBox>)typeof(MainWindow).GetField("_instancePropertyInputs", Private)!.GetValue(_window)!;
        Expect(inputs.TryGetValue(property, out var box), $"las propiedades del elemento no ofrecen «{property}» (hay: {string.Join(", ", inputs.Keys)})");
        box!.Text = value;
        Invoke("ApplyInstanceProperties_Click", _window, new RoutedEventArgs());
        _pump();
    }

    /// <summary>Opens Component, searches the library and places the first entry the predicate accepts.</summary>
    private static void Library(Func<string, bool> entry)
    {
        string? chosen = null;
        ExpectDialog("ArchitecturalLibraryWindow", dialog =>
        {
            var list = (ListView)dialog.GetType().GetField("_list", Private)!.GetValue(dialog)!;
            var row = list.Items.Cast<object>().FirstOrDefault(item => entry(EntryName(item)) || entry(CatalogName(EntryName(item))));
            if (row is null) throw new InvalidOperationException("no está en la biblioteca");
            chosen = EntryName(row);
            list.SelectedItem = row;
            dialog.GetType().GetMethod("PlaceSelected", Private)!.Invoke(dialog, null);
        });
        Arch(ArchitectureCommand.Component);
        for (var wait = 0; wait < 20 && DialogScripts.TryGetValue("ArchitecturalLibraryWindow", out var pending) && pending.Count > 0; wait++) _pump();
        Expect(chosen is not null, "la biblioteca no tiene el elemento buscado" + (Dialogs.Count > 0 ? $" ({Dialogs[^1]})" : ""));
    }

    /// <summary>The catalog (English) name behind a localized library entry, so steps can name presets either way.</summary>
    private static string CatalogName(string shown) =>
        ArchitecturalLibrary.ElementPresets.FirstOrDefault(preset => !string.IsNullOrWhiteSpace(preset.LocalizationKey) &&
            Application.Current.TryFindResource(preset.LocalizationKey) as string == shown)?.Name ?? shown;

    private static string EntryName(object row)
    {
        var source = row.GetType().GetProperty("Source")?.GetValue(row) ?? row;
        return source.GetType().GetProperty("Name")?.GetValue(source)?.ToString() ?? "";
    }

    /// <summary>Tells the driver how a person fills in a dialog the next action opens.</summary>
    private static void ExpectDialog(string type, Action<Window> fill)
    {
        if (!DialogScripts.TryGetValue(type, out var queue)) DialogScripts[type] = queue = new Queue<Action<Window>>();
        queue.Enqueue(fill);
    }

    private static void Answer(Window prompt, string text)
    {
        var box = Descendants<TextBox>(prompt).FirstOrDefault()
            ?? throw new InvalidOperationException("el cuadro de diálogo no tiene dónde escribir");
        box.Text = text;
        var accept = Descendants<Button>(prompt).FirstOrDefault(button => button.IsDefault);
        if (accept is not null) accept.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        else prompt.DialogResult = true;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void EditLevel(Window dialog, ListBox list, Guid id, string name, string elevation)
    {
        list.SelectedValue = id;
        ((TextBox)dialog.FindName("NameBox")).Text = name;
        ((TextBox)dialog.FindName("ElevationBox")).Text = elevation;
        Call(dialog, "Apply_Click");
    }

    private static void Call(Window dialog, string handler) =>
        dialog.GetType().GetMethod(handler, Private)!.Invoke(dialog, [dialog, new RoutedEventArgs()]);
}
