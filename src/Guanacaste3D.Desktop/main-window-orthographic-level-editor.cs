using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Creates an editable level datum shared by elevation and section drawings.</summary>
    private FrameworkElement CreateProjectedLevelLabel(Level level)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.White };
        row.Children.Add(new TextBlock { Text = level.Name + "  ", Foreground = Brushes.SteelBlue, FontSize = 11 });
        var button = new Button
        {
            Content = Units.FormatLength(level.Elevation), Tag = level.Id, FontSize = 11,
            Padding = new Thickness(0), BorderThickness = new Thickness(0), Foreground = Brushes.SteelBlue,
            Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = GetText("Dimension.Edit")
        };
        row.Children.Add(button);
        button.Click += (_, _) =>
        {
            var editor = new TextBox { Text = Units.LengthNumber(level.Elevation), Tag = level.Id, MinWidth = 55,
                ToolTip = GetText("LevelElevation.Enter") };
            row.Children.Remove(button);
            row.Children.Add(editor);
            editor.KeyDown += (_, key) =>
            {
                if (key.Key == Key.Escape)
                {
                    key.Handled = true;
                    RenderOrthographicViews();
                }
                else if (key.Key is Key.Enter or Key.Return)
                {
                    key.Handled = true;
                    ApplyProjectedLevelElevation(level.Id, editor.Text);
                }
            };
            editor.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                if (editor.Parent is not null && !editor.IsKeyboardFocusWithin) RenderOrthographicViews();
            });
            editor.Focus();
            editor.SelectAll();
        };
        return row;
    }

    /// <summary>Commits a datum elevation through the level transaction, preserving its constraint validation.</summary>
    private void ApplyProjectedLevelElevation(Guid levelId, string input)
    {
        if (!Units.TryParseLength(input, out var elevation))
        {
            StatusText.Text = GetText("LevelElevation.Invalid");
            RenderOrthographicViews();
            return;
        }
        try
        {
            var level = _session.Document.Levels.FirstOrDefault(candidate => candidate.Id == levelId);
            if (level is not null && level.Elevation != elevation)
            {
                // The session change event refreshes the plan, 3D scene, properties, and orthographic views.
                _session.UpdateLevel(levelId, level.Name, elevation);
                return;
            }
            RenderOrthographicViews();
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
            RenderOrthographicViews();
        }
    }
}
