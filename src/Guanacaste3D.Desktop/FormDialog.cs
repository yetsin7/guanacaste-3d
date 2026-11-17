using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Guanacaste3D.Desktop;

/// <summary>One labelled field of a <see cref="FormDialog"/>: free text, or a pick from a list of choices.</summary>
internal sealed record FormField(string Label, string Initial, IReadOnlyList<string>? Choices = null, bool Multiline = false);

/// <summary>
/// A small form with one labelled input per field, used where a command used to ask for a line of space-separated codes.
/// It returns the text of each field (the chosen entry for a list), in the order the fields were given.
/// </summary>
internal sealed class FormDialog : Window
{
    private readonly List<FrameworkElement> _inputs = [];

    private FormDialog(string title, string heading, IReadOnlyList<FormField> fields)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        FontSize = 12;
        SetResourceReference(BackgroundProperty, "AppBackgroundBrush");
        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 340 };
        if (heading.Length > 0)
            panel.Children.Add(new TextBlock { Text = heading, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var field in fields)
        {
            FrameworkElement input;
            if (field.Choices is { } choices)
            {
                var combo = new ComboBox { MinWidth = 160 };
                foreach (var choice in choices) combo.Items.Add(choice);
                combo.SelectedItem = choices.Contains(field.Initial) ? field.Initial : choices.FirstOrDefault();
                input = combo;
            }
            else
                input = new TextBox
                {
                    Text = field.Initial, MinWidth = 160, AcceptsReturn = field.Multiline, MinHeight = field.Multiline ? 80 : 0,
                    TextWrapping = field.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    VerticalScrollBarVisibility = field.Multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
                };
            _inputs.Add(input);
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var label = new TextBlock { Text = field.Label, Width = 140, VerticalAlignment = field.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(input);
            panel.Children.Add(row);
        }
        var accept = new Button { Content = Text("Dialog.Accept"), MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = Text("Dialog.Cancel"), MinWidth = 80, IsCancel = true };
        accept.Click += (_, _) => DialogResult = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            if (_inputs.OfType<TextBox>().FirstOrDefault() is { } first)
            {
                Keyboard.Focus(first);
                first.SelectAll();
            }
        };
    }

    /// <summary>Shows the form and returns each field's text, or null when it was cancelled.</summary>
    public static IReadOnlyList<string>? Ask(Window owner, string title, string heading, IReadOnlyList<FormField> fields)
    {
        var window = new FormDialog(title, heading, fields) { Owner = owner };
        if (window.ShowDialog() != true) return null;
        return window._inputs.Select(input => input switch
        {
            ComboBox combo => combo.SelectedItem as string ?? "",
            TextBox box => box.Text.Trim(),
            _ => ""
        }).ToList();
    }

    private static string Text(string key) => Application.Current.TryFindResource(key) as string ?? key;
}
