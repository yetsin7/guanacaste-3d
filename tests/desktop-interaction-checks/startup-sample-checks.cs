using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

internal static partial class InteractionChecks
{
    /// <summary>
    /// Started without a document, the application shows the Home screen; its breeze-block house sample opens in the
    /// Render tab with the saved camera, sun and filters, and opening it hides the Home screen.
    /// </summary>
    private static void StartupSampleChecks()
    {
        var opened = new MainWindow(showStartScreen: true) { Width = 1440, Height = 900, WindowState = WindowState.Normal };
        try
        {
            var home = (StartScreen)opened.FindName("StartScreenControl");
            var host = (IStartScreenHost)opened;
            // Evidence for visual review: the Home screen on its samples page.
            opened.Show();
            ((RadioButton)home.FindName("SamplesTab")).IsChecked = true;
            Pump();
            Directory.CreateDirectory("artifacts/interaction-evidence");
            var picture = new RenderTargetBitmap((int)opened.ActualWidth, (int)opened.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            picture.Render(opened);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(picture));
            using (var stream = File.Create("artifacts/interaction-evidence/home-screen.png")) encoder.Save(stream);
            Check(home.Visibility == Visibility.Visible && host.Samples.Any(sample => sample.Id == "cobogo") &&
                  host.Samples.All(sample => Application.Current.TryFindResource(sample.TitleKey) is string),
                "Startup shows the Home screen with its samples");

            host.OpenSample(host.Samples.Single(sample => sample.Id == "cobogo"));
            var session = (DocumentSession)typeof(MainWindow).GetField("_session", PrivateInstance)!.GetValue(opened)!;
            var camera = (NavigationCamera)typeof(MainWindow).GetField("_navigation", PrivateInstance)!.GetValue(opened)!;
            var tabs = (TabControl)opened.FindName("ViewTabs");
            var setup = session.Document.RenderSetup
                ?? throw new InvalidOperationException("The breeze-block house sample has no saved render setup.");
            Check(home.Visibility == Visibility.Collapsed && session.Document.ComponentFamilies.Count >= 9 &&
                  session.Document.Sheets.Count == 6 &&
                  typeof(MainWindow).GetField("_filePath", PrivateInstance)!.GetValue(opened) is null,
                "The breeze-block house opens from Home, with its families and sheets, as an untitled copy");
            Check(tabs.SelectedIndex == 6 && Math.Abs(camera.Position.Y - setup.CameraY) < 1e-3 &&
                  Math.Abs(camera.PitchDegrees - setup.PitchDegrees) < 1e-3,
                "The sample opens on the Render tab from its saved camera");
            var panel = (RenderPanel)opened.FindName("RenderPanelControl");
            var captured = panel.CaptureSetup(camera);
            Check(captured.Width == 1920 && captured.Height == 1080 && captured.DayOfYear == setup.DayOfYear &&
                  Math.Abs(captured.SolarHour - setup.SolarHour) < 1e-6 && captured.Look == setup.Look && !captured.Bloom &&
                  captured.Quality == setup.Quality,
                "The Render panel shows the sample's size, sun, quality and filters");

            // A recent list in a scratch folder: it keeps the newest first and forgets on request.
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"g3d-recents-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(folder);
            // RecentDocuments now prunes missing paths: create real files to exercise ordering and removal.
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "a.g3d"), "test");
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "b.g3d"), "test");
            var recents = new RecentDocuments(folder);
            recents.Add(System.IO.Path.Combine(folder, "a.g3d"));
            recents.Add(System.IO.Path.Combine(folder, "b.g3d"));
            recents.Add(System.IO.Path.Combine(folder, "a.g3d"));
            var listed = recents.Load();
            recents.Remove(System.IO.Path.Combine(folder, "b.g3d"));
            Check(listed.Count == 2 && listed[0].Path.EndsWith("a.g3d", StringComparison.OrdinalIgnoreCase) &&
                  recents.Load().Count == 1, "Recent projects keep the newest first, without duplicates, and can be removed");
            System.IO.Directory.Delete(folder, recursive: true);
        }
        finally
        {
            typeof(MainWindow).GetField("_dirty", PrivateInstance)!.SetValue(opened, false);
            opened.Close();
        }
    }
}
