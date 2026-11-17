using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Asks how many longitudinal bars a beam or footing gets, of which diameter and on which face. Diameters are the
/// bar designations in use in Costa Rica (#3 to #6), so nobody has to type 0.012 for a 12 mm bar.
/// </summary>
internal sealed class RebarDialog : Window
{
    private static readonly (string Label, double Diameter)[] Diameters =
    [
        ("#3 (9.5 mm)", 0.0095), ("#4 (12 mm)", 0.012), ("#5 (16 mm)", 0.016), ("#6 (19 mm)", 0.019)
    ];

    private readonly TextBox _count = new() { Text = "3", MinWidth = 120 };
    private readonly ComboBox _diameter = new() { MinWidth = 120 };
    private readonly ComboBox _face = new() { MinWidth = 120 };

    private RebarDialog(string hostName)
    {
        Title = Text("Concrete.Rebar");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        FontSize = 12;
        SetResourceReference(BackgroundProperty, "AppBackgroundBrush");
        foreach (var (label, _) in Diameters) _diameter.Items.Add(label);
        _diameter.SelectedIndex = 1;
        _face.Items.Add(Text("Rebar.FaceBottom"));
        _face.Items.Add(Text("Rebar.FaceTop"));
        _face.SelectedIndex = 0;

        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 300 };
        panel.Children.Add(new TextBlock { Text = string.Format(CultureInfo.CurrentCulture, Text("Rebar.Heading"), hostName),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        Row(panel, Text("Rebar.Count"), _count);
        Row(panel, Text("Rebar.Diameter"), _diameter);
        Row(panel, Text("Rebar.Face"), _face);
        var accept = new Button { Content = Text("Dialog.Accept"), MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = Text("Dialog.Cancel"), MinWidth = 80, IsCancel = true };
        accept.Click += (_, _) =>
        {
            if (!int.TryParse(_count.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var count) || count is < 1 or > 20)
            {
                _count.Focus();
                _count.SelectAll();
                return;
            }
            DialogResult = true;
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(accept);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            Keyboard.Focus(_count);
            _count.SelectAll();
        };
    }

    private static void Row(Panel parent, string label, FrameworkElement input)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var text = new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(text, Dock.Left);
        row.Children.Add(text);
        row.Children.Add(input);
        parent.Children.Add(row);
    }

    /// <summary>Returns the bars the person chose, or null when the dialog was cancelled.</summary>
    public static (int Count, double Diameter, bool Top)? Ask(Window owner, string hostName)
    {
        var window = new RebarDialog(hostName) { Owner = owner };
        if (window.ShowDialog() != true) return null;
        return (int.Parse(window._count.Text.Trim(), CultureInfo.CurrentCulture), Diameters[window._diameter.SelectedIndex].Diameter,
            window._face.SelectedIndex == 1);
    }

    private static string Text(string key) => Application.Current.TryFindResource(key) as string ?? key;
}
