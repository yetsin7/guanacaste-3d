using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Interop;

internal static partial class DocumentationChecks
{
    /// <summary>Verifies protected datums, historical native migration, editable families, exact scales, and saved compositions.</summary>
    private static void CheckBaseLevelsAndSheetFamilies()
    {
        var session = new DocumentSession(BuildingDocument.Create("Perimeter and roof"));
        var floor = session.Document.Levels.Single(level => level.Role == LevelRole.BaseFloor);
        var roof = session.Document.Levels.Single(level => level.Role == LevelRole.BaseRoof);
        Check(floor.Name == DefaultLabels.Get("LevelName", 1) && roof.Name == DefaultLabels.Get("LevelName", 2) &&
            floor.Elevation == 0 && roof.Elevation == 3, "New projects include the protected floor and roof at metric default elevations");
        Reject(() => session.RemoveLevel(floor.Id), "The empty base floor cannot be deleted");
        Reject(() => session.RemoveLevel(roof.Id), "The empty base roof cannot be deleted");
        Check(!session.CanUndo, "Rejected base-level deletion does not create an undo step");
        session.UpdateLevel(roof.Id, "Roof datum", 3.4);
        Reject(() => session.RemoveLevel(roof.Id), "Renaming the roof does not remove its protection");
        Reject(() => session.UpdateLevel(roof.Id, "Roof datum", -1), "The base roof cannot be moved below the floor");
        var optional = session.AddLevel(); session.RemoveLevel(optional);
        Check(session.Document.Levels.Count == 2, "Unused additional levels remain removable");
        var basementProject = session.Document.Clone(); basementProject.SchemaVersion = 13;
        basementProject.Levels = basementProject.Levels.Select(level => level with { Role = LevelRole.Additional }).ToList();
        basementProject.Levels.Add(new(Guid.NewGuid(), "Basement", -2));
        var migratedBasement = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(basementProject));
        Check(migratedBasement.Levels.Single(level => level.Id == floor.Id).Role == LevelRole.BaseFloor &&
            migratedBasement.Levels.Single(level => level.Id == roof.Id).Role == LevelRole.BaseRoof,
            "Migrating a basement project preserves the established floor and roof identities");
        var host = session.AddWall(floor.Id, new(0, 0), new(9.79, 0));
        session.AddWall(floor.Id, new(9.79, 0), new(9.79, 11.15));
        session.AddWall(floor.Id, new(0, 0), new(0, 3.2));
        session.AddWall(floor.Id, new(0, 11.15), new(9.79, 11.15));
        session.AddOpening(host, OpeningKind.Door, 4);
        foreach (var dimension in PlanDimensions.ForSelection(session.Document, host)) session.AddDimension(dimension);
        for (var index = 0; index < 4; index++) session.AddGridAxis((index + 1).ToString(),
            new(index * 3.2, -1), new(index * 3.2, 12));
        for (var index = 0; index < 5; index++) session.AddGridAxis(((char)('A' + index)).ToString(),
            new(-1, index * 2.5), new(11, index * 2.5));
        var output = Path.GetFullPath("artifacts/documentation-evidence"); Directory.CreateDirectory(output);
        CheckHistoricalContainer(session.Document, floor.Id, roof.Id, output);
        var template = ProjectTemplateDocument.CreateMetricArchitecture(); template.Levels = [new("Floor datum", 0)];
        Check(template.CreateProject("One-level template").Levels.Count(BaseLevels.IsProtected) == 2,
            "A legacy one-level template gains its protected roof when creating a project");
        var sheetId = session.AddSheet(SheetPaperSize.A3, "Perimeter walls");
        var sheet = session.Document.Sheets.Single(sheet => sheet.Id == sheetId);
        var family = session.Document.SheetFamilies.Single(family => family.Id == sheet.TitleBlockFamilyId).Clone();
        Check(family.Category == FamilyCategory.TitleBlocks && family.SheetLayout!.Width == 297 && family.SheetLayout.Height == 420,
            "New drawing sheets use a persistent portrait title-block family in physical millimeters");
        var marker = new SheetGraphic(Guid.NewGuid(), SheetGraphicKind.Text, new(25, 66), new(25, 66), "Editable family field", 2.5);
        family.SheetLayout!.Graphics.Add(marker);
        var view = session.Document.Views.Single(view => view.Kind == ProjectViewKind.Plan && view.LevelId == floor.Id);
        session.UpdateSheet(sheet with { Description = "New perimeter walls, dimensions and reference axes", Subtitle = "Perimeter and roof",
            Number = "ARQ-01", DateText = "27/10/2026", Viewports = [new(Guid.NewGuid(), view.Id, 25, 90, 247, 235, 100)] }, family);
        var familyPath = Path.Combine(output, "architectural-sheet.g3df"); NativeDocumentStore.SaveFamily(familyPath, family);
        var loadedFamily = NativeDocumentStore.LoadFamily(familyPath);
        Check(loadedFamily.SheetLayout!.Graphics.Any(graphic => graphic.Id == marker.Id && graphic.Text == marker.Text),
            "Editable family graphics survive an independent native .g3df round trip");
        var composition = SheetComposition.Build(session.Document, session.Document.Sheets[0], DateTimeOffset.UnixEpoch);
        Check(composition.Width == 297 && composition.Height == 420 && composition.Viewports.Single().Scale == 100,
            "Composition preserves paper orientation and the user's exact viewport scale");
        Check(composition.Graphics.OfType<SheetText>().Any(text => text.Text == "ARQ-01") &&
            composition.Graphics.OfType<SheetText>().Any(text => text.Text == session.Document.Name),
            "Title-block fields regenerate from sheet and project parameters");
        var secondId = session.AddSheetFromFamily(loadedFamily);
        var second = session.Document.Sheets.Single(sheet => sheet.Id == secondId);
        Check(second.TitleBlockFamilyId == sheet.TitleBlockFamilyId, "Imported native families can be shared between sheets");
        var changedFamily = loadedFamily.Clone();
        changedFamily.SheetLayout = changedFamily.SheetLayout! with { Graphics = changedFamily.SheetLayout.Graphics.Select(graphic =>
            graphic.Id == marker.Id ? graphic with { Text = "Shared family update" } : graphic).ToList() };
        session.UpdateSheet(second, changedFamily);
        Check(session.Document.Sheets.All(candidate => SheetComposition.Build(session.Document, candidate, DateTimeOffset.UnixEpoch)
            .Graphics.OfType<SheetText>().Any(text => text.Text == "Shared family update")),
            "Editing one shared family regenerates every sheet using it");
        session.Undo();
        Check(session.Document.SheetFamilies.Single().SheetLayout!.Graphics.Any(graphic => graphic.Text == marker.Text),
            "Undo restores the previous reusable family definition");
        session.Redo();
        var landscape = SheetFamilies.Create(SheetPaperSize.A3, portrait: false);
        var north = session.Document.Views.Single(view => view.Id == BuiltInProjectViews.NorthElevationId);
        var east = session.Document.Views.First(view => view.Kind == ProjectViewKind.Elevation && view.Orientation == ViewOrientation.East);
        session.UpdateSheet(second with { Name = "Architectural elevations", Number = "ARQ-02", TitleBlockFamilyId = landscape.Id,
            Viewports = [new(Guid.NewGuid(), north.Id, 20, 90, 185, 117, 100, "1"),
                new(Guid.NewGuid(), east.Id, 215, 90, 185, 117, 150, "2")] }, landscape);
        var reopened = Reopen(session.Document, "sheet-families.g3d");
        Check(reopened.SheetFamilies.Count == 2 && reopened.Sheets[1].Viewports.Select(viewport => viewport.Scale).SequenceEqual(new[] { 100d, 150d }) &&
            reopened.Levels.Count(BaseLevels.IsProtected) == 2, "Project save/reopen retains families, compositions, independent scales, and base protections");
        var snapshot = session.Document;
        Reject(() => session.UpdateSheet(session.Document.Sheets[0] with { Viewports =
            [new(Guid.NewGuid(), view.Id, 900, 90, 100, 100)] }), "Out-of-paper placements are rejected atomically");
        Check(ReferenceEquals(snapshot, session.Document), "A rejected sheet edit preserves the active project and its history");
        var pdf = PdfPlanExporter.ExportDocumentation(reopened, SheetPaperSize.A4, DateTimeOffset.UnixEpoch);
        File.WriteAllBytes(Path.Combine(output, "editable-sheet-compositions.pdf"), pdf);
        var pdfText = Encoding.Latin1.GetString(pdf);
        Check(System.Text.RegularExpressions.Regex.Matches(pdfText, @"/Type /Page\b").Count == 2 && pdfText.Contains("re W n"),
            "PDF export prints the two saved compositions and clips viewports independently");
        var templatePath = Path.Combine(output, "sheet-family-template.g3dt"); TemplateFile.Save(templatePath, reopened);
        Check(TemplateFile.Load(templatePath).SheetFamilies.Count == 2, "Reusable sheet families survive template save and reopening");
    }

    /// <summary>Constructs an authentic historical compressed container to test migration after header integrity validation.</summary>
    private static void CheckHistoricalContainer(BuildingDocument source, Guid floorId, Guid roofId, string output)
    {
        var legacy = source.Clone(); legacy.SchemaVersion = 13;
        legacy.Levels.RemoveAll(level => level.Id == roofId); legacy.Views.RemoveAll(view => view.LevelId == roofId);
        legacy.Levels = legacy.Levels.Select(level => level with { Role = LevelRole.Additional }).ToList();
        var json = Encoding.UTF8.GetBytes(ProjectFile.SerializeDocument(legacy));
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(json);
        var path = Path.Combine(output, "historical-single-level.g3d");
        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("G3DNATV1")); writer.Write(1); writer.Write((byte)NativeDocumentKind.Project);
            writer.Write(13); writer.Write(legacy.Id.ToByteArray()); writer.Write((long)json.Length);
            writer.Write(compressed.Length); writer.Write(SHA256.HashData(json)); writer.Write(compressed.ToArray());
        }
        var loaded = ProjectFile.Load(path);
        Check(loaded.Levels.Count == 2 && loaded.Levels.Single(level => level.Id == floorId).Role == LevelRole.BaseFloor &&
            loaded.Walls.SequenceEqual(source.Walls), "Old native single-level projects gain the roof without moving their existing model");
        Check(NativeDocumentStore.TryInspect(path, out var info) && info!.SchemaVersion == 13,
            "Opening an older project migrates in memory without overwriting its stored original");
    }
}
