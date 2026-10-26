using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private Guid? _selectedViewId;

    /// <summary>Displays the generated north elevation.</summary>
    private void ShowElevation_Click(object sender, RoutedEventArgs e) => SelectProjectView(_elevationViewId);

    /// <summary>Displays the generated section view.</summary>
    private void ShowSection_Click(object sender, RoutedEventArgs e) =>
        SelectProjectView(BuiltInProjectViews.SectionAId);

    /// <summary>Redraws generated elevation and section views when their viewport changes.</summary>
    private void OrthographicCanvas_SizeChanged(object sender, SizeChangedEventArgs e) =>
        RenderOrthographicViews();

    /// <summary>Selects a persistent view and coordinates the active level and workspace tab.</summary>
    private void SelectProjectView(Guid viewId)
    {
        var view = _session.Document.Views.FirstOrDefault(candidate => candidate.Id == viewId);
        if (view is null) return;
        _selectedViewId = view.Id;
        if (view.Kind == ProjectViewKind.Elevation) _elevationViewId = view.Id;
        _selectedWallId = null;
        _selectedOpeningId = null;
        _selectedRoomId = null;
        _selectedFloorId = null;
        _selectedSheetId = null;
        if (view.LevelId is Guid levelId) _activeLevelId = levelId;
        ViewTabs.SelectedIndex = GetViewTabIndex(view.Kind);
        RefreshLevelPicker();
        RefreshProperties();
        RenderOrthographicViews();
    }

    /// <summary>Maps a persistent view kind to the corresponding workspace tab.</summary>
    private static int GetViewTabIndex(ProjectViewKind kind) => kind switch
    {
        ProjectViewKind.Plan => 0,
        ProjectViewKind.Model3D => 1,
        ProjectViewKind.Schedule => 2,
        ProjectViewKind.Elevation => 4,
        ProjectViewKind.Section => 5,
        _ => 0
    };

    /// <summary>Renders all generated orthographic workspaces from the shared document.</summary>
    private void RenderOrthographicViews()
    {
        if (ElevationCanvas is null || SectionCanvas is null || _session is null) return;
        var (elevation, name) = CurrentElevation();
        // The Elevation tab carries the name of the elevation it shows, so a renamed view is renamed everywhere.
        if (ViewTabs.Items.Count > 4 && ViewTabs.Items[4] is TabItem elevationTab) elevationTab.Header = name;
        RenderProjectedView(ElevationCanvas, elevation);
        RenderProjectedView(SectionCanvas, ViewOrientation.East);
    }

    /// <summary>Projects model walls, openings, floors, and levels into one orthographic canvas.</summary>
    private void RenderProjectedView(Canvas canvas, ViewOrientation orientation)
    {
        canvas.Children.Clear();
        if (canvas.ActualWidth < 100 || canvas.ActualHeight < 100) return;
        var walls = _session.Document.Walls.ToArray();
        var projectedPoints = walls.SelectMany(wall => new[]
        {
            ProjectCoordinate(wall.Start, orientation),
            ProjectCoordinate(wall.End, orientation)
        }).ToArray();
        var minimumCoordinate = projectedPoints.Length == 0 ? -5 : projectedPoints.Min();
        var maximumCoordinate = projectedPoints.Length == 0 ? 5 : projectedPoints.Max();
        var minimumElevation = Math.Min(-0.5, _session.Document.Levels.Min(level => level.Elevation) - 0.5);
        var maximumElevation = walls.Length == 0
            ? _session.Document.Levels.Max(level => level.Elevation) + 3
            : walls.Max(wall =>
                _session.Document.Levels.Single(level => level.Id == wall.LevelId).Elevation + wall.Height);
        var horizontalRange = Math.Max(1, maximumCoordinate - minimumCoordinate);
        var verticalRange = Math.Max(1, maximumElevation - minimumElevation);
        var scale = Math.Min(
            (canvas.ActualWidth - 140) / horizontalRange,
            (canvas.ActualHeight - 100) / verticalRange);
        var horizontalOffset = (canvas.ActualWidth - horizontalRange * scale) / 2;

        DrawProjectedLevels(canvas, minimumCoordinate, maximumCoordinate, minimumElevation,
            horizontalOffset, scale);
        DrawProjectedFloors(canvas, orientation, minimumCoordinate, minimumElevation,
            horizontalOffset, scale);
        foreach (var wall in walls)
        {
            DrawProjectedWall(canvas, wall, orientation, minimumCoordinate, minimumElevation,
                horizontalOffset, scale, ReferenceEquals(canvas, SectionCanvas));
        }
    }

    /// <summary>Draws persistent level datums across an orthographic view.</summary>
    private void DrawProjectedLevels(
        Canvas canvas,
        double minimumCoordinate,
        double maximumCoordinate,
        double minimumElevation,
        double horizontalOffset,
        double scale)
    {
        foreach (var level in _session.Document.Levels)
        {
            var elevationY = ProjectElevation(canvas, level.Elevation, minimumElevation, scale);
            var line = new Line
            {
                X1 = horizontalOffset - 28,
                X2 = horizontalOffset + (maximumCoordinate - minimumCoordinate) * scale + 28,
                Y1 = elevationY,
                Y2 = elevationY,
                Stroke = Brushes.SteelBlue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 5, 4 }
            };
            canvas.Children.Add(line);
            var label = new TextBlock
            {
                Text = $"{level.Name}  {level.Elevation:0.00} m",
                Foreground = Brushes.SteelBlue,
                FontSize = 11,
                Background = Brushes.White
            };
            Canvas.SetLeft(label, horizontalOffset - 105);
            Canvas.SetTop(label, elevationY - 18);
            canvas.Children.Add(label);
        }
    }

    /// <summary>Draws floor slabs as coordinated horizontal profiles.</summary>
    private void DrawProjectedFloors(
        Canvas canvas,
        ViewOrientation orientation,
        double minimumCoordinate,
        double minimumElevation,
        double horizontalOffset,
        double scale)
    {
        foreach (var floor in _session.Document.Floors)
        {
            var level = _session.Document.Levels.Single(candidate => candidate.Id == floor.LevelId);
            if (floor.IsToposolid)
            {
                DrawToposolidProfile(canvas, floor, level.Elevation, orientation, minimumCoordinate, minimumElevation,
                    horizontalOffset, scale);
                continue;
            }
            var projected = floor.Boundary.Select(point => ProjectCoordinate(point, orientation)).ToArray();
            var left = horizontalOffset + (projected.Min() - minimumCoordinate) * scale;
            var top = ProjectElevation(canvas, level.Elevation, minimumElevation, scale);
            var slab = new Rectangle
            {
                Width = Math.Max(3, (projected.Max() - projected.Min()) * scale),
                Height = Math.Max(3, floor.Thickness * scale),
                Fill = new SolidColorBrush(Color.FromRgb(127, 143, 134)),
                Stroke = Brushes.DarkSlateGray,
                StrokeThickness = 1
            };
            Canvas.SetLeft(slab, left);
            Canvas.SetTop(slab, top);
            canvas.Children.Add(slab);
        }
    }

    /// <summary>Draws one wall profile and its hosted voids in an orthographic view.</summary>
    private void DrawProjectedWall(
        Canvas canvas,
        Wall wall,
        ViewOrientation orientation,
        double minimumCoordinate,
        double minimumElevation,
        double horizontalOffset,
        double scale,
        bool useCutAppearance)
    {
        var level = _session.Document.Levels.Single(candidate => candidate.Id == wall.LevelId);
        var start = ProjectCoordinate(wall.Start, orientation);
        var end = ProjectCoordinate(wall.End, orientation);
        var projectedLength = Math.Abs(end - start);
        var left = horizontalOffset + (Math.Min(start, end) - minimumCoordinate) * scale;
        var top = ProjectElevation(canvas, level.Elevation + wall.Height, minimumElevation, scale);
        var wallWidth = Math.Max(4, projectedLength * scale);
        var wallType = _session.Document.WallTypes.Single(type => type.Id == wall.WallTypeId);
        var wallShape = new Rectangle
        {
            Width = wallWidth,
            Height = wall.Height * scale,
            Fill = useCutAppearance
                ? WallAppearanceRendering.CreateBrush(
                    wallType.SectionFillColor, wallType.SectionFillPattern)
                : WallAppearanceRendering.CreateBrush(wallType.PlanFillColor, WallFillPattern.Solid, 0.58),
            Stroke = Brushes.DarkSlateGray,
            StrokeThickness = 1
        };
        Canvas.SetLeft(wallShape, left);
        Canvas.SetTop(wallShape, top);
        canvas.Children.Add(wallShape);

        if (projectedLength < 0.01) return;
        foreach (var opening in _session.Document.Openings.Where(candidate => candidate.WallId == wall.Id))
        {
            var direction = (end - start) / wall.Length;
            var openingCenter = start + direction * opening.Offset;
            var openingWidth = Math.Max(2, Math.Abs(direction) * opening.Width * scale);
            var openingLeft = horizontalOffset + (openingCenter - minimumCoordinate) * scale - openingWidth / 2;
            var openingTop = ProjectElevation(
                canvas, level.Elevation + opening.Sill + opening.Height, minimumElevation, scale);
            var openingShape = new Rectangle
            {
                Width = openingWidth,
                Height = opening.Height * scale,
                Fill = Brushes.White,
                Stroke = opening.Kind == OpeningKind.Window ? Brushes.SteelBlue : Brushes.DarkSlateGray,
                StrokeThickness = 1
            };
            Canvas.SetLeft(openingShape, openingLeft);
            Canvas.SetTop(openingShape, openingTop);
            canvas.Children.Add(openingShape);
        }
    }

    /// <summary>Projects a plan coordinate onto the horizontal axis of a generated view.</summary>
    private static double ProjectCoordinate(Point2 point, ViewOrientation orientation) => orientation switch
    {
        // Seen from the opposite side the left-to-right axis runs backwards, so south and west mirror north and east.
        ViewOrientation.East => point.Y,
        ViewOrientation.South => -point.X,
        ViewOrientation.West => -point.Y,
        _ => point.X
    };

    /// <summary>Converts a model elevation into a canvas Y coordinate.</summary>
    private static double ProjectElevation(
        Canvas canvas, double elevation, double minimumElevation, double scale) =>
        canvas.ActualHeight - 45 - (elevation - minimumElevation) * scale;
}
