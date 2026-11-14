using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// Builds a small house from an empty project the way a person does it: ribbon buttons, clicks at plan points, the
/// keyboard, and the type menus the tools open. Clicks go through the same canvas handlers as the mouse, with the
/// element under the pointer found by hit testing, so a button that only works when called from code fails here.
/// Every step runs even when an earlier one failed, and the run writes a report and one picture per step to
/// artifacts/interaction-evidence/house-from-scratch/.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string Evidence = "artifacts/interaction-evidence/house-from-scratch";
    private static bool _initialised;

    /// <summary>Scripts a person follows in a dialog a step expects, keyed by window type name; run instead of closing it.</summary>
    private static readonly Dictionary<string, Queue<Action<Window>>> DialogScripts = new(StringComparer.Ordinal);

    /// <summary>Working windows a step drives itself (the family editor); the watchdog leaves them open.</summary>
    private static readonly HashSet<string> KeepOpen = new(StringComparer.Ordinal);

    private static MainWindow _window = null!;
    private static Action _pump = null!;
    private static readonly List<string> Dialogs = [];
    private static ContextMenu? _lastMenu;

    private sealed record Outcome(int Number, string Step, bool Passed, string Detail, IReadOnlyList<string> Dialogs);

    private static DocumentSession Session => (DocumentSession)typeof(MainWindow).GetField("_session", Private)!.GetValue(_window)!;
    private static BuildingDocument Doc => Session.Document;
    private static Canvas PlanCanvas => (Canvas)_window.FindName("PlanCanvas");
    private static string Status => ((TextBlock)_window.FindName("StatusText")).Text;

    public static void Run(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("House from scratch", "artifacts/interaction-evidence/house-from-scratch", Steps(), check);
    }

    /// <summary>Connects the driver to the main window once, so the type menus the tools open are tracked.</summary>
    private static void Initialise(MainWindow window, Action pump)
    {
        _window = window;
        _pump = pump;
        if (_initialised) return;
        _initialised = true;
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((sender, _) => _lastMenu = (ContextMenu)sender));
    }

    /// <summary>
    /// Runs the steps of one scenario in order, each even when an earlier one failed, and writes the report and one plan
    /// picture per step to <paramref name="evidence"/>. Fails the suite at the end when any step failed and a check is given.
    /// </summary>
    private static List<Outcome> RunScenario(string title, string evidence, IEnumerable<(string Name, Func<string> Action)> steps,
        Action<bool, string>? check, string reportTitle = "Casa desde cero — resultado de la prueba automática")
    {
        Evidence = evidence;
        Directory.CreateDirectory(Evidence);
        var watchdog = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        watchdog.Tick += (_, _) => CloseUnexpectedWindows();
        watchdog.Start();

        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
        var outcomes = new List<Outcome>();
        try
        {
            foreach (var (name, action) in steps)
            {
                Dialogs.Clear();
                DialogScripts.Clear();
                KeepOpen.Clear();
                string detail;
                bool passed;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    detail = action();
                    passed = true;
                }
                catch (Exception exception)
                {
                    var inner = exception is TargetInvocationException { InnerException: { } cause } ? cause : exception;
                    detail = inner.Message + (inner is StepFailed ? "" : $" ({inner.GetType().Name})");
                    passed = false;
                }
                detail += $" [{clock.Elapsed.TotalSeconds:0.0} s]";
                outcomes.Add(new Outcome(outcomes.Count + 1, name, passed, detail, Dialogs.ToList()));
                Console.WriteLine($"{(passed ? "PASO" : "FALLA")} {outcomes.Count,2}. {name}: {detail}" +
                    (Dialogs.Count > 0 ? "  [ventanas: " + string.Join(" | ", Dialogs) + "]" : ""));
                SavePlan($"{Evidence}/{outcomes.Count:00}.png");
                Escape();
            }
        }
        finally
        {
            watchdog.Stop();
            WriteReport(outcomes, reportTitle);
        }
        var failed = outcomes.Where(outcome => !outcome.Passed).ToList();
        check?.Invoke(failed.Count == 0, $"{title}: {outcomes.Count - failed.Count} of {outcomes.Count} steps work like a person does them" +
            (failed.Count == 0 ? "" : " — fails: " + string.Join(", ", failed.Select(outcome => outcome.Number + " " + outcome.Step))));
        return outcomes;
    }

    /// <summary>The steps of the house, in the order a person builds it in the reference BIM application.</summary>
    private static IEnumerable<(string Name, Func<string> Action)> Steps()
    {
        Guid ground = Guid.Empty;
        yield return ("Nuevo proyecto", () =>
        {
            App("NewProject");
            Expect(Doc.Walls.Count == 0 && Doc.Levels.Count >= 1, $"el proyecto nuevo no está vacío ({Doc.Walls.Count} muros)");
            ground = Doc.Levels.OrderBy(level => level.Elevation).First().Id;
            var opened = (Guid)typeof(MainWindow).GetField("_activeLevelId", Private)!.GetValue(_window)!;
            Expect(opened == ground, $"el proyecto nuevo no abre en la planta del nivel más bajo, sino en «{Doc.Levels.FirstOrDefault(level => level.Id == opened)?.Name}»");
            Zoom(30);
            return $"{Doc.Levels.Count} niveles: {string.Join(", ", Doc.Levels.Select(level => $"{level.Name} {level.Elevation:0.00}"))}";
        });
        yield return ("Nivel nuevo", () =>
        {
            var before = Doc.Levels.Count;
            Arch(ArchitectureCommand.Level);
            Expect(Doc.Levels.Count == before + 1, "el botón Nivel no creó un nivel");
            var created = Doc.Levels.OrderBy(level => level.Elevation).Last();
            Expect(Doc.Views.Any(view => view.Kind == ProjectViewKind.Plan && view.LevelId == created.Id), "el nivel nuevo no tiene planta");
            // The plan in use stays: setting up levels first must not move the drawing to the top level.
            var active = (Guid)typeof(MainWindow).GetField("_activeLevelId", Private)!.GetValue(_window)!;
            Expect(active == ground, $"crear el nivel cambió la planta activa a «{Doc.Levels.Single(level => level.Id == active).Name}»");
            Expect(Status.Contains(created.Name, StringComparison.Ordinal), $"la barra de estado no dice qué nivel se creó: {Status}");
            return $"nuevo nivel a {created.Elevation:0.00} m; se sigue en la planta del {Doc.Levels.Single(level => level.Id == ground).Name}";
        });
        yield return ("Eje con dos clics", () =>
        {
            var before = Doc.Grids.Count;
            Arch(ArchitectureCommand.Grid);
            Click(-2, -2);
            Click(-2, 10);
            Expect(Doc.Grids.Count == before + 1, $"no se creó el eje (estado: {Status})");
            return $"eje «{Doc.Grids[^1].Name}»";
        });
        yield return ("Muros exteriores en cadena (10 × 8 m)", () =>
        {
            Arch(ArchitectureCommand.Wall);
            foreach (var (x, y) in new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 8.0), (0.0, 8.0), (0.0, 0.0) }) Click(x, y);
            Escape();
            Expect(Doc.Walls.Count == 4, $"se esperaban 4 muros y hay {Doc.Walls.Count}");
            var corners = Doc.Walls.SelectMany(wall => new[] { wall.Start, wall.End }).GroupBy(Round).ToList();
            Expect(corners.Count == 4 && corners.All(group => group.Count() == 2), "las esquinas no cierran: " +
                string.Join(" ", corners.Select(group => $"{group.Key}×{group.Count()}")));
            return "4 muros con las 4 esquinas unidas";
        });
        yield return ("Muro tecleando la longitud (8 m)", () =>
        {
            var before = Doc.Walls.Count;
            Arch(ArchitectureCommand.Wall);
            Click(5, 0);
            Move(5, 3);
            TypeKeys("8");
            Key(System.Windows.Input.Key.Enter);
            Escape();
            Expect(Doc.Walls.Count == before + 1, $"no se creó el muro (estado: {Status})");
            var wall = Doc.Walls[^1];
            Expect(Math.Abs(wall.Length - 8) < 1e-6, $"el muro mide {wall.Length:0.###} m, no 8");
            return $"muro de {wall.Length:0.00} m hasta ({wall.End.X:0.##}, {wall.End.Y:0.##})";
        });
        yield return ("Puerta exterior", () => PlaceOpening(ArchitectureCommand.Door, 2.5, 0, OpeningKind.Door));
        yield return ("Puerta interior", () => PlaceOpening(ArchitectureCommand.Door, 5, 4, OpeningKind.Door));
        yield return ("Ventana", () => PlaceOpening(ArchitectureCommand.Window, 7.5, 8, OpeningKind.Window));
        yield return ("Piso", () =>
        {
            var before = Doc.Floors.Count(floor => floor.Kind == FloorKind.Floor);
            Arch(ArchitectureCommand.Floor);
            PickMenu(0);
            // The floor is sketched like the reference application: its corners, closing on the first one.
            foreach (var (x, y) in new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 8.0), (0.0, 8.0), (0.0, 0.0) }) Click(x, y);
            var floors = Doc.Floors.Where(floor => floor.Kind == FloorKind.Floor).ToList();
            Expect(floors.Count > before, $"no se creó el piso (estado: {Status})");
            return $"{floors.Count - before} piso(s), {floors.Sum(floor => floor.CalculateArea()):0.0} m²";
        });
        yield return ("Habitaciones automáticas", () =>
        {
            Arch(ArchitectureCommand.RoomAutomatic);
            Expect(Doc.Rooms.Count == 2, $"se esperaban 2 habitaciones y hay {Doc.Rooms.Count} (estado: {Status})");
            Expect(Doc.Rooms.All(room => room.Name.StartsWith("Habitación ", StringComparison.Ordinal)),
                "las habitaciones nuevas no llevan nombre en español: " + string.Join(", ", Doc.Rooms.Select(room => room.Name)));
            return string.Join(", ", Doc.Rooms.Select(room => $"{room.Number} {room.Name}"));
        });
        yield return ("Etiquetar todas las habitaciones", () =>
        {
            Arch(ArchitectureCommand.TagAllRooms);
            var labels = PlanCanvas.Children.OfType<Border>().Count(tag => Doc.Rooms.Any(room => (tag.Child as TextBlock)?.Text?.Contains(room.Number) == true));
            Expect(labels >= 2, $"se dibujaron {labels} etiquetas");
            return $"{labels} etiquetas en el plano";
        });
        yield return ("La etiqueta de habitación no tapa el nombre", () =>
        {
            foreach (var room in Doc.Rooms)
            {
                var numbers = PlanCanvas.Children.OfType<FrameworkElement>().Where(element => Equals(element.Tag, room.Id) && Shows(element, room.Number)).ToList();
                var name = PlanCanvas.Children.OfType<TextBlock>().SingleOrDefault(block => Equals(block.Tag, room.Id) && block.Text.Contains(room.Name));
                Expect(numbers.Count == 1, $"el número {room.Number} aparece {numbers.Count} veces");
                Expect(name is not null && !Bounds(numbers[0]).IntersectsWith(Bounds(name)), $"la burbuja {room.Number} tapa el nombre «{room.Name}»");
            }
            return "una etiqueta por habitación: número en burbuja y nombre debajo";
        });
        yield return ("Marcas de alzado en el lado de su nombre", () =>
        {
            var house = new Rect(Canvas(0, 8), Canvas(10, 0));
            Rect Marker(string name) => Bounds(PlanCanvas.Children.OfType<TextBlock>().Single(block => block.Text == name));
            Expect(Marker("Vista Norte").Bottom < house.Top && Marker("Vista Sur").Top > house.Bottom &&
                   Marker("Vista Este").Left > house.Right && Marker("Vista Oeste").Right < house.Left,
                "las marcas no están en su lado: " + string.Join(", ", new[] { "Vista Norte", "Vista Sur", "Vista Este", "Vista Oeste" }
                    .Select(name => $"{name} {Marker(name).TopLeft:0}")));
            return "Norte arriba, Sur abajo, Este a la derecha, Oeste a la izquierda";
        });
        yield return ("El alzado Norte muestra la fachada norte vista desde fuera", () =>
        {
            var window = Doc.Openings.Single(opening => opening.Kind == OpeningKind.Window);
            Invoke("SelectProjectView", BuiltInProjectViews.NorthElevationId);
            _pump();
            var canvas = (Canvas)_window.FindName("ElevationCanvas");
            var walls = canvas.Children.OfType<FrameworkElement>().Where(element => element.Tag is Guid id && Doc.Walls.Any(wall => wall.Id == id))
                .Select(Bounds).ToList();
            var drawn = canvas.Children.OfType<FrameworkElement>().FirstOrDefault(element => Equals(element.Tag, window.Id));
            Expect(walls.Count > 0 && drawn is not null, "el alzado no dibujó los muros o la ventana");
            var middle = (walls.Min(rect => rect.Left) + walls.Max(rect => rect.Right)) / 2;
            SaveWindow($"{Evidence}/alzado-norte.png");
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            _pump();
            // The window is on the east half of the north wall; seen from the north, east is on the left.
            Expect(Bounds(drawn!).Right <= middle, "la ventana del lado este sale a la derecha: el alzado se ve desde dentro");
            return "la ventana del lado este aparece a la izquierda, como se ve desde el norte";
        });
        yield return ("Cielo raso automático", () =>
        {
            Arch(ArchitectureCommand.CeilingAutomatic);
            PickMenu(0);
            var ceilings = Doc.Floors.Count(floor => floor.Kind == FloorKind.Ceiling);
            Expect(ceilings >= 1, $"no se creó el cielo (estado: {Status})");
            return $"{ceilings} cielo(s)";
        });
        yield return ("Cubierta por perímetro, cerrando en el primer punto", () =>
        {
            Arch(ArchitectureCommand.RoofByFootprint);
            PickMenu(menu => menu.Header?.ToString()?.Contains("Gable", StringComparison.OrdinalIgnoreCase) == true);
            foreach (var (x, y) in new[] { (-0.5, -0.5), (10.5, -0.5), (10.5, 8.5), (-0.5, 8.5), (-0.5, -0.5) }) Click(x, y);
            var roofs = Doc.Floors.Where(floor => floor.Kind == FloorKind.Roof).ToList();
            Expect(roofs.Count == 1, $"al volver al primer punto no se cerró el contorno (estado: {Status})");
            return $"cubierta «{roofs[0].Name}» de {RoofGeometry.SurfaceArea(roofs[0]):0.0} m² inclinados";
        });
        yield return ("Columna", () =>
        {
            var before = Doc.Columns.Count;
            Arch(ArchitectureCommand.Column);
            PickMenuIfOpen(0);
            Click(12, 0);
            Expect(Doc.Columns.Count == before + 1, $"no se colocó la columna (estado: {Status})");
            return "columna en (12, 0)";
        });
        yield return ("Escalera con dos clics", () =>
        {
            var before = Doc.StructuralElements.Count(element => element.Kind == StructuralElementKind.Stair);
            Arch(ArchitectureCommand.Stair);
            Click(1, 1);
            Click(1, 5);
            var stairs = Doc.StructuralElements.Where(element => element.Kind == StructuralElementKind.Stair).ToList();
            Expect(stairs.Count == before + 1, $"no se creó la escalera (estado: {Status})");
            return $"escalera de {stairs[^1].Length:0.00} m";
        });
        yield return ("Seleccionar la escalera y poner barandillas", () =>
        {
            Click(1, 3);
            var selected = (Guid?)typeof(MainWindow).GetField("_selectedStructuralId", Private)!.GetValue(_window);
            var stair = Doc.StructuralElements.LastOrDefault(element => element.Kind == StructuralElementKind.Stair);
            Expect(stair is not null && selected == stair.Id, "el clic sobre la escalera no la seleccionó");
            Arch(ArchitectureCommand.RailingOnHost);
            // The stair brings its guards when placed; asking again must not stack a second set on top.
            var before = Doc.StructuralElements.Count(element => element.Kind == StructuralElementKind.Railing);
            Arch(ArchitectureCommand.RailingOnHost);
            var railings = Doc.StructuralElements.Count(element => element.Kind == StructuralElementKind.Railing);
            Expect(railings == 2 && before == 2, $"la escalera tiene {railings} barandillas, se esperaban 2 sin duplicar (estado: {Status})");
            return $"{railings} barandillas, sin duplicados al repetir el comando";
        });
        yield return ("Seleccionar un muro con un clic", () =>
        {
            Click(2, 8);
            var selected = (Guid?)typeof(MainWindow).GetField("_selectedWallId", Private)!.GetValue(_window);
            var north = Doc.Walls.FirstOrDefault(wall => Math.Abs(wall.Start.Y - 8) < 1e-6 && Math.Abs(wall.End.Y - 8) < 1e-6);
            Expect(north is not null && selected == north.Id, "el clic sobre el muro norte no lo seleccionó");
            return "muro norte seleccionado";
        });
        yield return ("Cotas", () =>
        {
            var before = Doc.Dimensions.Count;
            App("AlignedDimension");
            Expect(Doc.Dimensions.Count > before, $"no se creó ninguna cota (estado: {Status})");
            return $"{Doc.Dimensions.Count - before} cotas";
        });
        yield return ("Deshacer y rehacer", () =>
        {
            var walls = Doc.Walls.Count;
            var dimensions = Doc.Dimensions.Count;
            App("Undo");
            var undone = Doc.Dimensions.Count != dimensions || Doc.Walls.Count != walls;
            App("Redo");
            Expect(undone && Doc.Dimensions.Count == dimensions && Doc.Walls.Count == walls, "deshacer o rehacer no devolvieron el modelo");
            return "el último paso se deshace y se rehace";
        });
        yield return ("Vista 3D", () => ShowsView("Model3D"));
        yield return ("Alzado con la cubierta", () =>
        {
            var shown = ShowsView("Elevation");
            var roof = Doc.Floors.FirstOrDefault(floor => floor.Kind == FloorKind.Roof);
            Invoke("SelectProjectView", BuiltInProjectViews.NorthElevationId);
            _pump();
            var canvas = (Canvas)_window.FindName("ElevationCanvas");
            var drawn = roof is not null && canvas.Children.OfType<FrameworkElement>().Any(element => Equals(element.Tag, roof.Id));
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            _pump();
            Expect(roof is not null && drawn, $"la cubierta no se dibuja en el alzado (cubierta: {(roof is null ? "no hay" : $"{roof.Shape}, pendiente {roof.Pitch:0}°, IsPitched {roof.IsPitched}")})");
            return shown + ", con la cubierta";
        });
        yield return ("Sección con dos clics", () =>
        {
            var before = Doc.Views.Count(view => view.Kind == ProjectViewKind.Section);
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            _pump();
            App("Section");
            Click(-1, 4);
            Click(11, 4);
            var after = Doc.Views.Count(view => view.Kind == ProjectViewKind.Section);
            Expect(after == before + 1, $"no se creó la sección (estado: {Status})");
            return "sección creada";
        });
        yield return ("Tabla de planificación", () => ShowsView("Schedule"));
        yield return ("Lámina", () =>
        {
            var before = Doc.Sheets.Count;
            App("Sheet");
            Expect(Doc.Sheets.Count == before + 1, "no se creó la lámina");
            return $"lámina {Doc.Sheets[^1].Number}";
        });
        yield return ("Guardar y volver a abrir", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "g3d-casa-" + Guid.NewGuid().ToString("N") + ProjectFile.NativeExtension);
            typeof(MainWindow).GetField("_filePath", Private)!.SetValue(_window, path);
            var saved = (bool)Invoke("SaveProject", false)!;
            Expect(saved && File.Exists(path), "no se guardó el proyecto");
            var counts = Counts(Doc);
            Invoke("OpenProjectPath", path);
            for (var i = 0; i < 40 && Counts(Doc) != counts; i++) _pump();
            Expect(Counts(Doc) == counts, $"al reabrir cambió el modelo: {counts} → {Counts(Doc)}");
            File.Delete(path);
            return counts;
        });
        yield return ("La casa está en el nivel donde se dibujó", () =>
        {
            Expect(Doc.Walls.All(wall => wall.LevelId == ground), "hay muros en otro nivel: " +
                string.Join(", ", Doc.Walls.Select(wall => Doc.Levels.Single(level => level.Id == wall.LevelId).Name).Distinct()));
            return $"{Doc.Walls.Count} muros en {Doc.Levels.Single(level => level.Id == ground).Name}";
        });
        yield return ("Presupuesto de la casa", () =>
        {
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            var budget = ConstructionBudget.Build(Doc, Session.GetBudgetSettings());
            Expect(budget.Takeoff.Find(MaterialCatalog.Door)?.NetQuantity == 2 && budget.Takeoff.Find(MaterialCatalog.RoofSheet) is not null,
                "el presupuesto no ve las puertas o la cubierta");
            return $"₡{budget.Total:#,0} con {budget.Lines.Count} materiales";
        });
    }

    // ---------------------------------------------------------------------------------------------------- actions

    private static string PlaceOpening(ArchitectureCommand command, double x, double y, OpeningKind kind)
    {
        var before = Doc.Openings.Count(opening => opening.Kind == kind);
        Arch(command);
        PickMenuIfOpen(0);
        Move(x, y);
        Click(x, y);
        var placed = Doc.Openings.Where(opening => opening.Kind == kind).ToList();
        Expect(placed.Count == before + 1, $"no se colocó (estado: {Status})");
        var host = Doc.Walls.Single(wall => wall.Id == placed[^1].WallId);
        return $"{placed[^1].Width:0.00} × {placed[^1].Height:0.00} en el muro de ({host.Start.X:0.#},{host.Start.Y:0.#})–({host.End.X:0.#},{host.End.Y:0.#})";
    }

    private static string ShowsView(string command)
    {
        var tabs = (TabControl)_window.FindName("ViewTabs");
        tabs.SelectedIndex = 0;
        _pump();
        App(command);
        Expect(tabs.SelectedIndex != 0, $"la vista no cambió (estado: {Status})");
        var header = (tabs.SelectedItem as TabItem)?.Header?.ToString() ?? tabs.SelectedIndex.ToString();
        SaveWindow($"{Evidence}/view-{command}.png");
        tabs.SelectedIndex = 0;
        _pump();
        return "pestaña " + header;
    }

    private static string Counts(BuildingDocument document) =>
        $"{document.Walls.Count} muros, {document.Openings.Count} vanos, {document.Floors.Count} losas, {document.Rooms.Count} habitaciones, " +
        $"{document.StructuralElements.Count} elementos, {document.Columns.Count} columnas";

    private static void Arch(ArchitectureCommand command)
    {
        _lastMenu = null;
        var route = typeof(MainWindow).GetMethod("ArchitectureRibbon_CommandInvoked", Private)!;
        route.Invoke(_window, [null, new ArchitectureCommandEventArgs(command)]);
        _pump();
    }

    private static void App(string command)
    {
        _lastMenu = null;
        var handled = (bool)Invoke("ExecuteApplicationCommand", command)!;
        Expect(handled, $"el comando {command} no hace nada");
        _pump();
    }

    private static void PickMenu(int index) => PickMenu(menu => menu == _lastMenu!.Items.OfType<MenuItem>().ElementAt(index));

    private static void PickMenu(Func<MenuItem, bool> choose)
    {
        Expect(_lastMenu is { IsOpen: true }, "el botón no abrió el menú de tipos");
        var item = _lastMenu!.Items.OfType<MenuItem>().FirstOrDefault(choose);
        Expect(item is not null, "el menú no tiene el tipo buscado: " + string.Join(", ", _lastMenu.Items.OfType<MenuItem>().Select(menu => menu.Header)));
        _lastMenu.IsOpen = false;
        item!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        _pump();
    }

    private static void PickMenuIfOpen(int index)
    {
        if (_lastMenu is { IsOpen: true }) PickMenu(index);
    }

    private static void Zoom(double pixelsPerMetre) => Zoom(pixelsPerMetre, 5, 4);

    /// <summary>Zooms the plan and centres it on a model point, as a person frames the part they are drawing.</summary>
    private static void Zoom(double pixelsPerMetre, double centreX, double centreY)
    {
        typeof(MainWindow).GetField("_scale", Private)!.SetValue(_window, pixelsPerMetre);
        var pan = typeof(MainWindow).GetField("_panOffset", Private)!;
        pan.SetValue(_window, pan.FieldType == typeof(Vector)
            ? new Vector(-centreX * pixelsPerMetre, centreY * pixelsPerMetre)
            : (object)new Point(-centreX * pixelsPerMetre, centreY * pixelsPerMetre));
        Invoke("RenderPlan");
        _pump();
    }

    /// <summary>Moves the pointer to a model point through the plan's MouseMove handler.</summary>
    private static void Move(double x, double y)
    {
        var point = Canvas(x, y);
        SetPointer(point);
        try
        {
            var move = new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = UIElement.MouseMoveEvent, Source = PlanCanvas };
            Invoke("PlanCanvas_MouseMove", PlanCanvas, move);
        }
        finally { SetPointer(null); }
        _pump();
    }

    /// <summary>A full left click at a model point: move, press and release, on the element the pointer is over.</summary>
    private static void Click(double x, double y, int clickCount = 1)
    {
        Move(x, y);
        var point = Canvas(x, y);
        var target = HitElement(point);
        SetPointer(point);
        try
        {
            var down = Button(UIElement.MouseLeftButtonDownEvent, target, clickCount);
            Invoke("PlanCanvas_MouseLeftButtonDown", PlanCanvas, down);
            // A real mouse always releases the button, even when the press was handled. Without the release the plan
            // still thinks the button is down, and the next pointer move drags whatever is selected by its grip.
            var up = Button(UIElement.MouseLeftButtonUpEvent, target, clickCount);
            Invoke("PlanCanvas_MouseLeftButtonUp", PlanCanvas, up);
        }
        finally { SetPointer(null); }
        _pump();
    }

    private static MouseButtonEventArgs Button(RoutedEvent routed, object source, int clickCount)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routed, Source = source };
        typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!.SetValue(args, clickCount);
        return args;
    }

    /// <summary>The topmost plan shape under a canvas point, as the mouse would find it; the canvas when nothing is there.</summary>
    private static object HitElement(Point point)
    {
        DependencyObject? found = null;
        VisualTreeHelper.HitTest(PlanCanvas, null, result =>
        {
            if (result.VisualHit is FrameworkElement { Tag: not null, IsHitTestVisible: true } element && element != PlanCanvas)
            {
                found = element;
                return HitTestResultBehavior.Stop;
            }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(point));
        return (object?)found ?? PlanCanvas;
    }

    private static void TypeKeys(string text)
    {
        foreach (var character in text)
        {
            var key = character switch
            {
                >= '0' and <= '9' => System.Windows.Input.Key.D0 + (character - '0'),
                '.' => System.Windows.Input.Key.OemPeriod,
                _ => throw new ArgumentOutOfRangeException(nameof(text))
            };
            Key(key, character.ToString());
        }
    }

    /// <summary>Sends a key the way the window receives it: preview to the main window, then to the focused element.</summary>
    private static void Key(Key key, string? text = null)
    {
        var source = PresentationSource.FromVisual(_window)!;
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        // Raising the routed event runs the window's class handler (OnPreviewKeyDown) and its XAML handler, as a key does.
        _window.RaiseEvent(preview);
        if (!preview.Handled && Keyboard.FocusedElement is UIElement focused)
            focused.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
        // A printable key also produces text input, which is where the window reads a typed wall length.
        if (text is not null && !preview.Handled)
        {
            var composition = new TextComposition(InputManager.Current, _window, text);
            _window.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });
        }
        _pump();
    }

    private static void Escape()
    {
        Key(System.Windows.Input.Key.Escape);
        Key(System.Windows.Input.Key.Escape);
        foreach (var menu in new[] { _lastMenu }.OfType<ContextMenu>()) menu.IsOpen = false;
    }

    private static Point Canvas(double x, double y) => (Point)Invoke("WorldToCanvas", new Point2(x, y))!;

    private static void SetPointer(Point? point) =>
        typeof(MainWindow).GetField("_planPointerOverride", Private)!.SetValue(_window, point);

    private static object? Invoke(string name, params object?[] arguments)
    {
        var method = typeof(MainWindow).GetMethods(Private).Where(candidate => candidate.Name == name)
            .First(candidate => candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(_window, arguments);
    }

    /// <summary>Where an element is drawn on its canvas, in canvas pixels.</summary>
    private static Rect Bounds(FrameworkElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = element.ActualWidth > 0 ? element.ActualWidth : element.DesiredSize.Width;
        var height = element.ActualHeight > 0 ? element.ActualHeight : element.DesiredSize.Height;
        if (element is System.Windows.Shapes.Shape shape && shape.RenderedGeometry.Bounds is { IsEmpty: false } geometry
            && double.IsNaN(System.Windows.Controls.Canvas.GetLeft(element)))
            return geometry;
        var left = System.Windows.Controls.Canvas.GetLeft(element);
        var top = System.Windows.Controls.Canvas.GetTop(element);
        return new Rect(double.IsNaN(left) ? 0 : left, double.IsNaN(top) ? 0 : top, width, height);
    }

    /// <summary>Whether a drawn element reads exactly as the text, alone or as the content of a bubble.</summary>
    private static bool Shows(FrameworkElement element, string text) => element switch
    {
        TextBlock block => block.Text == text,
        Border { Child: TextBlock inner } => inner.Text == text,
        _ => false
    };

    private static string Round(Point2 point) => $"({point.X:0.###},{point.Y:0.###})";

    private static void Expect(bool condition, string failure)
    {
        if (!condition) throw new StepFailed(failure);
    }

    private sealed class StepFailed(string message) : Exception(message);

    // ---------------------------------------------------------------------------------------------------- dialogs

    /// <summary>
    /// Records and closes any window a step opens that a person would have to answer, answering No first (do not save,
    /// do not replace). Drawing should not ask questions; each dialog is listed in the report next to its step.
    /// </summary>
    private static void CloseUnexpectedWindows()
    {
        foreach (var extra in Application.Current.Windows.OfType<Window>()
                     .Where(window => window != _window && window.IsVisible && window.GetType().Name is not ("ProjectOpenProgressWindow" or "SplashWindow")).ToList())
        {
            if (KeepOpen.Contains(extra.GetType().Name)) continue;
            if (DialogScripts.TryGetValue(extra.GetType().Name, out var queue) && queue.TryDequeue(out var script))
            {
                // A dialog the step expects: the person fills it in; whatever the script leaves open is then closed.
                Dialogs.Add($"{extra.GetType().Name} «{extra.Title}» (rellenado)");
                try { script(extra); }
                catch (Exception exception) { Dialogs.Add($"error al rellenar {extra.GetType().Name}: {exception.Message}"); }
                if (extra.IsVisible) extra.Close();
                continue;
            }
            Dialogs.Add($"{extra.GetType().Name} «{extra.Title}»");
            extra.Close();
        }
        foreach (var dialog in NativeDialogs())
        {
            Dialogs.Add("mensaje «" + dialog.Text + "»");
            foreach (var command in new[] { 7, 2, 1 }) PostMessage(dialog.Handle, 0x0111, (IntPtr)command, IntPtr.Zero);
        }
    }

    private static List<(IntPtr Handle, string Text)> NativeDialogs()
    {
        var found = new List<(IntPtr, string)>();
        EnumThreadWindows(GetCurrentThreadId(), (handle, _) =>
        {
            var name = new StringBuilder(64);
            GetClassName(handle, name, name.Capacity);
            if (name.ToString() == "#32770" && IsWindowVisible(handle))
            {
                var text = new StringBuilder();
                EnumChildWindows(handle, (child, _) =>
                {
                    var buffer = new StringBuilder(512);
                    GetClassName(child, name, name.Capacity);
                    if (name.ToString() == "Static" && GetWindowText(child, buffer, buffer.Capacity) > 0) text.Append(buffer).Append(' ');
                    return true;
                }, IntPtr.Zero);
                found.Add((handle, text.ToString().Trim()));
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder name, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    // ---------------------------------------------------------------------------------------------------- evidence

    private static void WriteReport(IReadOnlyList<Outcome> outcomes, string title)
    {
        var report = new StringBuilder($"# {title}\n\n");
        report.AppendLine($"Pasos que funcionan: **{outcomes.Count(outcome => outcome.Passed)} de {outcomes.Count}**.\n");
        report.AppendLine("| # | Paso | Resultado | Detalle | Ventanas que aparecieron |");
        report.AppendLine("|---|---|---|---|---|");
        foreach (var outcome in outcomes)
            report.AppendLine($"| {outcome.Number} | {outcome.Step} | {(outcome.Passed ? "funciona" : "**falla**")} | " +
                $"{outcome.Detail.Replace("|", "/")} | {string.Join("; ", outcome.Dialogs).Replace("|", "/")} |");
        File.WriteAllText($"{Evidence}/report.md", report.ToString(), new UTF8Encoding(false));
    }

    private static void SavePlan(string path) => Save(PlanCanvas, path);

    private static void SaveWindow(string path) => Save((FrameworkElement)_window.Content, path);

    private static void Save(FrameworkElement element, string path)
    {
        if (element.ActualWidth < 1 || element.ActualHeight < 1) return;
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
