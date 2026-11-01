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
    }
}
