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
    private bool _creatingGrid;
    private Point2? _gridStart;
    private Point2? _gridPointer;
    private Guid? _selectedGridId;

    /// <summary>Starts a project datum rather than toggling the unrelated background snap grid.</summary>
    private void BeginGridTool()
    {
        SetWallTool(false);
        _selectedElectricalId = null;
        _creatingGrid = true;
        ViewTabs.SelectedIndex = 0;
        PlanCanvas.Cursor = Cursors.Cross;
        Keyboard.Focus(PlanCanvas);
        StatusText.Text = GetText("Documentation.GridFirstPoint");
    }

    /// <summary>Discards provisional endpoints and selection when another tool becomes active.</summary>
    private void CancelGridTool()
    {
        _creatingGrid = false;
        _gridStart = _gridPointer = null;
        _selectedGridId = null;
    }

    /// <summary>Updates the full axis preview from snapped model coordinates.</summary>
    private bool TryUpdateGridPreview(MouseEventArgs e)
    {
        if (!_creatingGrid) return false;
        _gridPointer = SnapWallPoint(CanvasToWorld(PlanPosition(e)));
        RenderPlan();
        return true;
    }

    /// <summary>Accepts two endpoints and automatically offers the next unused numeric reference.</summary>
    private bool TryHandleGridPlacement(MouseButtonEventArgs e)
    {
        if (!_creatingGrid) return false;
        e.Handled = true;
        var point = SnapWallPoint(CanvasToWorld(PlanPosition(e)));
        if (_gridStart is not Point2 start)
        {
            _gridStart = _gridPointer = point;
            StatusText.Text = GetText("Documentation.GridSecondPoint");
            return true;
        }
        if (!new SectionCut(start, point).IsValid)
        {
            StatusText.Text = GetText("Documentation.SectionTooShort");
            return true;
        }
        var number = 1;
        while (_session.Document.Grids.Any(axis => axis.Name == number.ToString())) number++;
        _selectedGridId = _session.AddGridAxis(number.ToString(), start, point);
        _gridStart = _gridPointer = null;
        RefreshAll();
        StatusText.Text = GetText("Documentation.GridFirstPoint");
        return true;
    }

    /// <summary>Draws named project axes on every plan, with bubble heads independent of zoom.</summary>
    private void RenderGridAxes()
    {
        foreach (var axis in _session.Document.Grids) DrawGridAxis(axis, false);
        if (_creatingGrid && _gridStart is Point2 start && _gridPointer is Point2 end)
            DrawGridAxis(new(Guid.Empty, GetText("Documentation.GridPreview"), start, end), true);
    }

    /// <summary>Builds thin dashed datums whose context menu edits the reference without touching the model.</summary>
    private void DrawGridAxis(GridAxis axis, bool preview)
    {
        var start = WorldToCanvas(axis.Start);
        var end = WorldToCanvas(axis.End);
        var brush = preview || axis.Id == _selectedGridId ? Brushes.RoyalBlue : Brushes.SlateGray;
        var menu = preview ? null : new ContextMenu();
        if (menu is not null)
        {
            var rename = new MenuItem { Header = GetText("Documentation.RenameAxis"), Tag = axis.Id };
            rename.Click += RenameGridAxis_Click;
            var remove = new MenuItem { Header = GetText("Documentation.DeleteAxis"), Tag = axis.Id };
            remove.Click += DeleteGridAxis_Click;
            menu.Items.Add(rename);
            menu.Items.Add(remove);
        }
        PlanCanvas.Children.Add(new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y,
            Stroke = brush, StrokeThickness = 1, StrokeDashArray = [10, 3, 2, 3],
            Tag = axis.Id, ContextMenu = menu, IsHitTestVisible = !preview });
        foreach (var point in new[] { start, end })
        {
            var head = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13),
                BorderBrush = brush, BorderThickness = new Thickness(1), Background = Brushes.White,
                Tag = axis.Id, ContextMenu = menu, IsHitTestVisible = !preview,
                Child = new TextBlock { Text = axis.Name, FontSize = 11, Foreground = brush,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false } };
            Canvas.SetLeft(head, point.X - 13);
            Canvas.SetTop(head, point.Y - 13);
            PlanCanvas.Children.Add(head);
        }
    }

    /// <summary>Selects a datum independently of nearby walls; double-click edits its name.</summary>
    private bool TrySelectGridAxis(MouseButtonEventArgs e)
    {
        _selectedGridId = null;
        if ((e.OriginalSource as FrameworkElement)?.Tag is not Guid id ||
            !_session.Document.Grids.Any(axis => axis.Id == id)) return false;
        _selectedGridId = id;
        _selectedElectricalId = null;
        _selectedViewId = _selectedWallId = _selectedOpeningId = _selectedColumnId = _selectedStructuralId = null;
        _selectedRoomId = _selectedFloorId = null;
        if (e.ClickCount > 1) RenameGridAxis(id);
        else { RefreshProperties(); RenderPlan(); }
        e.Handled = true;
        return true;
    }

    /// <summary>Routes the datum context menu to the shared rename operation.</summary>
    private void RenameGridAxis_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: Guid id }) RenameGridAxis(id);
    }

    /// <summary>Validates a user-supplied reference name and preserves dimensions attached to its identity.</summary>
    private void RenameGridAxis(Guid id)
    {
        if (_session.Document.Grids.FirstOrDefault(axis => axis.Id == id) is not { } axis) return;
        var name = PromptWindow.Ask(this, GetText("Documentation.RenameAxis"), axis.Name);
        if (name is null) return;
        try { _session.UpdateGridAxis(id, name, axis.Start, axis.End); RefreshAll(); }
        catch (InvalidDataException exception) { StatusText.Text = LocalizedErrors.Translate(exception.Message); }
    }

    /// <summary>Deletes one datum and its annotations as an undoable operation.</summary>
    private void DeleteGridAxis_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: Guid id }) return;
        _session.RemoveGridAxis(id);
        _selectedGridId = null;
        RefreshAll();
    }
}
