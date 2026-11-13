using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Edits a wall through its actual 3D dimension control and verifies undo and invalid input.</summary>
    private static void ModelDimensionChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var wallId = session.AddWall(level, new Point2(100, 100), new Point2(104, 100));
        var tabs = (TabControl)_window.FindName("ViewTabs");
        try
        {
            tabs.SelectedIndex = 1;
            SetField("_selectedOpeningId", null);
            SetField("_selectedWallId", (Guid?)wallId);
            Invoke("RefreshAll");
            Pump();
            var overlay = (Canvas)_window.FindName("ModelDimensionOverlay");
            var chip = overlay.Children.OfType<Border>().First();
            ((Button)chip.Child).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var editor = (TextBox)chip.Child;
            editor.Text = "5 m";
            PressKey(editor, Key.Enter);
            Check(Math.Abs(session.Document.Walls.Single(wall => wall.Id == wallId).Length - 5) < 1e-6,
                "Entering a 3D wall dimension changes its length");
            session.Undo();
            Check(Math.Abs(session.Document.Walls.Single(wall => wall.Id == wallId).Length - 4) < 1e-6,
                "Undo restores the wall after a 3D dimension edit");
            Invoke("ApplyModelDimension", Guanacaste3D.Core.Navigation.PickedKind.Wall, wallId, 0, "invalid");
            Check(Math.Abs(session.Document.Walls.Single(wall => wall.Id == wallId).Length - 4) < 1e-6,
                "Invalid 3D dimension input leaves the wall unchanged");
        }
        finally
        {
            SetField("_selectedWallId", null);
            tabs.SelectedIndex = 0;
            session.RemoveWall(wallId);
        }
        ModelOpeningDatumChecks(session);
        ModelColumnDimensionChecks(session);
        ModelFloorThicknessChecks(session);
    }

    /// <summary>Places a window sill on its level using its 3D datum and verifies undo.</summary>
    private static void ModelOpeningDatumChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var wallId = session.AddWall(level, new Point2(130, 130), new Point2(134, 130));
        var openingId = session.AddOpening(wallId, OpeningKind.Window, 2);
        var tabs = (TabControl)_window.FindName("ViewTabs");
        try
        {
            tabs.SelectedIndex = 1;
            SetField("_selectedWallId", (Guid?)wallId);
            SetField("_selectedOpeningId", (Guid?)openingId);
            Invoke("RefreshAll");
            Pump();
            var opening = session.Document.Openings.Single(item => item.Id == openingId);
            var overlay = (Canvas)_window.FindName("ModelDimensionOverlay");
            var chip = overlay.Children.OfType<Border>().ElementAt(3);
            ((Button)chip.Child).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var editor = (TextBox)chip.Child;
            editor.Text = "0 m";
            PressKey(editor, Key.Enter);
            Check(session.Document.Openings.Single(item => item.Id == openingId).Sill == 0,
                "A zero 3D sill dimension places the window at its level");
            session.Undo();
            Check(session.Document.Openings.Single(item => item.Id == openingId).Sill == opening.Sill,
                "Undo restores the window sill edited in 3D");
        }
        finally
        {
            SetField("_selectedOpeningId", null);
            SetField("_selectedWallId", null);
            tabs.SelectedIndex = 0;
            session.RemoveWall(wallId);
        }
    }

    /// <summary>Verifies each editable column dimension in the real 3D overlay and its undo history.</summary>
    private static void ModelColumnDimensionChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var columnId = session.AddColumn(level, new Point2(110, 110), 0.4, 0.5, 3);
        var tabs = (TabControl)_window.FindName("ViewTabs");
        try
        {
            tabs.SelectedIndex = 1;
            SetField("_selectedWallId", null);
            SetField("_selectedOpeningId", null);
            SetField("_selectedColumnId", (Guid?)columnId);
            Invoke("RefreshAll");
            Pump();
            var overlay = (Canvas)_window.FindName("ModelDimensionOverlay");
            foreach (var (index, input) in new[] { (0, "0.6 m"), (1, "0.7 m"), (2, "4 m") })
            {
                var chip = overlay.Children.OfType<Border>().ElementAt(index);
                ((Button)chip.Child).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var editor = (TextBox)chip.Child;
                editor.Text = input;
                PressKey(editor, Key.Enter);
            }
            var column = session.Document.Columns.Single(item => item.Id == columnId);
            Check(Math.Abs(column.Width - 0.6) < 1e-8 && Math.Abs(column.Depth - 0.7) < 1e-8 &&
                  Math.Abs(column.Height - 4) < 1e-8 && column.Position == new Point2(110, 110),
                "3D column dimensions update width, depth and height while retaining the insertion point");
            session.Undo();
            Check(Math.Abs(session.Document.Columns.Single(item => item.Id == columnId).Height - 3) < 1e-8,
                "Undo restores the height edited through a 3D column dimension");
            var beforeInvalid = session.Document.Columns.Single(item => item.Id == columnId);
            Invoke("ApplyModelDimension", Guanacaste3D.Core.Navigation.PickedKind.Column, columnId, 0, "0.01 m");
            Check(session.Document.Columns.Single(item => item.Id == columnId) == beforeInvalid,
                "A rejected 3D column dimension leaves the model unchanged");
        }
        finally
        {
            SetField("_selectedColumnId", null);
            tabs.SelectedIndex = 0;
            session.RemoveColumn(columnId);
        }
    }


    /// <summary>Edits a flat slab thickness through the 3D overlay while preserving its top elevation.</summary>
    private static void ModelFloorThicknessChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var floorId = session.AddFloor(level,
            [new Point2(120, 120), new Point2(124, 120), new Point2(124, 124), new Point2(120, 124)],
            0.2, elevationOffset: 0.5);
        var tabs = (TabControl)_window.FindName("ViewTabs");
        try
        {
            tabs.SelectedIndex = 1;
            SetField("_selectedWallId", null);
            SetField("_selectedOpeningId", null);
            SetField("_selectedColumnId", null);
            SetField("_selectedFloorId", (Guid?)floorId);
            Invoke("RefreshAll");
            Pump();
            var overlay = (Canvas)_window.FindName("ModelDimensionOverlay");
            var chip = overlay.Children.OfType<Border>().ElementAt(2);
            ((Button)chip.Child).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var editor = (TextBox)chip.Child;
            editor.Text = "0.3 m";
            PressKey(editor, Key.Enter);
            var floor = session.Document.Floors.Single(item => item.Id == floorId);
            Check(Math.Abs(floor.Thickness - 0.3) < 1e-8 && Math.Abs(floor.ElevationOffset - 0.5) < 1e-8,
                "Editing a flat slab thickness in 3D preserves its top elevation");
            session.Undo();
            Check(Math.Abs(session.Document.Floors.Single(item => item.Id == floorId).Thickness - 0.2) < 1e-8,
                "Undo restores the flat slab thickness edited in 3D");
            var datumChip = overlay.Children.OfType<Border>().ElementAt(3);
            ((Button)datumChip.Child).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var datumEditor = (TextBox)datumChip.Child;
            datumEditor.Text = "1.2 m";
            PressKey(datumEditor, Key.Enter);
            floor = session.Document.Floors.Single(item => item.Id == floorId);
            Check(Math.Abs(floor.ElevationOffset - 1.4) < 1e-8 && Math.Abs(floor.Thickness - 0.2) < 1e-8,
                "Editing the 3D underside dimension moves a slab while preserving its thickness");
            session.Undo();
            Check(Math.Abs(session.Document.Floors.Single(item => item.Id == floorId).ElevationOffset - 0.5) < 1e-8,
                "Undo restores the slab elevation edited through its underside dimension");
            datumChip = overlay.Children.OfType<Border>().ElementAt(3);
            ((Button)datumChip.Child).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            datumEditor = (TextBox)datumChip.Child;
            datumEditor.Text = "0 m";
            PressKey(datumEditor, Key.Enter);
            Check(Math.Abs(session.Document.Floors.Single(item => item.Id == floorId).ElevationOffset - 0.2) < 1e-8,
                "A zero underside dimension places the slab underside on its level");

        }
        finally
        {
            SetField("_selectedFloorId", null);
            tabs.SelectedIndex = 0;
            session.RemoveFloor(floorId);
        }
    }

}
