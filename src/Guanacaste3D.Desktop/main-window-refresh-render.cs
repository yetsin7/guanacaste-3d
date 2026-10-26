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
        var views = new TreeViewItem { Header = GetText("Tree.Views"), IsExpanded = true };
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

    /// <summary>Rebuilds the 3D view's models: the BIM mesh plus a light proxy of every linked model.</summary>
    private void RebuildModelVisual()
    {
        _modelDirty = false;
        ModelViewport.Children.Clear();
        _selectionVisuals.Clear();
        _shownSelection = null;
        _picker = null;
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new AmbientLight(Color.FromRgb(150, 160, 156))
        });
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new DirectionalLight(Color.FromRgb(255, 246, 232), new Vector3D(0.45, -0.70, -0.55))
        });
        var scene = BuildingMesher.Build(_session.Document, includeGround: true);
        var thickness = EdgeThickness();
        foreach (var group in _clayMode ? ScenePresentation.ToWpfClay(scene) : ScenePresentation.ToWpfModels(scene))
            ModelViewport.Children.Add(new ModelVisual3D { Content = group });
        if (_showEdges)
        {
            // The ground plane would add four enormous lines at the horizon, so only the building contributes edges.
            var positions = new List<System.Numerics.Vector3>(scene.Positions);
            var indices = new List<int>();
            for (var triangle = 0; triangle < scene.TriangleCount; triangle++)
            {
                if (scene.TriangleKinds[triangle] == SurfaceKind.Ground) continue;
                indices.Add(scene.Indices[triangle * 3]);
                indices.Add(scene.Indices[triangle * 3 + 1]);
                indices.Add(scene.Indices[triangle * 3 + 2]);
            }
            AddEdgeVisual(FeatureEdges.Extract(positions, indices), System.Numerics.Matrix4x4.Identity, thickness);
        }
        foreach (var (link, loaded) in _links.Visible())
        {
            foreach (var group in ScenePresentation.ToWpfModels(loaded.Proxy, loaded.Model.Materials, link.Placement))
                ModelViewport.Children.Add(new ModelVisual3D { Content = group });
            if (_showEdges) AddEdgeVisual(EdgesOf(link.Id, loaded), link.Placement.ToMatrix(), thickness);
        }
        UpdateModelSelection(true);
        UpdateModelCamera();
    }

    /// <summary>
    /// Width of drawn edges in meters, kept deliberately subtle so the model reads like a Revit hidden-line view.
    /// The narrow upper bound avoids heavy outlines on large projects while the lower bound remains depth-tested.
    /// </summary>
    private float EdgeThickness()
    {
        var (min, max) = SceneBounds();
        return Math.Clamp(0.0012f * Math.Max(3, (max - min).Length() / 2), 0.0025f, 0.012f);
    }

    private bool _clayMode = true;
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

    /// <summary>Switches the 3D view between the grey study model and the project's colors, and edges on or off.</summary>
    private void ViewStyle_Changed(object sender, RoutedEventArgs e)
    {
        if (ClayCheck is null || EdgesCheck is null) return;
        _clayMode = ClayCheck.IsChecked == true;
        _showEdges = EdgesCheck.IsChecked == true;
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
        if (_dimensionChips.Count > 0) PositionDimensionChips();
    }

    /// <summary>Provides keyboard access to primary modeling, view, and history commands.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
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
        else if (e.Key == Key.W) { SetWallTool(true); e.Handled = true; }
        else if (e.Key == Key.R) { Room_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.F) { FitPlan_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Escape && _drawingWall) { HandleWallEscape(); e.Handled = true; }
        else if (e.Key == Key.Escape) { SetWallTool(false); e.Handled = true; }
        else if (e.Key == Key.F2 && _selectedViewId is Guid viewId) { RenameView(viewId); e.Handled = true; }
        else if (e.Key == Key.Delete) { Delete_Click(this, new RoutedEventArgs()); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }
}
