using System.Diagnostics;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Interop;

// Builds the ten-storey tower step by step and prints the design review after each step: first the bare
// architecture and structure, showing everything it still lacks, then the coordinated building with its services.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ten-storey-building <output directory>");
    return 2;
}

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);

var clock = Stopwatch.StartNew();
var shell = TenStoreyBuilding.CreateArchitecture();
Console.WriteLine($"1. Architecture and structure ({clock.Elapsed.TotalSeconds:0.0} s): {Describe(shell)}");
Console.WriteLine(BuildingReview.Format(BuildingReview.Review(shell)));

clock.Restart();
var session = new DocumentSession(shell);
TenStoreyBuilding.AddServices(session);
var building = session.Document;
var review = BuildingReview.Review(building);
Console.WriteLine();
Console.WriteLine($"2. With building services ({clock.Elapsed.TotalSeconds:0.0} s): {Describe(building)}");
Console.WriteLine(BuildingReview.Format(review));

ProjectFile.Save(Path.Combine(outputDirectory, "ten-storey-building.g3d"), building);
File.WriteAllBytes(Path.Combine(outputDirectory, "ten-storey-building.pdf"), PdfPlanExporter.ExportSheets(building, DateTimeOffset.Now));
File.WriteAllText(Path.Combine(outputDirectory, "ten-storey-building.ifc"), IfcExporter.ToStep(building, DateTimeOffset.Now));
Console.WriteLine();
Console.WriteLine($"Saved the project, its drawing sheets and an IFC4 model in {outputDirectory}.");
return review.Count == 0 ? 0 : 1;

/// <summary>Summarizes the model's authored content for a diagnostic build log.</summary>
static string Describe(BuildingDocument document) =>
    $"{document.Levels.Count} levels, {document.Walls.Count} walls, {document.Openings.Count} doors and windows, " +
    $"{document.Rooms.Count} rooms, {document.Columns.Count} columns, {document.StructuralElements.Count} elements, " +
    $"{document.ElectricalDevices.Count} devices, {document.ElectricalCircuits.Count} circuits, {document.PlumbingSystems.Count} plumbing systems";
