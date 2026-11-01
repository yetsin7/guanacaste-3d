using System.IO;
using System.Numerics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Desktop;

/// <summary>Exercises actual modeling controls against throwaway documents without accessing user projects.</summary>
internal static partial class WorkspaceChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MainWindow _window = null!;
    private static int _count;
    private static readonly string Evidence = Path.GetFullPath("artifacts/model-workspace-evidence");

    /// <summary>Runs navigation, movement, material-isolation, and compact-layout regressions on a WPF dispatcher.</summary>
    [STAThread]
    private static int Main(string[] arguments)
    {
        var application = new App();
        application.InitializeComponent();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _window = new MainWindow { WindowState = WindowState.Normal, Width = 1440, Height = 950 };
        Directory.CreateDirectory(Evidence);
        try
        {
            _window.Show();
            Pump();
            if (arguments.Contains("--flight-only"))
            {
                FlightNavigationChecks();
                Console.WriteLine($"Flight workspace checks passed: {_count} assertions.");
                return 0;
            }
            ProjectionChecks();
            NavigationChecks();
            OrbitInteractionChecks();
            PointerGestureChecks();
            FlightNavigationChecks();
            StairWorkspaceChecks();
            OpeningMovementChecks();
            GeneralMovementChecks();
            SlabVoidChecks();
            MaterialChecks();
            ReferenceNavigationChecks();
            ReferenceGraphicsStateChecks();
            Console.WriteLine($"Model workspace checks passed: {_count} assertions.");
            return 0;
        }
        catch (Exception exception)
        {
            SaveBitmap("failure.png");
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            SetField("_dirty", false);
            _window.Close();
            application.Shutdown();
        }
    }

    /// <summary>Installs a new isolated document and waits for its real model visuals.</summary>
    private static DocumentSession UseDocument(Action<DocumentSession, Guid> populate)
    {
        Invoke("SetWallTool", false);
        Invoke("SelectExtendedElement", null, null, null, null, null, null);
        var session = new DocumentSession(BuildingDocument.Create("Model workspace regression"));
        var level = session.Document.Levels[0].Id;
        populate(session, level);
        SetField("_session", session);
        SetField("_activeLevelId", level);
        SetField("_selectedWallId", null);
        SetField("_selectedOpeningId", null);
        SetField("_picker", null);
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 1;
        Invoke("RefreshAll");
        WaitFor(() => !Field<bool>("_modelDirty"));
        return session;
    }

    /// <summary>Aims the shared camera and returns the exact pixel of a world-space test target.</summary>
    private static Point Aim(Vector3 target, Vector3? position = null)
    {
        var camera = Field<NavigationCamera>("_navigation");
        camera.Position = position ?? target + new Vector3(0, -10, 5);
        camera.LookAt(target);
        Invoke("UpdateModelCamera");
        Pump();
        return Pixel(target);
    }

    /// <summary>Projects world coordinates through the same camera and surface dimensions used by input handling.</summary>
    private static Point Pixel(Vector3 world)
    {
        var surface = (FrameworkElement)_window.FindName("ModelSurface");
        var pixel = Field<NavigationCamera>("_navigation").Project(world, (float)surface.ActualWidth, (float)surface.ActualHeight)
            ?? throw new InvalidOperationException("The regression target is behind the camera.");
        return new Point(pixel.X, pixel.Y);
    }

    /// <summary>Selects an element through the production 3D ray-picking entry point.</summary>
    private static void Select(Point pixel) => Invoke("HandleViewClick", pixel, _window.FindName("ModelSurface"));

    /// <summary>Waits for asynchronous scene publication while processing real WPF layout and dispatcher work.</summary>
    private static void WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (!condition() && DateTime.UtcNow < deadline) Pump();
        Check(condition(), "Asynchronous workspace state completes within its deadline");
    }

    /// <summary>Processes a bounded dispatcher slice without blocking background geometry completion.</summary>
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Writes the actual composed window so layout and visual behavior can be reviewed.</summary>
    private static void SaveBitmap(string name)
    {
        _window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)_window.ActualWidth, (int)_window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(Evidence, name));
        encoder.Save(stream);
    }

    /// <summary>Invokes a production handler, supplying omitted optional arguments with their declared defaults.</summary>
    private static object? Invoke(string name, params object?[] arguments)
    {
        var method = typeof(MainWindow).GetMethod(name, PrivateInstance)!;
        var parameters = method.GetParameters();
        var supplied = parameters.Select((parameter, index) => index < arguments.Length ? arguments[index] : parameter.DefaultValue).ToArray();
        return method.Invoke(_window, supplied);
    }

    /// <summary>Reads private test state without exposing automation hooks in the shipping application.</summary>
    private static T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, PrivateInstance)!.GetValue(_window)!;

    /// <summary>Sets isolated selection and lifecycle state without writing user configuration or documents.</summary>
    private static void SetField(string name, object? value) => typeof(MainWindow).GetField(name, PrivateInstance)!.SetValue(_window, value);

    /// <summary>Fails immediately on a broken acceptance condition and records each successful assertion.</summary>
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _count++;
        Console.WriteLine("OK: " + description);
    }
}
