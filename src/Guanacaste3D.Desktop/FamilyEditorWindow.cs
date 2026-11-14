using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop;

/// <summary>
/// The family editor: furniture and other components made in the program. A family has length parameters, types that
/// give them values, and boxes whose corner, size, start and height are numbers, parameter names or formulas
/// ("Alto - 0.03"). The preview regenerates on every change; a family is saved as .g3df and loaded into the project
/// as the current type, ready to place with clicks.
/// </summary>
internal sealed class FamilyEditorWindow : Window
{
    private readonly Action<FamilyDocument, Guid> _loadAndPlace;
    private FamilyDocument _family;
    private Guid _typeId;
    private readonly TextBox _name = new() { Width = 220, Margin = new Thickness(4, 0, 12, 0) };
    private readonly ComboBox _category = new() { Width = 170, Margin = new Thickness(4, 0, 12, 0) };
    private readonly ComboBox _types = new() { Width = 150, Margin = new Thickness(4, 0, 6, 0), DisplayMemberPath = "Name" };
    private readonly StackPanel _parameters = new();
    private readonly StackPanel _solids = new();
    private readonly Canvas _plan = new() { Background = Brushes.White, ClipToBounds = true };
    private readonly Canvas _iso = new() { Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4, 8, 4), TextWrapping = TextWrapping.Wrap };
    private bool _loading;

    private static readonly string[] BoxFields = [FamilyGeometry.ProfileXKey, FamilyGeometry.ProfileYKey, FamilyGeometry.ProfileWidthKey,
        FamilyGeometry.ProfileDepthKey, FamilyGeometry.OffsetKey, FamilyGeometry.DepthKey];

    public FamilyEditorWindow(FamilyDocument? family, Action<FamilyDocument, Guid> loadAndPlace)
    {
        _loadAndPlace = loadAndPlace;
        _family = family?.Clone() ?? FamilyAuthoring.Create(Text("FamilyEditor.NewName"), FamilyCategory.Furniture);
        _typeId = _family.Types[0].Id;
        Title = Text("FamilyEditor.Title");
        Width = 1240;
        Height = 760;
        MinWidth = 900;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        foreach (var category in new[] { FamilyCategory.Furniture, FamilyCategory.Casework, FamilyCategory.PlumbingFixtures,
                     FamilyCategory.LightingFixtures, FamilyCategory.GenericModels, FamilyCategory.DecorativeComponents })
            _category.Items.Add(new ComboBoxItem { Content = Text("FamilyCategory." + category), Tag = category });
        _name.LostFocus += (_, _) => Apply(family => family.Name = string.IsNullOrWhiteSpace(_name.Text) ? family.Name : _name.Text.Trim());
        _category.SelectionChanged += (_, _) =>
        {
            if (_category.SelectedItem is ComboBoxItem { Tag: FamilyCategory category }) Apply(family => family.Category = category);
        };
        _types.SelectionChanged += (_, _) =>
        {
            if (_loading || _types.SelectedItem is not FamilyTypeDefinition type) return;
            _typeId = type.Id;
            Refresh();
        };

        var header = new WrapPanel { Margin = new Thickness(8) };
        header.Children.Add(Label("FamilyEditor.Name"));
        header.Children.Add(_name);
        header.Children.Add(Label("FamilyEditor.Category"));
        header.Children.Add(_category);
        header.Children.Add(Label("FamilyEditor.Type"));
        header.Children.Add(_types);
        header.Children.Add(Button("FamilyEditor.NewType", (_, _) => NewType()));

        var parameterBox = Section("FamilyEditor.Parameters", _parameters, Button("FamilyEditor.AddParameter", (_, _) => AddParameter()));
        var solidBox = Section("FamilyEditor.Solids", _solids, Button("FamilyEditor.AddBox", (_, _) => AddBox()));
        var preview = new Grid();
        preview.RowDefinitions.Add(new RowDefinition());
        preview.RowDefinitions.Add(new RowDefinition());
        var planFrame = Framed("FamilyEditor.PlanView", _plan);
        var isoFrame = Framed("FamilyEditor.IsoView", _iso);
        Grid.SetRow(isoFrame, 1);
        preview.Children.Add(planFrame);
        preview.Children.Add(isoFrame);
        _plan.SizeChanged += (_, _) => DrawPreview();
        _iso.SizeChanged += (_, _) => DrawPreview();

        var body = new Grid { Margin = new Thickness(8, 0, 8, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        Grid.SetColumn(solidBox, 1);
        Grid.SetColumn(preview, 2);
        body.Children.Add(parameterBox);
        body.Children.Add(solidBox);
        body.Children.Add(preview);

        var footer = new DockPanel { Margin = new Thickness(8) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button("FamilyEditor.New", (_, _) => NewFamily()));
        buttons.Children.Add(Button("FamilyEditor.Open", (_, _) => OpenFile()));
        buttons.Children.Add(Button("FamilyEditor.Save", (_, _) => SaveFile()));
        buttons.Children.Add(Button("FamilyEditor.LoadIntoProject", (_, _) => LoadIntoProject()));
        buttons.Children.Add(Button("FamilyEditor.Close", (_, _) => Close()));
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        footer.Children.Add(_status);

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(footer);
        layout.Children.Add(body);
        Content = layout;
        Refresh();
    }

    /// <summary>The family being edited, as it stands (for checks and for saving).</summary>
    public FamilyDocument Family => _family;

    /// <summary>The type shown and loaded.</summary>
    public Guid CurrentType => _typeId;

    /// <summary>Applies a change to a copy and keeps it only when the family is still valid; otherwise says why.</summary>
    private bool Apply(Action<FamilyDocument> change)
    {
        var draft = _family.Clone();
        try
        {
            change(draft);
            draft.Validate();
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or ArgumentException)
        {
            _status.Text = LocalizedErrors.Translate(exception.Message);
            Refresh();
            return false;
        }
        _family = draft;
        _status.Text = Text("FamilyEditor.Valid");
        Refresh();
        return true;
    }

    private void Refresh()
    {
        _loading = true;
        _name.Text = _family.Name;
        _category.SelectedItem = _category.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, _family.Category));
        _types.ItemsSource = _family.Types.ToList();
        _types.SelectedItem = _family.Types.FirstOrDefault(type => type.Id == _typeId);
        _loading = false;
        BuildParameterRows();
        BuildSolidRows();
        DrawPreview();
    }

    private void BuildParameterRows()
    {
        _parameters.Children.Clear();
        var type = _family.Types.First(candidate => candidate.Id == _typeId);
        foreach (var parameter in _family.Parameters.Where(parameter => parameter.Kind == FamilyParameterKind.Length))
        {
            var value = type.Values.FirstOrDefault(item => item.ParameterId == parameter.Id)?.Number ?? parameter.DefaultNumber ?? 0;
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var box = new TextBox { Width = 90, Text = value.ToString("0.###", CultureInfo.CurrentCulture), Tag = parameter.Name };
            box.LostFocus += (_, _) =>
            {
                if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number))
                    Apply(family => FamilyAuthoring.SetValue(family, _typeId, parameter.Name, number));
                else _status.Text = Text("FamilyEditor.InvalidNumber");
            };
            DockPanel.SetDock(box, Dock.Right);
            row.Children.Add(box);
            row.Children.Add(new TextBlock { Text = parameter.Name + " (m)", VerticalAlignment = VerticalAlignment.Center });
            _parameters.Children.Add(row);
        }
    }

    private void BuildSolidRows()
    {
        _solids.Children.Clear();
        var headers = new UniformGrid { Columns = BoxFields.Length + 2 };
        headers.Children.Add(new TextBlock { Text = Text("FamilyEditor.SolidName"), FontWeight = FontWeights.SemiBold });
        foreach (var field in BoxFields) headers.Children.Add(new TextBlock { Text = Text("FamilyEditor.Field." + field), FontWeight = FontWeights.SemiBold });
        headers.Children.Add(new TextBlock());
        _solids.Children.Add(headers);
        foreach (var operation in _family.Operations.Where(operation => operation.Kind == FamilyOperationKind.SolidExtrusion))
        {
            var row = new UniformGrid { Columns = BoxFields.Length + 2, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock { Text = operation.Name, VerticalAlignment = VerticalAlignment.Center });
            foreach (var field in BoxFields)
            {
                var box = new TextBox { Text = operation.Expressions.GetValueOrDefault(field, ""), Margin = new Thickness(2, 0, 2, 0), Tag = field };
                box.LostFocus += (_, _) =>
                {
                    if (operation.Expressions.GetValueOrDefault(field) == box.Text) return;
                    Apply(family =>
                    {
                        var index = family.Operations.FindIndex(candidate => candidate.Id == operation.Id);
                        var expressions = new Dictionary<string, string>(family.Operations[index].Expressions, StringComparer.Ordinal) { [field] = box.Text.Trim() };
                        var changed = family.Operations[index] with { Expressions = expressions };
                        family.Operations[index] = changed with { Profile = [.. FamilyGeometry.ProfileOf(family, changed)] };
                    });
                };
                row.Children.Add(box);
            }
            var remove = new Button { Content = "✕", ToolTip = Text("FamilyEditor.RemoveSolid"), Margin = new Thickness(2, 0, 0, 0) };
            remove.Click += (_, _) => Apply(family => family.Operations.RemoveAll(candidate => candidate.Id == operation.Id));
            row.Children.Add(remove);
            _solids.Children.Add(row);
        }
    }

    private void DrawPreview()
    {
        _plan.Children.Clear();
        _iso.Children.Clear();
        SceneMesh mesh;
        try { mesh = FamilyGeometry.Tessellate(_family, _typeId); }
        catch (InvalidDataException exception) { _status.Text = LocalizedErrors.Translate(exception.Message); return; }
        if (mesh.TriangleCount == 0 || _plan.ActualWidth < 10 || _iso.ActualWidth < 10)
        {
            if (mesh.TriangleCount == 0) _status.Text = Text("FamilyEditor.Empty");
            return;
        }
        var edges = FeatureEdges.Extract(mesh.Positions, mesh.Indices);
        // Top view: every solid's outline, plus its feature edges seen from above.
        Draw(_plan, edges.Select(edge => (new Point(edge.A.X, -edge.A.Y), new Point(edge.B.X, -edge.B.Y))));
        // Isometric: the same edges on fixed axes, as the cover sheet draws the model.
        static Point Iso(System.Numerics.Vector3 point) => new((point.X - point.Y) * 0.70710678, -((point.X + point.Y) * 0.40824829 + point.Z * 0.81649658));
        Draw(_iso, edges.Select(edge => (Iso(edge.A), Iso(edge.B))));
    }

    private static void Draw(Canvas canvas, IEnumerable<(Point A, Point B)> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0) return;
        var points = list.SelectMany(line => new[] { line.A, line.B }).ToList();
        var (minX, maxX, minY, maxY) = (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y), points.Max(p => p.Y));
        var scale = Math.Min((canvas.ActualWidth - 30) / Math.Max(0.01, maxX - minX), (canvas.ActualHeight - 30) / Math.Max(0.01, maxY - minY));
        Point Map(Point point) => new(15 + (point.X - minX) * scale, 15 + (point.Y - minY) * scale);
        foreach (var (a, b) in list)
        {
            var (from, to) = (Map(a), Map(b));
            canvas.Children.Add(new Line { X1 = from.X, Y1 = from.Y, X2 = to.X, Y2 = to.Y, Stroke = Brushes.DimGray, StrokeThickness = 1.2 });
        }
    }

    private void NewType()
    {
        var name = TextPrompt.Ask(this, Text("FamilyEditor.NewType"), Text("FamilyEditor.TypeName"), Text("Prompt.Accept"), Text("Prompt.Cancel"),
            $"Tipo {_family.Types.Count + 1}");
        if (string.IsNullOrWhiteSpace(name)) return;
        Guid created = Guid.Empty;
        if (Apply(family => created = FamilyAuthoring.AddType(family, name, _typeId))) { _typeId = created; Refresh(); }
    }

    private void AddParameter()
    {
        var name = TextPrompt.Ask(this, Text("FamilyEditor.AddParameter"), Text("FamilyEditor.ParameterName"), Text("Prompt.Accept"), Text("Prompt.Cancel"), "Ancho");
        if (string.IsNullOrWhiteSpace(name)) return;
        var value = TextPrompt.Ask(this, Text("FamilyEditor.AddParameter"), Text("FamilyEditor.ParameterValue"), Text("Prompt.Accept"), Text("Prompt.Cancel"), "1");
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)) { _status.Text = Text("FamilyEditor.InvalidNumber"); return; }
        Apply(family => FamilyAuthoring.AddLengthParameter(family, name, number));
    }

    private void AddBox()
    {
        var number = _family.Operations.Count + 1;
        Apply(family => FamilyAuthoring.AddBox(family, $"Caja {number}", "0", "0", "0.5", "0.5", "0", "0.5"));
    }

    /// <summary>Starts another family; the one being edited stays loaded in the project if it was loaded.</summary>
    private void NewFamily()
    {
        _family = FamilyAuthoring.Create(Text("FamilyEditor.NewName"), FamilyCategory.Furniture);
        _typeId = _family.Types[0].Id;
        _status.Text = Text("FamilyEditor.Empty");
        Refresh();
    }

    private void OpenFile()
    {
        var dialog = new OpenFileDialog { Filter = Text("Dialog.FamilyFilter"), InitialDirectory = FileDialogLocations.FamilyFolder(), CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var loaded = NativeDocumentStore.LoadFamily(dialog.FileName);
            loaded.Validate();
            _family = loaded;
            _typeId = _family.Types[0].Id;
            _status.Text = string.Format(CultureInfo.CurrentCulture, Text("FamilyEditor.Opened"), _family.Name);
            Refresh();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            _status.Text = LocalizedErrors.Translate(exception.Message);
        }
    }

    private void SaveFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = Text("Dialog.FamilyFilter"), InitialDirectory = FileDialogLocations.FamilyFolder(),
            FileName = _family.Name + NativeDocumentStore.FamilyExtension, AddExtension = true, DefaultExt = NativeDocumentStore.FamilyExtension
        };
        if (dialog.ShowDialog(this) != true) return;
        SaveTo(dialog.FileName);
    }

    /// <summary>Saves the family with the current type's mesh, so it also opens as a placeable component elsewhere.</summary>
    public void SaveTo(string path)
    {
        try
        {
            var saved = FamilyAuthoring.Bake(_family, _typeId);
            saved.Id = _family.Id;
            saved.Name = _family.Name;
            saved.Types = [.. _family.Types];
            NativeDocumentStore.SaveFamily(path, saved);
            _status.Text = string.Format(CultureInfo.CurrentCulture, Text("FamilyEditor.Saved"), System.IO.Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _status.Text = LocalizedErrors.Translate(exception.Message);
        }
    }

    /// <summary>Loads the current type into the project and arms placement there; the editor stays open.</summary>
    public void LoadIntoProject()
    {
        try
        {
            _family.Validate();
            _loadAndPlace(_family, _typeId);
            _status.Text = Text("FamilyEditor.Loaded");
        }
        catch (InvalidDataException exception) { _status.Text = LocalizedErrors.Translate(exception.Message); }
    }

    private static TextBlock Label(string key) => new() { Text = Text(key), VerticalAlignment = VerticalAlignment.Center };

    private static Button Button(string key, RoutedEventHandler click)
    {
        var button = new Button { Content = Text(key), Margin = new Thickness(4, 0, 4, 0), Padding = new Thickness(10, 3, 10, 3) };
        button.Click += click;
        return button;
    }

    private static GroupBox Section(string key, Panel content, Button add) => new()
    {
        Header = Text(key), Margin = new Thickness(0, 0, 8, 0),
        Content = new DockPanel { Children = { Docked(add, Dock.Bottom), new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } } }
    };

    private static UIElement Docked(UIElement element, Dock dock)
    {
        DockPanel.SetDock(element, dock);
        if (element is FrameworkElement framework) framework.Margin = new Thickness(0, 6, 0, 0);
        return element;
    }

    private static GroupBox Framed(string key, Canvas canvas) => new() { Header = Text(key), Content = canvas, Margin = new Thickness(0, 0, 0, 6) };

    private static string Text(string key) => Application.Current.TryFindResource(key) as string ?? key;
}
