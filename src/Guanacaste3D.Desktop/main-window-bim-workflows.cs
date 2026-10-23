using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>Provides localized values for the coordinated schedule grid.</summary>
internal sealed record ScheduleDisplayRow(
    string Category,
    string Number,
    string Name,
    string Level,
    double Quantity,
    string Unit);

public partial class MainWindow
{
    private bool _drawingRoom;
    private Point2? _pendingRoomStart;
    private Guid? _selectedRoomId;
    private Guid? _selectedFloorId;
    private Guid? _selectedSheetId;

    /// <summary>Resolves a user-facing string from the active WPF resource dictionary.</summary>
    private static string GetText(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    /// <summary>Formats a localized string with culture-aware values.</summary>
    private static string FormatText(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, GetText(key), values);

    /// <summary>Starts rectangular room placement in the active plan view.</summary>
    private void Room_Click(object sender, RoutedEventArgs e)
    {
        _drawingWall = false;
        _drawingRoom = true;
        _selectedViewId = null;
        _pendingWallStart = null;
        _pendingRoomStart = null;
        ViewTabs.SelectedIndex = 0;
        StatusText.Text = GetText("Status.RoomFirstCorner");
        RenderPlan();
    }

    /// <summary>Creates a floor from the bounding rectangle of active-level walls.</summary>
    private void Floor_Click(object sender, RoutedEventArgs e)
    {
        var walls = _session.Document.Walls.Where(wall => wall.LevelId == _activeLevelId).ToArray();
        if (walls.Length < 2)
        {
            StatusText.Text = GetText("Status.FloorNeedsWalls");
            return;
        }
        var points = walls.SelectMany(wall => new[] { wall.Start, wall.End }).ToArray();
        var minimumX = points.Min(point => point.X);
        var maximumX = points.Max(point => point.X);
        var minimumY = points.Min(point => point.Y);
        var maximumY = points.Max(point => point.Y);
        if (maximumX - minimumX < 0.1 || maximumY - minimumY < 0.1)
        {
            StatusText.Text = GetText("Status.FloorNeedsEnclosure");
            return;
        }
        var index = _session.Document.Floors.Count + 1;
        _selectedFloorId = _session.AddFloor(_activeLevelId,
        [
            new Point2(minimumX, minimumY),
            new Point2(maximumX, minimumY),
            new Point2(maximumX, maximumY),
            new Point2(minimumX, maximumY)
        ], name: $"{GetText("Entity.Floor")} {index}");
        StatusText.Text = GetText("Status.FloorCreated");
        RefreshAll();
    }

    /// <summary>Adds an A1 drawing sheet and opens the sheet workspace.</summary>
    private void AddSheet_Click(object sender, RoutedEventArgs e)
    {
        var index = _session.Document.Sheets.Count + 1;
        _selectedSheetId = _session.AddSheet(
            SheetPaperSize.A1, $"{GetText("Entity.Sheet")} {index}");
        _selectedViewId = null;
        ViewTabs.SelectedIndex = 3;
        StatusText.Text = GetText("Status.SheetCreated");
        RefreshAll();
    }

    /// <summary>Displays the coordinated project schedule.</summary>
    private void ShowSchedule_Click(object sender, RoutedEventArgs e) =>
        SelectProjectView(BuiltInProjectViews.ProjectScheduleId);

    /// <summary>Displays the drawing-sheet workspace.</summary>
    private void ShowSheet_Click(object sender, RoutedEventArgs e)
    {
        _selectedViewId = null;
        ViewTabs.SelectedIndex = 3;
    }

    /// <summary>Assigns the selected catalog type to the selected wall.</summary>
    private void ApplyWallType_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid wallId)
        {
            StatusText.Text = GetText("Status.SelectWallForType");
            return;
        }
        if (WallTypePicker.SelectedItem is not ComboBoxItem { Tag: Guid wallTypeId }) return;
        _session.SetWallType(wallId, wallTypeId);
        StatusText.Text = GetText("Status.WallTypeApplied");
    }

    /// <summary>Refreshes project-browser matches while the search query changes.</summary>
    private void ProjectSearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ProjectTree is not null && _session is not null) RefreshTree();
    }

    /// <summary>Updates the status bar when the active workspace tab changes.</summary>
    private void ViewTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, ViewTabs)) return;
        if (ActiveViewText is null || ViewTabs.SelectedItem is not TabItem tab) return;
        ActiveViewText.Text = tab.Header?.ToString() ?? string.Empty;
    }

    /// <summary>Completes room placement when the plan canvas receives two corners.</summary>
    private bool TryHandleRoomPlacement(MouseButtonEventArgs e)
    {
        if (!_drawingRoom) return false;
        var point = CanvasToWorld(e.GetPosition(PlanCanvas));
        if (SnapGridCheck.IsChecked == true)
            point = new Point2(
                Math.Round(point.X * 10, MidpointRounding.AwayFromZero) / 10,
                Math.Round(point.Y * 10, MidpointRounding.AwayFromZero) / 10);
        if (_pendingRoomStart is null)
        {
            _pendingRoomStart = point;
            StatusText.Text = GetText("Status.RoomSecondCorner");
            return true;
        }
        var index = _session.Document.Rooms.Count + 1;
        _selectedRoomId = _session.AddRoom(
            _activeLevelId, _pendingRoomStart.Value, point, $"{GetText("Entity.Room")} {index}");
        _pendingRoomStart = null;
        _drawingRoom = false;
        StatusText.Text = GetText("Status.RoomCreated");
        RefreshAll();
        return true;
    }

    /// <summary>Cancels room placement and clears its provisional corner.</summary>
    private void CancelExtendedDrawing()
    {
        _drawingRoom = false;
        _pendingRoomStart = null;
    }

    /// <summary>Selects a room or floor from its rendered plan element.</summary>
    private bool TrySelectExtendedPlanElement(MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.Tag is not Guid id) return false;
        if (_session.Document.Rooms.Any(room => room.Id == id))
        {
            SelectExtendedElement(roomId: id);
            return true;
        }
        if (_session.Document.Floors.Any(floor => floor.Id == id))
        {
            SelectExtendedElement(floorId: id);
            return true;
        }
        return false;
    }

    /// <summary>Centralizes selection state for rooms, floors, and sheets.</summary>
    private void SelectExtendedElement(Guid? roomId = null, Guid? floorId = null, Guid? sheetId = null)
    {
        _selectedRoomId = roomId;
        _selectedFloorId = floorId;
        _selectedSheetId = sheetId;
        _selectedViewId = null;
        _selectedWallId = null;
        _selectedOpeningId = null;
        RefreshProperties();
        RenderPlan();
        RenderSheet();
    }

    /// <summary>Handles project-browser selection for new coordinated BIM entities.</summary>
    private bool TryHandleProjectTreeSelection(Guid id)
    {
        if (_session.Document.Views.Any(view => view.Id == id))
        {
            SelectProjectView(id);
            return true;
        }
        var room = _session.Document.Rooms.FirstOrDefault(candidate => candidate.Id == id);
        if (room is not null)
        {
            _activeLevelId = room.LevelId;
            ViewTabs.SelectedIndex = 0;
            SelectExtendedElement(roomId: id);
            return true;
        }
        var floor = _session.Document.Floors.FirstOrDefault(candidate => candidate.Id == id);
        if (floor is not null)
        {
            _activeLevelId = floor.LevelId;
            ViewTabs.SelectedIndex = 0;
            SelectExtendedElement(floorId: id);
            return true;
        }
        if (_session.Document.Sheets.Any(sheet => sheet.Id == id))
        {
            ViewTabs.SelectedIndex = 3;
            SelectExtendedElement(sheetId: id);
            return true;
        }
        var type = _session.Document.WallTypes.FirstOrDefault(candidate => candidate.Id == id);
        if (type is null) return false;
        WallTypePicker.SelectedItem = WallTypePicker.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => Equals(item.Tag, id));
        PropertiesText.Text = $"{GetText("Tool.WallType")}: {type.Name}\n{type.Function}\n{type.Thickness:0.00} m\n{type.MaterialName}";
        return true;
    }

    /// <summary>Deletes the selected room, floor, or sheet when one owns selection.</summary>
    private bool DeleteExtendedSelection()
    {
        if (_selectedRoomId is Guid roomId) _session.RemoveRoom(roomId);
        else if (_selectedFloorId is Guid floorId) _session.RemoveFloor(floorId);
        else if (_selectedSheetId is Guid sheetId) _session.RemoveSheet(sheetId);
        else return false;
        _selectedRoomId = null;
        _selectedFloorId = null;
        _selectedSheetId = null;
        RefreshAll();
        return true;
    }

    /// <summary>Refreshes wall types, schedules, sheets, and extended selection state.</summary>
    private void RefreshExtendedWorkflows()
    {
        RefreshWallTypePicker();
        RefreshSchedule();
        RenderSheet();
        RenderOrthographicViews();
    }

    /// <summary>Synchronizes the wall-type picker with the project catalog and selection.</summary>
    private void RefreshWallTypePicker()
    {
        var selectedWall = _session.Document.Walls.FirstOrDefault(wall => wall.Id == _selectedWallId);
        var selectedTypeId = selectedWall?.WallTypeId ??
            (WallTypePicker.SelectedItem as ComboBoxItem)?.Tag as Guid? ?? BuiltInWallTypes.Basic200Id;
        WallTypePicker.Items.Clear();
        foreach (var type in _session.Document.WallTypes)
        {
            var item = new ComboBoxItem { Content = $"{type.Name} · {type.Thickness * 1000:0} mm", Tag = type.Id };
            WallTypePicker.Items.Add(item);
            if (type.Id == selectedTypeId) WallTypePicker.SelectedItem = item;
        }
    }

    /// <summary>Builds localized rows for the coordinated project schedule.</summary>
    private void RefreshSchedule()
    {
        ScheduleGrid.ItemsSource = ProjectScheduleService.Build(_session.Document)
            .Select(row => new ScheduleDisplayRow(
                GetText($"Schedule.{row.Category}"), row.Number, row.Name,
                row.Level, row.Quantity, row.Unit))
            .ToArray();
    }

    /// <summary>Adds wall types, rooms, floors, schedules, and sheets to the project tree.</summary>
    private void AppendExtendedProjectTree(TreeViewItem project)
    {
        var query = ProjectSearchInput?.Text?.Trim() ?? string.Empty;

        var types = new TreeViewItem { Header = GetText("Tree.WallTypes"), IsExpanded = query.Length > 0 };
        foreach (var type in _session.Document.WallTypes.Where(type => MatchesProjectSearch(type.Name)))
            types.Items.Add(new TreeViewItem { Header = type.Name, Tag = type.Id });
        var rooms = new TreeViewItem { Header = GetText("Tree.Rooms"), IsExpanded = query.Length > 0 };
        foreach (var room in _session.Document.Rooms.Where(room =>
                     MatchesProjectSearch(room.Name) || MatchesProjectSearch(room.Number)))
            rooms.Items.Add(new TreeViewItem { Header = $"{room.Number} · {room.Name}", Tag = room.Id });
        var floors = new TreeViewItem { Header = GetText("Tree.Floors"), IsExpanded = query.Length > 0 };
        foreach (var floor in _session.Document.Floors.Where(floor => MatchesProjectSearch(floor.Name)))
            floors.Items.Add(new TreeViewItem { Header = floor.Name, Tag = floor.Id });
        var sheets = new TreeViewItem { Header = GetText("Tree.Sheets"), IsExpanded = true };
        foreach (var sheet in _session.Document.Sheets.Where(sheet =>
                     MatchesProjectSearch(sheet.Name) || MatchesProjectSearch(sheet.Number)))
            sheets.Items.Add(new TreeViewItem { Header = $"{sheet.Number} · {sheet.Name}", Tag = sheet.Id });

        project.Items.Add(types);
        project.Items.Add(rooms);
        project.Items.Add(floors);
        project.Items.Add(sheets);
    }

    /// <summary>Determines whether a project-browser label matches the active search query.</summary>
    private bool MatchesProjectSearch(string value)
    {
        var query = ProjectSearchInput?.Text?.Trim() ?? string.Empty;
        return query.Length == 0 || value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>Updates the properties palette for rooms, floors, sheets, and wall types.</summary>
    private bool TryRefreshExtendedProperties()
    {
        var selectedView = _session.Document.Views.FirstOrDefault(view => view.Id == _selectedViewId);
        if (selectedView is not null)
        {
            PropertiesText.Text = FormatText(
                "Properties.ViewSummary", selectedView.Name, selectedView.Kind, selectedView.Orientation);
            return true;
        }
        var levelNames = _session.Document.Levels.ToDictionary(level => level.Id, level => level.Name);
        var room = _session.Document.Rooms.FirstOrDefault(candidate => candidate.Id == _selectedRoomId);
        if (room is not null)
        {
            PropertiesText.Text = FormatText(
                "Properties.RoomSummary", room.Number, room.Name, room.Area, levelNames[room.LevelId]);
            return true;
        }
        var floor = _session.Document.Floors.FirstOrDefault(candidate => candidate.Id == _selectedFloorId);
        if (floor is not null)
        {
            PropertiesText.Text = FormatText(
                "Properties.FloorSummary", floor.Name, floor.CalculateArea(), floor.Thickness, levelNames[floor.LevelId]);
            return true;
        }
        var sheet = _session.Document.Sheets.FirstOrDefault(candidate => candidate.Id == _selectedSheetId);
        if (sheet is null) return false;
        PropertiesText.Text = FormatText("Properties.SheetSummary", sheet.Number, sheet.Name, sheet.PaperSize);
        return true;
    }

}
