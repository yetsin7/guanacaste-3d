using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;

namespace HouseFlowChecks;

/// <summary>
/// Drives the real main window the way a person does: ribbon buttons are pressed through UI Automation and the plan is
/// clicked in model coordinates. Every private-member access of the app lives in this one class, so a step list never
/// needs reflection and a renamed member breaks one place only.
/// </summary>
internal sealed class HouseDriver
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly MainWindow _window;

    private HouseDriver(MainWindow window) => _window = window;

    /// <summary>Opens an isolated window on a throwaway project; nothing touches the documents or recovery files of the user.</summary>
    public static HouseDriver Start()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "g3d-house-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("G3D_RECOVERY_DIR", Path.Combine(scratch, "recovery"));
        Environment.SetEnvironmentVariable("G3D_LOG_DIR", Path.Combine(scratch, "logs"));
        Environment.SetEnvironmentVariable("G3D_NO_EXPLORER", "1");
        var application = new App();
        application.InitializeComponent();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow { Width = 1440, Height = 1000, WindowState = WindowState.Normal };
        window.Show();
        window.Activate();
        var driver = new HouseDriver(window);
        driver.Pump();
        return driver;
    }

    /// <summary>Values a step records before acting so its expectation can compare before and after.</summary>
    public Dictionary<string, int> Marks { get; } = [];

    /// <summary>The live document behind the window.</summary>
    public DocumentSession Session => Get<DocumentSession>("_session");

    /// <summary>Whether the wall tool is armed, as the window itself reports it.</summary>
    public bool DrawingWall => Get<bool>("_drawingWall");

    /// <summary>Whether a door or window is attached to the cursor.</summary>
    public bool PlacingOpening => Get<object?>("_placingOpening") is not null;

    /// <summary>Status bar text, which is what the person reads after each action.</summary>
    public string Status => ((TextBlock)_window.FindName("StatusText")).Text;

    /// <summary>Presses a ribbon button by its command name, through UI Automation so a disabled or hidden button fails.</summary>
    public void PressRibbon(string command)
    {
        var ribbon = (DependencyObject)_window.FindName("ArchitectureRibbonControl");
        var button = Descendants(ribbon).OfType<Button>().FirstOrDefault(item => item.Tag as string == command)
            ?? throw new InvalidOperationException($"The ribbon has no button for '{command}'.");
        if (!button.IsVisible || !button.IsEnabled)
            throw new InvalidOperationException($"The '{command}' ribbon button is not usable (visible={button.IsVisible}, enabled={button.IsEnabled}).");
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();
        Pump();
    }

    /// <summary>Sets a check box of the wall options the way a click would.</summary>
    public void SetCheck(string name, bool value) => ((CheckBox)_window.FindName(name)).IsChecked = value;

    /// <summary>Sets the plan zoom so model metres map to a predictable number of pixels.</summary>
    public void ZoomPlan(double pixelsPerMetre)
    {
        Set("_scale", pixelsPerMetre);
        Set("_panOffset", new Point(0, 0));
        Call("RenderPlan");
        Pump();
    }

    /// <summary>Clicks the plan at a model point while the wall tool is armed.</summary>
    public void ClickWall(Point2 world)
    {
        Call("HandleWallPlacementClick", Call("WorldToCanvas", world)!);
        Pump();
    }

    /// <summary>Lets layout and queued dispatcher work finish, like the pause between two human actions.</summary>
    public void Pump()
    {
        _window.UpdateLayout();
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Closes the window without saving.</summary>
    public void Close() => _window.Close();

    private T Get<T>(string field) => (T)typeof(MainWindow).GetField(field, Private)!.GetValue(_window)!;
    private void Set(string field, object value) => typeof(MainWindow).GetField(field, Private)!.SetValue(_window, value);
    private object? Call(string method, params object[] arguments) =>
        typeof(MainWindow).GetMethod(method, Private)!.Invoke(_window, arguments);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
