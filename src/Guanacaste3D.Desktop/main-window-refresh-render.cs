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
        Title = "Guanacaste 3D — " + _session.Document.Name + (_dirty ? " *" : "");
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
            views.Items.Add(new TreeViewItem { Header = view.Name, Tag = view.Id });
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

    /// <summary>Renders the shared BIM scene mesh with daylight-style lighting in the 3D view.</summary>
    private void Render3D()
    {
        if (ModelViewport is null || _session is null) return;
        ModelViewport.Children.Clear();
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new AmbientLight(Color.FromRgb(150, 160, 156))
        });
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new DirectionalLight(Color.FromRgb(255, 246, 232), new Vector3D(0.45, -0.70, -0.55))
        });
        var scene = BuildingMesher.Build(_session.Document, includeGround: true);
        foreach (var group in ScenePresentation.ToWpfModels(scene))
            ModelViewport.Children.Add(new ModelVisual3D { Content = group });
        var (min, max) = BuildingMesher.Build(_session.Document).Bounds();
        if (_session.Document.Walls.Count == 0 && _session.Document.Floors.Count == 0)
            (min, max) = (new System.Numerics.Vector3(-5, -5, 0), new System.Numerics.Vector3(5, 5, 3));
        var aspect = ModelViewport.ActualHeight > 0 ? ModelViewport.ActualWidth / ModelViewport.ActualHeight : 1.6;
        ModelViewport.Camera = ScenePresentation.ToWpfCamera(CurrentRenderCamera(min, max), aspect);
    }

    /// <summary>Provides keyboard access to primary modeling, view, and history commands.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (TryHandleDirectWallLengthKey(e))
        {
            e.Handled = true;
            base.OnPreviewKeyDown(e);
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
        else if (e.Key == Key.Delete) { Delete_Click(this, new RoutedEventArgs()); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }
}
