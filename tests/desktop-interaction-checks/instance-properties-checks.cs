using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Exercises the left palette against real instance transactions and selection changes.</summary>
    private static void InstancePropertiesChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var columnId = session.AddColumn(level, new Point2(710, 710));
        var initialColumn = session.Document.Columns.Single(column => column.Id == columnId);
        session.UpdateColumn(columnId, initialColumn.Width, 0.314159, initialColumn.Height, initialColumn.Shape);
        var floorId = session.AddFloor(level, [new(720, 720), new(724, 720), new(724, 724), new(720, 724)], 0.2);
        var wallId = session.AddWall(level, new(730, 730), new(736, 730));
        var openingId = session.AddOpening(wallId, OpeningKind.Window, 3);
        try
        {
            SetField("_selectedViewId", null);
            SetField("_selectedColumnId", (Guid?)columnId);
            Invoke("RefreshProperties");
            var editor = Field<StackPanel>("_instancePropertiesEditor");
            var inputs = Field<Dictionary<string, TextBox>>("_instancePropertyInputs");
            Check(editor.Visibility == Visibility.Visible && inputs.Keys.Order().SequenceEqual(new[] { "Depth", "Height", "Width" }),
                "Selecting a column exposes its dimensions in the left properties palette");
            var originalWidth = session.Document.Columns.Single(column => column.Id == columnId).Width;
            inputs["Width"].Text = "90 cm";
            var apply = editor.Children.OfType<Button>().First(button => Equals(button.Tag, "ApplyInstanceProperties"));
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Math.Abs(session.Document.Columns.Single(column => column.Id == columnId).Width - 0.9) < 1e-8,
                "Applying a unit-bearing width from the palette updates the selected column");
            Check(session.Document.Columns.Single(column => column.Id == columnId).Depth == 0.314159,
                "Editing width preserves the full model precision of an untouched depth");
            session.Undo();
            Check(Math.Abs(session.Document.Columns.Single(column => column.Id == columnId).Width - originalWidth) < 1e-8,
                "Undo restores the column edited from the properties palette");
            SetField("_selectedColumnId", null);
            SetField("_selectedFloorId", (Guid?)floorId);
            Invoke("RefreshProperties");
            inputs = Field<Dictionary<string, TextBox>>("_instancePropertyInputs");
            var thicknessInput = inputs["Thickness"];
            var originalText = thicknessInput.Text;
            thicknessInput.Text = "0.4 m";
            thicknessInput.Focus();
            thicknessInput.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(thicknessInput), 0, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(thicknessInput.Text == originalText && session.Document.Floors.Single(floor => floor.Id == floorId).Thickness == 0.2,
                "Escape restores a pending property edit without modifying the slab");
            thicknessInput.Text = "0.3 m";
            thicknessInput.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(thicknessInput), 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(session.Document.Floors.Single(floor => floor.Id == floorId).Thickness == 0.3,
                "Enter commits the focused instance property through its keyboard event");
            session.Undo();
            inputs = Field<Dictionary<string, TextBox>>("_instancePropertyInputs");
            inputs["Thickness"].Text = "invalid";
            apply = editor.Children.OfType<Button>().First(button => Equals(button.Tag, "ApplyInstanceProperties"));
            var undoName = session.UndoName;
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Math.Abs(session.Document.Floors.Single(floor => floor.Id == floorId).Thickness - 0.2) < 1e-8 &&
                  session.UndoName == undoName, "Invalid palette input leaves the selected slab and history unchanged");
            SetField("_selectedFloorId", null);
            SetField("_selectedWallId", (Guid?)wallId);
            SetField("_selectedOpeningId", (Guid?)openingId);
            Invoke("RefreshProperties");
            Check(((FrameworkElement)_window.FindName("WallInstanceExpander")).Visibility == Visibility.Collapsed &&
                  ((FrameworkElement)_window.FindName("WallOpeningExpander")).Visibility == Visibility.Visible &&
                  ((TextBlock)_window.FindName("PropertiesText")).Text.Contains("Ventana"),
                "Selecting a window shows opening properties rather than host-wall parameters");
        }
        finally
        {
            SetField("_selectedColumnId", null);
            SetField("_selectedFloorId", null);
            SetField("_selectedOpeningId", null);
            SetField("_selectedWallId", null);
            session.RemoveColumn(columnId);
            session.RemoveFloor(floorId);
            session.RemoveWall(wallId);
        }
    }
}
