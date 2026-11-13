using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Exposes only measurements that have an unambiguous transactional edit for the selected element.</summary>
    private void ConfigureModelDimensionEditor(Border chip, PickedKind kind, Guid id, int index, double value)
    {
        if (!((kind is PickedKind.Wall or PickedKind.Column) && index is >= 0 and < 3) &&
            !(kind == PickedKind.Opening && index is 0 or 2 or 3) &&
            !(kind == PickedKind.Floor && index == 2 &&
              _session.Document.Floors.Any(item => item.Id == id && !item.IsPitched && !item.IsToposolid))) return;
        var label = chip.Child;
        chip.Child = null;
        var button = new Button
        {
            Content = label, Padding = new Thickness(0), BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent, Cursor = Cursors.Hand,
            ToolTip = GetText("Dimension.Edit")
        };
        chip.Child = button;
        button.Click += (_, e) =>
        {
            e.Handled = true;
            var editor = new TextBox { Text = Units.LengthNumber(value), MinWidth = 56,
                ToolTip = GetText("Dimension.EnterMeters") };
            chip.Child = editor;
            editor.KeyDown += (_, key) =>
            {
                if (key.Key == Key.Escape)
                {
                    key.Handled = true;
                    UpdateModelSelection(true);
                }
                else if (key.Key is Key.Enter or Key.Return)
                {
                    key.Handled = true;
                    ApplyModelDimension(kind, id, index, editor.Text);
                }
            };
            editor.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                if (editor.Parent is not null && !editor.IsKeyboardFocusWithin) UpdateModelSelection(true);
            });
            editor.Focus();
            editor.SelectAll();
        };
    }

    /// <summary>Applies a 3D measurement as one validated model transaction; invalid input leaves the model unchanged.</summary>
    private void ApplyModelDimension(PickedKind kind, Guid id, int index, string input)
    {
        if (!Units.TryParseLength(input, out var value) || value <= 0)
        {
            StatusText.Text = GetText("Dimension.Invalid");
            UpdateModelSelection(true);
            return;
        }
        try
        {
            if (kind == PickedKind.Wall && _session.Document.Walls.FirstOrDefault(item => item.Id == id) is { } wall)
            {
                var edit = index switch
                {
                    0 => new WallDimensionEdit(Length: value),
                    1 => new WallDimensionEdit(Thickness: value),
                    _ => new WallDimensionEdit(Height: value + wall.BaseOffset)
                };
                _session.ApplyWallDimensions(id, edit);
            }
            else if (kind == PickedKind.Floor && index == 2 &&
                _session.Document.Floors.FirstOrDefault(item => item.Id == id && !item.IsPitched && !item.IsToposolid) is { } floor)
            {
                _session.UpdateFloor(id, floor.Name, value, floor.ElevationOffset);
            }
            else if (kind == PickedKind.Column && _session.Document.Columns.FirstOrDefault(item => item.Id == id) is { } column)
            {
                _session.UpdateColumn(id, index == 0 ? value : column.Width, index == 1 ? value : column.Depth,
                    index == 2 ? value : column.Height, column.Shape);
            }
            else if (kind == PickedKind.Opening && _session.Document.Openings.FirstOrDefault(item => item.Id == id) is { } opening)
            {
                _session.UpdateOpening(id, opening.Offset, index == 0 ? value : opening.Width,
                    index == 3 ? value : opening.Sill, index == 2 ? value : opening.Height);
            }
            RefreshAll();
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
            UpdateModelSelection(true);
        }
    }
}
