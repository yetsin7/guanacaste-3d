using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Synchronizes all views and property surfaces with the current document.</summary>
    private void RefreshAll()
    {
        if (_session is null) return;
        SynchronizeModelViewGraphics();
        UpdateWindowTitle();
        if (!_session.Document.Levels.Any(level => level.Id == _activeLevelId))
            _activeLevelId = _session.Document.Levels[0].Id;
        if (!_session.Document.Walls.Any(wall => wall.Id == _selectedWallId)) _selectedWallId = null;
        if (!_session.Document.Openings.Any(opening => opening.Id == _selectedOpeningId)) _selectedOpeningId = null;
        RefreshLevelPicker();
        RefreshTree();
        RefreshProperties();
        RenderPlan();
        Render3D();
        RefreshExtendedWorkflows();
    }

    /// <summary>Rebuilds the level selector while preserving the active level.</summary>
    private void RefreshLevelPicker()
    {
        _refreshingLevels = true;
        LevelPicker.Items.Clear();
        foreach (var level in _session.Document.Levels)
        {
            var item = new ComboBoxItem { Content = level.Name, Tag = level.Id };
            LevelPicker.Items.Add(item);
            if (level.Id == _activeLevelId) LevelPicker.SelectedItem = item;
        }
        _refreshingLevels = false;
    }

    /// <summary>Builds the project browser hierarchy for all supported BIM entities.</summary>
    private void RefreshTree()
    {
        ProjectTree.Items.Clear();
        var project = new TreeViewItem { Header = _session.Document.Name, IsExpanded = true };
        var views = new TreeViewItem { Header = GetText("Tree.Views"), IsExpanded = true, ContextMenu = CreateAllViewsMenu() };
        foreach (var view in _session.Document.Views.Where(view => MatchesProjectSearch(view.Name)))
            views.Items.Add(new TreeViewItem { Header = view.Name, Tag = view.Id, ContextMenu = CreateViewMenu(view.Id) });
        project.Items.Add(views);
        var levels = new TreeViewItem { Header = GetText("Tree.Levels"), IsExpanded = true };
        foreach (var level in _session.Document.Levels)
        {
            var branch = new TreeViewItem
            {
                Header = $"{level.Name}  ({level.Elevation:0.##} m)",
                Tag = level.Id,
                IsExpanded = true
            };
            foreach (var wall in _session.Document.Walls.Where(candidate => candidate.LevelId == level.Id))
            {
                var wallBranch = new TreeViewItem
                {
                    Header = $"{GetText("Entity.Wall")} · {wall.Length:0.00} m",
                    Tag = wall.Id
                };
                foreach (var opening in _session.Document.Openings.Where(candidate => candidate.WallId == wall.Id))
                {
                    wallBranch.Items.Add(new TreeViewItem
                    {
                        Header = $"{GetText(opening.Kind == OpeningKind.Door ? "Entity.Door" : "Entity.Window")} · {opening.Width:0.00} m",
                        Tag = opening.Id
                    });
                }
                branch.Items.Add(wallBranch);
            }
            levels.Items.Add(branch);
        }
        project.Items.Add(levels);
        AppendExtendedProjectTree(project);
        ProjectTree.Items.Add(project);
    }

    /// <summary>Shows coordinated parameters for the selected element or document summary.</summary>
    private void RefreshProperties()
    {
        UpdateModelSelection(false);
        UpdateDocumentationProperties();
        if (TryRefreshSheetProperties()) return;
        if (TryRefreshExtendedProperties()) return;
        var selectedOpening = _session.Document.Openings.FirstOrDefault(opening => opening.Id == _selectedOpeningId);
        if (selectedOpening is not null)
        {
            OpeningOffsetInput.Text = selectedOpening.Offset.ToString("0.###", CultureInfo.CurrentCulture);
            OpeningWidthInput.Text = selectedOpening.Width.ToString("0.###", CultureInfo.CurrentCulture);
            OpeningSillInput.Text = selectedOpening.Sill.ToString("0.###", CultureInfo.CurrentCulture);
            OpeningHeightInput.Text = selectedOpening.Height.ToString("0.###", CultureInfo.CurrentCulture);
        }

        var wall = _session.Document.Walls.FirstOrDefault(candidate => candidate.Id == _selectedWallId);
        if (wall is null)
        {
            PropertiesText.Text = FormatText(
                "Properties.ProjectSummary",
                _session.Document.Name,
                _session.Document.Levels.Count,
                _session.Document.Walls.Count);
            return;
        }
        PropertiesText.Text = FormatText(
            "Properties.WallSummary",
            wall.Length,
            wall.Thickness,
            wall.Height,
            _session.Document.Openings.Count(opening => opening.WallId == wall.Id),
            WallGeometry.NetVolume(wall, _session.Document.Openings.Where(opening => opening.WallId == wall.Id)),
            wall.Id.ToString()[..8]);
        LoadWallDimensionFields(wall);
        WallTypePicker.SelectedItem = WallTypePicker.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => Equals(item.Tag, wall.WallTypeId));
    }

    /// <summary>Marks the 3D view stale after a change; it is rebuilt only while the tab is showing.</summary>
    private void Render3D()
    {
        if (ModelViewport is null || _session is null) return;
        MarkSceneDirty();
        if (ViewTabs.SelectedIndex == Model3DTabIndex) RebuildModelVisual();
    }

    /// <summary>Builds the model off the UI thread and publishes only the most recent completed snapshot.</summary>
    private async void RebuildModelVisual()
    {
        _modelDirty = true;
        var request = ++_modelBuildRequest;
        var version = _sceneVersion;
        var document = ModelDisplayDocument();
        var xray = _xRay;
        var style = _modelDisplayStyle;
        var detail = _modelDetail;
        var shadowSun = _modelShadows && _modelSunPreset != "Off" ? ModelSunDirection() : (System.Numerics.Vector3?)null;
        var hiddenDocument = _revealHidden ? _session.Document.Clone() : null;
        var hiddenIds = _revealHidden ? ModelElementIds(_session.Document).Except(ModelElementIds(document)).ToHashSet() : null;
        var showEdges = _showEdges || style is ModelDisplayStyle.Wireframe or ModelDisplayStyle.HiddenLine;
        var textures = xray || style is ModelDisplayStyle.Wireframe or ModelDisplayStyle.HiddenLine or ModelDisplayStyle.Shaded or ModelDisplayStyle.ConsistentColors
            ? null : RenderTextureLibrary.Previews3D(document.RenderMaterials, _filePath);
        await _modelBuildGate.WaitAsync().ConfigureAwait(false);
        ModelSceneSnapshot snapshot;
        try
        {
            if (request != Volatile.Read(ref _modelBuildRequest) || version != Volatile.Read(ref _sceneVersion)) return;
            snapshot = await Task.Run(() => ModelSceneSnapshot.Build(document, false, xray, showEdges, textures, shadowSun, hiddenDocument, hiddenIds, detail)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("The interactive 3D scene could not be built: {0}", exception);
            return;
        }
        finally { _modelBuildGate.Release(); }
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        try { await Dispatcher.InvokeAsync(() => PublishModelVisual(snapshot, request, version)); }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("The interactive 3D scene could not be published: {0}", exception);
        }
    }

    /// <summary>Publishes frozen geometry on the owning dispatcher if it still represents the current view.</summary>
    private void PublishModelVisual(ModelSceneSnapshot snapshot, int request, int version)
    {
        if (!IsLoaded || request != _modelBuildRequest || version != _sceneVersion || ViewTabs.SelectedIndex != Model3DTabIndex) return;
        _modelDirty = false;
        var bounds = snapshot.Bounds;
        if (_links.Bounds() is { } linked)
            bounds = snapshot.HasGeometry ? (System.Numerics.Vector3.Min(bounds.Min, linked.Min), System.Numerics.Vector3.Max(bounds.Max, linked.Max)) : linked;
        _boundsCache = (version, bounds);
        ModelViewport.Children.Clear();
        _selectionVisuals.Clear();
        _shownSelection = null;
        _picker = null;
        var ambient = (byte)Math.Clamp(_modelAmbient * 255, 0, 255);
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new AmbientLight(Color.FromRgb(ambient, ambient, ambient))
        });
        var sun = ModelSunDirection();
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new DirectionalLight(_modelSunPreset == "Off" ? Colors.Black : Color.FromRgb(255, 246, 232),
                new Vector3D(-sun.X, -sun.Z, sun.Y))
        });
        var thickness = EdgeThickness();
        foreach (var group in SceneDisplayStyles.Apply(snapshot.Models, _xRay ? ModelDisplayStyle.Textures : _modelDisplayStyle))
            ModelViewport.Children.Add(new ModelVisual3D { Content = group });
        if (snapshot.Edges is { } edges) ModelViewport.Children.Add(new ModelVisual3D { Content = edges });
        foreach (var (link, loaded) in _links.Visible())
        {
            foreach (var group in SceneDisplayStyles.Apply(ScenePresentation.ToWpfModels(loaded.Proxy, loaded.Model.Materials, link.Placement), _xRay ? ModelDisplayStyle.Textures : _modelDisplayStyle))
                ModelViewport.Children.Add(new ModelVisual3D { Content = group });
            if (_showEdges || _modelDisplayStyle is ModelDisplayStyle.Wireframe or ModelDisplayStyle.HiddenLine)
                AddEdgeVisual(EdgesOf(link.Id, loaded), link.Placement.ToMatrix(), thickness);
        }
        if (snapshot.GroundShadow is { } shadow) ModelViewport.Children.Add(new ModelVisual3D { Content = shadow });
        foreach (var hidden in snapshot.Revealed) ModelViewport.Children.Add(new ModelVisual3D { Content = hidden });
        UpdateModelSelection(true);
        UpdateModelCamera();
        _ = GetSceneAsync();
        RefreshModelRealisticPreview();
    }

    /// <summary>
    /// Width of depth-tested outlines in meters, scaled for readable continuous lines at normal project framing.
    /// </summary>
    private float EdgeThickness()
    {
        var (min, max) = SceneBounds();
        return ScenePresentation.EdgeThicknessForBounds(min, max);
    }

    private bool _xRay;
    private bool _showEdges = true;
    private readonly Dictionary<Guid, List<EdgeSegment>> _linkEdges = [];

    /// <summary>Feature edges of a linked model's proxy, computed once and kept until the links change.</summary>
    private List<EdgeSegment> EdgesOf(Guid id, LoadedModel loaded)
    {
        if (_linkEdges.TryGetValue(id, out var cached)) return cached;
        return _linkEdges[id] = FeatureEdges.Extract(loaded.Proxy.Positions, loaded.Proxy.Indices, maximumEdges: 40_000);
    }

    private void AddEdgeVisual(IEnumerable<EdgeSegment> edges, System.Numerics.Matrix4x4 placement, float thickness)
    {
        var lines = ScenePresentation.ToWpfEdges(edges, placement, thickness, Color.FromRgb(52, 56, 62));
        if (lines is not null) ModelViewport.Children.Add(new ModelVisual3D { Content = lines });
    }

    /// <summary>Updates model edges and service visibility while preserving project colors.</summary>
    private void ViewStyle_Changed(object sender, RoutedEventArgs e)
    {
        if (_synchronizingGraphics) return;
        if (EdgesCheck is null) return;
        _showEdges = EdgesCheck.IsChecked == true;
        _xRay = XRayCheck?.IsChecked == true;
        PersistModelViewGraphics();
        RefreshModelRealisticPreview();
        if (ViewTabs.SelectedIndex == Model3DTabIndex) RebuildModelVisual();
        else _modelDirty = true;
    }

    /// <summary>Points the 3D view's camera where the shared navigation camera is.</summary>
    private void UpdateModelCamera()
    {
        if (ModelViewport is null) return;
        var (min, max) = SceneBounds();
        var radius = Math.Max(3, (max - min).Length() / 2);
        var aspect = ModelViewport.ActualHeight > 0 ? ModelViewport.ActualWidth / ModelViewport.ActualHeight : 1.6;
        ModelViewport.Camera = ScenePresentation.ToWpfCamera(_navigation, aspect, radius);
        ViewNavigation?.Redraw();
        UpdateModelRealisticCamera();
        if (_dimensionChips.Count > 0) PositionDimensionChips();
    }

    /// <summary>When the E key was last pressed, for the E then H hide sequence.</summary>
    private DateTime? _eKeyAt;

    /// <summary>
    /// E then H, pressed one after the other, hides the selection in the active view. E still flies the camera in 3D;
    /// the H is taken only when it follows an E within a moment.
    /// </summary>
    private bool TryHideSequence(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is TextBox)
        {
            _eKeyAt = null;
            return false;
        }
        if (e.Key == Key.E)
        {
            _eKeyAt = DateTime.UtcNow;
            return false;
        }
        var pressedE = _eKeyAt is { } at && DateTime.UtcNow - at < TimeSpan.FromSeconds(1.5);
        _eKeyAt = null;
        if (e.Key != Key.H || !pressedE) return false;
        HideSelectionInView();
        return true;
    }

    /// <summary>Provides keyboard access to primary modeling, view, and history commands.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (TryHandleViewNavigationKey(e)) { e.Handled = true; return; }
        if (_movingElement && (e.Key == Key.Delete ||
            (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.Z or Key.Y))) CancelMoveTool();
        if (e.Key == Key.Escape && _movingElement)
        {
            CancelMoveTool();
            e.Handled = true;
            return;
        }
        if (TryHideSequence(e))
        {
            e.Handled = true;
            return;
        }
        // While the pointer is over the 3D view or render preview, W A S D Q E fly the camera instead of triggering tools.
        if (NavigationOwnsKey(e.Key))
        {
            base.OnPreviewKeyDown(e);
            return;
        }
        if (TryHandleDirectWallLengthKey(e))
        {
            e.Handled = true;
            base.OnPreviewKeyDown(e);
            return;
        }
        if (e.Key == Key.Space && TryFlipDoorWithSpace())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && TryPlumbingSpace())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && TryElectricalSpace())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && TryRotateComponent())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox && TryCyclePlantingSpecies())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space && _drawingWall && Keyboard.FocusedElement is not TextBox && TryFlipWallSide())
        {
            e.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is TextBox)
        {
            base.OnPreviewKeyDown(e);
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { SaveProject(false); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { _session.Undo(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { _session.Redo(); e.Handled = true; }
        // Per-view visibility: Ctrl+A selects what the view shows, Ctrl+H hides the selection in that view only.
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A) { SelectAllInView(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.H) { HideSelectionInView(); e.Handled = true; }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.H) { ShowAllInView(); e.Handled = true; }
        else if (e.Key == Key.W) { SetWallTool(true); e.Handled = true; }
        else if (e.Key == Key.R) { Room_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.F) { FitPlan_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Escape && (CancelElectricalTools() | CancelPlumbingTools()))
        {
            StatusText.Text = GetText("Status.SelectionMode");
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && CancelTopoPointTool()) e.Handled = true;
        else if (e.Key == Key.Escape && _drawingWall) { HandleWallEscape(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _viewSelection = null; SetWallTool(false); e.Handled = true; }
        else if (e.Key == Key.F2 && _selectedGridId is Guid gridId) { RenameGridAxis(gridId); e.Handled = true; }
        else if (e.Key == Key.F2 && _selectedViewId is Guid viewId) { RenameView(viewId); e.Handled = true; }
        else if (e.Key == Key.Delete) { Delete_Click(this, new RoutedEventArgs()); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }
}
