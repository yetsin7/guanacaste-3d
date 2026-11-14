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
document.Validate();
var json = ProjectFile.SerializeDocument(document);
File.WriteAllText(Path.Combine(directory, "wall.g3d.json"), json);
NativeDocumentStore.SaveProject(Path.Combine(directory, "wall.g3d"), document);
// Independently compute the rectangular wall reference from the same C# inputs.
// This is an analytic fixture oracle, NOT yet the production WallGeometry engine.
var length = Math.Sqrt(Math.Pow(4.0 - 0.0, 2) + Math.Pow(0.0 - 0.0, 2));
var volume = length * thickness * height;
var area = 2 * (length * height + length * thickness + height * thickness);
Console.WriteLine(FormattableString.Invariant($"{wallId:N}, {volume:F12}, {area:F12}"));
return 0;
