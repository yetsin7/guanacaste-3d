using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Exercises hosted-opening clipboard commands without cutting or duplicating the host wall.</summary>
    private static void OpeningClipboardChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var wall = session.AddWall(level, new Point2(600, 600), new Point2(608, 600));
        var opening = session.AddOpening(wall, OpeningKind.Door, 2, flipHand: true, flipFacing: true);
        var narrowWall = session.AddWall(level, new Point2(610, 600), new Point2(611.2, 600));
        session.AddOpening(narrowWall, OpeningKind.Door, 0.6);
        try
        {
            SetField("_selectedWallId", (Guid?)wall);
            SetField("_selectedOpeningId", (Guid?)opening);
            var originalWall = session.Document.Walls.Single(item => item.Id == wall);
            var historyBeforeWallCommands = session.UndoName;
            foreach (var command in new[] { "Rotate", "FlipWall", "Pin", "DisallowJoin", "AttachTop" })
                Invoke("ExecuteApplicationCommand", command);
            Check(session.Document.Walls.Single(item => item.Id == wall) == originalWall &&
                session.UndoName == historyBeforeWallCommands,
                "Wall-only commands do not alter a door's host while the door is selected");
            SetField("_selectedOpeningId", null);
            Invoke("ExecuteApplicationCommand", "FlipWall");
            Check(session.Document.Walls.Single(item => item.Id == wall).Start == originalWall.End,
                "A wall-only command still works when the wall itself is selected");
            session.Undo();
            SetField("_selectedWallId", (Guid?)wall);
            SetField("_selectedOpeningId", (Guid?)opening);
            Invoke("ExecuteApplicationCommand", "Copy");
            Invoke("ExecuteApplicationCommand", "Paste");
            var copyId = Field<Guid?>("_selectedOpeningId");
            var copy = session.Document.Openings.Single(item => item.Id == copyId);
            Check(copy.Id != opening && copy.WallId == wall && copy.FlipHand && copy.FlipFacing &&
                  session.Document.Walls.Count(item => item.Id == wall) == 1,
                "Copy and paste preserve the host and the opening orientation");
            session.Undo();
            Check(session.Document.Openings.All(item => item.Id != copyId), "Undo removes the pasted opening");
            SetField("_selectedWallId", (Guid?)wall);
            SetField("_selectedOpeningId", (Guid?)opening);
            var wallCount = session.Document.Walls.Count;
            Invoke("ExecuteApplicationCommand", "CopyElement");
            var duplicateId = Field<Guid?>("_selectedOpeningId");
            Check(duplicateId != opening && session.Document.Openings.Any(item => item.Id == duplicateId) &&
                session.Document.Walls.Count == wallCount && Field<WallOpening?>("_openingClipboard")?.Id == opening,
                "Copy Element duplicates the selected opening without copying its host or replacing the clipboard");
            session.Undo();
            Check(session.Document.Openings.All(item => item.Id != duplicateId), "Undo removes the duplicated opening");
            SetField("_selectedWallId", (Guid?)narrowWall);
            var beforeRejectedPaste = session.Document.Openings.Count;
            var undoName = session.UndoName;
            Invoke("ExecuteApplicationCommand", "Paste");
            Check(session.Document.Openings.Count == beforeRejectedPaste && session.UndoName == undoName,
                "Pasting into a full wall leaves the model and history unchanged");
            SetField("_selectedWallId", (Guid?)wall);
            SetField("_selectedOpeningId", (Guid?)opening);
            Invoke("ExecuteApplicationCommand", "Cut");
            Check(session.Document.Walls.Any(item => item.Id == wall) && session.Document.Openings.All(item => item.Id != opening),
                "Cut removes only the selected opening and leaves its host wall intact");
            session.Undo();
            Check(session.Document.Openings.Any(item => item.Id == opening), "Undo restores the cut opening");
            session.Redo();
            Invoke("ExecuteApplicationCommand", "Paste");
            var restored = session.Document.Openings.Single(item => item.Id == Field<Guid?>("_selectedOpeningId"));
            Check(Math.Abs(restored.Offset - 2) < 1e-8 && restored.FlipHand && restored.FlipFacing,
                "Pasting a cut opening restores its original position and orientation");
        }
        finally
        {
            SetField("_openingClipboard", null);
            SetField("_wallClipboard", null);
            SetField("_selectedOpeningId", null);
            SetField("_selectedWallId", null);
            session.RemoveWall(wall);
            session.RemoveWall(narrowWall);
        }
    }
}
