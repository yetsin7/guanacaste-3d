using Guanacaste3D.Core;

/// <summary>Fails the executable check suite when an expected invariant is not satisfied.</summary>
static void Check(bool valid, string explanation)
{
    if (!valid) throw new Exception("FAILED: " + explanation);
    Console.WriteLine("OK: " + explanation);
}

var document = BuildingDocument.Create();
document.Validate();
Check(document.Levels.Count == 1, "Default level");

var session = new DocumentSession(document);
var id = session.AddWall(document.Levels[0].Id, new Point2(0, 0), new Point2(5, 0));
Check(session.Document.Walls.Single().Length == 5, "Metric wall geometry");
session.Undo();
Check(session.Document.Walls.Count == 0, "Undo");
session.Redo();
Check(session.Document.Walls.Single().Id == id, "Redo");
session.RemoveWall(id);
Check(session.Document.Walls.Count == 0, "Delete wall");

var failed = false;
try { session.AddWall(document.Levels[0].Id, new Point2(1, 1), new Point2(1, 1)); }
catch (InvalidDataException) { failed = true; }
Check(failed && session.Document.Walls.Count == 0, "Reject zero-length wall atomically");

session.AddWall(document.Levels[0].Id, new Point2(0, 0), new Point2(5, 0));
var hostedWallId = session.Document.Walls.Single().Id;
var doorId = session.AddOpening(hostedWallId, OpeningKind.Door, offset: 2.0);
var windowId = session.AddOpening(hostedWallId, OpeningKind.Window,
    offset: 4.0, width: 0.8, sill: 1.0, height: 1.0);
Check(session.Document.Openings.Count == 2, "Host wall has two apertures");
var hostedWall = session.Document.Walls.Single();
var netVolume = WallGeometry.NetVolume(hostedWall, session.Document.Openings);
var volumeExpected = (5 * 3 - 0.9 * 2.1 - 0.8 * 1.0) * 0.20;
Check(Math.Abs(netVolume - volumeExpected) < 0.00000001, "Void volume is subtracted from wall");
failed = false;
try { session.AddOpening(hostedWallId, OpeningKind.Door, offset: 2.2); }
catch (InvalidDataException) { failed = true; }
Check(failed && session.Document.Openings.Count == 2, "Overlapping openings are rejected atomically");
failed = false;
try { session.AddOpening(hostedWallId, OpeningKind.Window, offset: 4.95); }
catch (InvalidDataException) { failed = true; }
Check(failed, "Out-of-bounds apertures are rejected");
session.Undo();
Check(session.Document.Openings.Count == 1, "Undo removes second opening");
session.Redo();
Check(session.Document.Openings.Count == 2, "Redo restores hosted opening");

var filename = Path.Combine(Path.GetTempPath(), $"guanacaste-tests-{Guid.NewGuid():N}.g3");
try
{
    ProjectFile.Save(filename, session.Document);
    Check(ProjectFile.IsNativePath(filename) && File.ReadLines(filename).First() == "G3-PROJECT/1",
        "Native projects use the .g3 extension and format header");
    Check(ProjectFile.Load(filename).Walls[0].Length == 5, "Save/load versioned project");
    Check(ProjectFile.Load(filename).Openings.Count == 2, "Save/load hosted openings");
    ProjectFile.Save(filename, session.Document);
    Check(File.Exists(filename + ".bak"), "Preserve previous file backup");
    var unsupported = session.Document.Clone();
    unsupported.SchemaVersion = 99;
    File.WriteAllText(filename, "G3-PROJECT/1\n" +
        System.Text.Json.JsonSerializer.Serialize(unsupported));
    failed = false;
    try { ProjectFile.Load(filename); } catch (InvalidDataException) { failed = true; }
    Check(failed, "Reject unsupported project version");
    File.WriteAllText(filename, "G3-PROJECT/1\n{\"SchemaVersion\":1}");
    failed = false;
    try { ProjectFile.Load(filename); } catch (InvalidDataException) { failed = true; }
    Check(failed, "Reject incomplete JSON project");
}
finally
{
    if (File.Exists(filename)) File.Delete(filename);
    if (File.Exists(filename + ".bak")) File.Delete(filename + ".bak");
}
session.RemoveWall(hostedWallId);
Check(session.Document.Openings.Count == 0, "Wall deletion cascades to its openings");
session.Undo();
Check(session.Document.Openings.Count == 2, "Undo restores hosted elements");
var originalPosition = session.Document.Walls.Single(w => w.Id == hostedWallId).Start;
session.MoveWall(hostedWallId, 2, -1);
Check(session.Document.Walls.Single(w => w.Id == hostedWallId).Start ==
    new Point2(originalPosition.X + 2, originalPosition.Y - 1),
    "Moving wall updates its location");
Check(session.Document.Openings.Count(o => o.WallId == hostedWallId) == 2,
    "Moving wall preserves hosted apertures");
var copyId = session.DuplicateWall(hostedWallId);
Check(session.Document.Walls.Count == 2 && copyId != hostedWallId,
    "Duplicating wall creates an independent BIM element");
Check(session.Document.Openings.Count(o => o.WallId == copyId) == 2,
    "Duplicating wall copies hosted openings");
session.Undo();
Check(session.Document.Walls.Count == 1 && session.Document.Openings.Count == 2,
    "Undo duplication removes copied hosted elements");
var rotatedWall = session.Document.Walls.Single(w => w.Id == hostedWallId);
session.RotateWall(hostedWallId, 90);
var afterRotation = session.Document.Walls.Single(w => w.Id == hostedWallId);
Check(Math.Abs(afterRotation.Length - rotatedWall.Length) < 0.000001,
    "Wall rotation preserves length");
Check(session.Document.Openings.Count(o => o.WallId == hostedWallId) == 2,
    "Wall rotation retains hosted openings");
session.Undo();
Check(session.Document.Walls.Single(w => w.Id == hostedWallId).End == rotatedWall.End,
    "Undo restores pre-rotation coordinates");
var firstOpening = session.Document.Openings.Single(o => o.Id == doorId);
session.UpdateOpening(doorId, firstOpening.Offset, 1.0, 0, 2.1);
Check(Math.Abs(session.Document.Openings.Single(o => o.Id == doorId).Width - 1) < 0.000001,
    "Parametric opening dimensions can be edited");
failed = false;
try { session.UpdateOpening(doorId, 4.0, 1.0, 0, 2.1); }
catch (InvalidDataException) { failed = true; }
Check(failed && session.Document.Openings.Single(o => o.Id == doorId).Offset == firstOpening.Offset,
    "Overlapping opening edit is rejected atomically");
var beforePreciseMove = session.Document.Walls.Single(w => w.Id == hostedWallId);
session.MoveWall(hostedWallId, -0.25, 0.75);
var movedPrecisely = session.Document.Walls.Single(w => w.Id == hostedWallId);
Check(Math.Abs(movedPrecisely.Start.X - beforePreciseMove.Start.X + 0.25) < 0.000001 &&
      Math.Abs(movedPrecisely.Start.Y - beforePreciseMove.Start.Y - 0.75) < 0.000001,
      "Fractional metric translation");
session.RotateWall(hostedWallId, 37.5);
Check(Math.Abs(session.Document.Walls.Single(w => w.Id == hostedWallId).Length -
    movedPrecisely.Length) < 0.000001, "Arbitrary angle preserves wall length");
session.Undo();
Check(session.Document.Walls.Single(w => w.Id == hostedWallId).End == movedPrecisely.End,
    "Undo restores precise transformation");
var schedule = QuantitySchedule.ToCsv(session.Document);
Check(schedule.Contains("NetVolume_m3") && schedule.Contains("DoorCount") &&
    schedule.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 2,
    "Quantity takeoff exports one row per wall");
Check(schedule.Contains(",1,1"), "Quantity takeoff includes door and window counts");
var wallBeforeInvalidMove = session.Document.Walls.Single(w => w.Id == hostedWallId);
failed = false;
try { session.MoveWall(hostedWallId, double.NaN, 0); }
catch (InvalidDataException) { failed = true; }
Check(failed && session.Document.Walls.Single(w => w.Id == hostedWallId) == wallBeforeInvalidMove,
    "Non-finite wall translation is rejected atomically");
failed = false;
try { session.RotateWall(hostedWallId, double.PositiveInfinity); }
catch (InvalidDataException) { failed = true; }
Check(failed, "Non-finite wall rotation is rejected");
for (int i = 0; i < 100; i++) session.Rename("Proyecto " + i);
for (int i = 0; i < 40; i++) session.Undo();
Check(session.Document.Name == "Proyecto 59" && !session.CanUndo, "Undo is bounded to 40 snapshots");

var coordinatedDocument = BuildingDocument.Create();
var coordinatedSession = new DocumentSession(coordinatedDocument);
Check(coordinatedSession.Document.WallTypes.Count == 3, "New projects include built-in wall types");
var exteriorWallId = coordinatedSession.AddWall(
    coordinatedDocument.Levels[0].Id,
    new Point2(0, 0),
    new Point2(8, 0),
    wallTypeId: BuiltInWallTypes.Exterior300Id);
var exteriorWall = coordinatedSession.Document.Walls.Single(wall => wall.Id == exteriorWallId);
Check(exteriorWall.WallTypeId == BuiltInWallTypes.Exterior300Id &&
      Math.Abs(exteriorWall.Thickness - 0.30) < 0.000001,
      "Wall instances use reusable type parameters");
coordinatedSession.UpdateWallType(
    BuiltInWallTypes.Exterior300Id,
    "High-performance exterior 350 mm",
    WallFunction.Exterior,
    0.35,
    "Insulated concrete");
Check(Math.Abs(coordinatedSession.Document.Walls.Single().Thickness - 0.35) < 0.000001,
    "Wall type edits propagate to instances");
var roomId = coordinatedSession.AddRoom(
    coordinatedDocument.Levels[0].Id,
    new Point2(0, 0),
    new Point2(4, 5),
    "Meeting Room");
Check(Math.Abs(coordinatedSession.Document.Rooms.Single(room => room.Id == roomId).Area - 20) < 0.000001,
    "Room area is coordinated from its boundary");
var floorId = coordinatedSession.AddFloor(
    coordinatedDocument.Levels[0].Id,
    [new Point2(0, 0), new Point2(8, 0), new Point2(8, 6), new Point2(0, 6)]);
Check(Math.Abs(coordinatedSession.Document.Floors.Single(floor => floor.Id == floorId).CalculateArea() - 48) < 0.000001,
    "Floor area is coordinated from its polygon");
var sheetId = coordinatedSession.AddSheet();
Check(coordinatedSession.Document.Sheets.Single(sheet => sheet.Id == sheetId).Number == "A001",
    "Drawing sheets receive stable project numbers");
var projectRows = ProjectScheduleService.Build(coordinatedSession.Document);
Check(projectRows.Count(row => row.Category is "Wall" or "Room" or "Floor" or "Sheet") == 4,
    "Project schedule coordinates modeled and documented elements");

var coordinatedFilename = Path.Combine(
    Path.GetTempPath(), $"guanacaste-coordinated-{Guid.NewGuid():N}.g3");
var legacyFilename = Path.Combine(
    Path.GetTempPath(), $"guanacaste-legacy-{Guid.NewGuid():N}.g3d.json");
try
{
    ProjectFile.Save(coordinatedFilename, coordinatedSession.Document);
    var coordinatedReload = ProjectFile.Load(coordinatedFilename);
    Check(coordinatedReload.Rooms.Count == 1 && coordinatedReload.Floors.Count == 1 &&
          coordinatedReload.Sheets.Count == 1 && coordinatedReload.WallTypes.Count == 3,
        "Coordinated BIM elements survive a project round trip");

    var legacyDocument = coordinatedSession.Document.Clone();
    legacyDocument.SchemaVersion = 1;
    legacyDocument.WallTypes = [];
    File.WriteAllText(coordinatedFilename, "G3-PROJECT/1\n" +
        System.Text.Json.JsonSerializer.Serialize(legacyDocument));
    var upgradedDocument = ProjectFile.Load(coordinatedFilename);
    Check(upgradedDocument.SchemaVersion == 3 && upgradedDocument.WallTypes.Count == 3 &&
          upgradedDocument.Views.Count >= 5 &&
          upgradedDocument.Walls.All(wall => upgradedDocument.WallTypes.Any(type => type.Id == wall.WallTypeId)),
        "Schema-one projects migrate to coordinated wall types and views");

    File.WriteAllText(legacyFilename, System.Text.Json.JsonSerializer.Serialize(legacyDocument));
    Check(ProjectFile.Load(legacyFilename).SchemaVersion == 3,
        "Legacy .g3d.json projects remain importable");
}
finally
{
    if (File.Exists(coordinatedFilename)) File.Delete(coordinatedFilename);
    if (File.Exists(coordinatedFilename + ".bak")) File.Delete(coordinatedFilename + ".bak");
    if (File.Exists(legacyFilename)) File.Delete(legacyFilename);
}

var viewDocument = BuildingDocument.Create("View Coordination");
Check(viewDocument.Views.Any(view => view.Kind == ProjectViewKind.Elevation) &&
      viewDocument.Views.Any(view => view.Kind == ProjectViewKind.Section) &&
      viewDocument.Views.Any(view => view.Kind == ProjectViewKind.Model3D),
    "New projects include coordinated orthographic and 3D views");
var viewSession = new DocumentSession(viewDocument);
var addedLevelId = viewSession.AddLevel();
Check(viewSession.Document.Views.Any(view =>
        view.Kind == ProjectViewKind.Plan && view.LevelId == addedLevelId),
    "Adding a level creates its coordinated plan view");

Console.WriteLine("Guanacaste 3D: all core checks passed.");
