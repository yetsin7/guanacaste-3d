using Guanacaste3D.Core;

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

var filename = Path.Combine(Path.GetTempPath(), $"guanacaste-tests-{Guid.NewGuid():N}.g3d.json");
try
{
    ProjectFile.Save(filename, session.Document);
    Check(ProjectFile.Load(filename).Walls[0].Length == 5, "Save/load versioned project");
    Check(ProjectFile.Load(filename).Openings.Count == 2, "Save/load hosted openings");
    ProjectFile.Save(filename, session.Document);
    Check(File.Exists(filename + ".bak"), "Preserve previous file backup");
    var unsupported = session.Document.Clone();
    unsupported.SchemaVersion = 99;
    File.WriteAllText(filename, System.Text.Json.JsonSerializer.Serialize(unsupported));
    failed = false;
    try { ProjectFile.Load(filename); } catch (InvalidDataException) { failed = true; }
    Check(failed, "Reject unsupported project version");
    File.WriteAllText(filename, "{\"SchemaVersion\":1}");
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
for (int i = 0; i < 100; i++) session.Rename("Proyecto " + i);
for (int i = 0; i < 40; i++) session.Undo();
Check(session.Document.Name == "Proyecto 59" && !session.CanUndo, "Undo is bounded to 40 snapshots");

Console.WriteLine("Guanacaste 3D: all core checks passed.");
