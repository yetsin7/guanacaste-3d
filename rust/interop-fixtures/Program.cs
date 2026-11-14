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
document.Walls.Add(new Wall(wallId, level.Id, new Point2(0, 0), new Point2(4, 0), 0.2, 3));
document.Validate();
var json = ProjectFile.SerializeDocument(document);
File.WriteAllText(Path.Combine(directory, "wall.g3d.json"), json);
NativeDocumentStore.SaveProject(Path.Combine(directory, "wall.g3d"), document);
Console.WriteLine($"{wallId:N}, {2.4:F12}, {26.8:F12}");
return 0;
