using Guanacaste3D.Core;

static class GridAxisChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Ejes asociativos"));
        var level = session.Document.Levels[0].Id;
        var id = session.AddGridAxis("A", new Point2(0, 0), new Point2(10, 0));
        var other = session.AddGridAxis("8", new Point2(0, 3), new Point2(10, 3));
        var rejected = false;
        try { session.AddGridAxis(" a ", new Point2(0, 4), new Point2(10, 4)); }
        catch (DuplicateGridAxisNameException ex) { rejected = ex.ExistingAxisId == id; }
        check(rejected && session.Document.Grids.Count == 2, "Duplicate alphabetic axis labels are rejected with existing axis ID");

        rejected = false;
        try { session.UpdateGridAxis(id, " 8 ", new Point2(0, 0), new Point2(10, 0)); }
        catch (DuplicateGridAxisNameException ex) { rejected = ex.ExistingAxisId == other; }
        check(rejected && session.Document.Grids.Single(x => x.Id == id).Name == "A",
            "Duplicate numerical labels cannot overwrite existing axes");

        var wallId = session.AddWall(level, new Point2(1, 0), new Point2(7, 0));
        var crossing = session.AddWall(level, new Point2(4, -2), new Point2(4, 2));
        session.AddOpening(wallId, OpeningKind.Door, offset: 2);
        session.MoveGridAxis(id, 0, 1.5);
        var shifted = session.Document.Walls.Single(x => x.Id == wallId);
        check(shifted.Start == new Point2(1, 1.5) && shifted.End == new Point2(7, 1.5),
            "Moving axis translates wall aligned to its centerline");
        check(session.Document.Walls.Single(x => x.Id == crossing).Start == new Point2(4, -2),
            "Crossing walls are not inadvertently translated");
        check(session.Document.Openings.Single().WallId == wallId, "Hosted opening remains attached after axis translation");
        session.Undo();
        check(session.Document.Walls.Single(x => x.Id == wallId).Start == new Point2(1, 0) &&
              session.Document.Grids.Single(x => x.Id == id).Start == new Point2(0, 0),
            "Undo atomically restores grid and hosted wall coordinates");
        session.Redo();
        check(session.Document.Walls.Single(x => x.Id == wallId).Start == new Point2(1, 1.5),
            "Redo replays associative axis move");
        check(session.Document.Walls.Single(x => x.Id == wallId).GridAxisId == id,
            "Axis association persists after moving the grid");

        // Removing the datum must NEVER remove the wall or its hosted door.
        session.RemoveGridAxis(id);
        check(!session.Document.Grids.Any(x => x.Id == id) &&
              session.Document.Walls.Any(x => x.Id == wallId && x.GridAxisId == null) &&
              session.Document.Openings.Any(x => x.WallId == wallId),
            "Deleting an axis releases wall association without deleting wall or doors");
        session.Undo();
        check(session.Document.Grids.Any(x => x.Id == id) &&
              session.Document.Walls.Single(x => x.Id == wallId).GridAxisId == id,
            "Undo axis deletion restores its association");

        // Removing the wall may delete its HOSTED door by the ordinary wall ownership rule,
        // but must never delete the independent datum.
        session.RemoveWall(wallId);
        check(session.Document.Grids.Any(x => x.Id == id) &&
              !session.Document.Walls.Any(x => x.Id == wallId),
            "Deleting a wall preserves its grid axis");
        session.Undo();
        check(session.Document.Walls.Any(x => x.Id == wallId) &&
              session.Document.Grids.Any(x => x.Id == id),
            "Undo wall deletion restores wall and leaves axis intact");

        var unlinkedWall = session.AddWall(level, new Point2(1, 3), new Point2(7, 3));
        check(session.Document.Walls.Single(x => x.Id == unlinkedWall).GridAxisId == other,
            "A new centerline-aligned wall automatically binds to its axis");
        session.SetWallGridAxis(unlinkedWall, null);
        check(session.Document.Walls.Single(x => x.Id == unlinkedWall).GridAxisId is null &&
              session.Document.Walls.Single(x => x.Id == unlinkedWall).GridAxisAssociationDisabled,
            "A wall can explicitly release its datum association");
        var beforeUnlinkedMove = session.Document.Walls.Single(x => x.Id == unlinkedWall).Start;
        session.MoveGridAxis(other, 0, -0.5);
        check(session.Document.Walls.Single(x => x.Id == unlinkedWall).Start == beforeUnlinkedMove,
            "Detached wall stays fixed when its former grid is moved");
        session.Undo();
        session.SetWallGridAxis(unlinkedWall, other);
        check(session.Document.Walls.Single(x => x.Id == unlinkedWall).GridAxisId == other,
            "A wall can rebind to an existing aligned datum");
        session.MoveWall(unlinkedWall, 0, 1);
        check(session.Document.Walls.Single(x => x.Id == unlinkedWall).GridAxisId is null &&
              session.Document.Grids.Any(x => x.Id == other),
            "Moving an individual wall detaches it from the axis without moving the axis");

    }
}
