using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Guanacaste3D.Core;

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
        ThicknessInput.Text = wall.Thickness.ToString("0.00", CultureInfo.CurrentCulture);
        HeightInput.Text = wall.Height.ToString("0.00", CultureInfo.CurrentCulture);
        WallTypePicker.SelectedItem = WallTypePicker.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => Equals(item.Tag, wall.WallTypeId));
    }

    /// <summary>Renders coordinated walls, openings, floors, lighting, and camera state in 3D.</summary>
    private void Render3D()
    {
        if (ModelViewport is null || _session is null) return;
        ModelViewport.Children.Clear();
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new AmbientLight(Color.FromRgb(178, 193, 184))
        });
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new DirectionalLight(Colors.White, new Vector3D(-2, -6, -3))
        });
        AddGround();
        RenderFloorSlabs3D();
        foreach (var wall in _session.Document.Walls)
        {
            AddWallModel(wall);
        }
        var allPoints = _session.Document.Walls.SelectMany(wall => new[] { wall.Start, wall.End }).ToArray();
        var centerX = allPoints.Length == 0 ? 0 : (allPoints.Min(point => point.X) + allPoints.Max(point => point.X)) / 2;
        var centerY = allPoints.Length == 0 ? 0 : (allPoints.Min(point => point.Y) + allPoints.Max(point => point.Y)) / 2;
        var range = allPoints.Length == 0 ? 10 : Math.Max(
            allPoints.Max(point => point.X) - allPoints.Min(point => point.X),
            allPoints.Max(point => point.Y) - allPoints.Min(point => point.Y));
        var distance = Math.Max(12, range * 1.7);
        var cameraPosition = new Point3D(centerX + distance, distance * 0.8, -centerY + distance);
        var target = new Point3D(centerX, 1, -centerY);
        ModelViewport.Camera = new PerspectiveCamera(
            cameraPosition, target - cameraPosition, new Vector3D(0, 1, 0), 50);
    }

    /// <summary>Creates the wall solids segmented around hosted openings.</summary>
    private void AddWallModel(Wall wall)
    {
        var elevation = _session.Document.Levels.First(level => level.Id == wall.LevelId).Elevation;
        var length = wall.Length;
        var offsetX = -(wall.End.Y - wall.Start.Y) * wall.Thickness / (2 * length);
        var offsetY = (wall.End.X - wall.Start.X) * wall.Thickness / (2 * length);
        var hostedOpenings = _session.Document.Openings.Where(opening => opening.WallId == wall.Id);
        foreach (var solid in WallGeometry.Solids(wall, hostedOpenings))
        {
            var start = PointAlongWall(wall, solid.From);
            var end = PointAlongWall(wall, solid.To);
            var corners = new[]
            {
                WallVertex(start, 1, solid.Bottom, elevation, offsetX, offsetY),
                WallVertex(end, 1, solid.Bottom, elevation, offsetX, offsetY),
                WallVertex(end, -1, solid.Bottom, elevation, offsetX, offsetY),
                WallVertex(start, -1, solid.Bottom, elevation, offsetX, offsetY),
                WallVertex(start, 1, solid.Top, elevation, offsetX, offsetY),
                WallVertex(end, 1, solid.Top, elevation, offsetX, offsetY),
                WallVertex(end, -1, solid.Top, elevation, offsetX, offsetY),
                WallVertex(start, -1, solid.Top, elevation, offsetX, offsetY)
            };
            var mesh = CreateBoxMesh(corners);
            var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(185, 203, 192)));
            ModelViewport.Children.Add(new ModelVisual3D
            {
                Content = new GeometryModel3D(mesh, material) { BackMaterial = material }
            });
        }
    }

    /// <summary>Returns a point at a metric offset along a wall center line.</summary>
    private static Point2 PointAlongWall(Wall wall, double position) => new(
        wall.Start.X + position * (wall.End.X - wall.Start.X) / wall.Length,
        wall.Start.Y + position * (wall.End.Y - wall.Start.Y) / wall.Length);

    /// <summary>Converts a wall edge point into the 3D coordinate system.</summary>
    private static Point3D WallVertex(
        Point2 point, int side, double height, double elevation, double offsetX, double offsetY) =>
        new(point.X + side * offsetX, elevation + height, -(point.Y + side * offsetY));

    /// <summary>Builds a closed rectangular mesh from eight ordered corner points.</summary>
    private static MeshGeometry3D CreateBoxMesh(IReadOnlyList<Point3D> corners)
    {
        int[][] faces =
        [
            [0, 1, 5, 4], [2, 3, 7, 6], [1, 2, 6, 5],
            [3, 0, 4, 7], [4, 5, 6, 7], [3, 2, 1, 0]
        ];
        var mesh = new MeshGeometry3D();
        foreach (var face in faces)
        {
            var positionOffset = mesh.Positions.Count;
            foreach (var vertexIndex in face) mesh.Positions.Add(corners[vertexIndex]);
            foreach (var triangleIndex in new[] { 0, 1, 2, 0, 2, 3 })
                mesh.TriangleIndices.Add(positionOffset + triangleIndex);
        }
        return mesh;
    }

    /// <summary>Adds a large neutral ground plane beneath the project geometry.</summary>
    private void AddGround()
    {
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(-150, -0.02, -150), new(150, -0.02, -150),
                new(150, -0.02, 150), new(-150, -0.02, 150)
            },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };
        var material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(238, 243, 240)));
        ModelViewport.Children.Add(new ModelVisual3D
        {
            Content = new GeometryModel3D(mesh, material) { BackMaterial = material }
        });
    }

    /// <summary>Provides keyboard access to primary modeling, view, and history commands.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
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
        else if (e.Key == Key.Escape) { SetWallTool(false); e.Handled = true; }
        else if (e.Key == Key.Delete) { Delete_Click(this, new RoutedEventArgs()); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }
}
