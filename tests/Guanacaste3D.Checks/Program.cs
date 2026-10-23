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

var elementDeletion = new DocumentSession(session.Document);
elementDeletion.RemoveOpening(doorId);
Check(elementDeletion.Document.Walls.Count == 1 &&
      elementDeletion.Document.Openings.Count == 1 &&
      elementDeletion.Document.Openings[0].Id == windowId,
    "Deleting an opening does not delete its host or other openings");
elementDeletion.Undo();
Check(elementDeletion.Document.Openings.Count == 2 &&
      elementDeletion.Document.Openings.Any(o => o.Id == doorId),
    "Undo restores only the removed opening and keeps its host");

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

// CAD exchange: output is schematic DXF R12, with exact metric geometry and per-level layers.
var exportedDxf = DxfExporter.ToAscii(session.Document);
Check(exportedDxf.Contains("$ACADVER\r\n1\r\nAC1009\r\n") &&
      exportedDxf.EndsWith("0\r\nEOF\r\n", StringComparison.Ordinal),
    "DXF R12 has the required header and closing marker");

Check(exportedDxf.IndexOf("2\r\nLTYPE\r\n", StringComparison.Ordinal) <
      exportedDxf.IndexOf("2\r\nLAYER\r\n", StringComparison.Ordinal) &&
      exportedDxf.Contains("2\r\nCONTINUOUS\r\n") &&
      exportedDxf.Contains("2\r\nBLOCKS\r\n") &&
      exportedDxf.Contains("0\r\nLAYER\r\n2\r\n0\r\n"),
    "DXF R12 declares a base layer, a line type and a BLOCKS section");

Check(exportedDxf.Contains("G3D-L001-WALL") &&
      exportedDxf.Contains("G3D-L001-DOOR") &&
      exportedDxf.Contains("G3D-L001-WINDOW"),
    "DXF separates wall outlines, door markers and window markers");
Check(exportedDxf.Split("0\r\nLINE\r\n", StringSplitOptions.None).Length - 1 == 14,
    "DXF emits three rectangular wall sections and two opening markers");
var additionalLevel = Guid.NewGuid();
var drawing = session.Document.Clone();
drawing.Levels.Add(new Level(additionalLevel, "Mezzanine", 3.25));
drawing.Walls.Add(new Wall(Guid.NewGuid(), additionalLevel,
    new Point2(0, 0), new Point2(2.5, 0), 0.2, 3));
var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture =
        System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
    var metricDxf = DxfExporter.ToAscii(drawing);
    Check(metricDxf.Contains("G3D-L002-WALL") &&
          metricDxf.Contains("30\r\n3.25\r\n") &&
          metricDxf.Contains("11\r\n2.5\r\n"),
        "DXF preserves multiple levels, elevations and invariant decimal points");
}
finally
{
    System.Globalization.CultureInfo.CurrentCulture = originalCulture;
}
var invalidForExport = session.Document.Clone();
invalidForExport.Walls[0] = invalidForExport.Walls[0] with { Thickness = double.NaN };
failed = false;
try { DxfExporter.ToAscii(invalidForExport); }
catch (InvalidDataException) { failed = true; }
Check(failed, "DXF refuses invalid BIM geometry before export");


// Slabs: transaction safety, geometry, backward-compatible persistence and CAD exchange.
var slabDocument = BuildingDocument.Create();
var slabSession = new DocumentSession(slabDocument);
var slabId = slabSession.AddSlab(slabDocument.Levels[0].Id,
    new Point2(4, 5), new Point2(0, 0), thickness: 0.2);
var slab = slabSession.Document.Slabs.Single();
Check(slab.Id == slabId && slab.Min == new Point2(0, 0) &&
      slab.Max == new Point2(4, 5) &&
      Math.Abs(slab.Area - 20) < 1e-9 &&
      Math.Abs(slab.Volume - 4) < 1e-9,
    "Slab corners normalize and exact metric area and volume are computed");
slabSession.UpdateSlab(slabId, 0.3);
Check(Math.Abs(slabSession.Document.Slabs.Single().Volume - 6) < 1e-9,
    "Slab thickness edit recalculates volume");
slabSession.Undo();
Check(Math.Abs(slabSession.Document.Slabs.Single().Thickness - 0.2) < 1e-9,
    "Undo restores slab thickness");
slabSession.Redo();
Check(Math.Abs(slabSession.Document.Slabs.Single().Thickness - 0.3) < 1e-9,
    "Redo restores edited slab thickness");
slabSession.ResizeSlab(slabId, 6, 5);
Check(Math.Abs(slabSession.Document.Slabs.Single().Area - 30) < 1e-9 &&
      Math.Abs(slabSession.Document.Slabs.Single().Volume - 9) < 1e-9,
    "Resizing a slab changes area and volume while preserving thickness");
slabSession.Undo();
Check(Math.Abs(slabSession.Document.Slabs.Single().Area - 20) < 1e-9,
    "Undo restores the slab's previous footprint");
failed = false;
try { slabSession.ResizeSlab(slabId, 0.1, 5); }
catch (InvalidDataException) { failed = true; }
Check(failed && slabSession.Document.Slabs.Single().Area == 20,
    "Invalid resize cannot modify slab dimensions");
var stableSlab = slabSession.Document.Slabs.Single();
failed = false;
try { slabSession.UpdateSlab(slabId, -1); }
catch (InvalidDataException) { failed = true; }
Check(failed && slabSession.Document.Slabs.Single() == stableSlab,
    "Invalid slab thickness is rejected without mutation");
failed = false;
try { slabSession.AddSlab(slabDocument.Levels[0].Id,
    new Point2(double.NaN, 0), new Point2(2, 2)); }
catch (InvalidDataException) { failed = true; }
Check(failed && slabSession.Document.Slabs.Count == 1,
    "Invalid slab coordinates are rejected without mutation");
failed = false;
try { slabSession.AddSlab(slabDocument.Levels[0].Id,
    new Point2(0, 0), new Point2(0.1, 2)); }
catch (InvalidDataException) { failed = true; }
Check(failed && slabSession.Document.Slabs.Count == 1,
    "Near-zero slab dimensions are rejected atomically");
slabSession.MoveSlab(slabId, -2, 3);
Check(slabSession.Document.Slabs.Single().Min == new Point2(-2, 3) &&
      slabSession.Document.Slabs.Single().Area == 20,
    "Moving a slab keeps its measured area");
var slabCopy = slabSession.DuplicateSlab(slabId);
Check(slabSession.Document.Slabs.Count == 2 &&
      slabCopy != slabId &&
      slabSession.Document.Slabs.Single(s => s.Id == slabCopy).Area == 20,
    "Duplicating slabs creates a new stable ID and preserves dimensions");
slabSession.RemoveSlab(slabCopy);
Check(slabSession.Document.Slabs.Count == 1, "Delete removes just the selected slab");
slabSession.Undo();
Check(slabSession.Document.Slabs.Count == 2, "Undo restores the deleted slab");
var crossTypeIds = slabSession.Document.Clone();
crossTypeIds.Walls.Add(new Wall(slabId, slabDocument.Levels[0].Id,
    new Point2(0, 0), new Point2(3, 0), 0.2, 3));
failed = false;
try { crossTypeIds.Validate(); }
catch (InvalidDataException) { failed = true; }
Check(failed, "Identifiers cannot be reused across slab and wall element types");
var slabCsv = SlabSchedule.ToCsv(slabSession.Document);
Check(slabCsv.Contains("Area_m2,Volume_m3") &&
      slabCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 3 &&
      slabCsv.Contains(",20,6"),
    "Slab CSV exports validated metric area and volume for each slab");
var slabDxf = DxfExporter.ToAscii(slabSession.Document);
Check(slabDxf.Contains("G3D-L001-SLAB") &&
      slabDxf.Split("0\r\nLINE\r\n", StringSplitOptions.None).Length - 1 == 8,
    "Schematic DXF exports four perimeter lines per rectangular slab");
var slabProjectPath = Path.Combine(Path.GetTempPath(), $"guanacaste-slab-{Guid.NewGuid():N}.g3d.json");
try
{
    ProjectFile.Save(slabProjectPath, slabSession.Document);
    var reloadedSlabProject = ProjectFile.Load(slabProjectPath);
    Check(reloadedSlabProject.Slabs.Count == 2 &&
          reloadedSlabProject.Slabs.Any(s => s.Id == slabId && s.Area == 20),
        "Slabs survive project save and reload with persistent IDs");
    var legacy = System.Text.Json.Nodes.JsonNode.Parse(
        System.Text.Json.JsonSerializer.Serialize(slabSession.Document))!.AsObject();
    legacy.Remove("Slabs");
    File.WriteAllText(slabProjectPath, legacy.ToJsonString());
    Check(ProjectFile.Load(slabProjectPath).Slabs.Count == 0,
        "Legacy schema-1 projects without Slabs field remain loadable");
}
finally
{
    if (File.Exists(slabProjectPath)) File.Delete(slabProjectPath);
    if (File.Exists(slabProjectPath + ".bak")) File.Delete(slabProjectPath + ".bak");
}

var examplePath = Path.Combine(AppContext.BaseDirectory,
    "samples", "casa-demostrativa.g3d.json");
var exampleDocument = ProjectFile.Load(examplePath);
Check(exampleDocument.Levels.Count == 2 &&
      exampleDocument.Walls.Count == 5 &&
      exampleDocument.Openings.Count == 3 &&
      exampleDocument.Slabs.Count == 2,
    "Bundled house sample opens as a validated multi-level BIM project");
Check(exampleDocument.WallTypes.Count == 1 &&
      exampleDocument.Walls.Count(w => w.TypeId == exampleDocument.WallTypes[0].Id) == 3,
    "Bundled sample contains one shared type across three walls");
Check(exampleDocument.Slabs.All(s => Math.Abs(s.Area - 48) < 1e-9) &&
      Math.Abs(exampleDocument.Slabs.Sum(s => s.Volume) - 20.16) < 1e-9,
    "Bundled house sample contains correct slab takeoff quantities");

// Type/instance parameter semantics (independent implementation; not Autodesk code).
var typedDocument = BuildingDocument.Create();
var typedSession = new DocumentSession(typedDocument);
var typedWall1 = typedSession.AddWall(typedDocument.Levels[0].Id,
    new Point2(0, 0), new Point2(6, 0));
var typedWall2 = typedSession.AddWall(typedDocument.Levels[0].Id,
    new Point2(0, 3), new Point2(6, 3));
var reusableType = typedSession.CreateWallTypeForWall(typedWall1, "Muro genérico 25 cm", .25);
typedSession.SetWallType(typedWall2, reusableType);
Check(typedSession.Document.WallTypes.Count == 1 &&
      typedSession.Document.Walls.All(w => w.TypeId == reusableType && w.Thickness == .25),
    "Shared wall type synchronizes thickness across separate instances");
typedSession.UpdateWallType(reusableType, "Muro genérico 30 cm", .30);
Check(typedSession.Document.Walls.All(w => w.Thickness == .30) &&
      typedSession.Document.WallTypes.Single().Name == "Muro genérico 30 cm",
    "Updating a type propagates its shared thickness and name atomically");
typedSession.Undo();
Check(typedSession.Document.Walls.All(w => w.Thickness == .25),
    "Undo restores all wall instances together");
typedSession.Redo();
Check(typedSession.Document.Walls.All(w => w.Thickness == .30),
    "Redo reapplies the synchronized type change");
failed = false;
try { typedSession.UpdateWall(typedWall1, .40, 3); }
catch (InvalidDataException) { failed = true; }
Check(failed && typedSession.Document.Walls.All(w => w.Thickness == .30),
    "Type-bound wall cannot override a shared thickness locally");
typedSession.UpdateWall(typedWall1, .30, 4);
Check(typedSession.Document.Walls.Single(w => w.Id == typedWall1).Height == 4,
    "Type-bound wall retains editable instance height");
typedSession.DetachWallType(typedWall2);
Check(typedSession.Document.Walls.Single(w => w.Id == typedWall2).TypeId is null &&
      typedSession.Document.Walls.Single(w => w.Id == typedWall2).Thickness == .30,
    "Detaching a wall keeps its last thickness");
typedSession.UpdateWallType(reusableType, "Muro genérico 35 cm", .35);
Check(typedSession.Document.Walls.Single(w => w.Id == typedWall1).Thickness == .35 &&
      typedSession.Document.Walls.Single(w => w.Id == typedWall2).Thickness == .30,
    "Detached wall does not change when its former type changes");
var typedDuplicate = typedSession.DuplicateWall(typedWall1);
Check(typedSession.Document.Walls.Single(w => w.Id == typedDuplicate).TypeId == reusableType,
    "Duplicating a typed wall retains its type reference");
failed = false;
try { typedSession.RemoveWallType(reusableType); }
catch (InvalidDataException) { failed = true; }
Check(failed && typedSession.Document.WallTypes.Count == 1,
    "Cannot remove a wall type still referenced by its instances");
var typedInvalid = typedSession.Document.Clone();
var firstTypedIndex = typedInvalid.Walls.FindIndex(w => w.Id == typedWall1);
typedInvalid.Walls[firstTypedIndex] = typedInvalid.Walls[firstTypedIndex] with { Thickness = .9 };
failed = false;
try { typedInvalid.Validate(); }
catch (InvalidDataException) { failed = true; }
Check(failed, "Project validation catches mismatched shared type thickness");
var typeBeforeFailure = typedSession.Document.WallTypes.Single();
failed = false;
try { typedSession.CreateWallTypeForWall(typedWall2, "Muro genérico 35 cm", .2); }
catch (InvalidDataException) { failed = true; }
Check(failed && typedSession.Document.WallTypes.Count == 1 &&
      typedSession.Document.WallTypes.Single() == typeBeforeFailure,
    "Duplicate type names are rejected without corrupting the document");
var typedFilename = Path.Combine(Path.GetTempPath(), $"guanacaste-walltypes-{Guid.NewGuid():N}.g3d.json");
try
{
    ProjectFile.Save(typedFilename, typedSession.Document);
    var reloadedTypes = ProjectFile.Load(typedFilename);
    Check(reloadedTypes.WallTypes.Count == 1 &&
          reloadedTypes.Walls.Count(w => w.TypeId == reusableType) == 2,
        "Wall type definitions and references survive project save/reopen");
    var untypedLegacy = typedDocument.Clone();
    untypedLegacy.Walls.Add(new Wall(Guid.NewGuid(), untypedLegacy.Levels[0].Id,
        new Point2(0, 0), new Point2(2, 0), .2, 3));
    var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(
        System.Text.Json.JsonSerializer.Serialize(untypedLegacy))!.AsObject();
    legacyJson.Remove("WallTypes");
    File.WriteAllText(typedFilename, legacyJson.ToJsonString());
    Check(ProjectFile.Load(typedFilename).WallTypes.Count == 0 &&
          ProjectFile.Load(typedFilename).Walls.Single().TypeId is null,
        "Legacy projects without wall type catalog remain compatible");
}
finally
{
    if (File.Exists(typedFilename)) File.Delete(typedFilename);
    if (File.Exists(typedFilename + ".bak")) File.Delete(typedFilename + ".bak");
}

Console.WriteLine("Guanacaste 3D: all core checks passed.");
