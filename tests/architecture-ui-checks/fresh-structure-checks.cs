using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// A feature explored from a brand-new empty project, one small scenario at a time, with a picture of the whole window
/// after every step. Unlike the long cobogó house this does not reuse earlier work, so a gap the long script hid
/// (because a previous step had already made the element it needs) shows up here. Opt-in: G3D_FRESH=structure.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    public static void RunFreshStructure(MainWindow window, Action<bool, string> check, Action pump)
    {
        Initialise(window, pump);
        RunScenario("Fresh structure", "artifacts/interaction-evidence/fresh-structure", FreshStructureSteps(), check,
            "Estructura desde un proyecto vacío — resultado de la prueba automática");
    }

    private static IEnumerable<(string Name, Func<string> Action)> FreshStructureSteps()
    {
        yield return ("F1 Proyecto nuevo y vacío", () =>
        {
            App("NewProject");
            Expect(Doc.Walls.Count == 0 && Doc.Columns.Count == 0 && Doc.StructuralElements.Count == 0, "el proyecto nuevo no está vacío");
            Zoom(60, 4, 3);
            SaveWindow($"{Evidence}/F1-ventana.png");
            return "vacío";
        });
        yield return ("F2 Columna y viga con los botones de Estructura, sin tener nada dibujado antes", () =>
        {
            var columns = Doc.Columns.Count;
            Arch(ArchitectureCommand.Column);
            Click(1, 1);
            Click(5, 1);
            Escape();
            Expect(Doc.Columns.Count == columns + 2, $"hay {Doc.Columns.Count - columns} de 2 columnas (estado: {Status})");
            App("Beam");
            Click(1, 1);
            Click(5, 1);
            Escape();
            Expect(Count(StructuralElementKind.Beam) == 1, $"no se creó la viga (estado: {Status})");
            SaveWindow($"{Evidence}/F2-ventana.png");
            return "2 columnas y 1 viga";
        });
        yield return ("F3 Seleccionar la viga con un clic: Propiedades ofrece su sección", () =>
        {
            Escape();
            Click(3, 1);
            var beam = Doc.StructuralElements.Single(element => element.Kind == StructuralElementKind.Beam);
            var selected = (Guid?)typeof(MainWindow).GetField("_selectedStructuralId", Private)!.GetValue(_window);
            Expect(selected == beam.Id, $"el clic sobre la viga no la selecciona (estado: {Status})");
            var column = (Guid?)typeof(MainWindow).GetField("_selectedColumnId", Private)!.GetValue(_window);
            Expect(column is null, "al seleccionar la viga sigue seleccionada una columna");
            var inputs = (System.Collections.IDictionary)typeof(MainWindow).GetField("_instancePropertyInputs", Private)!.GetValue(_window)!;
            Expect(inputs.Contains("Width") && inputs.Contains("Height"), "Propiedades no deja editar la sección de la viga");
            SaveWindow($"{Evidence}/F3-ventana.png");
            // A person widens the beam to 0.25 × 0.45 and presses Apply.
            ((TextBox)inputs["Width"]!).Text = "0.25";
            ((TextBox)inputs["Height"]!).Text = "0.45";
            Invoke("ApplyInstanceProperties_Click", _window, new RoutedEventArgs());
            var changed = Doc.StructuralElements.Single(element => element.Id == beam.Id);
            Expect(Math.Abs(changed.Width - 0.25) < 1e-9 && Math.Abs(changed.Height - 0.45) < 1e-9,
                $"la viga quedó de {changed.Width:0.00} × {changed.Height:0.00} (estado: {Status})");
            return "viga de 0.25 × 0.45";
        });
        yield return ("F4 Armadura de la viga: 4 barras #5 en la cara inferior desde el diálogo", () =>
        {
            var before = Count(StructuralElementKind.Rebar);
            ExpectDialog("RebarDialog", window =>
            {
                SaveWindow(window, $"{Evidence}/F4-armadura.png");
                var boxes = Descendants<TextBox>(window).ToList();
                boxes[0].Text = "4";
                var diameter = Descendants<ComboBox>(window).First();
                diameter.SelectedIndex = 2;
                Descendants<Button>(window).First(button => button.IsDefault)
                    .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            });
            App("Rebar");
            var bars = Doc.StructuralElements.Where(element => element.Kind == StructuralElementKind.Rebar).ToList();
            Expect(bars.Count == before + 4, $"se crearon {bars.Count - before} de 4 barras (estado: {Status})");
            Expect(bars.All(bar => Math.Abs(bar.Width - 0.016) < 1e-9), "las barras no son del diámetro elegido (#5 = 16 mm)");
            SaveWindow($"{Evidence}/F4-ventana.png");
            return "4 barras #5 abajo";
        });
        yield return ("F5 Armadura sin nada seleccionado: dice qué hacer", () =>
        {
            Escape();
            Click(10, 10);
            App("Rebar");
            Expect(Status.Contains("Seleccione", StringComparison.Ordinal), $"el botón no explica qué falta: «{Status}»");
            return Status;
        });
        yield return ("F6 Barras en un área: losa de 4 × 3 m y formulario con sus esquinas ya puestas", () =>
        {
            Escape();
            Arch(ArchitectureCommand.Floor);
            PickMenu(0);
            foreach (var (x, y) in new[] { (7.0, 0.0), (11.0, 0.0), (11.0, 3.0), (7.0, 3.0), (7.0, 0.0) }) Click(x, y);
            var before = Count(StructuralElementKind.Rebar);
            ExpectDialog("FormDialog", window =>
            {
                var boxes = Descendants<TextBox>(window).ToList();
                Expect(boxes[0].Text == "7" && boxes[2].Text == "11", "el formulario no toma el contorno de la losa seleccionada");
                SaveWindow(window, $"{Evidence}/F6-barras-area.png");
                Descendants<Button>(window).First(button => button.IsDefault)
                    .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            });
            App("RebarArea");
            var made = Count(StructuralElementKind.Rebar) - before;
            Expect(made >= 10, $"se crearon {made} barras (estado: {Status})");
            return $"{made} barras cada 20 cm";
        });
        yield return ("F7 Estribo por recorrido con el formulario", () =>
        {
            var before = Count(StructuralElementKind.Rebar);
            ExpectDialog("FormDialog", window => Descendants<Button>(window).First(button => button.IsDefault)
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
            App("RebarPath");
            var made = Count(StructuralElementKind.Rebar) - before;
            Expect(made >= 3, $"el recorrido creó {made} tramos (estado: {Status})");
            return $"{made} tramos";
        });
    }

    private static void SaveWindow(Window window, string path) => Save((FrameworkElement)window.Content, path);
}
