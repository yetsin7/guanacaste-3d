using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Adds a level and makes it the active plan.</summary>
    private void AddLevel_Click(object sender, RoutedEventArgs e)
    {
        _activeLevelId = _session.AddLevel();
        RefreshAll();
    }

    /// <summary>Switches the active plan when the level selector changes.</summary>
    private void LevelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingLevels || LevelPicker.SelectedItem is not ComboBoxItem item || item.Tag is not Guid id)
            return;
        _activeLevelId = id;
        _selectedViewId = _session.Document.Views.First(view =>
            view.Kind == ProjectViewKind.Plan && view.LevelId == id).Id;
        _selectedWallId = null;
        _pendingWallStart = null;
        RefreshAll();
    }

    /// <summary>Coordinates project-browser selection with the active model selection.</summary>
    private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item || item.Tag is not Guid id) return;
        if (TryHandleProjectTreeSelection(id)) return;
        _selectedViewId = null;
        if (_session.Document.Openings.FirstOrDefault(opening => opening.Id == id) is WallOpening opening)
        {
            _selectedOpeningId = id;
            _selectedWallId = opening.WallId;
            RefreshProperties();
            RenderPlan();
        }
        else if (_session.Document.Walls.Any(wall => wall.Id == id))
        {
            _selectedOpeningId = null;
            _selectedWallId = id;
            RefreshProperties();
            RenderPlan();
        }
        else if (_session.Document.Levels.Any(level => level.Id == id))
        {
            _activeLevelId = id;
            _selectedViewId = _session.Document.Views.First(view =>
                view.Kind == ProjectViewKind.Plan && view.LevelId == id).Id;
            _selectedWallId = null;
            _selectedOpeningId = null;
            RefreshLevelPicker();
            RenderPlan();
            RefreshProperties();
        }
    }

    /// <summary>Handles wall placement and element selection in the plan canvas.</summary>
    private void PlanCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryHandleRoomPlacement(e)) return;
        if (!_drawingWall)
        {
            if (TrySelectExtendedPlanElement(e)) return;
            _selectedViewId = null;
            _selectedWallId = (e.OriginalSource as FrameworkElement)?.Tag is Guid id
                && _session.Document.Walls.Any(wall => wall.Id == id) ? id : null;
            _selectedWallOffset = null;
            _selectedOpeningId = null;
            if (_selectedWallId is Guid wallId)
            {
                var wall = _session.Document.Walls.First(candidate => candidate.Id == wallId);
                var clicked = CanvasToWorld(e.GetPosition(PlanCanvas));
                var directionX = (wall.End.X - wall.Start.X) / wall.Length;
                var directionY = (wall.End.Y - wall.Start.Y) / wall.Length;
                _selectedWallOffset = Math.Clamp(
                    (clicked.X - wall.Start.X) * directionX + (clicked.Y - wall.Start.Y) * directionY,
                    0, wall.Length);
            }
            RefreshProperties();
            RenderPlan();
            return;
        }

        var point = CanvasToWorld(e.GetPosition(PlanCanvas));
        if (SnapGridCheck.IsChecked == true)
        {
            point = new Point2(
                Math.Round(point.X * 10, MidpointRounding.AwayFromZero) / 10,
                Math.Round(point.Y * 10, MidpointRounding.AwayFromZero) / 10);
        }
        if (_pendingWallStart is null)
        {
            _pendingWallStart = point;
            StatusText.Text = GetText("Status.WallSecondPoint");
            RenderPlan();
            return;
        }
        try
        {
            var wallTypeId = (WallTypePicker.SelectedItem as ComboBoxItem)?.Tag as Guid?;
            _selectedWallId = _session.AddWall(
                _activeLevelId, _pendingWallStart.Value, point, wallTypeId: wallTypeId);
            _selectedViewId = null;
            _pendingWallStart = null;
            StatusText.Text = GetText("Status.WallCreated");
            RefreshAll();
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.InvalidGeometry"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Cancels any in-progress sketch when the secondary mouse button is pressed.</summary>
    private void PlanCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pendingWallStart = null;
        CancelExtendedDrawing();
        RenderPlan();
        StatusText.Text = GetText("Status.DrawingCancelled");
    }

    /// <summary>Zooms the plan around the pointer position.</summary>
    private void PlanCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var cursor = e.GetPosition(PlanCanvas);
        var anchor = CanvasToWorld(cursor);
        _scale = Math.Clamp(_scale * (e.Delta > 0 ? 1.15 : 1.0 / 1.15), 8, 200);
        _panOffset = new Point(
            cursor.X - PlanCanvas.ActualWidth / 2 - anchor.X * _scale,
            cursor.Y - PlanCanvas.ActualHeight / 2 + anchor.Y * _scale);
        RenderPlan();
    }

    /// <summary>Redraws the plan after the viewport size changes.</summary>
    private void PlanCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderPlan();

    /// <summary>Transforms a model coordinate into the current plan-canvas coordinate system.</summary>
    private Point WorldToCanvas(Point2 point) =>
        new(PlanCanvas.ActualWidth / 2 + _panOffset.X + point.X * _scale,
            PlanCanvas.ActualHeight / 2 + _panOffset.Y - point.Y * _scale);

    /// <summary>Transforms a plan-canvas coordinate into model space.</summary>
    private Point2 CanvasToWorld(Point point) =>
        new((point.X - PlanCanvas.ActualWidth / 2 - _panOffset.X) / _scale,
            (PlanCanvas.ActualHeight / 2 + _panOffset.Y - point.Y) / _scale);

    /// <summary>Fits the active level geometry into the available plan viewport.</summary>
    private void FitPlan_Click(object sender, RoutedEventArgs e)
    {
        var walls = _session.Document.Walls.Where(wall => wall.LevelId == _activeLevelId).ToArray();
        if (walls.Length == 0)
        {
            _scale = 36;
            _panOffset = new Point();
            RenderPlan();
            StatusText.Text = GetText("Status.PlanCentered");
            return;
        }
        var points = walls.SelectMany(wall => new[] { wall.Start, wall.End }).ToArray();
        var minimumX = points.Min(point => point.X);
        var maximumX = points.Max(point => point.X);
        var minimumY = points.Min(point => point.Y);
        var maximumY = points.Max(point => point.Y);
        var width = Math.Max(1, PlanCanvas.ActualWidth - 100);
        var height = Math.Max(1, PlanCanvas.ActualHeight - 100);
        _scale = Math.Clamp(Math.Min(
            width / Math.Max(1, maximumX - minimumX),
            height / Math.Max(1, maximumY - minimumY)), 8, 200);
        _panOffset = new Point(
            -(minimumX + maximumX) / 2 * _scale,
            (minimumY + maximumY) / 2 * _scale);
        RenderPlan();
        StatusText.Text = GetText("Status.PlanFit");
    }

    /// <summary>Toggles the plan grid from either ribbon or menu commands.</summary>
    private void ToggleGrid_Click(object sender, RoutedEventArgs e) =>
        ShowGridCheck.IsChecked = ShowGridCheck.IsChecked != true;

    /// <summary>Redraws the plan when the grid visibility changes.</summary>
    private void GridVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (PlanCanvas is not null) RenderPlan();
    }

    /// <summary>Renders the active coordinated plan, including grid, BIM elements, and sketch feedback.</summary>
    private void RenderPlan()
    {
        if (PlanCanvas is null || _session is null) return;
        PlanCanvas.Children.Clear();
        if (PlanCanvas.ActualWidth < 1 || PlanCanvas.ActualHeight < 1) return;
        var left = -PlanCanvas.ActualWidth / 2 - _panOffset.X;
        var right = PlanCanvas.ActualWidth / 2 - _panOffset.X;
        var top = -PlanCanvas.ActualHeight / 2 - _panOffset.Y;
        var bottom = PlanCanvas.ActualHeight / 2 - _panOffset.Y;
        if (ShowGridCheck.IsChecked == true)
        {
            for (var gridX = Math.Ceiling(left / _scale); gridX <= right / _scale; gridX++)
                AddLine(new Point2(gridX, -bottom / _scale), new Point2(gridX, -top / _scale), 0.5, Brushes.Gainsboro);
            for (var gridY = Math.Ceiling(top / _scale); gridY <= bottom / _scale; gridY++)
                AddLine(new Point2(left / _scale, -gridY), new Point2(right / _scale, -gridY), 0.5, Brushes.Gainsboro);
        }
        AddLine(new Point2(left / _scale, 0), new Point2(right / _scale, 0), 1, Brushes.LightSteelBlue);
        AddLine(new Point2(0, -bottom / _scale), new Point2(0, -top / _scale), 1, Brushes.LightSteelBlue);
        RenderExtendedPlanElements();
        foreach (var wall in _session.Document.Walls.Where(candidate => candidate.LevelId == _activeLevelId))
        {
            var color = wall.Id == _selectedWallId ? Brushes.DarkOrange : Brushes.DarkSlateGray;
            AddLine(wall.Start, wall.End, Math.Max(3, wall.Thickness * _scale), color, wall.Id);
            foreach (var opening in _session.Document.Openings.Where(candidate => candidate.WallId == wall.Id))
            {
                var directionX = (wall.End.X - wall.Start.X) / wall.Length;
                var directionY = (wall.End.Y - wall.Start.Y) / wall.Length;
                var from = new Point2(
                    wall.Start.X + directionX * opening.Begin,
                    wall.Start.Y + directionY * opening.Begin);
                var to = new Point2(
                    wall.Start.X + directionX * opening.End,
                    wall.Start.Y + directionY * opening.End);
                AddLine(from, to, Math.Max(4, wall.Thickness * _scale + 2), Brushes.White, wall.Id);
                if (opening.Kind == OpeningKind.Window) AddLine(from, to, 2, Brushes.SteelBlue, wall.Id);
            }
        }
        if (_pendingWallStart is Point2 start)
        {
            var canvasPoint = WorldToCanvas(start);
            var marker = new Ellipse { Width = 12, Height = 12, Fill = Brushes.ForestGreen };
            Canvas.SetLeft(marker, canvasPoint.X - 6);
            Canvas.SetTop(marker, canvasPoint.Y - 6);
            PlanCanvas.Children.Add(marker);
        }
    }

    /// <summary>Adds a selectable model-space line to the plan canvas.</summary>
    private void AddLine(Point2 start, Point2 end, double thickness, Brush color, Guid? tag = null)
    {
        var startPoint = WorldToCanvas(start);
        var endPoint = WorldToCanvas(end);
        var line = new Line
        {
            X1 = startPoint.X,
            Y1 = startPoint.Y,
            X2 = endPoint.X,
            Y2 = endPoint.Y,
            Stroke = color,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Square,
            StrokeEndLineCap = PenLineCap.Square,
            SnapsToDevicePixels = true,
            Tag = tag
        };
        PlanCanvas.Children.Add(line);
    }
}
