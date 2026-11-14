using Guanacaste3D.Core;

/// <summary>Rooms close when walls are drawn to the face of other walls, and curtain walls bound rooms.</summary>
static class RoomFaceJoinChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Caras"));
        var level = session.Document.Levels[0].Id;
        session.AddWallsAroundBoundary(level, [new(0, 0), new(6, 0), new(6, 4), new(0, 4)], BuiltInWallTypes.Basic200Id);
        // A partition drawn between the inner faces of the 20 cm walls, not between their centerlines.
        session.AddWall(level, new Point2(3, 0.1), new Point2(3, 3.9), 0.10);
        var rooms = RoomPolygonDetection.Detect(session.Document, level);
        check(rooms.Count == 2, $"Rooms: a partition drawn to the wall faces splits the rectangle into two rooms ({rooms.Count})");

        var glazed = new DocumentSession(BuildingDocument.Create("Muro cortina"));
        var ground = glazed.Document.Levels[0].Id;
        glazed.AddWall(ground, new Point2(0, 0), new Point2(0, 5));
        glazed.AddWall(ground, new Point2(0, 5), new Point2(5, 5));
        glazed.AddWall(ground, new Point2(5, 5), new Point2(5, 0));
        glazed.AddStructuralElement(ground, StructuralElementKind.CurtainWall, "Muro cortina", new Point2(0.1, 0), new Point2(4.9, 0));
        check(RoomPolygonDetection.Detect(glazed.Document, ground).Count == 1,
            "Rooms: a curtain wall closes a room, even when drawn to the faces of the walls beside it");

        // A whole storey selected (walls and rooms) and pasted to the level above brings its rooms; deleting it removes them.
        var tower = new DocumentSession(BuildingDocument.Create("Pisos"));
        var floor1 = tower.Document.Levels[0].Id;
        var floor2 = tower.AddLevel();
        tower.AddWallsAroundBoundary(floor1, [new(0, 0), new(6, 0), new(6, 4), new(0, 4)], BuiltInWallTypes.Basic200Id);
        tower.AddAutomaticRooms(floor1);
        var storey = tower.Document.Walls.Select(wall => wall.Id).Concat(tower.Document.Rooms.Select(room => room.Id)).ToHashSet();
        tower.CopyLevelContent(floor1, [floor2], storey);
        check(tower.Document.Rooms.Count(room => room.LevelId == floor2) == 1,
            "Rooms: a selected storey pasted to another level brings its rooms");
        tower.RemoveElements(tower.Document.Walls.Where(wall => wall.LevelId == floor2).Select(wall => wall.Id)
            .Concat(tower.Document.Rooms.Where(room => room.LevelId == floor2).Select(room => room.Id)).ToList());
        check(tower.Document.Rooms.All(room => room.LevelId != floor2) && tower.Document.Walls.All(wall => wall.LevelId != floor2),
            "Rooms: deleting a selection removes its rooms with its walls");

        // Ctrl+A and Delete on an installed storey: the panel and a light go, nothing is left pointing at them.
        var wired = new DocumentSession(BuildingDocument.Create("Instalación"));
        var installed = wired.Document.Levels[0].Id;
        var panel = wired.AddElectricalDevice(installed, ElectricalDeviceKind.Panel, "TP", new Point2(0, 0));
        var outlet = wired.AddElectricalDevice(installed, ElectricalDeviceKind.Outlet, "T1", new Point2(2, 0));
        var light = wired.AddElectricalDevice(installed, ElectricalDeviceKind.CeilingFixture, "L1", new Point2(3, 2));
        var toggle = wired.AddElectricalDevice(installed, ElectricalDeviceKind.Switch, "S1", new Point2(1, 2));
        var circuit = wired.AddCircuit(panel, "Tomas");
        wired.AssignCircuit(outlet, circuit);
        wired.ToggleSwitchLink(toggle, light);
        var before = ProjectFile.SerializeDocument(wired.Document);
        wired.RemoveElements([panel, light]);
        var left = wired.Document;
        check(left.ElectricalCircuits.Count == 0 && left.ElectricalDevices.Single(device => device.Id == outlet).CircuitId is null &&
              left.ElectricalDevices.Single(device => device.Id == toggle).Controls.Count == 0 && left.ElectricalDevices.Count == 2,
            "Deleting a selected panel and light removes their circuits and links, leaving no dangling references");
        wired.Undo();
        check(ProjectFile.SerializeDocument(wired.Document) == before, "Deleting an installation is one undoable step");
    }
}
