using System.Globalization;
using System.IO;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Interop;

/// <summary>Exercises the model-to-documentation contract separately from memory-intensive rendering benchmarks.</summary>
internal static partial class DocumentationChecks
{
    private static int _checks;

    /// <summary>Runs deterministic geometry, persistence, PDF, and WPF interaction regressions.</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            CheckSections();
            CheckAxes();
            CheckElectricalMigration();
            CheckPrintedDocumentation();
            CheckDesktop();
            Console.WriteLine($"Documentation checks passed: {_checks}.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    /// <summary>Checks real intersections, finite extent, excluded objects, hosted openings, and migration.</summary>
    private static void CheckSections()
    {
        var session = new DocumentSession(BuildingDocument.Create("Section verification"));
        var level = session.Document.Levels[0].Id;
        var wall = session.AddWall(level, new(0, 0), new(8, 0));
        session.AddWall(level, new(0, 4), new(8, 4));
        var cut = new SectionCut(new(2, -1), new(2, 1));
        var id = session.AddSectionView("Cross section", cut);
        var contours = SectionGeometry.Build(BuildingMesher.Build(session.Document), cut);
        var wallContour = contours.Where(contour => contour.Kind == SurfaceKind.Wall).ToArray();
        Check(wallContour.Length == 1 && wallContour[0].Closed, "A cut joins triangle segments into one closed wall profile");
        Near(wallContour[0].Points.Max(point => point.X) - wallContour[0].Points.Min(point => point.X), 0.2,
            "The section shows actual wall thickness");
        Near(wallContour[0].Points.Max(point => point.Y), 3, "Section elevations retain metric model height");
        Check(SignedArea(wallContour[0]) > 0, "Section exteriors retain outward winding for overlapping-solid fills");
        Check(SectionGeometry.Build(BuildingMesher.Build(session.Document), new(new(12, -1), new(12, 1))).Count == 0,
            "Objects outside the cutting plane do not appear as elevations");
        var clipped = SectionGeometry.Build(BuildingMesher.Build(session.Document), new(new(2, 0), new(2, 0.05)));
        Check(clipped.SelectMany(contour => contour.Points).All(point => point.X >= -1e-5 && point.X <= 0.05001),
            "Cut geometry is clipped to the finite endpoints");
        var reversed = SectionGeometry.Build(BuildingMesher.Build(session.Document), new(cut.End, cut.Start));
        Check(reversed.Count == contours.Count, "Reversing a cutting line preserves its physical profiles");
        session.AddOpening(wall, OpeningKind.Window, 2, width: 1, sill: 1, height: 1);
        var windowContours = SectionGeometry.Build(BuildingMesher.Build(session.Document), cut);
        Check(windowContours.Count(contour => contour.Kind == SurfaceKind.Wall) == 2,
            "A cut through a window separates the wall below and above the void");
        Check(windowContours.Any(contour => contour.Kind == SurfaceKind.Glass), "Window glazing is included in cut geometry");
        var longitudinal = SectionGeometry.Build(BuildingMesher.Build(session.Document), new(new(-1, 0), new(9, 0)))
            .Where(contour => contour.Kind == SurfaceKind.Wall && contour.Closed).ToArray();
        Check(longitudinal.Sum(contour => WindingAt(contour, new(3, 1.5))) == 0 &&
            longitudinal.Sum(contour => WindingAt(contour, new(3, 0.5))) > 0,
            "Longitudinal section fills the wall below the window while retaining its empty void");
        var original = session.Document.Views.Single(view => view.Id == id).Cut;
        session.UpdateSectionCut(id, new(new(6, -1), new(6, 1)));
        session.Undo();
        Check(session.Document.Views.Single(view => view.Id == id).Cut == original, "Undo restores the cut location");
        session.Redo();
        Check(session.Document.Views.Single(view => view.Id == id).Cut!.Start.X == 6, "Redo restores the edited cut");
        Reject(() => session.AddSectionView("Cross section", cut), "Duplicate section names are rejected atomically");
        Reject(() => session.UpdateSectionCut(id, new(new(0, 0), new(0, 0))), "Zero-length sections are rejected");
        var reopened = Reopen(session.Document, "sections.g3d");
        Check(reopened.Views.Single(view => view.Id == id).Cut == session.Document.Views.Single(view => view.Id == id).Cut,
            "Native project save and reopen preserve section endpoints and identity");
        var legacyDocument = BuildingDocument.Create();
        legacyDocument.SchemaVersion = 11;
        var legacy = ProjectFile.SerializeDocument(legacyDocument);
        var migrated = ProjectFile.DeserializeDocument(legacy);
        Check(migrated.SchemaVersion == BuildingDocument.CurrentSchemaVersion && migrated.Grids.Count == 0 &&
            migrated.Views.Single(view => view.Id == BuiltInProjectViews.SectionAId).Cut is null,
            "Schema 11 projects migrate with their legacy section and no invented axes");
        var overlap = new DocumentSession(BuildingDocument.Create("Overlapping solid verification"));
        overlap.AddColumn(overlap.Document.Levels[0].Id, new(0, 0));
        overlap.AddColumn(overlap.Document.Levels[0].Id, new(0, 0.2));
        var overlapContours = SectionGeometry.Build(BuildingMesher.Build(overlap.Document), new(new(0, -1), new(0, 1)));
        Check(overlapContours.All(contour => contour.Closed) && overlapContours.Sum(contour => WindingAt(contour, new(1.1, 1))) == 2,
            "Overlapping solids preserve a nonzero interior instead of creating an artificial void");
    }

    /// <summary>Verifies editable dimensions against shared axes and safe deletion with history.</summary>
    private static void CheckAxes()
    {
        var session = new DocumentSession(BuildingDocument.Create("Axis verification"));
        var level = session.Document.Levels[0].Id;
        var axis = session.AddGridAxis("A", new(0, -10), new(0, 10));
        var column = session.AddColumn(level, new(3.2, 0));
        var dimension = PlanDimensions.ForSelection(session.Document, column).Single(item => item.ReferenceId == axis);
        Near(PlanDimensions.Resolve(session.Document, dimension)!.Value.Value, 3, "Column clearance measures to the grid line");
        session.AddDimension(dimension);
        session.EditDimension(dimension, 5);
        Near(session.Document.Columns.Single().Position.X, 5.2, "Editing a grid dimension moves the column to the requested clearance");
        var upper = session.AddLevel();
        var upperColumn = session.AddColumn(upper, new(4, 0));
        Check(PlanDimensions.ForSelection(session.Document, upperColumn).Any(item => item.ReferenceId == axis),
            "One project axis is available on every building level");
        session.UpdateGridAxis(axis, "B", new(1, -10), new(1, 10));
        Near(PlanDimensions.Resolve(session.Document, session.Document.Dimensions.Single())!.Value.Value, 4,
            "Moving or renaming an axis updates saved measurements by identity");
        var reopened = Reopen(session.Document, "axes.g3d");
        Check(reopened.Grids.Single().Name == "B" && reopened.Dimensions.Single().ReferenceId == axis,
            "Native storage preserves axes and their associative dimensions");
        session.RemoveGridAxis(axis);
        Check(session.Document.Columns.Count == 2 && session.Document.Dimensions.Count == 0,
            "Deleting an axis removes only dependent annotations, retaining columns");
        session.Undo();
        Check(session.Document.Grids.Count == 1 && session.Document.Dimensions.Count == 1,
            "Undo restores the axis together with its dimensions");
        Reject(() => session.AddGridAxis("b", new(2, -10), new(2, 10)), "Axis names are unique regardless of case");
        Reject(() => session.AddGridAxis("C", new(double.NaN, 0), new(1, 0)), "Nonfinite axis coordinates are rejected");
        var clone = session.Document.Clone();
        clone.Grids.Clear();
        Check(session.Document.Grids.Count == 1, "Cloned axis collections do not mutate the source project");
    }

    /// <summary>Checks that printed sheets contain current annotations and real family geometry.</summary>
    private static void CheckPrintedDocumentation()
    {
        var session = new DocumentSession(BuildingDocument.Create("Documentation verification"));
        var level = session.Document.Levels[0].Id;
        var wall = session.AddWall(level, new(0, 0), new(6, 0));
        session.AddDimension(PlanDimensions.ForSelection(session.Document, wall).Single());
        session.AddModelText(level, new(1, 2), "Plan note");
        session.AddGridAxis("A", new(-1, -2), new(-1, 4));
        session.AddSectionView("Long section", new(new(-2, 0), new(8, 0)));
        var family = session.CreateInPlaceFamily(BimCategoryCatalog.GenericModelsId, "Pedestal");
        session.AddInPlaceExtrusion(family, InPlaceFamilyRules.HorizontalPlane(0),
            [new(1, 1), new(2, 1), new(2, 2), new(1, 2)], 0, 1.5);
        var primitives = PdfPlanExporter.PlanPrimitives(session.Document, level);
        var texts = primitives.OfType<SheetText>().Select(text => text.Text).ToArray();
        Check(texts.Contains(6d.ToString("0.00", CultureInfo.CurrentCulture)) && texts.Contains("Plan note"),
            "PDF primitives include permanent dimensions and placed notes");
        Check(texts.Contains("A") && texts.Contains("Long section"), "PDF plans include axis bubbles and section references");
        var familySegments = FamilyPlanGeometry.ForLevel(session.Document.InPlaceFamilies.Single(), 0, 3);
        Check(familySegments.All(segment => primitives.OfType<SheetPolyline>().Any(line =>
                line.Points.SequenceEqual(new[] { segment.Start, segment.End }))),
            "Family outlines in PDF match the coordinated plan geometry");
        var familyCut = SectionGeometry.Build(BuildingMesher.Build(session.Document), new(new(1.5, 0.5), new(1.5, 2.5)));
        Check(familyCut.Any(contour => contour.Closed && contour.Points.Any(point => Math.Abs(point.Y - 1.5) < 1e-5)),
            "In-place family solids participate in actual section cuts");
        var pdf = PdfPlanExporter.Export(session.Document, SheetPaperSize.A3, DateTimeOffset.UnixEpoch);
        var output = Path.GetFullPath("artifacts/documentation-evidence");
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "coordinated-plans.pdf"), pdf);
        Check(System.Text.Encoding.ASCII.GetString(pdf.AsSpan(0, 5)) == "%PDF-", "The coordinated plan exports as a PDF document");
        var complete = PdfPlanExporter.ExportDocumentation(session.Document, SheetPaperSize.A3, DateTimeOffset.UnixEpoch);
        File.WriteAllBytes(Path.Combine(output, "coordinated-documentation.pdf"), complete);
        var pdfText = System.Text.Encoding.Latin1.GetString(complete);
        Check(System.Text.RegularExpressions.Regex.Matches(pdfText, @"/Type /Page\b").Count == session.Document.Levels.Count +
            session.Document.Views.Count(view => view.Kind is ProjectViewKind.Section or ProjectViewKind.Elevation),
            "The documentation PDF has one page per plan, section, and elevation");
        Check(pdfText.Contains("Long section"), "Saved section names are printed in the PDF title blocks");
        var section = session.Document.Views.Single(view => view.Name == "Long section");
        var cutPrimitives = PdfPlanExporter.GeneratedViewPrimitives(session.Document, section);
        Check(cutPrimitives.OfType<SheetPolyline>().Any(line => line.Weight == 0.35) &&
            cutPrimitives.OfType<SheetText>().Any(text => text.Text.Contains(session.Document.Levels[0].Name)),
            "Printed sections include actual cut contours and coordinated level names");
    }

    /// <summary>Protects schema 12 electrical projects when applying the documentation migration.</summary>
    private static void CheckElectricalMigration()
    {
        var session = new DocumentSession(BuildingDocument.Create("Electrical migration verification"));
        var id = session.AddElectricalDevice(session.Document.Levels[0].Id, ElectricalDeviceKind.Switch,
            "S1", new(2, 0));
        var previous = session.Document.Clone();
        previous.SchemaVersion = 12;
        var migrated = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(previous));
        Check(migrated.SchemaVersion == 13 && migrated.ElectricalDevices.Single().Id == id && migrated.Grids.Count == 0,
            "Schema 12 electrical projects retain their devices when upgraded for documentation");
    }

    /// <summary>Exercises the official compressed file format in a unique directory and removes only its own output.</summary>
    private static BuildingDocument Reopen(BuildingDocument document, string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "g3d-documentation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        try { ProjectFile.Save(path, document); return ProjectFile.Load(path); }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    /// <summary>Requires a rejected transaction to leave the caller in control of further edits.</summary>
    private static void Reject(Action action, string explanation)
    {
        try { action(); }
        catch (InvalidDataException) { Check(true, explanation); return; }
        throw new InvalidOperationException(explanation);
    }

    /// <summary>Uses a drafting tolerance to account for float tessellation without masking dimensional errors.</summary>
    private static void Near(double actual, double expected, string explanation) => Check(Math.Abs(actual - expected) < 0.00001, explanation);

    /// <summary>Checks oriented fill boundaries independently from the WPF rasterizer.</summary>
    private static double SignedArea(SectionContour contour) => contour.Points.Select((point, index) =>
        point.X * contour.Points[(index + 1) % contour.Points.Count].Y -
        point.Y * contour.Points[(index + 1) % contour.Points.Count].X).Sum() / 2;

    /// <summary>Evaluates nonzero filling at a model point, including multiple touching or overlapping cut profiles.</summary>
    private static int WindingAt(SectionContour contour, Point2 point)
    {
        var winding = 0;
        for (var index = 0; index < contour.Points.Count; index++)
        {
            var start = contour.Points[index];
            var end = contour.Points[(index + 1) % contour.Points.Count];
            var cross = (end.X - start.X) * (point.Y - start.Y) - (point.X - start.X) * (end.Y - start.Y);
            if (start.Y <= point.Y && end.Y > point.Y && cross > 0) winding++;
            else if (start.Y > point.Y && end.Y <= point.Y && cross < 0) winding--;
        }
        return winding;
    }

    /// <summary>Records one verified invariant and stops immediately on a regression.</summary>
    private static void Check(bool valid, string explanation)
    {
        if (!valid) throw new InvalidOperationException(explanation);
        _checks++;
        Console.WriteLine("OK: " + explanation);
    }
}
