using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Opens the application file menu under the blue file tab.</summary>
    private void FileMenuButton_Click(object sender, RoutedEventArgs e)
    {
        FileMenu.PlacementTarget = FileMenuButton;
        FileMenu.Placement = PlacementMode.Bottom;
        FileMenu.IsOpen = true;
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    /// <summary>
    /// Keeps the custom frame inside the monitor work area when maximized and swaps the caption glyph.
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        bool maximized = WindowState == WindowState.Maximized;
        // A maximized chrome-less window overhangs the screen by the resize border on every side.
        WindowFrame.BorderThickness = maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(1);
        WindowFrame.BorderBrush = maximized
            ? (System.Windows.Media.Brush)FindResource("ChromeBrush")
            : (System.Windows.Media.Brush)FindResource("PanelBorderBrush");
        MaximizeButton.Content = maximized ? "" : "";
        MaximizeButton.ToolTip = GetText(maximized ? "Window.Restore" : "Window.Maximize");
    }

    /// <summary>Applies the frame for the initial window state once the native window exists.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        MainWindow_StateChanged(this, EventArgs.Empty);
    }

    /// <summary>Shows "product - project - active view" in the caption, marking unsaved changes.</summary>
    private void UpdateWindowTitle()
    {
        if (_session is null) return;
        if (StartScreenControl?.Visibility == Visibility.Visible)
        {
            Title = $"{GetText("App.Name")} - {GetText("Action.Home")}";
            return;
        }
        string view = (ViewTabs?.SelectedItem as TabItem)?.Header?.ToString() ?? string.Empty;
        string project = _session.Document.Name + (_dirty ? " *" : string.Empty);
        Title = string.IsNullOrEmpty(view)
            ? $"{GetText("App.Name")} - {project}"
            : $"{GetText("App.Name")} - {project} - {view}";
    }

    /// <summary>Shows the green contextual tab only while its tool runs, falling back to the first tab.</summary>
    private void UpdateContextualTabs()
    {
        if (WallContextTab is null) return;
        WallContextTab.Visibility = _drawingWall ? Visibility.Visible : Visibility.Collapsed;
        if (!_drawingWall && WallContextTab.IsSelected) RibbonTabControl.SelectedIndex = 0;
    }
}
