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
using Guanacaste3D.Desktop.Controls;

/// <summary>Runs real WPF controls and routed events in an isolated test window; never opens user documents.</summary>
internal static class InteractionChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MainWindow _window = null!;
    private static Canvas _canvas = null!;
    private static Point2 _lastPointer;

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
            ProjectFile.Save("artifacts/interaction-evidence/placed-elements.g3d", session.Document);
            Check(ProjectFile.Load("artifacts/interaction-evidence/placed-elements.g3d").Dimensions.Count == 1,
                "UI-created annotation survives native save and reopen");
            CaptureDemoToolTip();
            RenderJoinScene();
            RenderPreviewAndImportChecks();
            CheckAssociatedDocumentStartup();
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

    /// <summary>Opens each registered document kind through the same path used by Windows file associations.</summary>
    private static void CheckAssociatedDocumentStartup()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"g3d-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var projectPath = Path.Combine(folder, "startup.g3d");
            ProjectFile.Save(projectPath, BuildingDocument.Create("StartupProject"));
            var familyPath = Path.Combine(folder, "startup.g3df");
            FamilyFile.Save(familyPath, OpeningFamilyDocument.CreateStandard(OpeningKind.Door, "StartupFamily"));
            var templatePath = Path.Combine(folder, "startup.g3dt");
            TemplateFile.Save(templatePath, OfficialTemplates.CreateArchitecturalMetric());

            foreach (var path in new[] { projectPath, familyPath, templatePath })
            {
                var opened = new MainWindow(path);
                var loaded = (DocumentSession)typeof(MainWindow).GetField("_session", PrivateInstance)!.GetValue(opened)!;
                if (path == projectPath)
                    Check(loaded.Document.Name == "StartupProject", "Associated .g3d loads the project");
                else if (path == familyPath)
                    Check(loaded.Document.OpeningTypes.Any(type => type.FamilyName == "StartupFamily"),
                        "Associated .g3df imports the family");
                else
                    Check(loaded.Document.Levels.Count > 0 &&
                        typeof(MainWindow).GetField("_filePath", PrivateInstance)!.GetValue(opened) is null,
                        "Associated .g3dt creates an unsaved project");
                typeof(MainWindow).GetField("_dirty", PrivateInstance)!.SetValue(opened, false);
                opened.Close();
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Verifies that demonstrations require an explicit request, then captures the Door scene.</summary>
    private static void CaptureDemoToolTip()
    {
        var door = FindButtons(_window).First(button => button.Tag as string == "Door");
        Check(door.ToolTip is string && door.ContextMenu is { Items.Count: 1 },
            "Door hover remains a plain label and its demonstration requires the context menu");
        var tip = CommandDemoToolTip.CreateDemo("Door", door.ToolTip.ToString()!);
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
    /// <summary>Pumps the dispatcher until a condition holds or the time runs out.</summary>
    private static bool WaitFor(Func<bool> condition, int seconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            Pump();
            if (condition()) return true;
        }
        return condition();
    }

    /// <summary>
    /// Exercises the Render tab against the real window: the live preview appears and follows the shared camera, a model
    /// dropped on the window is imported through its dialog, linked, saved beside the project and restored when it is reopened.
    /// </summary>
    private static void RenderPreviewAndImportChecks()
    {
        var folder = Path.Combine(Path.GetTempPath(), "guanacaste-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // Earlier checks edited the project; mark it clean so opening the sample does not wait for the save prompt.
            SetField("_dirty", false);
            Invoke("OpenSample_Click", _window, new RoutedEventArgs());
            Pump();
            var tabs = (TabControl)_window.FindName("ViewTabs");
            tabs.SelectedIndex = 6;
            var panel = (FrameworkElement)_window.FindName("RenderPanelControl");
            var status = (TextBlock)panel.FindName("StatusText");
            var image = (Image)panel.FindName("PreviewImage");
            Check(WaitFor(() => image.Source is WriteableBitmap { PixelWidth: > 0 }, 60), "The Render tab shows a live ray-traced preview of the open project");
            Check(WaitFor(() => status.Text.Contains("muestra") || status.Text.Contains("lista"), 20), "The preview reports its progress in Spanish");

            byte[] Snapshot()
            {
                var bitmap = (WriteableBitmap)image.Source;
                var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
                bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                return pixels;
            }
            WaitFor(() => status.Text.Contains("lista"), 40);
            var before = Snapshot();
            var camera = Field<Guanacaste3D.Core.Navigation.NavigationCamera>("_navigation");
            camera.Position = new System.Numerics.Vector3(-8, 8, 3);
            camera.LookAt(new System.Numerics.Vector3(0, 0, 1));
            var navigator = panel.GetType().GetProperty("Navigator", PrivateInstance | System.Reflection.BindingFlags.Public)!.GetValue(panel)!;
            ((Action?)navigator.GetType().GetField("Changed", PrivateInstance)!.GetValue(navigator))?.Invoke();
            Check(WaitFor(() => !Snapshot().SequenceEqual(before), 40), "Moving the shared camera updates the live preview");

            // A mesh dropped on the window: the import dialog is accepted automatically, as the user would press Import.
            var model = Path.Combine(folder, "tree.obj");
            File.WriteAllLines(model, ["v 0 0 0", "v 2 0 0", "v 0 2 0", "v 0 0 3", "f 1 2 3", "f 1 3 4", "f 1 4 2", "f 2 4 3"]);
            var accept = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            accept.Tick += (_, _) =>
            {
                foreach (var dialog in Application.Current.Windows.OfType<ImportWindow>().Where(window => window.IsVisible))
                {
                    var button = (Button)dialog.FindName("ImportButton");
                    if (button.IsEnabled && button.Content is string text && text != "Cerrar")
                        dialog.GetType().GetMethod("Import_Click", PrivateInstance)!.Invoke(dialog, [dialog, new RoutedEventArgs()]);
                    else if (button.Content is "Cerrar") dialog.DialogResult = true;
                }
            };
            accept.Start();
            tabs.SelectedIndex = 1;
            var import = (Task)Invoke("ImportFilesAsync", (object)new[] { model })!;
            Check(WaitFor(() => import.IsCompleted, 60), "Importing a dropped file finishes");
            accept.Stop();
            var links = Field<object>("_links");
            var list = (System.Collections.IList)links.GetType().GetProperty("Links")!.GetValue(links)!;
            Check(list.Count == 1, "The dropped mesh is linked to the project");
            Check(Field<bool>("_dirty"), "Linking a model marks the project as changed");

            var path = Path.Combine(folder, "casa.g3d");
            SetField("_filePath", path);
            Check((bool)Invoke("SaveProject", false)!, "The project saves with a linked model");
            Check(File.Exists(path + ".links.json"), "Saving writes the links file beside the project");
            Invoke("OpenProjectPath", path);
            Check(WaitFor(() =>
            {
                links = Field<object>("_links");
                var reopened = (System.Collections.IList)links.GetType().GetProperty("Links")!.GetValue(links)!;
                var loaded = reopened.Count == 1 && (bool)links.GetType().GetMethod("IsLoaded")!.Invoke(links, [((Guanacaste3D.Core.Import.ModelLink)reopened[0]!).Id])!;
                return loaded;
            }, 30), "Reopening the project restores and reloads its linked model");
        }
        finally
        {
            SetField("_dirty", false);
            Directory.Delete(folder, recursive: true);
        }
    }

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
        _lastPointer = world;
        SetField("_placementPointer", (Point2?)world);
        Invoke("RenderPlan");
        Pump();
    }

    /// <summary>Calls the same placement transaction used by the mouse handler with deterministic coordinates.</summary>
    private static void ClickPlan()
    {
        Invoke("CommitOpeningPlacement", _lastPointer);
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
