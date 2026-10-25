using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>Edits reusable wall geometry and coordinated plan and section drafting appearance.</summary>
public partial class WallTypeEditorWindow : Window
{
    private readonly DocumentSession _session;
    private bool _loading;

    /// <summary>Creates the editor and selects the wall type associated with the current context.</summary>
    public WallTypeEditorWindow(DocumentSession session, Guid selectedTypeId)
    {
        InitializeComponent();
        _session = session;
        PopulateEnumPickers();
        PopulateTypePicker(selectedTypeId);
    }

    /// <summary>Creates localized choices while retaining domain enum values as stable tags.</summary>
    private void PopulateEnumPickers()
    {
        foreach (var function in Enum.GetValues<WallFunction>())
            FunctionPicker.Items.Add(new ComboBoxItem
            {
                Content = Text($"WallFunction.{function}"), Tag = function
            });
        foreach (var pattern in Enum.GetValues<WallFillPattern>())
        {
            var label = Text($"WallFillPattern.{pattern}");
            PlanPatternPicker.Items.Add(new ComboBoxItem { Content = label, Tag = pattern });
            SectionPatternPicker.Items.Add(new ComboBoxItem { Content = label, Tag = pattern });
        }
    }

    /// <summary>Lists all project wall types and preserves the caller's current selection.</summary>
    private void PopulateTypePicker(Guid selectedTypeId)
    {
        _loading = true;
        foreach (var wallType in _session.Document.WallTypes)
        {
            var item = new ComboBoxItem { Content = wallType.Name, Tag = wallType.Id };
            TypePicker.Items.Add(item);
            if (wallType.Id == selectedTypeId) TypePicker.SelectedItem = item;
        }
        TypePicker.SelectedItem ??= TypePicker.Items[0];
        _loading = false;
        LoadSelectedType();
    }

    /// <summary>Loads a newly selected type into editable controls.</summary>
    private void TypePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) LoadSelectedType();
    }

    /// <summary>Copies the selected domain type into the editor without mutating the document.</summary>
    private void LoadSelectedType()
    {
        if (TypePicker.SelectedItem is not ComboBoxItem { Tag: Guid typeId }) return;
        var wallType = _session.Document.WallTypes.Single(candidate => candidate.Id == typeId);
        NameInput.Text = wallType.Name;
        ThicknessInput.Text = wallType.Thickness.ToString("0.###", CultureInfo.CurrentCulture);
        MaterialInput.Text = wallType.MaterialName;
        PlanColorInput.Text = wallType.PlanFillColor;
        SectionColorInput.Text = wallType.SectionFillColor;
        SelectTaggedItem(FunctionPicker, wallType.Function);
        SelectTaggedItem(PlanPatternPicker, wallType.PlanFillPattern);
        SelectTaggedItem(SectionPatternPicker, wallType.SectionFillPattern);
        ValidationText.Text = string.Empty;
    }

    /// <summary>Validates editor fields and commits one undoable type update.</summary>
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TypePicker.SelectedItem is not ComboBoxItem { Tag: Guid typeId } ||
            FunctionPicker.SelectedItem is not ComboBoxItem { Tag: WallFunction function } ||
            PlanPatternPicker.SelectedItem is not ComboBoxItem { Tag: WallFillPattern planPattern } ||
            SectionPatternPicker.SelectedItem is not ComboBoxItem { Tag: WallFillPattern sectionPattern } ||
            !double.TryParse(ThicknessInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var thickness) ||
            thickness is < 0.03 or > 5 || string.IsNullOrWhiteSpace(NameInput.Text) ||
            string.IsNullOrWhiteSpace(MaterialInput.Text) || !IsRgbColor(PlanColorInput.Text) ||
            !IsRgbColor(SectionColorInput.Text))
        {
            ValidationText.Text = Text("WallTypeEditor.InvalidValues");
            return;
        }
        _session.UpdateWallType(typeId, NameInput.Text, function, thickness, MaterialInput.Text,
            PlanColorInput.Text, planPattern, SectionColorInput.Text, sectionPattern);
        DialogResult = true;
    }

    /// <summary>Selects a combo-box entry by its stable enum tag.</summary>
    private static void SelectTaggedItem(ComboBox picker, object value) =>
        picker.SelectedItem = picker.Items.OfType<ComboBoxItem>().First(item => Equals(item.Tag, value));

    /// <summary>Checks the persisted seven-character RGB color contract.</summary>
    private static bool IsRgbColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    /// <summary>Resolves localized editor text from application resources.</summary>
    private static string Text(string key) => Application.Current.TryFindResource(key) as string ?? key;
}
