using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Manages door and window types: create, duplicate, edit, delete, and purge.
/// Type edits are session transactions, so size changes propagate to every
/// placed instance, can be undone, and are rejected whole when an instance
/// would no longer fit its host wall.
/// </summary>
public partial class OpeningTypesWindow : Window
{
    private readonly DocumentSession _session;
    private Guid? _editingTypeId;

    /// <summary>Opens the type manager for a session, optionally preselecting a type.</summary>
    public OpeningTypesWindow(DocumentSession session, Guid? initialTypeId = null)
    {
        InitializeComponent();
        _session = session;
        var kinds = Enum.GetValues<OpeningKind>()
            .Select(kind => new LabeledValue<OpeningKind?>(Text($"OpeningKind.{kind}"), kind)).ToList();
        KindBox.ItemsSource = kinds;
        KindFilterBox.ItemsSource = kinds.Prepend(new LabeledValue<OpeningKind?>(Text("OpeningTypes.AllKinds"), null)).ToArray();
        KindFilterBox.SelectedIndex = 0;
        _session.Changed += Session_Changed;
        Closed += (_, _) => _session.Changed -= Session_Changed;
        ResetEditor(OpeningKind.Door);
        RefreshAll();
        if (initialTypeId is Guid id) TypesList.SelectedValue = id;
    }

    /// <summary>Resolves a localized string, falling back to its key when missing.</summary>
    private static string Text(string key) => ProjectParametersWindow.Text(key);

    /// <summary>Formats a localized pattern with culture-aware arguments.</summary>
    private static string Format(string key, params object[] values) => ProjectParametersWindow.Format(key, values);

    /// <summary>Refreshes the list and history buttons after any committed change.</summary>
    private void Session_Changed(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(RefreshAll, System.Windows.Threading.DispatcherPriority.Background);

    /// <summary>Lists types of the filtered kind with family, size, and instance count.</summary>
    private void RefreshAll()
    {
        var filter = KindFilterBox.SelectedValue as OpeningKind?;
        var counts = DocumentSession.CountOpeningInstances(_session.Document);
        var items = _session.Document.OpeningTypes
            .Where(type => filter is null || type.Kind == filter)
            .OrderBy(type => type.Kind).ThenBy(type => type.FamilyName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(type => type.Width).ThenBy(type => type.Height)
            .Select(type => new LabeledValue<Guid>(
                $"{Text($"OpeningKind.{type.Kind}")} · {type.DisplayName}\n" +
                Format("OpeningTypes.ListDetail", Meters(type.Width), Meters(type.Height), Meters(type.DefaultSill),
                    counts.GetValueOrDefault(type.Id)), type.Id))
            .ToArray();
        TypesList.DisplayMemberPath = nameof(LabeledValue<Guid>.Label);
        TypesList.SelectedValuePath = nameof(LabeledValue<Guid>.Value);
        TypesList.ItemsSource = items;
        if (_editingTypeId is Guid id && items.Any(item => item.Value == id)) TypesList.SelectedValue = id;
        else if (_editingTypeId is not null && _session.Document.OpeningTypes.All(type => type.Id != _editingTypeId))
            ResetEditor(OpeningKind.Door);
        UndoButton.IsEnabled = _session.CanUndo;
        RedoButton.IsEnabled = _session.CanRedo;
        UndoButton.ToolTip = _session.UndoName is { } undo ? Format("Parameters.UndoNamed", Text(undo)) : null;
        RedoButton.ToolTip = _session.RedoName is { } redo ? Format("Parameters.RedoNamed", Text(redo)) : null;
    }

    /// <summary>Formats a metric length for display in the current culture.</summary>
    private static string Meters(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);

    /// <summary>Reapplies the kind filter.</summary>
    private void KindFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshAll();
    }

    /// <summary>Loads the selected type into the editor.</summary>
    private void TypesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypesList.SelectedValue is not Guid id ||
            _session.Document.OpeningTypes.FirstOrDefault(type => type.Id == id) is not { } type) return;
        _editingTypeId = id;
        KindBox.SelectedValue = type.Kind;
        KindBox.IsEnabled = false;
        FamilyBox.Text = type.FamilyName;
        NameBox.Text = type.Name;
        WidthBox.Text = Meters(type.Width);
        HeightBox.Text = Meters(type.Height);
        SillBox.Text = Meters(type.DefaultSill);
        SillBox.IsEnabled = type.Kind == OpeningKind.Window;
        MaterialBox.Text = type.MaterialName;
        UsageText.Text = Format("OpeningTypes.Usage",
            DocumentSession.CountOpeningInstances(_session.Document).GetValueOrDefault(id));
    }

    /// <summary>Clears the editor for a new type of the given kind.</summary>
    private void ResetEditor(OpeningKind kind)
    {
        _editingTypeId = null;
        TypesList.SelectedItem = null;
        KindBox.IsEnabled = true;
        KindBox.SelectedValue = kind;
        FamilyBox.Text = kind == OpeningKind.Door ? "Single Door" : "Fixed Window";
        NameBox.Text = MaterialBox.Text = UsageText.Text = string.Empty;
        WidthBox.Text = Meters(kind == OpeningKind.Door ? 0.9 : 1.2);
        HeightBox.Text = Meters(kind == OpeningKind.Door ? 2.1 : 1.2);
        SillBox.Text = Meters(kind == OpeningKind.Door ? 0 : 0.9);
        SillBox.IsEnabled = kind == OpeningKind.Window;
    }

    /// <summary>Keeps the sill rules in sync with the kind chosen for a new type.</summary>
    private void KindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_editingTypeId is null && KindBox.SelectedValue is OpeningKind kind && IsLoaded) ResetEditor(kind);
    }

    /// <summary>Starts a new type.</summary>
    private void New_Click(object sender, RoutedEventArgs e) =>
        ResetEditor(KindBox.SelectedValue as OpeningKind? ?? OpeningKind.Door);

    /// <summary>Creates or updates the type from the editor as one transaction.</summary>
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadLength(WidthBox, out var width) || !TryReadLength(HeightBox, out var height) ||
            !TryReadLength(SillBox, out var sill)) return;
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(FamilyBox.Text))
        {
            StatusText.Text = Text("OpeningTypes.NeedNames");
            return;
        }
        RunTransaction(() =>
        {
            if (_editingTypeId is Guid id)
                _session.UpdateOpeningType(id, FamilyBox.Text, NameBox.Text, width, height, sill, MaterialBox.Text);
            else
                _editingTypeId = _session.AddOpeningType((OpeningKind)KindBox.SelectedValue, FamilyBox.Text,
                    NameBox.Text, width, height, sill, MaterialBox.Text);
        });
    }

    /// <summary>Duplicates the selected type under a unique copy name.</summary>
    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_editingTypeId is not Guid id) return;
        var source = _session.Document.OpeningTypes.First(type => type.Id == id);
        var name = Format("OpeningTypes.CopyName", source.Name);
        for (var index = 2; _session.Document.OpeningTypes.Any(type =>
                 type.FamilyName == source.FamilyName && type.Name == name); index++)
            name = Format("OpeningTypes.CopyNameIndexed", source.Name, index);
        RunTransaction(() => _editingTypeId = _session.DuplicateOpeningType(id, name));
    }

    /// <summary>Deletes the selected type when it has no placed instances.</summary>
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingTypeId is not Guid id) return;
        RunTransaction(() => _session.RemoveOpeningType(id));
    }

    /// <summary>Removes every unused type after confirmation.</summary>
    private void Purge_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Text("OpeningTypes.ConfirmPurge"), Text("OpeningTypes.WindowTitle"),
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        var removed = _session.PurgeUnusedOpeningTypes();
        StatusText.Text = Format("OpeningTypes.Purged", removed);
    }

    /// <summary>Parses a length field in meters, reporting invalid input in the status bar.</summary>
    private bool TryReadLength(TextBox box, out double value)
    {
        value = 0;
        if (ParameterValueText.TryParse(ParameterSpec.Length, box.Text, CultureInfo.CurrentCulture, out var number, out _)
            && number is double parsed)
        {
            value = parsed;
            return true;
        }
        StatusText.Text = Format("Parameters.InvalidValue", box.Text, Text("ParameterSpec.Length"));
        box.Focus();
        return false;
    }

    /// <summary>Runs a session operation, reporting the committed transaction or the validation failure.</summary>
    private void RunTransaction(Action operation)
    {
        try
        {
            operation();
            StatusText.Text = _session.UndoName is { } name ? Format("Parameters.Committed", Text(name)) : string.Empty;
        }
        catch (System.IO.InvalidDataException exception)
        {
            StatusText.Text = Format("Parameters.Rejected", LocalizedErrors.Translate(exception.Message));
        }
    }

    /// <summary>Undoes the most recent transaction in the shared session.</summary>
    private void Undo_Click(object sender, RoutedEventArgs e) => _session.Undo();

    /// <summary>Redoes the most recently undone transaction in the shared session.</summary>
    private void Redo_Click(object sender, RoutedEventArgs e) => _session.Redo();

    /// <summary>Closes the manager; all changes are already committed transactions.</summary>
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
