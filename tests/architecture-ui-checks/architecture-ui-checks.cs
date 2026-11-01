using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// Drives the real Architecture ribbon and main window: the layout of the tab, that every command does something
/// (none falls through to the "planned" message), and that the face, host and automatic commands change the model.
/// </summary>
internal static class ArchitectureUiChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MainWindow _window = null!;

    [STAThread]
    private static int Main()
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        var application = new App();
        application.InitializeComponent();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        CheckStartupArguments();
        _window = new MainWindow();
        Check(_window.WindowState == WindowState.Maximized, "The main workspace starts maximized");
        _window.Width = 1920;
        _window.Height = 1040;
        _window.WindowState = WindowState.Normal;
        try
        {
            _window.Show();
            Pump();
            IconResourceChecks.Run(Check);
            CheckLayout();
            CheckCompactWindowLayout();
            CheckEveryCommandResponds();
            CheckFaceAndHostCommands();
            CheckShowcaseRenders();
            CheckInPlaceFamilyPlans();
            CheckPresentationCommands();
            Console.WriteLine("Architecture UI checks passed.");
            Environment.ExitCode = 0;
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAILED: " + exception.Message);
            Environment.ExitCode = 1;
            return 1;
        }
        finally
        {
            Console.Out.Flush();
            // Background warm-up threads keep the process alive after shutdown, so leave explicitly.
            Environment.Exit(Environment.ExitCode);
        }
    }

    private static string Status() => ((TextBlock)_window.FindName("StatusText")).Text;

    /// <summary>Confirms that shell noise cannot be mistaken for a project while registered documents still open.</summary>
    private static void CheckStartupArguments()
    {
        Check(ResolveStartupPath(["Guanacaste3D.Desktop.exe"]) is null,
            "The executable path is ignored as a startup document");
        Check(ResolveStartupPath(["--background", "unrelated.txt"]) is null,
            "Unrelated shell arguments are ignored during startup");
        Check(ResolveStartupPath(["--open", @"C:\Models\House.G3D"]) == @"C:\Models\House.G3D",
            "A native project argument is preserved");
        Check(ResolveStartupPath([@"C:\Models\Legacy.g3d.json"]) == @"C:\Models\Legacy.g3d.json",
            "A legacy project argument is preserved");
    }

    /// <summary>Invokes the internal startup boundary without widening the production API.</summary>
    private static string? ResolveStartupPath(string[] arguments)
    {
        var resolver = typeof(App).Assembly.GetType("Guanacaste3D.Desktop.StartupDocumentResolver")!;
        var method = resolver.GetMethod("FindSupportedPath", BindingFlags.Static | BindingFlags.Public)!;
        return (string?)method.Invoke(null, [arguments]);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    /// <summary>The tab has the panels, columns and split buttons of the reference layout.</summary>
    private static void CheckLayout()
    {
        var ribbon = (ArchitectureRibbon)_window.FindName("ArchitectureRibbonControl");
        var tags = Descendants<Button>(ribbon).Select(button => button.Tag as string).Where(tag => tag is not null).ToList();
        string[] expected =
        [
            "Modify", "Wall", "Door", "Window", "Component", "Column", "Roof", "Ceiling", "Floor", "CurtainSystem", "CurtainGrid",
            "Mullion", "Railing", "Ramp", "Stair", "ModelText", "ModelLine", "ModelGroup", "Room", "RoomSeparator", "TagRoom",
            "Area", "AreaBoundary", "TagArea", "FaceOpening", "ShaftOpening", "WallOpening", "VerticalOpening", "DormerOpening",
            "Level", "Grid", "SetWorkPlane", "ShowWorkPlane", "ReferencePlane", "Viewer"
        ];
        Check(expected.SequenceEqual(tags.Where(tag => expected.Contains(tag))), "The ribbon lists the reference commands in the reference order");
        var architectureViewport = Descendants<ScrollViewer>(ribbon).Single();
        Check(architectureViewport.ScrollableWidth < 1,
            $"Every Architecture panel fits a 1920 px window (overflow: {architectureViewport.ScrollableWidth:F1} px)");
        var splitButtons = Descendants<Button>(ribbon).Count(button => Guanacaste3D.Desktop.Controls.RibbonButton.GetHasMenu(button));
        Check(splitButtons >= 14, $"Split buttons carry a dropdown arrow ({splitButtons})");
        var titles = Descendants<TextBlock>(ribbon).Select(block => block.Text).ToList();
        foreach (var title in new[] { "Seleccionar", "Construir", "Circulación", "Modelo", "Habitación y área", "Hueco", "Referencia", "Plano de trabajo" })
            Check(titles.Any(text => text.StartsWith(title, StringComparison.Ordinal)), $"Panel '{title}' is present");
        SaveBitmap("artifacts/interaction-evidence/architecture-ribbon.png");
    }

    /// <summary>Verifies that narrow workspaces expose every ribbon group without a visible scroll bar (wheel panning or fit-to-width).</summary>
    private static void CheckCompactWindowLayout()
    {
        _window.Width = 1024;
        _window.Height = 720;
        Pump();
        SaveBitmap("artifacts/interaction-evidence/architecture-ribbon-overflow.png");
        var tabs = (TabControl)_window.FindName("RibbonTabControl");
        for (var index = 0; index < tabs.Items.Count; index++)
        {
            if (tabs.Items[index] is not TabItem { Visibility: Visibility.Visible }) continue;
            tabs.SelectedIndex = index;
            Pump();
            if (tabs.SelectedContent is not DependencyObject content) continue;
            var viewer = Descendants<ScrollViewer>(content).FirstOrDefault();
            Check(viewer is not null, $"Ribbon tab {index} uses the shared overflow viewport");
            Check(viewer!.ActualWidth <= _window.ActualWidth,
                $"Ribbon tab {index} stays inside the compact window");
            if (viewer.ScrollableWidth <= 0) continue;
            Check(viewer.ComputedHorizontalScrollBarVisibility != Visibility.Visible,
                $"Ribbon tab {index} shows no scroll bar even when it overflows");
            viewer.ScrollToHorizontalOffset(viewer.ScrollableWidth);
            Pump();
            Check(Math.Abs(viewer.HorizontalOffset - viewer.ScrollableWidth) < 1,
                $"Ribbon tab {index} can reach its final command group");
        }
        tabs.SelectedIndex = 0;
        _window.Width = 1920;
        _window.Height = 1040;
        Pump();
    }

    /// <summary>Every command in the enum must act: none may end on the "will be added later" message.</summary>
    private static void CheckEveryCommandResponds()
    {
        // Commands that open modal dialogs must not run in this non-interactive sweep.
        // RoomSeparatorCurve calls PromptWindow.Ask for radius/start/sweep and otherwise blocks CI.
        var modal = new[] { ArchitectureCommand.Levels, ArchitectureCommand.SetWorkPlane, ArchitectureCommand.WorkPlaneByLevel,
            ArchitectureCommand.OpeningTypes, ArchitectureCommand.ApplyWallType, ArchitectureCommand.RoomSeparatorCurve };
        Check(typeof(MainWindow).GetMethod("BeginCurvedRoomSeparatorTool", PrivateInstance) is not null,
            "Curved room separators have a dedicated parameter-dialog entry point");
        var route = typeof(MainWindow).GetMethod("ArchitectureRibbon_CommandInvoked", PrivateInstance)!;
        var unanswered = new List<ArchitectureCommand>();
        foreach (var command in Enum.GetValues<ArchitectureCommand>().Except(modal))
        {
            SetStatus("");
            route.Invoke(_window, [null, new ArchitectureCommandEventArgs(command)]);
            Pump();
            if (Status().Contains("se incorporará", StringComparison.OrdinalIgnoreCase)) unanswered.Add(command);
            Invoke("CancelStructuralTool");
            foreach (var popup in Descendants<ContextMenu>(_window).ToList()) popup.IsOpen = false;
            foreach (var extra in Application.Current.Windows.OfType<Window>().Where(window => window.GetType().Name is "ArchitecturalLibraryWindow" or "ElementEditorWindow").ToList()) extra.Close();
        }
        Check(unanswered.Count == 0, "Every Architecture command responds" +
            (unanswered.Count == 0 ? "" : ": " + string.Join(", ", unanswered)));
    }

    private static void CheckFaceAndHostCommands()
    {
        var session = new DocumentSession(ArchitecturalLibrary.CreateProject("UI"));
        SetField("_session", session);
        var level = session.Document.Levels[0].Id;
        SetField("_activeLevelId", level);
        session.AddWallsAroundBoundary(level, [new(0, 0), new(8, 0), new(8, 6), new(0, 6)], BuiltInWallTypes.Basic200Id);
        Execute(ArchitectureCommand.RoomAutomatic);
        Check(session.Document.Rooms.Count == 1, "Automatic rooms create the room enclosed by the walls");

        SetField("_selectedRoomId", (Guid?)session.Document.Rooms.Single().Id);
        var before = session.Document.Walls.Count;
        Execute(ArchitectureCommand.WallByFace);
        Check(session.Document.Walls.Count == before + 4, "Wall by face builds walls around the selected room");
        Invoke("SlabByFace", ArchitecturalLibrary.SlabPresets.First(preset => preset.Name == "Hip Roof 25°"), 0.5);
        Check(session.Document.Floors.Any(floor => floor.Shape == RoofShape.Hip), "Roof by face creates a hip roof over the room");

        SetField("_selectedFloorId", (Guid?)session.Document.Floors.Single().Id);
        Execute(ArchitectureCommand.SlabEdge);
        Check(session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.SlabEdge) == 4,
            "Slab edge runs along the selected slab");

        var stair = session.AddStructuralElement(level, StructuralElementKind.Stair, "Stair", new(1, 1), new(1, 5));
        SetField("_selectedStructuralId", (Guid?)stair);
        Execute(ArchitectureCommand.RailingOnHost);
        Check(session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.Railing) == 2,
            "Guards are placed on both sides of the selected stair");

        Execute(ArchitectureCommand.WallOpening);
        Check(_window.FindName("StatusText") is TextBlock && GetField<OpeningKind?>("_placingOpening") == OpeningKind.Window &&
              session.Document.OpeningTypes.Any(type => type.Operation == OpeningOperation.Void),
            "Wall opening arms placement of a plain hole");
    }

    private static void CheckShowcaseRenders()
    {
        SetField("_session", new DocumentSession(ArchitecturalShowcase.Create()));
        SetField("_activeLevelId", ((DocumentSession)GetField<DocumentSession>("_session")).Document.Levels[0].Id);
        Invoke("CancelStructuralTool");
        Invoke("FitPlan_Click", _window, new RoutedEventArgs());
        Pump();
        SaveBitmap("artifacts/interaction-evidence/architecture-showcase-plan.png");
        var canvas = (Canvas)_window.FindName("PlanCanvas");
        Check(canvas.Children.OfType<System.Windows.Shapes.Polygon>().Count() > 40 &&
              canvas.Children.OfType<System.Windows.Shapes.Line>().Count() > 5,
            "The showcase house draws roofs, columns, guards, curtain wall and annotations in plan");
    }

    /// <summary>Checks that a family alone is drawn in the interactive plan and on a drawing sheet.</summary>
    private static void CheckInPlaceFamilyPlans()
    {
        var session = new DocumentSession(BuildingDocument.Create("Family views"));
        var familyId = session.CreateInPlaceFamily(BimCategoryCatalog.GenericModelsId, "Model block");
        session.AddInPlaceExtrusion(familyId, InPlaceFamilyRules.HorizontalPlane(0),
            [new Point2(0, 0), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1)], 0, 2);
        SetField("_session", session);
        SetField("_activeLevelId", session.Document.Levels[0].Id);
        Invoke("RenderPlan");
        var plan = (Canvas)_window.FindName("PlanCanvas");
        Check(plan.Children.OfType<System.Windows.Shapes.Line>().Any(line => Equals(line.Tag, familyId)),
            "An in-place family appears in the interactive floor plan");
        SetField("_selectedSheetId", (Guid?)session.AddSheet(SheetPaperSize.A3));
        Invoke("RenderSheet");
        var sheet = (Canvas)_window.FindName("SheetCanvas");
        Check(sheet.Children.OfType<Canvas>().Single().Children.OfType<System.Windows.Shapes.Polyline>().Count() >= 4,
            "A family-only project appears in the drawing sheet viewport");
    }

    /// <summary>Exercises presentation commands against a project whose only geometry belongs to a family.</summary>
    private static void CheckPresentationCommands()
    {
        Invoke("MarkSceneDirty");
        Check(Invoke("HasSceneContent") is true, "A family-only project is recognized as a renderable scene");
        var sceneTask = (Task<Guanacaste3D.Core.Rendering.RenderScene?>)Invoke("GetSceneAsync")!;
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (!sceneTask.IsCompleted && DateTime.UtcNow < timeout) Pump();
        Check(sceneTask.IsCompletedSuccessfully && sceneTask.Result is { TriangleCount: > 0 },
            "A family-only project builds the live and final render scene");
        Check(Invoke("ExecuteApplicationCommand", "Materials") is true, "Materials routes to the project material editor");
        Pump();
        var panel = (RenderPanel)_window.FindName("RenderPanelControl");
        Check(((StackPanel)panel.FindName("MaterialRows")).Children.Count > 0,
            "The material command reveals the populated project material controls");
        Check(Invoke("ExecuteApplicationCommand", "Walkthrough") is true, "Walkthrough routes to camera marks");
        Pump();
        Check(((Expander)panel.FindName("TourExpander")).IsExpanded, "The walkthrough command expands its camera editor");
        Check(Invoke("ExecuteApplicationCommand", "Camera") is true, "Camera opens the navigable model view");
        Check(Invoke("ExecuteApplicationCommand", "ResetCamera") is true, "Reset camera frames the current project");
        var clay = (CheckBox)_window.FindName("XRayCheck");
        var previous = clay.IsChecked;
        Check(Invoke("ExecuteApplicationCommand", "VisualStyle") is true && clay.IsChecked != previous,
            "Visual style switches service visibility while preserving project colors");
    }

    private static void Execute(ArchitectureCommand command)
    {
        typeof(MainWindow).GetMethod("ExecuteArchitectureCommand", PrivateInstance)!.Invoke(_window, [command]);
        Pump();
    }

    private static void SetStatus(string text) => ((TextBlock)_window.FindName("StatusText")).Text = text;

    private static void Pump()
    {
        _window.UpdateLayout();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void SaveBitmap(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bitmap = new RenderTargetBitmap((int)_window.ActualWidth, (int)_window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static object? Invoke(string name, params object[] arguments) =>
        typeof(MainWindow).GetMethod(name, PrivateInstance)!.Invoke(_window, arguments);

    private static T GetField<T>(string name) => (T)typeof(MainWindow).GetField(name, PrivateInstance)!.GetValue(_window)!;

    private static void SetField(string name, object? value) =>
        typeof(MainWindow).GetField(name, PrivateInstance)!.SetValue(_window, value);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("OK: " + message);
    }
}
