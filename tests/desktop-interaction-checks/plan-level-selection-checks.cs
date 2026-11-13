using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Ensures plan selection cannot include same-type elements on another level.</summary>
    private static void PlanLevelSelectionChecks(DocumentSession session)
    {
        var activeLevel = Field<Guid>("_activeLevelId");
        var upperLevel = session.AddLevel();
        var lowerWall = session.AddWall(activeLevel, new Point2(500, 500), new Point2(504, 500));
        var upperWall = session.AddWall(upperLevel, new Point2(500, 510), new Point2(504, 510));
        var door = session.AddOpening(lowerWall, OpeningKind.Door, 2);
        try
        {
            SetField("_selectedOpeningId", null);
            SetField("_selectedWallId", (Guid?)lowerWall);
            SetField("_viewSelection", null);
            Invoke("SelectSameTypeInView");
            var selection = Field<HashSet<Guid>>("_viewSelection");
            Check(selection.Contains(lowerWall) && !selection.Contains(upperWall),
                "Selecting matching instances in plan excludes walls on another level");
            Invoke("SelectAllInView");
            selection = Field<HashSet<Guid>>("_viewSelection");
            Check(selection.Contains(lowerWall) && selection.Contains(door) && !selection.Contains(upperWall),
                "Selecting all in plan includes visible openings only on the active level");
        }
        finally
        {
            SetField("_viewSelection", null);
            SetField("_selectedWallId", null);
            session.RemoveWall(lowerWall);
            session.RemoveWall(upperWall);
            session.RemoveLevel(upperLevel);
        }
        ZeroClearancePlanChecks(session);
    }

    /// <summary>Exercises zero-clearance alignment through the actual plan editor and undo history.</summary>
    private static void ZeroClearancePlanChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var wallId = session.AddWall(level, new Point2(600, 600), new Point2(606, 600));
        var columnId = session.AddColumn(level, new Point2(603, 604));
        try
        {
            SetField("_selectedWallId", null);
            SetField("_selectedOpeningId", null);
            SetField("_selectedColumnId", (Guid?)columnId);
            Invoke("RenderPlan");
            Pump();
            var label = _canvas.Children.OfType<Button>().First(button => button.Tag is PlanDimension dimension &&
                dimension.Kind == PlanDimensionKind.Below && dimension.OwnerId == columnId && dimension.ReferenceId == wallId);
            label.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
            var editor = _canvas.Children.OfType<TextBox>().Single(box => box.Tag is PlanDimension dimension &&
                dimension.OwnerId == columnId && dimension.Kind == PlanDimensionKind.Below);
            editor.Text = "0 m";
            PressKey(editor, Key.Enter);
            var gap = PlanDimensions.ForSelection(session.Document, columnId).Single(item =>
                item.Kind == PlanDimensionKind.Below && item.ReferenceId == wallId);
            Check(Math.Abs(PlanDimensions.Resolve(session.Document, gap)!.Value.Value) < 1e-8,
                "A zero plan clearance aligns a column and keeps its dimension editable");
            session.Undo();
            Check(PlanDimensions.Resolve(session.Document, gap)!.Value.Value > 1,
                "Undo restores the column's clearance after zero-distance alignment");
        }
        finally
        {
            SetField("_selectedColumnId", null);
            session.RemoveColumn(columnId);
            session.RemoveWall(wallId);
        }
    }
}
