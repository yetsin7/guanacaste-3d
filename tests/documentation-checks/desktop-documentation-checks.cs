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

internal static partial class DocumentationChecks
{
    private static MainWindow _window = null!;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>Uses a throwaway real WPF window and deterministic model coordinates, without OS pointer automation.</summary>
    private static void CheckDesktop()
    {
        var application = new App();
        application.InitializeComponent();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _window = new MainWindow { WindowState = WindowState.Normal, Width = 1400, Height = 900 };
        try
        {
            _window.Show();
            Pump();
            var session = new DocumentSession(BuildingDocument.Create("Coordinated documentation"));
            var level = session.Document.Levels[0].Id;
            var wall = session.AddWall(level, new(0, 0), new(6, 0));
            session.AddWall(level, new(6, 0), new(6, 4));
            session.AddOpening(wall, OpeningKind.Window, 3, sill: 1, height: 1);
            var column = session.AddColumn(level, new(1, 2));
            var footing = session.AddStructuralElement(level, StructuralElementKind.IsolatedFoundation, "F1", new(1, 2), new(1, 2));
            SetField("_session", session);
            SetField("_activeLevelId", level);
            Invoke("RefreshAll");
            Invoke("FitPlan_Click", _window, new RoutedEventArgs());
            Pump();
            Invoke("ExecuteApplicationCommand", "Beam");
            ClickAt(new(1, 2));
            ClickAt(new(5, 3));
            Check(session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.Beam) == 1,
                "The Beam command creates one individual diagonal beam instead of a beam system");
            var electrical = session.AddElectricalDevice(level, ElectricalDeviceKind.Switch, "S1", new(4, 0));
            SetField("_selectedElectricalId", (Guid?)electrical);
            Invoke("ExecuteApplicationCommand", "Grid");
            Check(Field<Guid?>("_selectedElectricalId") is null,
                "Starting a datum tool clears stale electrical selection from the properties palette");
            Check(Field<bool>("_creatingGrid") && ((Canvas)_window.FindName("PlanCanvas")).Cursor == Cursors.Cross,
                "Grid command arms an interactive axis tool with a coordinate cursor");
            ClickAt(new(-1, -2));
            Check(session.Document.Grids.Count == 0, "The first axis click creates no premature datum");
            ClickAt(new(-1, 5));
            Check(session.Document.Grids.Count == 1, "Two clicks create a persistent building axis");
            Invoke("SetWallTool", false);
            Invoke("RenderPlan");
            var plan = (Canvas)_window.FindName("PlanCanvas");
            Check(plan.Children.OfType<FrameworkElement>().Any(element => element.Tag is Guid id && id == session.Document.Grids[0].Id),
                "The plan shows a selectable reference for the saved axis");
            SetField("_selectedGridId", (Guid?)session.Document.Grids[0].Id);
            Invoke("Delete_Click", _window, new RoutedEventArgs());
            Check(session.Document.Grids.Count == 0 && session.Document.Columns.Count == 1,
                "Delete on a selected axis leaves structural elements intact");
            session.Undo();
            Invoke("RefreshAll");
            Invoke("ExecuteApplicationCommand", "Text");
            Check(Invoke("PlanToolArmed") is true, "The Annotate Text button activates the existing note placement tool");
            Invoke("ExecuteApplicationCommand", "Section");
            ClickAt(new(1, -2));
            Check(session.Document.Views.All(view => view.Cut is null), "The first cut click does not create a section prematurely");
            ClickAt(new(1, 5));
            var section = session.Document.Views.Single(view => view.Cut is not null);
            Check(((TabControl)_window.FindName("ViewTabs")).SelectedIndex == 5 &&
                  section.SectionMode == SectionDisplayMode.Structural,
                "The second cut click opens a structural section through the selected axis");
            Pump();
            var sectionCanvas = (Canvas)_window.FindName("SectionCanvas");
            Check(sectionCanvas.Children.OfType<System.Windows.Shapes.Path>().Any(), "The section canvas renders mesh intersection profiles");
            var planHeads = ((Canvas)_window.FindName("PlanCanvas")).Children.OfType<System.Windows.Shapes.Ellipse>()
                .Count(head => head.Tag is Guid id && id == section.Id && Math.Abs(head.Width - 28) < 0.1);
            Check(planHeads == 2, "The structural section axis uses two conventional circular reference heads");
            Check(((TabItem)((TabControl)_window.FindName("ViewTabs")).Items[5]).Header.ToString()!.Contains(section.Name),
                "The structural section tab retains the saved view name");
            Check(((TextBlock)_window.FindName("PropertiesActiveViewText")).Text.Contains(section.Name),
                "The structural section properties retain the saved view name");
            Check(_window.Title.Contains(section.Name), "The structural section window title retains the saved view name");
            var coordinates = Field<List<TextBox>>("_documentationCoordinateInputs");
            Check(coordinates.Count == 4 && ((TextBlock)_window.FindName("PropertiesText")).Text.Contains(
                (string)application.FindResource("Documentation.ViewKind.Section")),
                "Section properties show localized identity and editable cut endpoints");
            coordinates[3].Text = "6";
            Invoke("ApplyDocumentationEndpoints_Click", new Button { Tag = section.Id }, new RoutedEventArgs());
            Check(session.Document.Views.Single(view => view.Id == section.Id).Cut!.End.Y == 6,
                "Editing cut coordinates regenerates the same section view");
            Pump();
            Check(sectionCanvas.Children.OfType<System.Windows.Shapes.Path>().Any() &&
                Field<List<TextBox>>("_documentationCoordinateInputs").Count == 4,
                "Regenerating cut coordinates retains visible geometry and its editable properties");
            Check(sectionCanvas.Children.OfType<TextBlock>().All(label => Canvas.GetLeft(label) >= 0),
                "Section level labels remain inside the viewport after its extent changes");
            SaveBitmap("section-with-foundation.png");
            Invoke("SelectProjectView", BuiltInProjectViews.NorthElevationId);
            Pump();
            var elevation = (Canvas)_window.FindName("ElevationCanvas");
            Check(elevation.Children.OfType<FrameworkElement>().Any(element => element.Tag is Guid id && id == session.Document.Grids[0].Id),
                "Orthogonal project axes appear in elevations as vertical reference datums");
            Check(elevation.Children.OfType<FrameworkElement>().Any(element => element.Tag is Guid id && id == column),
                "Structural columns are included in coordinated elevation views");
            Check(elevation.Children.OfType<FrameworkElement>().Any(element => element.Tag is Guid id && id == footing),
                "Foundations appear in elevation instead of disappearing from documentation");
            SaveBitmap("elevation-with-structure.png");
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            Check(Field<Guid?>("_selectedViewId") == session.Document.Views.Single(view => view.Kind == ProjectViewKind.Plan && view.LevelId == level).Id,
                "Returning to the plan clears stale elevation identity from the properties palette");
            session.AddDimension(PlanDimensions.ForSelection(session.Document, wall)
                .First(dimension => dimension.Kind == PlanDimensionKind.WallLength));
            session.AddModelText(level, new(2, 3.5), "NPT +0.00");
            Invoke("RefreshAll");
            Pump();
            SaveBitmap("plan-with-axes-and-section.png");
            var reopened = Reopen(session.Document, "desktop-documentation.g3d");
            Check(reopened.Grids.Count == 1 && reopened.Views.Single(view => view.Id == section.Id).Cut is not null &&
                reopened.Dimensions.Count == 1, "The interactively authored documentation survives official save and reopen");
            CheckSheetDesktop(session);
        }
        finally
        {
            SetField("_dirty", false);
            _window.Close();
            application.Shutdown();
        }
    }

    /// <summary>Dispatches the production plan handler at an injected model coordinate.</summary>
    private static void ClickAt(Point2 point)
    {
        SetField("_planPointerOverride", (Point?)Invoke("WorldToCanvas", point));
        var canvas = (Canvas)_window.FindName("PlanCanvas");
        Invoke("PlanCanvas_MouseLeftButtonDown", canvas, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        SetField("_planPointerOverride", null);
        Pump();
    }

    /// <summary>Flushes queued WPF layouts before checking visible geometry or capturing evidence.</summary>
    private static void Pump()
    {
        _window.UpdateLayout();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Captures the composed application window for review of the tested workflow.</summary>
    private static void SaveBitmap(string name)
    {
        var directory = Path.GetFullPath("artifacts/documentation-evidence");
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)_window.ActualWidth, (int)_window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    /// <summary>Calls a private UI operation without adding a public automation API to the product.</summary>
    private static object? Invoke(string name, params object[] arguments) =>
        typeof(MainWindow).GetMethod(name, PrivateInstance)!.Invoke(_window, arguments);

    /// <summary>Reads isolated tool state from the throwaway window.</summary>
    private static T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, PrivateInstance)!.GetValue(_window)!;

    /// <summary>Supplies test-only selection and pointer state without editing user documents.</summary>
    private static void SetField(string name, object? value) =>
        typeof(MainWindow).GetField(name, PrivateInstance)!.SetValue(_window, value);
}
