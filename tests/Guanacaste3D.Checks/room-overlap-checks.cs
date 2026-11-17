using Guanacaste3D.Core;

/// <summary>A room with its own outline overlaps another only where both outlines cover the same ground.</summary>
static class RoomOverlapChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Rooms"));
        var level = session.Document.Levels[0].Id;
        var big = session.AddRoom(level, new(0, 0), new(10, 5), "Sala en L");
        var index = session.Document.Rooms.FindIndex(room => room.Id == big);
        session.Document.Rooms[index] = session.Document.Rooms[index] with
        {
            Boundary = [new(0, 0), new(10, 0), new(10, 3), new(6, 3), new(6, 5), new(0, 5)]
        };
        session.AddRoom(level, new(6.5, 3.2), new(9.5, 4.8), "Balcón");
        check(!ModelAudit.Analyze(session.Document).Any(issue => issue.Code == ModelAuditCode.OverlappingRooms),
            "Room overlap: a room standing in the notch of an L-shaped room does not overlap it");

        session.AddRoom(level, new(1, 1), new(3, 2), "Armario");
        var found = ModelAudit.Analyze(session.Document).Where(issue => issue.Code == ModelAuditCode.OverlappingRooms).ToList();
        check(found.Count == 1 && Math.Abs(found[0].ValueMeters - 2.0) < 0.15,
            "Room overlap: a room inside the L is reported with its real overlapping area (about 2 m²)");
    }
}
