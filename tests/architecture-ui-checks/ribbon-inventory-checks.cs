using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// Presses every ribbon button of every tab, each on a fresh copy of the two-storey sample house, and records what it
/// does: still only "planned", changes the model, opens a window, arms a tool, or fails. The result is written to
/// artifacts/interaction-evidence/ribbon-inventory.md so the work that is left can be counted instead of guessed.
/// It is informational (it never fails the suite); set G3D_RIBBON_INVENTORY=1 to run it.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    private sealed record InventoryRow(string Tab, string Command, string Label, string Result, string Status, string Dialogs);

    public static void RunRibbonInventory(MainWindow window, Action pump)
    {
        Initialise(window, pump);
        Evidence = "artifacts/interaction-evidence";
        Directory.CreateDirectory(Evidence);
        var baseline = TwoStoreyHouse.Create();
        var rows = new List<InventoryRow>();
        var watchdog = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        watchdog.Tick += (_, _) => CloseUnexpectedWindows();
        watchdog.Start();
        typeof(MainWindow).GetField("_filePath", Private)!.SetValue(_window, null);
        try
        {
            var professional = typeof(MainWindow).GetMethod("ProfessionalRibbon_CommandInvoked", Private)!;
            foreach (var workspace in Enum.GetValues<RibbonWorkspace>())
            foreach (var command in RibbonCatalog.GetGroups(workspace).SelectMany(group => group.Commands).DistinctBy(command => command.CommandId))
            {
                var label = Application.Current.TryFindResource(command.LabelResourceKey) as string ?? command.LabelResourceKey;
                rows.Add(Press(Application.Current.TryFindResource("Ribbon." + workspace) as string ?? workspace.ToString(), command.CommandId, label, baseline,
                    () => professional.Invoke(_window, [null, new ProfessionalRibbonCommandEventArgs(workspace, command.CommandId, label)])));
            }
            var architecture = typeof(MainWindow).GetMethod("ArchitectureRibbon_CommandInvoked", Private)!;
            foreach (var command in Enum.GetValues<ArchitectureCommand>())
                rows.Add(Press("Arquitectura", command.ToString(), command.ToString(), baseline,
                    () => architecture.Invoke(_window, [null, new ArchitectureCommandEventArgs(command)])));
        }
        finally
        {
            watchdog.Stop();
            WriteInventory(rows);
        }
        foreach (var group in rows.GroupBy(row => row.Result.Split(':')[0]).OrderByDescending(group => group.Count()))
            Console.WriteLine($"INVENTARIO {group.Key}: {group.Count()}");
    }

    private static InventoryRow Press(string tab, string id, string label, BuildingDocument baseline, Action press)
    {
        var session = new DocumentSession(baseline.Clone());
        typeof(MainWindow).GetField("_session", Private)!.SetValue(_window, session);
        typeof(MainWindow).GetField("_activeLevelId", Private)!.SetValue(_window, session.Document.Levels.OrderBy(level => level.Elevation).First().Id);
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
        Invoke("RefreshAll");
        ((TextBlock)_window.FindName("StatusText")).Text = "";
        Dialogs.Clear();
        _lastMenu = null;
        var before = Fingerprint(session.Document);
        string result;
        try
        {
            press();
            _pump();
            var after = Fingerprint(Session.Document);
            var menu = _lastMenu is { IsOpen: true } ? $"menú de {_lastMenu.Items.Count} opciones" : null;
            result = Status.Contains("se incorporará", StringComparison.OrdinalIgnoreCase) ? "planificado"
                : !ReferenceEquals(Session, session) ? "cambia de proyecto"
                : after != before ? "cambia el modelo: " + Difference(before, after)
                : Dialogs.Count > 0 ? "abre ventana"
                : menu is not null ? "abre " + menu
                : Status.Length > 0 ? "responde"
                : "sin respuesta visible";
        }
        catch (Exception exception)
        {
            var inner = exception is TargetInvocationException { InnerException: { } cause } ? cause : exception;
            result = $"error: {inner.GetType().Name}: {inner.Message}";
        }
        var row = new InventoryRow(tab, id, label, result, Status, string.Join("; ", Dialogs));
        try
        {
            Invoke("CancelStructuralTool");
            Escape();
            if (_lastMenu is not null) _lastMenu.IsOpen = false;
            foreach (var extra in Application.Current.Windows.OfType<Window>().Where(window => window != _window && window.IsVisible).ToList()) extra.Close();
        }
        catch (Exception) { /* A failed clean-up must not hide the next button's result. */ }
        return row;
    }

    /// <summary>How many items every list of the document holds, so any added or removed element shows.</summary>
    private static string Fingerprint(BuildingDocument document) => string.Join(" ", typeof(BuildingDocument)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => typeof(ICollection).IsAssignableFrom(property.PropertyType))
        .Select(property => $"{property.Name}={((ICollection?)property.GetValue(document))?.Count ?? 0}"));

    private static string Difference(string before, string after)
    {
        var old = before.Split(' ').ToDictionary(part => part.Split('=')[0], part => int.Parse(part.Split('=')[1]));
        var changes = after.Split(' ').Select(part => (Name: part.Split('=')[0], Count: int.Parse(part.Split('=')[1])))
            .Where(item => old[item.Name] != item.Count).Select(item => $"{item.Name} {item.Count - old[item.Name]:+#;-#}");
        return string.Join(", ", changes);
    }

    private static void WriteInventory(IReadOnlyList<InventoryRow> rows)
    {
        var text = new StringBuilder("# Inventario de la cinta — qué hace cada botón hoy\n\n");
        text.AppendLine("Generado por `tests/architecture-ui-checks/ribbon-inventory-checks.cs` (G3D_RIBBON_INVENTORY=1). Cada botón se pulsa");
        text.AppendLine("sobre una copia nueva de la casa de dos plantas de ejemplo, sin nada seleccionado.\n");
        text.AppendLine("| Resultado | Botones |");
        text.AppendLine("|---|---|");
        foreach (var group in rows.GroupBy(row => row.Result.Split(':')[0]).OrderByDescending(group => group.Count()))
            text.AppendLine($"| {group.Key} | {group.Count()} |");
        foreach (var tab in rows.GroupBy(row => row.Tab))
        {
            text.AppendLine($"\n## {tab.Key}\n");
            text.AppendLine("| Botón | Id | Resultado | Barra de estado | Ventanas |");
            text.AppendLine("|---|---|---|---|---|");
            foreach (var row in tab)
                text.AppendLine($"| {Cell(row.Label)} | `{row.Command}` | {Cell(row.Result)} | {Cell(row.Status)} | {Cell(row.Dialogs)} |");
        }
        File.WriteAllText($"{Evidence}/ribbon-inventory.md", text.ToString(), new UTF8Encoding(false));
    }

    private static string Cell(string text) => text.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
}
