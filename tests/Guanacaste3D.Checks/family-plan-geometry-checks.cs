using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

/// <summary>Checks that in-place family solids remain coordinated across plans, render meshes, and persistence.</summary>
internal static class FamilyPlanGeometryChecks
{
    /// <summary>Exercises a cut solid, a low solid, story filtering, and reopening the native project.</summary>
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Family plan geometry"));
        var familyId = session.CreateInPlaceFamily(BimCategoryCatalog.GenericModelsId, "Plan block");
        var profile = new[] { new Point2(0, 0), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1) };
        session.AddInPlaceExtrusion(familyId, InPlaceFamilyRules.HorizontalPlane(0), profile, 0, 2);
        var family = session.Document.InPlaceFamilies.Single();
        var plan = FamilyPlanGeometry.ForLevel(family, 0, 3);
        check(plan.Count > 0 && plan.All(segment => segment.Start.X is >= -0.001 and <= 2.001 &&
                                         segment.Start.Y is >= -0.001 and <= 1.001),
            "A cut family solid appears with its model coordinates in the ground plan");
        check(FamilyPlanGeometry.ForLevel(family, 3, 6).Count == 0,
            "A family solid is absent from stories above its own geometry");
        check(FamilyGeometry.Tessellate(family).TriangleCount > 0 &&
              BuildingMesher.Build(session.Document).TriangleCount >= FamilyGeometry.Tessellate(family).TriangleCount,
            "The same family contributes triangles to the shared 3D and render mesh");

        var lowId = session.CreateInPlaceFamily(BimCategoryCatalog.GenericModelsId, "Low block");
        session.AddInPlaceExtrusion(lowId, InPlaceFamilyRules.HorizontalPlane(0), profile, 0, 0.5);
        check(FamilyPlanGeometry.ForLevel(session.Document.InPlaceFamilies.Single(item => item.Id == lowId), 0, 3).Count == 4,
            "Low furniture retains its perimeter below the plan cut plane");

        var path = Path.Combine(Path.GetTempPath(), $"family-plan-{Guid.NewGuid():N}.g3d");
        try
        {
            ProjectFile.Save(path, session.Document);
            var reopened = ProjectFile.Load(path).InPlaceFamilies.Single(item => item.Id == familyId);
            check(FamilyPlanGeometry.ForLevel(reopened, 0, 3).Count == plan.Count,
                "A reopened native project regenerates the same family plan geometry");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
        CheckTemplateExcludesModeledFamilies(session.Document, check);
    }

    /// <summary>Ensures native templates keep setup while excluding family instances and their geometry.</summary>
    private static void CheckTemplateExcludesModeledFamilies(BuildingDocument document, Action<bool, string> check)
    {
        var path = Path.Combine(Path.GetTempPath(), $"family-template-{Guid.NewGuid():N}.g3dt");
        try
        {
            TemplateFile.Save(path, document);
            var template = TemplateFile.Load(path);
            check(template.InPlaceFamilies.Count == 0 && template.Levels.Count == document.Levels.Count,
                "A native template retains levels without copying modeled in-place families");
            check(TemplateRules.CreateProject(template, "New project").InPlaceFamilies.Count == 0,
                "A project from a template starts without the source project's family solids");
            File.WriteAllText(path, NativeContainer.Seal(NativeContainer.TemplateSignature,
                BuildingDocument.CurrentSchemaVersion, ProjectFile.SerializeDocument(document)));
            var rejected = false;
            try { TemplateFile.Load(path); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "A template containing modeled families is rejected on open");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }
}
