using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// Plans, a sheet and the PDF explored from an empty project: a small house is drawn, annotated and laid out on a sheet
/// as a person would, with a picture of the window and the sheet after each step. Opt-in: G3D_FRESH=sheet.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    public static void RunFreshSheet(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("Fresh sheet", "artifacts/interaction-evidence/fresh-sheet", FreshSheetSteps(), check,
            "Láminas desde un proyecto vacío — resultado de la prueba automática");
    }

    private static IEnumerable<(string Name, Func<string> Action)> FreshSheetSteps()
    {
        yield return ("L1 Casa de 8 × 6 m con puerta, ventana y habitaciones", () =>
        {
            App("NewProject");
            Zoom(45, 4, 3);
            Arch(ArchitectureCommand.Wall);
            foreach (var (x, y) in new[] { (0.0, 0.0), (8.0, 0.0), (8.0, 6.0), (0.0, 6.0), (0.0, 0.0) }) Click(x, y);
            Escape();
            Arch(ArchitectureCommand.Wall);
            Click(4, 0);
            Click(4, 6);
            Escape();
            PlaceOpening(ArchitectureCommand.Door, 2, 0, OpeningKind.Door);
            PlaceOpening(ArchitectureCommand.Window, 6, 6, OpeningKind.Window);
            PlaceOpening(ArchitectureCommand.Door, 4, 3, OpeningKind.Door);
            Arch(ArchitectureCommand.RoomAutomatic);
            Expect(Doc.Rooms.Count == 2, $"hay {Doc.Rooms.Count} habitaciones (estado: {Status})");
            return "2 habitaciones";
        });
        yield return ("L2 Cotas y etiquetas desde la pestaña Anotar", () =>
        {
            App("AlignedDimension");
            App("TagAll");
            SaveWindow($"{Evidence}/L2-ventana.png");
            Expect(Doc.Dimensions.Count > 0, $"no hay cotas (estado: {Status})");
            return $"{Doc.Dimensions.Count} cotas";
        });
        yield return ("L3 Lámina nueva con la planta y la vista Norte", () =>
        {
            App("Sheet");
            var sheet = Doc.Sheets[^1];
            ExpectDialog("SheetEditorWindow", dialog =>
            {
                ((TextBox)dialog.GetType().GetField("_name", Private)!.GetValue(dialog)!).Text = "Planta y fachada";
                ((TextBox)dialog.GetType().GetField("_number", Private)!.GetValue(dialog)!).Text = "A-101";
                var picker = (ComboBox)dialog.GetType().GetField("_views", Private)!.GetValue(dialog)!;
                var labels = picker.Items.Cast<object>().Select(item => item.GetType().GetProperty("Label")?.GetValue(item)?.ToString() ?? "").ToList();
                foreach (var wanted in new[] { "Nivel 1", "Vista Norte" })
                {
                    var choice = picker.Items.Cast<object>().FirstOrDefault(item =>
                        item.GetType().GetProperty("Label")?.GetValue(item)?.ToString()?.Contains(wanted, StringComparison.OrdinalIgnoreCase) == true);
                    Expect(choice is not null, $"el selector de vistas no ofrece «{wanted}»: {string.Join(", ", labels)}");
                    picker.SelectedItem = choice;
                    Call(dialog, "AddView_Click");
                }
                Call(dialog, "Apply_Click");
            });
            typeof(MainWindow).GetField("_selectedSheetId", Private)!.SetValue(_window, (Guid?)sheet.Id);
            Invoke("EditSheet_Click", _window, new RoutedEventArgs());
            var updated = Doc.Sheets.Single(candidate => candidate.Id == sheet.Id);
            Expect(updated.Viewports.Count == 2 && updated.Viewports.Select(viewport => viewport.ViewId).Distinct().Count() == 2, $"la lámina tiene {updated.Viewports.Count} vistas ({updated.Viewports.Select(viewport => viewport.ViewId).Distinct().Count()} distintas), se esperaban la planta y la vista Norte, una vez cada una");
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 3;
            _pump(); _pump();
            SaveWindow($"{Evidence}/L3-lamina.png");
            return $"lámina {updated.Number} con {updated.Viewports.Count} vistas";
        });
        yield return ("L4 Exportar a PDF", () =>
        {
            var bytes = Guanacaste3D.Core.Interop.PdfPlanExporter.ExportDocumentation(Doc, SheetPaperSize.A3, DateTimeOffset.Now);
            Expect(bytes.Length > 2000, $"el PDF pesa {bytes.Length} bytes");
            var path = Path.Combine("artifacts/interaction-evidence/fresh-sheet", "lamina.pdf");
            File.WriteAllBytes(path, bytes);
            return $"PDF de {bytes.Length / 1024} KB";
        });
    }
}
