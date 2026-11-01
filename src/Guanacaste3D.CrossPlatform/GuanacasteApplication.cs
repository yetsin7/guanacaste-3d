using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Guanacaste3D.CrossPlatform;

public sealed class GuanacasteApplication : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
