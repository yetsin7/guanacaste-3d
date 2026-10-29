using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Guanacaste3D.Desktop;

public partial class App : Application
{
    private const int StartupStepCount = 4;

    /// <summary>Shows the splash, loads the model libraries and workspace, then reveals the main window.</summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var projectPath = StartupDocumentResolver.FindSupportedPath(e.Args);
        var splash = new SplashWindow();
        splash.Show();

        await ReportStepAsync(splash, 0, "Splash.Step.Model");
        // Core model and geometry code is pure computation, so it warms up off the UI thread.
        await Task.Run(StartupWarmup.LoadModelLibraries);

        await ReportStepAsync(splash, 1, "Splash.Step.Interface");
        // Started without a document, the application shows the Home screen so the person chooses what to work on.
        var window = new MainWindow(projectPath, showStartScreen: true);
        MainWindow = window;

        await ReportStepAsync(splash, 2, "Splash.Step.Workspace");
        var rendered = new TaskCompletionSource();
        window.ContentRendered += (_, _) => rendered.TrySetResult();
        window.WindowState = WindowState.Maximized;
        window.Show();
        await rendered.Task;

        splash.Report(StartupStepCount, StartupStepCount, GetText("Splash.Step.Ready"));
        splash.Close();
    }

    /// <summary>Publishes a startup step and yields so the splash paints before the next blocking step.</summary>
    private async Task ReportStepAsync(SplashWindow splash, int completedSteps, string key)
    {
        splash.Report(completedSteps, StartupStepCount, GetText(key));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Keeps each tooltip inside its active Guanacaste 3D window and registers it for focus-aware dismissal.
    /// </summary>
    private void ToolTip_Opened(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToolTip toolTip) WindowBoundToolTip.Open(toolTip);
    }

    /// <summary>Releases the window activation listener associated with a tooltip after it closes.</summary>
    private void ToolTip_Closed(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToolTip toolTip) WindowBoundToolTip.Close(toolTip);
    }

    /// <summary>Returns a localized application string, or the resource key when no translation exists.</summary>
    private static string GetText(string key) =>
        Current.TryFindResource(key) as string ?? key;
}
