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

session.AddWall(document.Levels[0].Id, new Point2(0, 0), new Point2(3, 4));
var filename = Path.Combine(Path.GetTempPath(), $"guanacaste-tests-{Guid.NewGuid():N}.g3d.json");
try
{
    ProjectFile.Save(filename, session.Document);
    Check(ProjectFile.Load(filename).Walls[0].Length == 5, "Save/load versioned project");
    ProjectFile.Save(filename, session.Document);
    Check(File.Exists(filename + ".bak"), "Preserve previous file backup");
    File.WriteAllText(filename, "{\"SchemaVersion\":99}");
    failed = false;
    try { ProjectFile.Load(filename); } catch (InvalidDataException) { failed = true; }
    Check(failed, "Reject unsupported project version");
}
finally
{
    if (File.Exists(filename)) File.Delete(filename);
    if (File.Exists(filename + ".bak")) File.Delete(filename + ".bak");
}
Console.WriteLine("Guanacaste 3D: all core checks passed.");
