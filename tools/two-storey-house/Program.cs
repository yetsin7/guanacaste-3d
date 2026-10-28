using Guanacaste3D.Core;
using Guanacaste3D.Core.Interop;

if (args.Length != 1)
    throw new ArgumentException("Provide an output directory for the house study.");

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);
var house = TwoStoreyHouse.Create();
ProjectFile.Save(Path.Combine(outputDirectory, "two-storey-house.g3d"), house);
NativeDocumentStore.SaveFamily(Path.Combine(outputDirectory, "residential-toilet.g3df"), ToiletFamily.Create());
File.WriteAllBytes(Path.Combine(outputDirectory, "two-storey-house.pdf"),
    PdfPlanExporter.ExportSheets(house, DateTimeOffset.Now));
Console.WriteLine($"Created the editable project and drawing sheets in {outputDirectory}.");
