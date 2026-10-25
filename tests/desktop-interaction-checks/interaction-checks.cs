using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;

/// <summary>Runs real WPF controls and routed events in an isolated test window; never opens user documents.</summary>
internal static class InteractionChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MainWindow _window = null!;
    private static Canvas _canvas = null!;

    /// <summary>Builds a throwaway project, exercises controls, writes a real WPF bitmap, and exits.</summary>
    [STAThread]
    private static int Main()
    {
        var application = new App();
        application.InitializeComponent();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _window = new MainWindow { Width = 1440, Height = 1000, WindowState = WindowState.Normal };
        try
        {
            _window.Show();
            _window.Activate();
            Pump();
            _canvas = (Canvas)_window.FindName("PlanCanvas");
            var session = Field<DocumentSession>("_session");
            var level = session.Document.Levels[0].Id;
            var wallId = session.AddWall(level, new(-6, 0), new(6, 0));
            session.AddWall(level, new(-6, 0), new(-6, 4));
            Invoke("Door_Click", _window, new RoutedEventArgs());
            Check(session.Document.Openings.Count == 0, "Door command arms placement without insertion");
            Move(new(-2.55, 0));
            Check(session.Document.Openings.Count == 0 && _canvas.Children.Cast<UIElement>().Any(item => item.Opacity == 0.65),
                "Pointer movement renders a ghost without modifying the document");
            ClickPlan();
            Check(session.Document.Openings.Count == 1, "Placement transaction commits the hosted preview");
            var door = session.Document.Openings.Single();
            Move(new(-2.55, 0));
            ClickPlan();
            Check(session.Document.Openings.Count == 1, "Overlapping placement is rejected");
            Invoke("Window_Click", _window, new RoutedEventArgs());
            Check(session.Document.Openings.Count == 1, "Window command also avoids immediate insertion");
            Move(new(3, 0));
            ClickPlan();
            Check(session.Document.Openings.Count == 2, "Window placement commits to the resolved wall");
            Invoke("SetWallTool", false);
            SetField("_selectedOpeningId", (Guid?)door.Id);
            SetField("_selectedWallId", (Guid?)wallId);
            Invoke("RenderPlan");
            Pump();
            var editor = _canvas.Children.OfType<Button>().First(button =>
                button.Tag is PlanDimension { Kind: PlanDimensionKind.OpeningStart } && button.Content!.ToString()!.Contains('m'));
            editor.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            var inline = _canvas.Children.OfType<TextBox>().Single(box => box.Tag is PlanDimension { Kind: PlanDimensionKind.OpeningStart });
            Check(inline.IsKeyboardFocusWithin && session.Document.Openings.Single(item => item.Id == door.Id).Begin < 5,
                "Clicking the number opens a focused in-place editor without moving the door yet");
            PressKey(inline, Key.Escape);
            Check(!_canvas.Children.OfType<TextBox>().Any(box => box.Tag is PlanDimension),
                "Escape closes the in-place editor and leaves the door where it was");
            editor = _canvas.Children.OfType<Button>().First(button =>
                button.Tag is PlanDimension { Kind: PlanDimensionKind.OpeningStart } && button.Content!.ToString()!.Contains('m'));
            editor.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            inline = _canvas.Children.OfType<TextBox>().Single(box => box.Tag is PlanDimension);
            inline.Text = "5 m";
            PressKey(inline, Key.Enter);
            Check(Math.Abs(session.Document.Openings.Single(item => item.Id == door.Id).Begin - 5) < 1e-9,
                "Clicking the rendered number and entering five meters moves the door exactly");
            var arrow = _canvas.Children.OfType<Button>().First(button =>
                button.Tag is PlanDimension { Kind: PlanDimensionKind.OpeningStart } && button.Content!.ToString() == "↗");
            arrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(session.Document.Dimensions.Count == 1, "Promotion arrow creates one persistent annotation");
            Invoke("SetWallTool", false);
            SetField("_selectedOpeningId", null);
            SetField("_selectedWallId", null);
            Invoke("RenderPlan");
            Check(_canvas.Children.OfType<Button>().Any(button => button.Tag is PlanDimension dimension && dimension.Id != Guid.Empty),
                "Permanent annotation remains visible after selection clears");
            Invoke("Column_Click", _window, new RoutedEventArgs());
            Move(new(0, 3));
            Check(session.Document.Columns.Count == 0, "Column preview does not create a column");

            Invoke("SetWallTool", false);
            Invoke("RenderPlan");
            Pump();
            Directory.CreateDirectory("artifacts/interaction-evidence");
            SetField("_scale", 60.0);
            Invoke("RenderPlan");
            Pump();
            SaveBitmap("artifacts/interaction-evidence/plan-dimensions.png");
            ProjectFile.Save("artifacts/interaction-evidence/placed-elements.g3", session.Document);
            Check(ProjectFile.Load("artifacts/interaction-evidence/placed-elements.g3").Dimensions.Count == 1,
                "UI-created annotation survives native save and reopen");
            CaptureDemoToolTip();
            RenderJoinScene();
            Console.WriteLine("Desktop interaction checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("artifacts/interaction-evidence");
            SaveBitmap("artifacts/interaction-evidence/failure.png");
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

    /// <summary>Opens the Door button's rich tooltip, lets its scene play, and captures it mid-animation.</summary>
    private static void CaptureDemoToolTip()
    {
        var door = FindButtons(_window).First(button => button.Tag as string == "Door");
        Check(door.ToolTip is ToolTip { Content: StackPanel }, "Door button carries a rich tooltip with a demonstration");
        var tip = (ToolTip)door.ToolTip;
        tip.PlacementTarget = door;
        tip.IsOpen = true;
        for (var frame = 0; frame < 30; frame++) Pump();
        var content = (FrameworkElement)tip.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create("artifacts/interaction-evidence/door-demo-tooltip.png")) encoder.Save(stream);
        tip.IsOpen = false;
        Check(content.ActualHeight > 150, "Door demonstration scene is laid out inside the tooltip");
    }

    /// <summary>Finds every button in the window's logical tree.</summary>
    private static IEnumerable<Button> FindButtons(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Button button) yield return button;
            foreach (var nested in FindButtons(child)) yield return nested;
        }
    }

    /// <summary>
    /// Renders an isolated plan with an L of mixed thickness, a tee, a crossing, a three-wall corner,
    /// a single door and a pair of doors, so join cleanup and door symbols can be reviewed together.
    /// </summary>
    private static void RenderJoinScene()
    {
        var scene = new DocumentSession(BuildingDocument.Create());
        var level = scene.Document.Levels[0].Id;
        var host = scene.AddWall(level, new(0, 0), new(6, 0));
        scene.AddWall(level, new(6, 0), new(6, 4), 0.3);
        scene.AddWall(level, new(0, 0), new(0, 4));
        var top = scene.AddWall(level, new(0, 4), new(6, 4));
        scene.AddWall(level, new(0, 4), new(-3, 4));
        scene.AddWall(level, new(3, 0), new(3, 4), 0.15);
        scene.AddWall(level, new(1.5, 2), new(4.5, 2), 0.15);
        scene.AddOpening(host, OpeningKind.Door, 1.5);
        scene.AddOpening(top, OpeningKind.Door, 4.5, openingTypeId: BuiltInOpeningTypes.DoubleDoor1500Id);
        SetField("_session", scene);
        SetField("_activeLevelId", level);
        Invoke("SetWallTool", false);
        Invoke("FitPlan_Click", _window, new RoutedEventArgs());
        Pump();
        SaveBitmap("artifacts/interaction-evidence/wall-joins-and-doors.png");
        // A close-up at 1:20-like zoom, centred on the single door, shows linings, casings and the leaf.
        SetField("_scale", 420.0);
        SetField("_panOffset", new Point(-1.5 * 420, 0.35 * 420));
        Invoke("RenderPlan");
        Pump();
        SaveBitmap("artifacts/interaction-evidence/door-detail.png");
        Check(_canvas.Children.OfType<System.Windows.Shapes.Polygon>().Count(polygon => polygon.Tag is Guid) > 0,
            "Join and door review scene renders");
    }

    /// <summary>Routes a real key-down event through the target control, as the keyboard would.</summary>
    private static void PressKey(UIElement target, Key key)
    {
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key)
            { RoutedEvent = Keyboard.KeyDownEvent });
        Pump();
    }

    /// <summary>Injects model coordinates into preview state; OS pointer automation is deliberately excluded.</summary>
    private static void Move(Point2 world)
    {
        SetField("_placementPointer", (Point2?)world);
        Invoke("RenderPlan");
        Pump();
    }

    /// <summary>Calls the same placement transaction used by the mouse handler with deterministic coordinates.</summary>
    private static void ClickPlan()
    {
        Invoke("CommitOpeningPlacement", Field<Point2?>("_placementPointer")!.Value);
        Pump();
    }

    /// <summary>Flushes layout and queued WPF input without a fixed sleep.</summary>
    private static void Pump()
    {
        _window.UpdateLayout();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Captures the actual composed WPF window for visual review.</summary>
    private static void SaveBitmap(string path)
    {
        var bitmap = new RenderTargetBitmap((int)_window.ActualWidth, (int)_window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>Invokes an existing UI handler without introducing a production automation API.</summary>
    private static object? Invoke(string name, params object[] arguments) =>
        typeof(MainWindow).GetMethod(name, PrivateInstance)!.Invoke(_window, arguments);

    /// <summary>Reads isolated test state from the actual window instance.</summary>
    private static T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, PrivateInstance)!.GetValue(_window)!;

    /// <summary>Sets selection or teardown state without editing any user document.</summary>
    private static void SetField(string name, object? value) => typeof(MainWindow).GetField(name, PrivateInstance)!.SetValue(_window, value);

    /// <summary>Fails immediately when an observable UI behavior differs from the contract.</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("OK: " + message);
    }
}
