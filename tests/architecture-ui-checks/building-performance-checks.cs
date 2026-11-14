using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;

/// <summary>
/// Times what a person does with a large building in the real window — the ten-storey tower of the samples — so that
/// the program never freezes: refresh, draw each level's plan, switch levels, open 3D, elevation and section, compose
/// and print the sheets, price it, find rooms, undo, save and reopen. Writes artifacts/interaction-evidence/
/// rendimiento-edificio.md. Opt-in with G3D_BUILDING=1.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    private static int CountItems(System.Windows.Controls.ItemCollection items) =>
        items.OfType<System.Windows.Controls.TreeViewItem>().Sum(item => 1 + CountItems(item.Items));

    private sealed record Timing(string Operation, double Milliseconds, double Limit, string Note);

    public static void RunBuildingPerformance(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        var timings = new List<Timing>();
        void Time(string operation, double limit, Func<string> action)
        {
            var clock = Stopwatch.StartNew();
            string note;
            try { note = action(); }
            catch (Exception exception) { note = "ERROR: " + (exception.InnerException ?? exception).Message; }
            clock.Stop();
            timings.Add(new Timing(operation, clock.Elapsed.TotalMilliseconds, limit, note));
            Console.WriteLine($"TIEMPO {clock.Elapsed.TotalMilliseconds,8:0} ms (límite {limit:0}) {operation}: {note}");
        }

        BuildingDocument tower = null!;
        Time("Crear la torre de 10 pisos (núcleo)", 20000, () =>
        {
            tower = TenStoreyBuilding.Create();
            return $"{tower.Levels.Count} niveles, {tower.Walls.Count} muros, {tower.Openings.Count} vanos, {tower.Rooms.Count} habitaciones, " +
                   $"{tower.StructuralElements.Count} elementos, {tower.ElectricalDevices.Count} eléctricos, {tower.Sheets.Count} láminas";
        });
        var session = new DocumentSession(tower);
        typeof(MainWindow).GetField("_session", Private)!.SetValue(_window, session);
        var levels = tower.Levels.OrderBy(level => level.Elevation).ToList();
        typeof(MainWindow).GetField("_activeLevelId", Private)!.SetValue(_window, levels[0].Id);
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;

        Time("Refrescar toda la ventana", 2000, () => { Invoke("RefreshAll"); _pump(); return ""; });
        Time("Desglose de un refresco (por parte)", 4000, () =>
        {
            var parts = new List<string>();
            foreach (var part in new[] { "SynchronizeModelViewGraphics", "RefreshLevelPicker", "RefreshViewShortcuts", "RefreshTree", "RefreshProperties",
                         "RenderPlan", "Render3D", "RefreshExtendedWorkflows" })
            {
                var clock = Stopwatch.StartNew();
                Invoke(part);
                parts.Add($"{part} {clock.Elapsed.TotalMilliseconds:0}");
            }
            var layout = Stopwatch.StartNew();
            _pump();
            parts.Add($"diseño y pintado {layout.Elapsed.TotalMilliseconds:0}");
            foreach (var part in new[] { "RefreshTree", "RenderPlan", "RefreshProperties" })
            {
                _pump();
                Invoke(part);
                var alone = Stopwatch.StartNew();
                _pump();
                parts.Add($"pintado tras {part} {alone.Elapsed.TotalMilliseconds:0}");
            }
            var idle = Stopwatch.StartNew();
            _pump();
            parts.Add($"espera vacía {idle.Elapsed.TotalMilliseconds:0}");
            var tree = (System.Windows.Controls.TreeView)_window.FindName("ProjectTree");
            parts.Add($"árbol: {CountItems(tree.Items)} nodos, virtualizado {System.Windows.Controls.VirtualizingPanel.GetIsVirtualizing(tree)}");
            return string.Join(" · ", parts);
        });
        Time("Encuadrar la planta baja (F)", 1500, () => { Invoke("FitPlan_Click", _window, new System.Windows.RoutedEventArgs()); _pump(); return ""; });
        var planTimes = new List<double>();
        Time("Cambiar de nivel en el selector, uno por uno (10)", 6000, () =>
        {
            foreach (var level in levels.Take(10))
            {
                var clock = Stopwatch.StartNew();
                var picker = (ComboBox)_window.FindName("LevelPicker");
                picker.SelectedItem = picker.Items.OfType<ComboBoxItem>().First(item => Equals(item.Tag, level.Id));
                _pump();
                planTimes.Add(clock.Elapsed.TotalMilliseconds);
            }
            return $"peor nivel {planTimes.Max():0} ms, promedio {planTimes.Average():0} ms";
        });
        Time("Dibujar la planta de un piso tipo (RenderPlan)", 800, () =>
        {
            var clock = Stopwatch.StartNew();
            for (var repeat = 0; repeat < 5; repeat++) Invoke("RenderPlan");
            return $"{clock.Elapsed.TotalMilliseconds / 5:0} ms por dibujo, {PlanCanvas.Children.Count} figuras";
        });
        Time("Mover el ratón sobre la planta (20 movimientos)", 2000, () =>
        {
            var handler = Stopwatch.StartNew();
            for (var step = 0; step < 20; step++)
            {
                SetPointer(Canvas(4 + step * 1.2, 8));
                Invoke("PlanCanvas_MouseMove", PlanCanvas, new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = System.Windows.UIElement.MouseMoveEvent });
                SetPointer(null);
            }
            var handlerMs = handler.Elapsed.TotalMilliseconds / 20;
            for (var step = 0; step < 20; step++) Move(4 + step * 1.2, 9);
            return $"manejador {handlerMs:0.0} ms por movimiento (el resto es la espera de la prueba)";
        });
        Time("Abrir la vista 3D", 15000, () =>
        {
            App("Model3D");
            var task = (Task<Guanacaste3D.Core.Rendering.RenderScene?>)Invoke("GetSceneAsync")!;
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) _pump();
            return task.IsCompletedSuccessfully ? $"{task.Result?.TriangleCount ?? 0} triángulos" : "no terminó en 60 s";
        });
        Time("Abrir el alzado Norte", 6000, () =>
        {
            Invoke("SelectProjectView", BuiltInProjectViews.NorthElevationId);
            _pump();
            return $"{((Canvas)_window.FindName("ElevationCanvas")).Children.Count} figuras";
        });
        Time("Abrir una sección", 6000, () =>
        {
            Invoke("SelectProjectView", tower.Views.First(view => view.Kind == ProjectViewKind.Section).Id);
            _pump();
            return $"{((Canvas)_window.FindName("SectionCanvas")).Children.Count} figuras";
        });
        Time("Abrir la primera lámina", 6000, () =>
        {
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 3;
            _pump();
            Invoke("RenderSheet");
            return $"{tower.Sheets.Count} láminas en el proyecto";
        });
        Time("Exportar todas las láminas a PDF", 30000, () =>
        {
            var bytes = Guanacaste3D.Core.Interop.PdfPlanExporter.ExportDocumentation(session.Document, SheetPaperSize.A1, DateTimeOffset.Now);
            return $"{bytes.Length / 1024} KB";
        });
        Time("Presupuesto de obra", 5000, () =>
        {
            var budget = ConstructionBudget.Build(session.Document, session.GetBudgetSettings());
            return $"₡{budget.Total:#,0}";
        });
        Time("Detectar habitaciones de un piso tipo", 5000, () => $"{RoomPolygonDetection.Detect(session.Document, levels[3].Id).Count} habitaciones");
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
        _pump();
        Time("Dibujar un muro y deshacerlo", 1500, () =>
        {
            typeof(MainWindow).GetField("_activeLevelId", Private)!.SetValue(_window, levels[3].Id);
            var before = session.Document.Walls.Count;
            session.AddWall(levels[3].Id, new Point2(2, 2), new Point2(5, 2));
            Invoke("RefreshAll");
            _pump();
            App("Undo");
            return session.Document.Walls.Count == before ? "" : "deshacer no quitó el muro";
        });
        var path = Path.Combine(Path.GetTempPath(), "torre-" + Guid.NewGuid().ToString("N") + ProjectFile.NativeExtension);
        Time("Guardar el proyecto", 10000, () =>
        {
            typeof(MainWindow).GetField("_filePath", Private)!.SetValue(_window, path);
            Invoke("SaveProject", false);
            return $"{new FileInfo(path).Length / 1024} KB";
        });
        Time("Abrir el proyecto guardado", 15000, () =>
        {
            Invoke("OpenProjectPath", path);
            for (var wait = 0; wait < 100 && Session.Document.Walls.Count != tower.Walls.Count; wait++) _pump();
            return Session.Document.Walls.Count == tower.Walls.Count ? "" : "el proyecto reabierto no coincide";
        });
        File.Delete(path);

        var report = new StringBuilder("# Rendimiento con un edificio de 10 pisos\n\n");
        report.AppendLine("| Operación | Tiempo (ms) | Límite (ms) | Resultado | Nota |\n|---|---|---|---|---|");
        foreach (var timing in timings)
            report.AppendLine($"| {timing.Operation} | {timing.Milliseconds:0} | {timing.Limit:0} | {(timing.Note.StartsWith("ERROR") ? "**error**" : timing.Milliseconds <= timing.Limit ? "bien" : "**lento**")} | {timing.Note.Replace("|", "/")} |");
        Directory.CreateDirectory("artifacts/interaction-evidence");
        File.WriteAllText("artifacts/interaction-evidence/rendimiento-edificio.md", report.ToString(), new UTF8Encoding(false));
        var bad = timings.Where(timing => timing.Note.StartsWith("ERROR") || timing.Milliseconds > timing.Limit).ToList();
        check(bad.Count == 0, "Building: every operation on the ten-storey tower stays within its time limit" +
            (bad.Count == 0 ? "" : " — " + string.Join(", ", bad.Select(timing => $"{timing.Operation} {timing.Milliseconds:0} ms"))));
    }
}
