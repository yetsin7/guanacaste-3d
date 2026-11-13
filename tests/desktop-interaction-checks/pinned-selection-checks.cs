using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Verifies that deleting a selection containing a pinned wall is rejected without clearing the selection.</summary>
    private static void PinnedSelectionChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var pinned = session.AddWall(level, new Point2(700, 700), new Point2(704, 700));
        var other = session.AddWall(level, new Point2(700, 705), new Point2(704, 705));
        session.SetWallsPinned([pinned], true);
        try
        {
            SetField("_viewSelection", new HashSet<Guid> { pinned, other });
            var undoName = session.UndoName;
            Check((bool)Invoke("DeleteViewSelection")! && session.Document.Walls.Any(wall => wall.Id == pinned) &&
                session.Document.Walls.Any(wall => wall.Id == other) && session.UndoName == undoName &&
                Field<HashSet<Guid>>("_viewSelection").Count == 2,
                "Deleting a selection containing a pinned wall preserves all elements and the selection");
        }
        finally
        {
            SetField("_viewSelection", null);
            session.SetWallsPinned([pinned], false);
            session.RemoveWall(pinned);
            session.RemoveWall(other);
        }
    }
}
