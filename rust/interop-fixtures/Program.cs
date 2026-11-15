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
}
// Calculate from the same point inputs used to construct the walls. This
// prevents an edited fixture from silently retaining obsolete hard-coded
// reference dimensions. This is still analytic, not production WallGeometry.
static void PrintWallMetrics(Guid id, Point2 a, Point2 b, double wallThickness, double wallHeight)
{
    var dx = b.X - a.X;
    var dy = b.Y - a.Y;
    var length = Math.Sqrt(dx * dx + dy * dy);
    if (!double.IsFinite(length) || length <= 0 ||
        !double.IsFinite(wallThickness) || wallThickness <= 0 ||
        !double.IsFinite(wallHeight) || wallHeight <= 0)
    {
        throw new InvalidOperationException("Fixture wall dimensions must be finite and positive");
    }
    var volume = length * wallThickness * wallHeight;
    var area = 2 * (length * wallHeight + length * wallThickness + wallHeight * wallThickness);
    Console.WriteLine(FormattableString.Invariant($"{id:N}, {volume:F12}, {area:F12}"));
}
PrintWallMetrics(wallId, start, end, thickness, height);
PrintWallMetrics(diagonalId, diagonalStart, diagonalEnd, thickness, height);
PrintWallMetrics(reversedId, reversedStart, reversedEnd, thickness, height);
PrintWallMetrics(variedId, variedStart, variedEnd, variedThickness, variedHeight);
return 0;
