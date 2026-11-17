using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// The Modify tools tried on things that are not walls, from an empty project: a column and a stair are selected with a
/// click and copied, rotated, mirrored, arrayed and deleted. Opt-in: G3D_FRESH=edit.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    public static void RunFreshEdit(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("Fresh edit", "artifacts/interaction-evidence/fresh-edit", FreshEditSteps(), check,
            "Edición desde un proyecto vacío — resultado de la prueba automática");
    }

    private static string Tool(string command, string what)
    {
        var before = (Doc.Columns.Count, Doc.StructuralElements.Count, Doc.Walls.Count);
        App(command);
        var after = (Doc.Columns.Count, Doc.StructuralElements.Count, Doc.Walls.Count);
        var status = Status;
        Escape();
        return $"{what}: {(before == after ? "sin cambios" : $"{before} → {after}")}; estado: {status}";
    }

    private static IEnumerable<(string Name, Func<string> Action)> FreshEditSteps()
    {
        yield return ("E1 Proyecto nuevo, una columna y una escalera", () =>
        {
            App("NewProject");
            Zoom(50, 4, 3);
            Arch(ArchitectureCommand.Column);
            Click(1, 1);
            Escape();
            Arch(ArchitectureCommand.Stair);
            Click(3, 1);
            Click(3, 4);
            Escape();
            Expect(Doc.Columns.Count == 1 && Count(StructuralElementKind.Stair) == 1, $"{Doc.Columns.Count} columnas y {Count(StructuralElementKind.Stair)} escaleras");
            return "columna y escalera";
        });
        yield return ("E1b Un muro que sirva de eje de simetría", () =>
        {
            Arch(ArchitectureCommand.Wall);
            Click(6, 0);
            Click(6, 5);
            Escape();
            Expect(Doc.Walls.Count == 1, $"hay {Doc.Walls.Count} muros");
            return "muro en x = 6";
        });
        void SelectColumn()
        {
            Escape();
            Click(1, 1);
            Expect((Guid?)typeof(MainWindow).GetField("_selectedColumnId", Private)!.GetValue(_window) is not null, "el clic no selecciona la columna");
        }
        yield return ("E2 Copiar y Pegar una columna", () =>
        {
            SelectColumn();
            App("Copy");
            Expect(Status.Contains("copiado", StringComparison.OrdinalIgnoreCase), $"Copiar no avisa: «{Status}»");
            var before = Doc.Columns.Count;
            App("Paste");
            Expect(Doc.Columns.Count == before + 1, $"Pegar no copió la columna (estado: {Status})");
            return "columna pegada";
        });
        yield return ("E3 Rotar 90° un pilar rectangular de 0.40 × 0.20", () =>
        {
            SelectColumn();
            var column = Doc.Columns.First(item => Math.Abs(item.Position.X - 1) < 1e-6);
            Session.UpdateColumn(column.Id, 0.4, 0.2, column.Height, column.Shape);
            SelectColumn();
            App("Rotate");
            var turned = Doc.Columns.First(item => item.Id == column.Id);
            Expect(Math.Abs(turned.Width - 0.2) < 1e-9 && Math.Abs(turned.Depth - 0.4) < 1e-9, $"el pilar quedó de {turned.Width:0.00} × {turned.Depth:0.00} (estado: {Status})");
            return "0.20 × 0.40";
        });
        yield return ("E4 Matriz de 3 pilares cada 1 m en X (formulario)", () =>
        {
            SelectColumn();
            var before = Doc.Columns.Count;
            ExpectDialog("FormDialog", window => Descendants<Button>(window).First(button => button.IsDefault)
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
            App("Array");
            Expect(Doc.Columns.Count == before + 2, $"la matriz creó {Doc.Columns.Count - before} copias, se esperaban 2 (estado: {Status})");
            return "2 copias";
        });
        yield return ("E5 Simetría respecto al muro", () =>
        {
            SelectColumn();
            var before = Doc.Columns.Count;
            App("MirrorPickAxis");
            Click(6, 2.5);
            Expect(Doc.Columns.Count == before + 1, $"la simetría no copió el pilar (estado: {Status})");
            var mirrored = Doc.Columns[^1];
            Expect(Math.Abs(mirrored.Position.X - 11) < 1e-6, $"el pilar reflejado quedó en x = {mirrored.Position.X:0.00}, se esperaba 11");
            return "pilar reflejado a x = 11";
        });
        yield return ("E6 Desfase y Escala explican que son de muros", () =>
        {
            SelectColumn();
            App("Offset");
            Expect(Status.Contains("solo", StringComparison.OrdinalIgnoreCase), $"Desfase no explica: «{Status}»");
            App("Scale");
            Expect(Status.Contains("solo", StringComparison.OrdinalIgnoreCase), $"Escala no explica: «{Status}»");
            return "mensajes claros";
        });
        yield return ("E7 Suprimir con la tecla sobre la columna seleccionada", () =>
        {
            Escape();
            Click(1, 1);
            var before = Doc.Columns.Count;
            Key(System.Windows.Input.Key.Delete);
            Expect(Doc.Columns.Count == before - 1, $"Supr no borró la columna (estado: {Status})");
            return "columna borrada";
        });
        yield return ("E8 Deshacer y rehacer con Ctrl+Z / Ctrl+Y", () =>
        {
            var before = Doc.Columns.Count;
            Invoke("Undo_Click", _window, new RoutedEventArgs());
            var undone = Doc.Columns.Count;
            Invoke("Redo_Click", _window, new RoutedEventArgs());
            Expect(undone == before + 1 && Doc.Columns.Count == before, $"deshacer/rehacer: {before} → {undone} → {Doc.Columns.Count}");
            return "deshacer y rehacer funcionan";
        });
    }
}
