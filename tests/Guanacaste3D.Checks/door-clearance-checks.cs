using Guanacaste3D.Core;

/// <summary>Nothing may stand in the clear zone in front of a door.</summary>
static class DoorClearanceChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Doors"));
        var level = session.Document.Levels[0].Id;
        // A wall along the x axis with a 0.9 m door centred at x = 3; the room is on the +y side.
        var wall = session.AddWall(level, new(0, 0), new(6, 0));
        session.AddOpening(wall, OpeningKind.Door, offset: 3, width: 0.9, height: 2.1);
        check(DoorClearance.Analyze(session.Document).Count == 0, "Door clearance: a door with nothing near it has no obstruction");

        var inFront = session.AddColumn(level, new(3, 0.7));
        var found = DoorClearance.Analyze(session.Document);
        check(found.Count == 1 && found[0].ObstructionId == inFront && Math.Abs(found[0].RequiredDepth - 0.9) < 1e-9,
            "Door clearance: a column 0.7 m in front of the door is reported with the 0.90 m it needs");
        check(ModelWarnings.Analyze(session.Document).Any(warning => warning.Code == "ObstructedDoor" && warning.ElementIds.Contains(inFront)),
            "Door clearance: the warning list names the obstructed door and the column");

        session.RemoveColumn(inFront);
        session.AddColumn(level, new(5.2, 0.7));
        check(DoorClearance.Analyze(session.Document).Count == 0, "Door clearance: a column beside the door, outside its zone, is fine");

        var behind = session.AddColumn(level, new(3, -0.6));
        check(DoorClearance.Analyze(session.Document).Any(item => item.ObstructionId == behind), "Door clearance: the other side of the door is kept clear too");
        session.RemoveColumn(behind);

        var far = session.AddColumn(level, new(3, 1.6));
        check(DoorClearance.Analyze(session.Document).Count == 0, "Door clearance: a column farther than the door is wide is allowed");
        check(DoorClearance.Analyze(session.Document, far).Count == 0, "Door clearance: checking only one element gives the same answer");
    }
}
