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
    }
}
