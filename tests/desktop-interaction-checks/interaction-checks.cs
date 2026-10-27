using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
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
            WallLoopClosingChecks(session);
            PlantingChecks(session);
            StairToolChecks(session);
            TexturedModelChecks(session);
            ElectricalToolChecks(session);
            CaptureDemoToolTip();
            IconChecks();
            RenderJoinScene();
            Model3DSelectionChecks();
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

    /// <summary>
    /// Draws two four-wall chains with the real placement handler, ending each a little off its first point, and checks
    /// that the last click is pulled onto that point, the loop is joined and the chain ends. The second loop is drawn
    /// from the exterior face, where the closing corner lies off the clicked point.
    /// </summary>
    private static void WallLoopClosingChecks(DocumentSession session)
    {
        SetField("_scale", 60.0);
        SetField("_panOffset", new Point(0, 0));
        Invoke("SetWallTool", true);
        ((CheckBox)_window.FindName("WallChainCheck")).IsChecked = true;
        ((CheckBox)_window.FindName("WallOrthogonalCheck")).IsChecked = false;
        var location = (ComboBox)_window.FindName("WallLocationLinePicker");
        foreach (var (reference, origin) in new[] { ("Center", new Point2(30, 30)), ("Exterior", new Point2(30, 40)) })
        {
            location.SelectedItem = location.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag as string == reference) ?? location.Items[0];
            var before = session.Document.Walls.Count;
            var corners = new[]
            {
                origin, new Point2(origin.X + 6, origin.Y), new Point2(origin.X + 6, origin.Y + 3),
                new Point2(origin.X, origin.Y + 3), new Point2(origin.X + 0.12, origin.Y + 0.25)
            };
            foreach (var corner in corners)
            {
                var pixel = (Point)Invoke("WorldToCanvas", corner)!;
                Invoke("HandleWallPlacementClick", pixel);
                Pump();
            }
            var loop = session.Document.Walls.Skip(before).ToArray();
            Check(loop.Length == 4 && loop[^1].End.DistanceTo(loop[0].Start) < 1e-6,
                $"A wall chain drawn from the {reference.ToLowerInvariant()} reference closes on its first wall from a nearby click");
            Check(Field<Point2?>("_pendingWallStart") is null && Field<object?>("_chainTail") is null,
                "Closing the loop ends the chain and leaves the wall tool armed for a new wall");
        }
        Invoke("SetWallTool", false);
    }

    /// <summary>
    /// Arms the site component command, places a tree, steps to the next species with Space and places it, then checks
    /// both plantings and their plan symbols.
    /// </summary>
    private static void PlantingChecks(DocumentSession session)
    {
        Invoke("ExecuteApplicationCommand", "SiteComponent");
        var before = session.Document.StructuralElements.Count(element => element.Kind == StructuralElementKind.Planting);
        Invoke("CreateStructuralFromTool", StructuralElementKind.Planting, new Point2(-20, 20), new Point2(-20, 20));
        Check(Invoke("TryCyclePlantingSpecies") is true, "Space steps to the next planting species while the tool is armed");
        Invoke("CreateStructuralFromTool", StructuralElementKind.Planting, new Point2(-10, 20), new Point2(-10, 20));
        var plants = session.Document.StructuralElements.Where(element => element.Kind == StructuralElementKind.Planting).ToArray();
        Check(plants.Length == before + 2 && plants[^2].Style == ElementStyle.Default &&
              plants[^1].Style == ElementStyle.Guanacaste,
            "The site component command plants a broadleaf tree, then a guanacaste tree after Space");
        var (scale, pan) = (Field<double>("_scale"), Field<Point>("_panOffset"));
        SetField("_scale", 20.0);
        SetField("_panOffset", new Point(300, -400));
        Invoke("RenderPlan");
        Pump();
        Check(plants.TakeLast(2).All(plant => _canvas.Children.OfType<System.Windows.Shapes.Polygon>()
                .Any(shape => shape.Tag is Guid id && id == plant.Id)),
            "Each planting is drawn in plan with its canopy symbol and can be picked");
        Invoke("CancelStructuralTool");
        // The later render checks time their scene; leave it as it was.
        foreach (var plant in plants.TakeLast(2)) session.RemoveStructuralElement(plant.Id);
        SetField("_scale", scale);
        SetField("_panOffset", pan);
        Invoke("RefreshAll");
        Pump();
    }

    /// <summary>Arms the stair tool, switches to the L stair with Space, places it and checks its automatic railings.</summary>
    private static void StairToolChecks(DocumentSession session)
    {
        Invoke("BeginStairTool");
        Check(Invoke("TryCyclePlantingSpecies") is true && Invoke("TryCyclePlantingSpecies") is true,
            "Space steps through the stair types while the stair tool is armed");
        var before = session.Document.StructuralElements.Count;
        Invoke("CreateStructuralFromTool", StructuralElementKind.Stair, new Point2(-30, -20), new Point2(-26, -20));
        var added = session.Document.StructuralElements.Skip(before).ToArray();
        Check(added.Count(element => element.Kind == StructuralElementKind.Stair && element.Style == ElementStyle.LShaped) == 1 &&
              added.Count(element => element.Kind == StructuralElementKind.Railing) >= 4,
            "Placing an L stair adds it with railings on its flights and landing in one step");
        session.Undo();
        Check(session.Document.StructuralElements.Count == before, "One undo removes the stair together with its railings");
        Invoke("CancelStructuralTool");
    }

    /// <summary>Assigns the shipped brick texture to walls and checks the 3D view draws walls with that image.</summary>
    private static void TexturedModelChecks(DocumentSession session)
    {
        // The grey study model ignores materials on purpose; project colors show them, like Revit's Realistic style.
        var clay = Field<bool>("_clayMode");
        SetField("_clayMode", false);
        Invoke("AssignRenderMaterial", SurfaceKind.Wall, "builtin:brick-red", 1.0, 0.6);
        Invoke("RebuildModelVisual");
        var viewport = (System.Windows.Controls.Viewport3D)_window.FindName("ModelViewport");
        var textured = viewport.Children.OfType<System.Windows.Media.Media3D.ModelVisual3D>()
            .Select(visual => visual.Content).OfType<System.Windows.Media.Media3D.GeometryModel3D>()
            .Any(model => model.Material is System.Windows.Media.Media3D.DiffuseMaterial { Brush: ImageBrush } &&
                          model.Geometry is System.Windows.Media.Media3D.MeshGeometry3D { TextureCoordinates.Count: > 0 });
        Check(textured, "The 3D view previews an assigned render texture on its surfaces");
        Invoke("AssignRenderMaterial", SurfaceKind.Wall, null!, 1.0, 0.0);
        Check(session.Document.RenderMaterials.Count == 0, "Choosing the stock material clears the assignment");
        SetField("_clayMode", clay);
        Invoke("RebuildModelVisual");
    }

    /// <summary>
    /// Places a panel and a switch on a wall and a luminaire on the ceiling with the ribbon commands, wires the switch to
    /// the light and a new circuit from the panel to both, then flips the switch and checks the light follows.
    /// </summary>
    private static void ElectricalToolChecks(DocumentSession session)
    {
        var level = session.Document.Levels[0].Id;
        var wall = session.Document.Walls.First(candidate => candidate.LevelId == level && candidate.Length > 3);
        var dx = (wall.End.X - wall.Start.X) / wall.Length;
        var dy = (wall.End.Y - wall.Start.Y) / wall.Length;
        Point2 Near(double along, double off) => new(wall.Start.X + dx * along - dy * off, wall.Start.Y + dy * along + dx * off);
        var before = session.Document.ElectricalDevices.Count;
        Invoke("ExecuteApplicationCommand", "ElectricalEquipment");
        Invoke("PlaceElectricalDevice", ElectricalDeviceKind.Panel, Near(0.6, 0.3));
        Invoke("ExecuteApplicationCommand", "LightSwitch");
        Invoke("PlaceElectricalDevice", ElectricalDeviceKind.Switch, Near(1.6, 0.3));
        Invoke("ExecuteApplicationCommand", "LightingFixture");
        Invoke("PlaceElectricalDevice", ElectricalDeviceKind.CeilingFixture, Near(2.0, 2.0));
        var placed = session.Document.ElectricalDevices.Skip(before).ToArray();
        var panel = placed.Single(device => device.Kind == ElectricalDeviceKind.Panel);
        var light = placed.Single(device => device.Kind == ElectricalDeviceKind.CeilingFixture);
        var lightSwitch = placed.Single(device => device.Kind == ElectricalDeviceKind.Switch);
        Check(placed.Length == 3 && Math.Abs(Math.Abs(DistanceToLine(wall, panel.Position)) - wall.Thickness / 2) < 1e-6,
            "Panels and switches attach to the wall face; luminaires go where clicked");

        Invoke("ExecuteApplicationCommand", "Wire");
        Invoke("HandleWiringClick", lightSwitch.Position);
        Invoke("HandleWiringClick", light.Position);
        Invoke("CancelElectricalTools");
        Invoke("ExecuteApplicationCommand", "Wire");
        Invoke("HandleWiringClick", panel.Position);
        Invoke("HandleWiringClick", light.Position);
        Invoke("HandleWiringClick", lightSwitch.Position);
        Invoke("CancelElectricalTools");
        var analysis = ElectricalAnalysis.Analyze(session.Document);
        Check(session.Document.ElectricalDevices.Single(device => device.Id == lightSwitch.Id).Controls.Contains(light.Id) &&
              session.Document.ElectricalCircuits.Count(circuit => circuit.PanelId == panel.Id) == 1 &&
              analysis.Brightness(light.Id) == 1,
            "The wiring tool links a switch to its light and wires a new circuit from the panel");
        Invoke("FlipSwitch", lightSwitch.Id);
        Check(ElectricalAnalysis.Analyze(session.Document).Brightness(light.Id) == 0, "Flipping the switch turns the light off at once");
        Invoke("RenderPlan");
        Pump();
        Check(_canvas.Children.OfType<System.Windows.Shapes.Ellipse>().Any(shape => shape.Tag is Guid id && id == light.Id),
            "Electrical devices are drawn in plan with their symbols");
        foreach (var device in placed) session.RemoveElectricalDevice(device.Id);
    }

    private static double DistanceToLine(Wall wall, Point2 point)
    {
        var dx = (wall.End.X - wall.Start.X) / wall.Length;
        var dy = (wall.End.Y - wall.Start.Y) / wall.Length;
        return (point.X - wall.Start.X) * -dy + (point.Y - wall.Start.Y) * dx;
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

    /// <summary>
    /// Every ribbon command has an icon of its own, and a contact sheet of all of them (large and at ribbon size) is written
    /// for visual review.
    /// </summary>
    private static void IconChecks()
    {
        var ids = Enum.GetValues<Guanacaste3D.Desktop.Controls.RibbonWorkspace>()
            .SelectMany(workspace => Guanacaste3D.Desktop.Controls.RibbonCatalog.GetGroups(workspace))
            .SelectMany(group => group.Commands).Select(command => command.CommandId).Distinct().ToList();
        var resources = Application.Current.Resources;
        var missing = ids.Where(id => resources["CmdIcon." + id] is not ImageSource).ToList();
        Check(ids.Count > 150 && missing.Count == 0, "Every ribbon command has its own icon" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : ""));
        IEnumerable<string> KeysOf(ResourceDictionary dictionary) =>
            dictionary.Keys.OfType<string>().Concat(dictionary.MergedDictionaries.SelectMany(KeysOf));
        var icons = KeysOf(resources).Where(key => key.StartsWith("CmdIcon.")).Distinct().OrderBy(key => key).ToList();
        const int columns = 14;
        var rows = (icons.Count + columns - 1) / columns;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, columns * 92, rows * 92));
            for (var index = 0; index < icons.Count; index++)
            {
                var x = index % columns * 92;
                var y = index / columns * 92;
                var image = (ImageSource)resources[icons[index]];
                context.DrawImage(image, new Rect(x + 6, y + 6, 48, 48));
                context.DrawImage(image, new Rect(x + 62, y + 6, 16, 16));
                context.DrawImage(image, new Rect(x + 62, y + 28, 24, 24));
                var label = new FormattedText(icons[index][8..], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 9, Brushes.DimGray, 1.0) { MaxTextWidth = 86, MaxTextHeight = 24 };
                context.DrawText(label, new Point(x + 3, y + 60));
            }
        }
        var sheet = new RenderTargetBitmap(columns * 92, rows * 92, 96, 96, PixelFormats.Pbgra32);
        sheet.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(sheet));
        using var stream = File.Create("artifacts/interaction-evidence/command-icons.png");
        encoder.Save(stream);
    }

    /// <summary>Verifies that demonstrations require an explicit request, then captures the Door scene.</summary>
    private static void CaptureDemoToolTip()
    {
        var door = FindButtons(_window).First(button => button.Tag as string == "Door");
        var allRibbonButtons = FindButtons(_window).Where(button => button.Tag is string).ToList();
        Check(door.ToolTip is ToolTip && allRibbonButtons.Count > 10 && allRibbonButtons.All(button => button.ToolTip is ToolTip),
            "Every ribbon button carries an animated demonstration tooltip");
        Check(new[] { "Door", "Window", "Wall", "Move", "Column", "Room", "Render", "Levels", "Schedules", "ManageLinks" }
            .Select(CommandDemoToolTip.KindFor).All(kind => kind != CommandDemoKind.None) &&
            CommandDemoToolTip.KindFor("Levels") == CommandDemoKind.OpenPanel && CommandDemoToolTip.KindFor("Move") == CommandDemoKind.MoveElement,
            "Tools and dialogs alike are mapped to a demonstration scene");
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
        Check(content.ActualHeight > 150, "Door demonstration scene is laid out inside the tooltip");
        Check(!tip.StaysOpen && tip.Placement == System.Windows.Controls.Primitives.PlacementMode.Custom &&
              tip.CustomPopupPlacementCallback is not null,
            "Tooltips use focus-aware placement constrained to the active Guanacaste 3D window");
        var competingWindow = new Window { Width = 320, Height = 200, ShowInTaskbar = false };
        competingWindow.Show();
        competingWindow.Activate();
        Pump();
        Check(!tip.IsOpen, "Tooltips close when Guanacaste 3D loses activation");
        competingWindow.Close();
        _window.Activate();
        Pump();
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
    /// Clicks an element in the 3D view the way the navigator reports a click: the element becomes the selection (so the plan
    /// and properties follow), is outlined in the model and carries temporary dimension labels; clicking the sky clears it.
    /// </summary>
    private static void Model3DSelectionChecks()
    {
        SetField("_dirty", false);
        Invoke("OpenSample_Click", _window, new RoutedEventArgs());
        var tabs = (TabControl)_window.FindName("ViewTabs");
        tabs.SelectedIndex = 1;
        Pump();
        var surface = (FrameworkElement)_window.FindName("ModelSurface");
        var overlay = (Canvas)_window.FindName("ModelOverlay");
        var camera = Field<Guanacaste3D.Core.Navigation.NavigationCamera>("_navigation");
        var document = Field<DocumentSession>("_session").Document;
        var wall = document.Walls[0];
        var datum = (float)document.Levels.First(level => level.Id == wall.LevelId).Elevation;
        var middle = new System.Numerics.Vector3((float)(wall.Start.X + wall.End.X) / 2, (float)(wall.Start.Y + wall.End.Y) / 2,
            datum + (float)wall.Height / 2);
        var pixel = camera.Project(middle, (float)surface.ActualWidth, (float)surface.ActualHeight);
        Check(pixel is not null, "A wall of the sample is in front of the 3D camera");
        Invoke("HandleViewClick", new Point(pixel!.Value.X, pixel.Value.Y), surface);
        Pump();
        Check(Field<Guid?>("_selectedWallId") is not null, "Clicking an element in the 3D view selects it");
        Check(overlay.Children.Count >= 3, "The selected element shows its temporary dimensions in the 3D view");
        SaveBitmap("artifacts/interaction-evidence/model-3d-selection.png");
        Invoke("HandleViewClick", new Point(2, 2), surface);
        Pump();
        Check(Field<Guid?>("_selectedWallId") is null && overlay.Children.Count == 0, "Clicking empty space clears the 3D selection");

        // With the wall tool armed, two clicks on the ground of the 3D view draw a wall, as two clicks in the plan do.
        var before = Field<DocumentSession>("_session").Document.Walls.Count;
        Invoke("SetWallTool", true);
        foreach (var ground in new[] { new System.Numerics.Vector3(-4, -3, 0), new System.Numerics.Vector3(-1, -3, 0) })
        {
            var at = camera.Project(ground, (float)surface.ActualWidth, (float)surface.ActualHeight);
            Check(at is not null, "The ground point for the new wall is on screen");
            Invoke("HandleViewClick", new Point(at!.Value.X, at.Value.Y), surface);
            Pump();
        }
        Check(Field<DocumentSession>("_session").Document.Walls.Count == before + 1, "Two clicks in the 3D view with the wall tool draw a wall");
        Invoke("SetWallTool", false);
        tabs.SelectedIndex = 0;
        Pump();

        // The plan carries one symbol per elevation; clicking one selects it, and a rename shows on the plan and the tab.
        var symbols = _canvas.Children.OfType<System.Windows.Shapes.Ellipse>()
            .Where(shape => shape.Tag is Guid id && document.Views.Any(view => view.Id == id && view.Kind == ProjectViewKind.Elevation)).ToList();
        Check(symbols.Count == 4, "The plan shows four elevation symbols");
        Invoke("FitPlan_Click", _window, new RoutedEventArgs());
        Pump();
        SaveBitmap("artifacts/interaction-evidence/plan-elevation-symbols.png");
        var north = (Guid)symbols.Select(shape => (Guid)shape.Tag).First(id => id == BuiltInProjectViews.NorthElevationId);
        var clickArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = symbols.First(shape => (Guid)shape.Tag == north)
        };
        Invoke("TrySelectElevationSymbol", clickArgs);
        Check(Field<Guid?>("_selectedViewId") == north, "Clicking an elevation symbol selects its view");
        Field<DocumentSession>("_session").RenameView(north, "Fachada principal");
        Invoke("RefreshAll");
        Invoke("RenderOrthographicViews");
        Pump();
        Check(_canvas.Children.OfType<TextBlock>().Any(label => label.Text == "Fachada principal") &&
              ((TabItem)tabs.Items[4]).Header as string == "Fachada principal", "A renamed elevation is renamed on the plan symbol and the tab");
        Field<DocumentSession>("_session").Undo();
        Invoke("RefreshAll");

        // Double-clicking a door opens its family with an editable plan view and 3D view.
        var door = Field<DocumentSession>("_session").Document.Openings.First(item => item.Kind == OpeningKind.Door);
        var type = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.OpeningFamilyViewsWindow")!;
        var family = (Window)Activator.CreateInstance(type, Field<DocumentSession>("_session"), door.Id)!;
        family.Owner = _window;
        family.Show();
        Pump();
        Pump();
        var familyViewport = FindVisual<System.Windows.Controls.Viewport3D>(family);
        Check(familyViewport is not null && familyViewport.Children.Count >= 3, "The door family opens with a 3D view of the door in its wall");
        var familyPlan = FindVisual<Canvas>(family, canvas => canvas.Background is not null && canvas.Children.Count > 0);
        Check(familyPlan is not null, "The door family opens with a plan view drawn from its parameters");
        var widthText = door.Width.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture);
        var widthBox = FindVisual<TextBox>(family, box => box.Text == widthText);
        Check(widthBox is not null, "The door family lists its width as an editable parameter");
        widthBox!.Text = (door.Width + 0.1).ToString("0.###", System.Globalization.CultureInfo.CurrentCulture);
        Pump();
        SaveBitmap("artifacts/interaction-evidence/door-family-views.png");
        var familyBitmap = new RenderTargetBitmap((int)family.ActualWidth, (int)family.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        familyBitmap.Render(family);
        using (var stream = File.Create("artifacts/interaction-evidence/door-family-window.png"))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(familyBitmap));
            encoder.Save(stream);
        }
        family.Close();
        SetField("_dirty", false);
    }

    private static T? FindVisual<T>(DependencyObject root, Func<T, bool>? match = null) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T found && (match is null || match(found))) return found;
            if (FindVisual(child, match) is { } nested) return nested;
        }
        return null;
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
            var standardSidebar = (FrameworkElement)_window.FindName("StandardSidebar");
            tabs.SelectedIndex = 6;
            Pump();
            var panel = (Control)_window.FindName("RenderPanelControl");
            var status = (TextBlock)panel.FindName("StatusText");
            var image = (Image)panel.FindName("PreviewImage");
            var imageHost = (FrameworkElement)panel.FindName("ImageHost");
            Check(panel.Visibility == Visibility.Visible && standardSidebar.Visibility == Visibility.Collapsed,
                "The Render workspace replaces the standard properties and project-browser sidebar");
            Check(Math.Abs(imageHost.TranslatePoint(new Point(), panel).X - 323) < 2,
                "The Render controls occupy the same 320-pixel sidebar column and divider");
            Check(panel.Background is SolidColorBrush { Color: { R: > 240, G: > 240, B: > 240 } },
                "The Render workspace uses the application's light theme");
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
            SaveBitmap("artifacts/interaction-evidence/render-workspace-light.png");
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
            Check(standardSidebar.Visibility == Visibility.Visible && panel.Visibility == Visibility.Collapsed,
                "Leaving Render restores the standard application sidebar");
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
