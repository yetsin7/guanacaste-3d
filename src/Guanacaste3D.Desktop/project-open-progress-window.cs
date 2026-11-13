using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>Shows a cancellable wait while a project is loaded without blocking the dispatcher.</summary>
internal sealed class ProjectOpenProgressWindow : Window
{
    private readonly Func<BuildingDocument> _load;
    private bool _completed;

    /// <summary>Creates a progress dialog whose cancellation discards the load result before it can replace the document.</summary>
    public ProjectOpenProgressWindow(Window? owner, string title, string message, string cancelText, Func<BuildingDocument> load)
    {
        _load = load;
        if (owner is { IsVisible: true }) Owner = owner;
        Title = title;
        Width = 430;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = Owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(new ProgressBar { Height = 18, IsIndeterminate = true });
        var cancel = new Button { Content = cancelText, HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 100, Margin = new Thickness(0, 18, 0, 0) };
        cancel.Click += (_, _) => Close();
        PreviewKeyDown += (_, key) =>
        {
            if (key.Key != System.Windows.Input.Key.Escape) return;
            key.Handled = true;
            Close();
        };
        panel.Children.Add(cancel);
        Content = panel;
        ContentRendered += LoadAfterRender;
    }

    /// <summary>The successfully validated project, or null when loading was cancelled or failed.</summary>
    public BuildingDocument? LoadedDocument { get; private set; }

    /// <summary>The load failure for the owning window to report after the progress dialog closes.</summary>
    public Exception? LoadError { get; private set; }

    /// <summary>Runs the reader off the UI thread; a dismissed dialog never exposes its late result.</summary>
    private async void LoadAfterRender(object? sender, EventArgs e)
    {
        ContentRendered -= LoadAfterRender;
        try
        {
            var document = await Task.Run(_load);
            if (!IsVisible) return;
            LoadedDocument = document;
        }
        catch (Exception exception)
        {
            if (!IsVisible) return;
            LoadError = exception;
        }
        _completed = true;
        DialogResult = true;
    }

    /// <summary>Clears the result when the dialog is dismissed before the reader finishes.</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_completed) LoadedDocument = null;
        base.OnClosing(e);
    }
}
