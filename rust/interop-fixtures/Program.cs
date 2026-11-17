using Guanacaste3D.Core;

// Reproducible C# serializer fixture. Never touches existing user projects.
if (args.Length != 1)
{
    Console.Error.WriteLine("usage: WallFixture <empty-output-directory>");
    return 2;
}
var directory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(directory);
if (Directory.EnumerateFileSystemEntries(directory).Any())
{
    Console.Error.WriteLine("fixture output directory must be empty");
    return 2;
}
var document = BuildingDocument.Create("Rust CSharp wall parity");
document.Id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
var level = document.Levels[0];
var wallId = Guid.Parse("00000000-0000-0000-0000-000000000002");
var start = new Point2(0, 0);
var end = new Point2(4, 0);
const double thickness = 0.2;
const double height = 3;
document.Walls.Add(new Wall(wallId, level.Id, start, end, thickness, height));
// Separate diagonal 3-4-5 wall checks rotated centerline metrics.
var diagonalId = Guid.Parse("00000000-0000-0000-0000-000000000003");
var diagonalStart = new Point2(10, 10);
var diagonalEnd = new Point2(13, 14);
document.Walls.Add(new Wall(diagonalId, level.Id, diagonalStart, diagonalEnd, thickness, height));
// Reversed centerline exercises negative X/Y direction without changing metric magnitudes.
var reversedId = Guid.Parse("00000000-0000-0000-0000-000000000004");
var reversedStart = new Point2(13, 14);
var reversedEnd = new Point2(10, 10);
document.Walls.Add(new Wall(reversedId, level.Id, reversedStart, reversedEnd, thickness, height));
// Non-default thickness and height catch assumptions hidden by uniform fixtures.
var variedId = Guid.Parse("00000000-0000-0000-0000-000000000005");
var variedStart = new Point2(-2, 1);
var variedEnd = new Point2(-2, 3);
const double variedThickness = 0.3;
const double variedHeight = 2.5;
document.Walls.Add(new Wall(variedId, level.Id, variedStart, variedEnd, variedThickness, variedHeight));
// Translation-invariance regression: survey coordinates far from the origin.
// The 3-4-5 centreline must retain the same metrics as the local diagonal.
var translatedId = Guid.Parse("00000000-0000-0000-0000-000000000006");
var translatedStart = new Point2(1_000_010, -999_990);
var translatedEnd = new Point2(1_000_013, -999_986);
document.Walls.Add(new Wall(translatedId, level.Id, translatedStart, translatedEnd, thickness, height));
document.Validate();
var json = ProjectFile.SerializeDocument(document);
File.WriteAllText(Path.Combine(directory, "wall.g3d.json"), json);
NativeDocumentStore.SaveProject(Path.Combine(directory, "wall.g3d"), document);
// Verify that both production C# readers can round-trip the exact fixture before
// Rust uses it as an oracle. Do not alter either persisted artifact.
var fromJson = ProjectFile.DeserializeDocument(json);
var fromNative = NativeDocumentStore.LoadProject(Path.Combine(directory, "wall.g3d"));
foreach (var restored in new[] { fromJson, fromNative })
{
    // Validate deserialized data with the production BIM and wall validators.
    // A round-trip must preserve invariants, not only selected scalar fields.
    restored.Validate();
    WallGeometry.Validate(restored.Walls, Array.Empty<WallOpening>());
    if (restored.Id != document.Id ||
        restored.SchemaVersion != document.SchemaVersion ||
        restored.Walls.Count != document.Walls.Count ||
        !restored.Walls.Select(w => w.Id).SequenceEqual(document.Walls.Select(w => w.Id)) ||
        !restored.Walls.Zip(document.Walls).All(pair =>
            pair.First.LevelId == pair.Second.LevelId &&
            pair.First.Start.Equals(pair.Second.Start) &&
            pair.First.End.Equals(pair.Second.End) &&
            pair.First.Thickness == pair.Second.Thickness &&
            pair.First.Height == pair.Second.Height))
    {
        Console.Error.WriteLine("C# serializer round-trip changed document identity or wall geometry");
        return 1;
    }
    // Assert geometry survives both production serializers, not just field equality.
    // This is deliberately calculated through the C# production implementation.
    foreach (var pair in restored.Walls.Zip(document.Walls))
    {
        var restoredVolume = WallGeometry.NetVolume(pair.First, Array.Empty<WallOpening>());
        var originalVolume = WallGeometry.NetVolume(pair.Second, Array.Empty<WallOpening>());
        if (!double.IsFinite(restoredVolume) ||
            Math.Abs(restoredVolume - originalVolume) > 1e-9)
        {
            Console.Error.WriteLine("C# serializer round-trip changed production wall volume");
            return 1;
        }
    }
}
// Use production WallGeometry for net volume; surface area remains an independent
// analytic cuboid reference until the production mesh-area oracle is integrated.
// The C# serializer parity test compares this report with Rust on both OSes.
static void PrintWallMetrics(Wall wall)
{
    var length = wall.Length;
    var wallThickness = wall.Thickness;
    var wallHeight = wall.Height;
    if (!double.IsFinite(length) || length <= 0 ||
        !double.IsFinite(wallThickness) || wallThickness <= 0 ||
        !double.IsFinite(wallHeight) || wallHeight <= 0)
    {
        throw new InvalidOperationException("Fixture wall dimensions must be finite and positive");
    }
    var volume = WallGeometry.NetVolume(wall, Array.Empty<WallOpening>());
    if (!double.IsFinite(volume) || volume <= 0)
        throw new InvalidOperationException("Production C# wall volume must be finite and positive");
    var area = 2 * (length * wallHeight + length * wallThickness + wallHeight * wallThickness);
    Console.WriteLine(FormattableString.Invariant($"{wall.Id:N}, {volume:F12}, {area:F12}"));
}
WallGeometry.Validate(document.Walls, Array.Empty<WallOpening>());
// Production C# metamorphic oracle: reversing a centreline or translating it
// by survey-scale coordinates must not change its physical net volume.
// Rust checks the corresponding five serialized walls independently.
var diagonalVolume = WallGeometry.NetVolume(document.Walls[1], Array.Empty<WallOpening>());
foreach (var candidate in new[] { document.Walls[2], document.Walls[4] })
{
    var volume = WallGeometry.NetVolume(candidate, Array.Empty<WallOpening>());
    if (!double.IsFinite(volume) || Math.Abs(volume - diagonalVolume) > 1e-9)
    {
        Console.Error.WriteLine("Production C# wall volume changed under reversal or translation");
        return 1;
    }
}
// Exercise the real C# opening-volume implementation independently of the
// no-opening serialization fixture. Rust has matching door/window mesh tests;
// full serialized opening parity remains a separate, unfinished milestone.
var openingHost = document.Walls[0];
var door = new WallOpening(
    Guid.Parse("00000000-0000-0000-0000-000000000101"),
    openingHost.Id, OpeningKind.Door, 1, 0.8, 0, 2);
var window = new WallOpening(
    Guid.Parse("00000000-0000-0000-0000-000000000102"),
    openingHost.Id, OpeningKind.Window, 3, 0.6, 1, 1);
var openings = new[] { door, window };
WallGeometry.Validate(document.Walls, openings);
var openingVolume = WallGeometry.NetVolume(openingHost, openings);
const double expectedOpeningVolume = 1.96;
if (!double.IsFinite(openingVolume) ||
    Math.Abs(openingVolume - expectedOpeningVolume) > 1e-9)
{
    Console.Error.WriteLine("Production C# door/window net volume differs from the geometric reference");
    return 1;
}
// Serialize the SAME production C# openings into independent JSON and native
// fixtures. The original no-opening fixtures remain immutable.
document.Openings.AddRange(openings);
document.Validate();
WallGeometry.Validate(document.Walls, document.Openings);
var openingJson = ProjectFile.SerializeDocument(document);
File.WriteAllText(Path.Combine(directory, "wall-with-openings.g3d.json"), openingJson);
var openingNativePath = Path.Combine(directory, "wall-with-openings.g3d");
NativeDocumentStore.SaveProject(openingNativePath, document);
foreach (var restored in new[]
{
    ProjectFile.DeserializeDocument(openingJson),
    NativeDocumentStore.LoadProject(openingNativePath)
})
{
    restored.Validate();
    WallGeometry.Validate(restored.Walls, restored.Openings);
    if (restored.Openings.Count != openings.Length ||
        !restored.Openings.Select(o => o.Id).SequenceEqual(openings.Select(o => o.Id)) ||
        Math.Abs(WallGeometry.NetVolume(restored.Walls[0], restored.Openings) - openingVolume) > 1e-9)
    {
        Console.Error.WriteLine("C# opening serialization changed geometry or identities");
        return 1;
    }
}
// Publish a machine-readable production C# opening oracle alongside the fixtures.
// Rust independently reads the C#-serialized base wall with identical openings.
File.WriteAllText(
    Path.Combine(directory, "opening-volume.txt"),
    openingVolume.ToString("F12", System.Globalization.CultureInfo.InvariantCulture) + "\n");
foreach (var wall in document.Walls)
    PrintWallMetrics(wall);
return 0;
