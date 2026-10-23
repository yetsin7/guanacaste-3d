using System.Windows;

namespace Guanacaste3D.Desktop;

public partial class App : Application
{
    /// <summary>Creates the main window and forwards an associated project path when present.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var projectPath = e.Args.FirstOrDefault(argument => !string.IsNullOrWhiteSpace(argument));
        var window = new MainWindow(projectPath);
        MainWindow = window;
        window.Show();
    }
}
