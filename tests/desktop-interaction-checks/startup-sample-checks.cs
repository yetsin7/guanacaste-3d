using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Desktop;

internal static partial class InteractionChecks
{
    /// <summary>
    /// Started without a document, the application opens the breeze-block house in the Render tab with its saved camera,
    /// sun and filters; saving keeps the panel's setup in the project.
    /// </summary>
    private static void StartupSampleChecks()
    {
        var opened = new MainWindow(openStartupSample: true);
        try
        {
            var session = (DocumentSession)typeof(MainWindow).GetField("_session", PrivateInstance)!.GetValue(opened)!;
            var camera = (NavigationCamera)typeof(MainWindow).GetField("_navigation", PrivateInstance)!.GetValue(opened)!;
            var tabs = (TabControl)opened.FindName("ViewTabs");
            var setup = BreezeBlockHouseShowcase.StreetView;
            Check(session.Document.ComponentFamilies.Count == 3 && session.Document.RenderSetup is not null &&
                  typeof(MainWindow).GetField("_filePath", PrivateInstance)!.GetValue(opened) is null,
                "Startup opens the breeze-block house, with its mesh families, as an untitled copy");
            Check(tabs.SelectedIndex == 6 && Math.Abs(camera.Position.Y - setup.CameraY) < 1e-3 &&
                  Math.Abs(camera.PitchDegrees - setup.PitchDegrees) < 1e-3,
                "Startup shows the Render tab from the sample's saved camera");
            var panel = (Guanacaste3D.Desktop.Controls.RenderPanel)opened.FindName("RenderPanelControl");
            var captured = panel.CaptureSetup(camera);
            Check(captured.Width == 1920 && captured.Height == 1080 && captured.DayOfYear == setup.DayOfYear &&
                  Math.Abs(captured.SolarHour - setup.SolarHour) < 1e-6 && captured.Look == setup.Look && !captured.Bloom &&
                  captured.Quality == setup.Quality,
                "The Render panel shows the sample's size, sun, quality and filters");
        }
        finally
        {
            typeof(MainWindow).GetField("_dirty", PrivateInstance)!.SetValue(opened, false);
            opened.Close();
        }
    }
}
