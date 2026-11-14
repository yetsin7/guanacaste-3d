using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;

/// <summary>
/// Phase 11 of the cobogó house: the dining furniture is made in the program's own family editor — a parametric table
/// with two types and a chair — saved as .g3df, loaded into the project and placed round the dining table with clicks.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    private static IEnumerable<(string Name, Func<string> Action)> CobogoFamilySteps()
    {
        yield return ("11.1 Mesa de comedor paramétrica en el Editor de familias (4 parámetros, tablero y 4 patas)", () =>
        {
            var editor = OpenEditor();
            SetEditorText(editor, "_name", "Mesa de comedor");
            foreach (var (name, value) in new[] { ("Ancho", "1.6"), ("Fondo", "0.9"), ("Alto", "0.75"), ("Pata", "0.05") }) AddEditorParameter(editor, name, value);
            AddEditorBox(editor, "0", "0", "Ancho", "Fondo", "Alto - 0.03", "0.03");
            foreach (var (x, y) in new[] { ("0.03", "0.03"), ("Ancho - Pata - 0.03", "0.03"), ("0.03", "Fondo - Pata - 0.03"), ("Ancho - Pata - 0.03", "Fondo - Pata - 0.03") })
                AddEditorBox(editor, x, y, "Pata", "Pata", "0", "Alto - 0.03");
            var family = (FamilyDocument)editor.GetType().GetProperty("Family")!.GetValue(editor)!;
            Expect(family.Name == "Mesa de comedor" && family.Parameters.Count == 4 && family.Operations.Count == 5,
                $"la familia quedó «{family.Name}» con {family.Parameters.Count} parámetros y {family.Operations.Count} sólidos");
            var size = EditorSize(editor);
            Expect(Math.Abs(size.X - 1.6) < 1e-3 && Math.Abs(size.Y - 0.9) < 1e-3 && Math.Abs(size.Z - 0.75) < 1e-3, $"la mesa mide {size}");
            return $"mesa de {size.X:0.00} × {size.Y:0.00} × {size.Z:0.00} m";
        });
        yield return ("11.2 Segundo tipo de mesa 120 × 80 cambiando solo dos valores", () =>
        {
            var editor = OpenEditor();
            ExpectDialog("Window", dialog => Answer(dialog, "120 × 80"));
            PressEditorButton(editor, "FamilyEditor.NewType");
            SetEditorParameter(editor, "Ancho", "1.2");
            SetEditorParameter(editor, "Fondo", "0.8");
            var size = EditorSize(editor);
            Expect(Math.Abs(size.X - 1.2) < 1e-3 && Math.Abs(size.Y - 0.8) < 1e-3, $"el tipo nuevo mide {size}");
            ((ComboBox)Field(editor, "_types")).SelectedIndex = 0;
            _pump();
            Expect(Math.Abs(EditorSize(editor).X - 1.6) < 1e-3, "el primer tipo cambió al editar el segundo");
            return "tipos 160 × 90 y 120 × 80";
        });
        yield return ("11.3 Guardar la mesa como .g3df y volver a abrirla", () =>
        {
            var editor = OpenEditor();
            var path = Path.Combine(Path.GetTempPath(), "mesa-" + Guid.NewGuid().ToString("N") + NativeDocumentStore.FamilyExtension);
            editor.GetType().GetMethod("SaveTo")!.Invoke(editor, [path]);
            var reopened = NativeDocumentStore.LoadFamily(path);
            File.Delete(path);
            Expect(reopened.Types.Count == 2 && reopened.Operations.Count == 5 && reopened.Mesh is not null,
                $"al reabrir: {reopened.Types.Count} tipos, {reopened.Operations.Count} sólidos");
            return "familia guardada y reabierta con sus 2 tipos";
        });
        yield return ("11.4 Colocar la mesa en el comedor con un clic", () =>
        {
            ActiveLevel(Ground);
            var editor = OpenEditor();
            PressEditorButton(editor, "FamilyEditor.LoadIntoProject");
            var before = Doc.StructuralElements.Count(element => element.FamilyId is not null);
            Click(4.2, 9.6);
            Escape();
            var placed = Doc.StructuralElements.Where(element => element.FamilyId is not null).ToList();
            Expect(placed.Count == before + 1, $"no se colocó la mesa (estado: {Status})");
            var family = Doc.ComponentFamilies.Single(candidate => candidate.Id == placed[^1].FamilyId);
            return $"«{family.Name}» en ({placed[^1].Start.X:0.0}, {placed[^1].Start.Y:0.0})";
        });
        yield return ("11.5 Silla desde cero y cuatro sillas alrededor de la mesa (Espacio gira)", () =>
        {
            var editor = OpenEditor();
            PressEditorButton(editor, "FamilyEditor.New");
            SetEditorText(editor, "_name", "Silla de comedor");
            foreach (var (name, value) in new[] { ("Ancho", "0.45"), ("Fondo", "0.45"), ("Asiento", "0.45"), ("Respaldo", "0.9") }) AddEditorParameter(editor, name, value);
            AddEditorBox(editor, "0", "0", "Ancho", "Fondo", "Asiento - 0.04", "0.04");
            AddEditorBox(editor, "0", "Fondo - 0.04", "Ancho", "0.04", "Asiento", "Respaldo - Asiento");
            foreach (var (x, y) in new[] { ("0", "0"), ("Ancho - 0.04", "0"), ("0", "Fondo - 0.04"), ("Ancho - 0.04", "Fondo - 0.04") })
                AddEditorBox(editor, x, y, "0.04", "0.04", "0", "Asiento - 0.04");
            var size = EditorSize(editor);
            Expect(Math.Abs(size.Z - 0.9) < 1e-3, $"la silla mide {size}");
            PressEditorButton(editor, "FamilyEditor.LoadIntoProject");
            var before = Doc.StructuralElements.Count(element => element.FamilyId is not null);
            Click(3.8, 10.4);
            Click(4.6, 10.4);
            Key(System.Windows.Input.Key.Space);
            Key(System.Windows.Input.Key.Space);
            Click(3.8, 8.8);
            Click(4.6, 8.8);
            Escape();
            var chairs = Doc.StructuralElements.Where(element => element.FamilyId is not null).Skip(before).ToList();
            Expect(chairs.Count == 4, $"se colocaron {chairs.Count} de 4 sillas (estado: {Status})");
            Expect(chairs.Select(chair => Math.Round(chair.Rotation, 3)).Distinct().Count() == 2, "las sillas del otro lado no se giraron con Espacio");
            editor.Close();
            return "4 sillas, dos giradas 180°";
        });
        yield return ("11.6 Los muebles propios aparecen en la vista 3D", () =>
        {
            var families = Doc.ComponentFamilies.Count(family => family.Metadata.ContainsKey("SourceFamily"));
            var mesh = Guanacaste3D.Core.Geometry.BuildingMesher.Build(Doc);
            var placed = Doc.StructuralElements.Where(element => element.FamilyId is not null).Select(element => element.Id).ToHashSet();
            var shown = mesh.ElementRanges.Count(range => placed.Contains(range.ElementId));
            Expect(families == 2 && shown >= 5, $"{families} familias propias cargadas, {shown} de 5 muebles en el modelo 3D");
            return $"{shown} muebles propios en 3D";
        });
    }

    private static Window OpenEditor()
    {
        KeepOpen.Add("FamilyEditorWindow");
        var editor = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.GetType().Name == "FamilyEditorWindow");
        if (editor is null)
        {
            App("FamilyEditor");
            editor = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.GetType().Name == "FamilyEditorWindow");
        }
        Expect(editor is not null, "el botón Editor de familias no abrió el editor");
        return editor!;
    }

    private static object Field(Window editor, string name) => editor.GetType().GetField(name, Private)!.GetValue(editor)!;

    private static void SetEditorText(Window editor, string field, string text)
    {
        var box = (TextBox)Field(editor, field);
        box.Text = text;
        box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        _pump();
    }

    private static void PressEditorButton(Window editor, string key)
    {
        var label = Application.Current.TryFindResource(key) as string ?? key;
        var button = Descendants<Button>(editor).FirstOrDefault(candidate => candidate.Content as string == label);
        Expect(button is not null, $"el editor no tiene el botón «{label}»");
        button!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        _pump();
    }

    private static void AddEditorParameter(Window editor, string name, string value)
    {
        ExpectDialog("Window", dialog => Answer(dialog, name));
        ExpectDialog("Window", dialog => Answer(dialog, value));
        PressEditorButton(editor, "FamilyEditor.AddParameter");
    }

    private static void SetEditorParameter(Window editor, string name, string value)
    {
        var box = Descendants<TextBox>((DependencyObject)Field(editor, "_parameters")).FirstOrDefault(candidate => Equals(candidate.Tag, name));
        Expect(box is not null, $"no está el parámetro «{name}»");
        box!.Text = value;
        box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        _pump();
    }

    /// <summary>Adds a box with the button and types its six fields, as the rows are rebuilt after each change.</summary>
    private static void AddEditorBox(Window editor, params string[] values)
    {
        PressEditorButton(editor, "FamilyEditor.AddBox");
        string[] fields = [FamilyGeometry.ProfileXKey, FamilyGeometry.ProfileYKey, FamilyGeometry.ProfileWidthKey, FamilyGeometry.ProfileDepthKey,
            FamilyGeometry.OffsetKey, FamilyGeometry.DepthKey];
        for (var index = 0; index < fields.Length; index++)
        {
            var rows = ((Panel)Field(editor, "_solids")).Children.OfType<UniformGrid>().Skip(1).ToList();
            var box = rows[^1].Children.OfType<TextBox>().Single(candidate => Equals(candidate.Tag, fields[index]));
            box.Text = values[index];
            box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            _pump();
        }
        var family = (FamilyDocument)editor.GetType().GetProperty("Family")!.GetValue(editor)!;
        var last = family.Operations[^1].Expressions;
        Expect(fields.Select((field, index) => last.GetValueOrDefault(field) == values[index]).All(ok => ok),
            "la caja no aceptó sus valores: " + ((TextBlock)Field(editor, "_status")).Text);
    }

    private static System.Numerics.Vector3 EditorSize(Window editor)
    {
        var family = (FamilyDocument)editor.GetType().GetProperty("Family")!.GetValue(editor)!;
        var type = (Guid)editor.GetType().GetProperty("CurrentType")!.GetValue(editor)!;
        var mesh = FamilyGeometry.Tessellate(family, type);
        return new(mesh.Positions.Max(p => p.X) - mesh.Positions.Min(p => p.X), mesh.Positions.Max(p => p.Y) - mesh.Positions.Min(p => p.Y),
            mesh.Positions.Max(p => p.Z) - mesh.Positions.Min(p => p.Z));
    }
}
