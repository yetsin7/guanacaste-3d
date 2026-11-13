using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private StackPanel? _instancePropertiesEditor;
    private readonly Dictionary<string, TextBox> _instancePropertyInputs = [];
    private readonly Dictionary<string, (string Text, double Value)> _instancePropertyOriginals = [];

    /// <summary>Shows editable instance dimensions for supported selected elements in the properties palette.</summary>
    private void UpdateInstancePropertiesEditor()
    {
        if (PropertiesText.Parent is not StackPanel panel) return;
        if (_instancePropertiesEditor is null)
        {
            _instancePropertiesEditor = new StackPanel { Margin = new Thickness(6, 4, 6, 8) };
            panel.Children.Insert(panel.Children.IndexOf(PropertiesText) + 1, _instancePropertiesEditor);
        }
        _instancePropertiesEditor.Children.Clear();
        _instancePropertyInputs.Clear();
        _instancePropertyOriginals.Clear();
        var column = _session.Document.Columns.FirstOrDefault(item => item.Id == _selectedColumnId);
        var floor = _session.Document.Floors.FirstOrDefault(item => item.Id == _selectedFloorId);
        var component = _session.Document.StructuralElements.FirstOrDefault(item => item.Id == _selectedStructuralId &&
            item.Kind == StructuralElementKind.Component);
        if (column is null && floor is null && component is null)
        {
            _instancePropertiesEditor.Visibility = Visibility.Collapsed;
            return;
        }
        _instancePropertiesEditor.Visibility = Visibility.Visible;
        if (column is not null)
        {
            AddInstanceProperty("Width", column.Width);
            AddInstanceProperty("Depth", column.Depth);
            AddInstanceProperty("Height", column.Height);
        }
        else if (floor is not null)
        {
            AddInstanceProperty("Thickness", floor.Thickness);
            AddInstanceProperty("Elevation", floor.ElevationOffset);
        }
        else if (component is not null)
        {
            AddInstanceProperty("Width", component.Width);
            AddInstanceProperty("Height", component.Height);
            AddInstanceProperty("Offset", component.Offset);
        }
        var apply = new Button { Content = GetText("InstanceProperties.Apply"), Tag = "ApplyInstanceProperties",
            HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 8, 0, 2) };
        apply.Click += ApplyInstanceProperties_Click;
        _instancePropertiesEditor.Children.Add(apply);
        var more = new Button { Content = GetText("InstanceProperties.More"), Tag = "MoreInstanceProperties",
            HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 2, 0, 2) };
        more.Click += (_, _) => TryEditSelectedElementType();
        _instancePropertiesEditor.Children.Add(more);
    }

    /// <summary>Creates a unit-aware length input using a stable identifier for the current selection.</summary>
    private void AddInstanceProperty(string name, double value)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var label = new TextBlock { Text = GetText("InstanceProperties." + name), Width = 110,
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(label, Dock.Left);
        row.Children.Add(label);
        var input = new TextBox { Text = Units.LengthNumber(value), Tag = name, MinWidth = 50 };
        input.PreviewKeyDown += InstanceProperty_PreviewKeyDown;
        row.Children.Add(input);
        _instancePropertyInputs[name] = input;
        _instancePropertyOriginals[name] = (input.Text, value);
        _instancePropertiesEditor!.Children.Add(row);
    }

    /// <summary>Commits palette edits with Enter or restores the focused field with Escape before view shortcuts run.</summary>
    private void InstanceProperty_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplyInstanceProperties_Click(sender, e);
        }
        else if (e.Key == Key.Escape && sender is TextBox { Tag: string name } input &&
                 _instancePropertyOriginals.TryGetValue(name, out var original))
        {
            input.Text = original.Text;
            input.SelectAll();
            e.Handled = true;
        }
    }

    /// <summary>Validates every edited length before committing one undoable instance update.</summary>
    private void ApplyInstanceProperties_Click(object sender, RoutedEventArgs e)
    {
        var values = new Dictionary<string, double>();
        foreach (var (name, input) in _instancePropertyInputs)
        {
            // Display precision must never round unrelated model dimensions during an edit.
            if (_instancePropertyOriginals.TryGetValue(name, out var original) && input.Text == original.Text)
            {
                values[name] = original.Value;
                continue;
            }
            if (!Units.TryParseLength(input.Text, out var value))
            {
                StatusText.Text = GetText("InstanceProperties.Invalid");
                return;
            }
            values[name] = value;
        }
        try
        {
            if (_session.Document.Columns.FirstOrDefault(item => item.Id == _selectedColumnId) is { } column &&
                values.TryGetValue("Depth", out var depth))
                _session.UpdateColumn(column.Id, values["Width"], depth, values["Height"], column.Shape);
            else if (_session.Document.Floors.FirstOrDefault(item => item.Id == _selectedFloorId) is { } floor &&
                     values.TryGetValue("Thickness", out var thickness))
                _session.UpdateFloor(floor.Id, floor.Name, thickness, values["Elevation"]);
            else if (_session.Document.StructuralElements.FirstOrDefault(item => item.Id == _selectedStructuralId &&
                         item.Kind == StructuralElementKind.Component) is { } component && values.ContainsKey("Offset"))
                _session.UpdateStructuralElement(component.Id, component.Name, values["Width"], values["Height"],
                    values["Offset"], component.Style);
            else return;
            StatusText.Text = GetText("Status.ElementUpdated");
        }
        catch (InvalidDataException exception) { StatusText.Text = LocalizedErrors.Translate(exception.Message); }
    }
}
