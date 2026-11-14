using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Paste aligned to selected levels: the way a typical floor becomes a building. The selection of the active plan (all
/// of it after Ctrl+A), or the whole storey when nothing is selected, is copied onto every level ticked in the dialog
/// in one undoable step, with doors and windows, rooms renumbered per storey, structure, furniture and circuits.
/// </summary>
public partial class MainWindow
{
    private void PasteToLevels()
    {
        var source = _session.Document.Levels.FirstOrDefault(level => level.Id == _activeLevelId);
        if (source is null) return;
        var others = _session.Document.Levels.Where(level => level.Id != source.Id).OrderBy(level => level.Elevation).ToList();
        if (others.Count == 0)
        {
            StatusText.Text = GetText("Status.PasteToLevelsNoLevels");
            return;
        }
        var selection = SelectedIds().ToHashSet();
        var targets = PickLevels(source, others, selection.Count);
        if (targets is not { Count: > 0 }) return;
        try
        {
            var created = _session.CopyLevelContent(source.Id, targets, selection.Count > 0 ? selection : null);
            RefreshAll();
            StatusText.Text = FormatText("Status.PastedToLevels", created, targets.Count);
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
        }
    }

    /// <summary>The level list with a tick per level, «Todos» and «Ninguno»; null when cancelled.</summary>
    private List<Guid>? PickLevels(Level source, IReadOnlyList<Level> levels, int selected)
    {
        var boxes = levels.Select(level => new CheckBox
        {
            Content = $"{level.Name}  ({Units.FormatLength(level.Elevation)})", Tag = level.Id, Margin = new Thickness(0, 2, 0, 2),
            IsChecked = level.Elevation > source.Elevation
        }).ToList();
        var list = new StackPanel();
        foreach (var box in boxes) list.Children.Add(box);
        var dialog = new Window
        {
            Title = GetText("PasteToLevels.Title"), Owner = this, Width = 380, Height = 460, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };
        var all = new Button { Content = GetText("PasteToLevels.All"), Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 2, 8, 2) };
        var none = new Button { Content = GetText("PasteToLevels.None"), Padding = new Thickness(8, 2, 8, 2) };
        all.Click += (_, _) => boxes.ForEach(box => box.IsChecked = true);
        none.Click += (_, _) => boxes.ForEach(box => box.IsChecked = false);
        var accept = new Button { Content = GetText("Prompt.Accept"), IsDefault = true, Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = GetText("Prompt.Cancel"), IsCancel = true, Width = 90 };
        accept.Click += (_, _) => dialog.DialogResult = true;
        var quick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        quick.Children.Add(all);
        quick.Children.Add(none);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);
        var layout = new DockPanel { Margin = new Thickness(12) };
        var heading = new TextBlock
        {
            Text = FormatText(selected > 0 ? "PasteToLevels.Selection" : "PasteToLevels.WholeLevel", selected, source.Name), TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(heading, Dock.Top);
        DockPanel.SetDock(quick, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(heading);
        layout.Children.Add(quick);
        layout.Children.Add(buttons);
        layout.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = layout;
        if (dialog.ShowDialog() != true) return null;
        return boxes.Where(box => box.IsChecked == true).Select(box => (Guid)box.Tag).ToList();
    }
}
