using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

/// <summary>
/// Regression checks for in-place families (schema 9): the category catalog, creation and naming,
/// extrusions on horizontal and inclined work planes, parameter regeneration, atomic rejection of
/// invalid profiles, undo and redo, persistence, and migration from schema 8.
/// </summary>
static class InPlaceFamilyChecks
{
    private const double Tolerance = 1e-6;
    private const string GenericModels = FamilyCategoryCatalog.GenericModelsId;

    /// <summary>A 2 m by 1 m rectangle in counter-clockwise order; its area is 2 m².</summary>
    private static readonly Point2[] TwoByOneRectangle = [new(0, 0), new(2, 0), new(2, 1), new(0, 1)];

    /// <summary>Runs every in-place family check in sequence.</summary>
    public static void Run(Action<bool, string> check)
    {
        CheckCategoryCatalog(check);
        CheckCreationAndNaming(check);
        CheckExtrusionVolumeAndWinding(check);
        CheckWorkPlaneOrientation(check);
        CheckParametricRegeneration(check);
        CheckRejectedChangesLeaveProjectUnchanged(check);
        CheckUndoRedoAndPersistence(check);
        CheckSchemaEightMigration(check);
        CheckSceneIntegration(check);
    }

    /// <summary>Creates a session on a new project so each check starts from an empty document.</summary>
    private static DocumentSession NewSession() => new(BuildingDocument.Create("In-place families"));

    /// <summary>Returns the rejection message when the action throws InvalidDataException, or null when it succeeds.</summary>
    private static string? RejectionMessage(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (InvalidDataException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>Verifies the registered categories and which ones accept in-place families.</summary>
    private static void CheckCategoryCatalog(Action<bool, string> check)
    {
        var categories = FamilyCategoryCatalog.All;
        check(categories.Count == 28 && categories.Select(category => category.Id).Distinct().Count() == 28,
            "The category catalog registers all 28 categories with unique stable identifiers");
        var inPlace = categories.Where(category => category.SupportsInPlace).Select(category => category.Id).ToArray();
        check(inPlace.SequenceEqual(new[] { GenericModels }),
            "Only Generic Models accepts in-place families in this increment");
        check(FamilyCategoryCatalog.Find(GenericModels)?.DefaultName == "Modelos genéricos",
            "Generic Models keeps its localizable display name");
    }

    /// <summary>Verifies family creation, trimmed storage, and the naming and category rules.</summary>
    private static void CheckCreationAndNaming(Action<bool, string> check)
    {
        var session = NewSession();
        var familyId = session.CreateInPlaceFamily(GenericModels, "  Pedestal  ");
        var family = session.Document.InPlaceFamilies.Single();
        check(family.Id == familyId && family.Name == "Pedestal" && family.CategoryId == GenericModels,
            "A family created in Generic Models stores its trimmed name and category");
        check(RejectionMessage(() => session.CreateInPlaceFamily("roofs", "Roof family")) is not null &&
              session.Document.InPlaceFamilies.Count == 1,
            "A category without in-place support is rejected without changing the project");
        check(RejectionMessage(() => session.CreateInPlaceFamily(GenericModels, "PEDESTAL")) is not null &&
              session.Document.InPlaceFamilies.Count == 1,
            "Family names are unique regardless of case");
        check(RejectionMessage(() => session.CreateInPlaceFamily(GenericModels, "   ")) is not null,
            "A blank family name is rejected");
        check(RejectionMessage(() => session.CreateInPlaceFamily("missing-category", "Ghost")) is not null,
            "An unknown category is rejected");
    }

    /// <summary>Verifies solid volume, face orientation, and winding normalization on a horizontal plane.</summary>
    private static void CheckExtrusionVolumeAndWinding(Action<bool, string> check)
    {
        var session = NewSession();
        var blockId = session.CreateInPlaceFamily(GenericModels, "Block");
        session.AddInPlaceExtrusion(blockId, WorkPlane.Horizontal(0), TwoByOneRectangle, 0, 1.5,
            name: "Body", materialName: "Concrete");
        var block = InPlaceFamilyRules.Tessellate(session.Document.InPlaceFamilies.Single());
        check(block.TriangleCount == 12, "A rectangular extrusion has eight side triangles and four cap triangles");
        check(Math.Abs(InPlaceFamilyRules.SignedVolume(block) - 3.0) < Tolerance,
            "The signed volume equals profile area times depth (2 m² × 1.5 m), so faces point outward");

        var clockwise = Enumerable.Reverse(TwoByOneRectangle).ToArray();
        var clockwiseId = session.CreateInPlaceFamily(GenericModels, "Clockwise block");
        session.AddInPlaceExtrusion(clockwiseId, WorkPlane.Horizontal(0), clockwise, 0, 1.0);
        var clockwiseMesh = InPlaceFamilyRules.Tessellate(session.Document.InPlaceFamilies.Single(family => family.Id == clockwiseId));
        check(Math.Abs(InPlaceFamilyRules.SignedVolume(clockwiseMesh) - 2.0) < Tolerance,
            "A clockwise profile is normalized, so it also encloses positive volume");

        const int sides = 32;
        const double radius = 0.5;
        var circle = Enumerable.Range(0, sides).Select(index =>
        {
            var angle = 2 * Math.PI * index / sides;
            return new Point2(radius * Math.Cos(angle), radius * Math.Sin(angle));
        }).ToArray();
        var circleArea = sides / 2.0 * radius * radius * Math.Sin(2 * Math.PI / sides);
        var columnId = session.CreateInPlaceFamily(GenericModels, "Column");
        session.AddInPlaceExtrusion(columnId, WorkPlane.Horizontal(0), circle, 0, 2.0);
        var column = InPlaceFamilyRules.Tessellate(session.Document.InPlaceFamilies.Single(family => family.Id == columnId));
        check(Math.Abs(InPlaceFamilyRules.SignedVolume(column) - circleArea * 2.0) < Tolerance,
            "A circular profile extrudes to its polygon area times depth");
    }

    /// <summary>Verifies that solids follow the real work plane, including vertical and inclined planes.</summary>
    private static void CheckWorkPlaneOrientation(Action<bool, string> check)
    {
        var session = NewSession();
        var verticalId = session.CreateInPlaceFamily(GenericModels, "Vertical block");
        var vertical = new WorkPlane(new Point3(0, 0, 0), new Point3(0, 1, 0), new Point3(1, 0, 0));
        session.AddInPlaceExtrusion(verticalId, vertical, TwoByOneRectangle, 0, 3.0);
        var (min, max) = InPlaceFamilyRules.Tessellate(session.Document.InPlaceFamilies.Single()).Bounds();
        check(Math.Abs(max.Y - min.Y - 3.0) < Tolerance && Math.Abs(max.Z - min.Z - 1.0) < Tolerance &&
              Math.Abs(max.X - min.X - 2.0) < Tolerance,
            "On a vertical plane the solid runs along its normal (+Y), not along the global Z axis");

        var normal = 1 / Math.Sqrt(2);
        var inclined = new WorkPlane(new Point3(0, 0, 0), new Point3(0, normal, normal), new Point3(1, 0, 0));
        var slantedId = session.CreateInPlaceFamily(GenericModels, "Slanted block");
        session.AddInPlaceExtrusion(slantedId, inclined, TwoByOneRectangle, 1.0, 2.0);
        var slanted = InPlaceFamilyRules.Tessellate(session.Document.InPlaceFamilies.Single(family => family.Id == slantedId));
        var offsets = slanted.Positions.Select(point => (point.Y + point.Z) * normal).ToArray();
        check(Math.Abs(offsets.Min() - 1.0) < 1e-5 && Math.Abs(offsets.Max() - 3.0) < 1e-5 &&
              Math.Abs(InPlaceFamilyRules.SignedVolume(slanted) - 4.0) < 1e-5,
            "On an inclined plane the solid spans offsets 1 to 3 along its normal, with volume 4 m³");
    }

    /// <summary>Verifies that editing a parameter regenerates the same extrusion, keeping its identity and material.</summary>
    private static void CheckParametricRegeneration(Action<bool, string> check)
    {
        var session = NewSession();
        var familyId = session.CreateInPlaceFamily(GenericModels, "Parametric block");
        session.SetInPlaceParameter(familyId, "Profundidad", 1.2);
        session.AddInPlaceExtrusion(familyId, WorkPlane.Horizontal(0), TwoByOneRectangle, 0, 1.2,
            depthParameter: "Profundidad", materialName: "Concrete", name: "Body");
        var before = session.Document.InPlaceFamilies.Single().Extrusions.Single();
        session.SetInPlaceParameter(familyId, "profundidad", 0.5);
        var family = session.Document.InPlaceFamilies.Single();
        var after = family.Extrusions.Single();
        var volume = InPlaceFamilyRules.SignedVolume(InPlaceFamilyRules.Tessellate(family));
        check(family.Parameters.Single().Name == "Profundidad" && family.Parameters.Single().Value == 0.5,
            "Changing a parameter keeps its name and updates its value, matched case-insensitively");
        check(after.Id == before.Id && after.Depth == 0.5 && after.MaterialName == "Concrete" && after.Plane == before.Plane,
            "Regeneration keeps the same extrusion with its material and work plane");
        check(Math.Abs(volume - 1.0) < Tolerance,
            "The solid follows the parameter (2 m² × 0.5 m) without creating a new extrusion");
        check(RejectionMessage(() => session.AddInPlaceExtrusion(familyId, WorkPlane.Horizontal(0),
                  TwoByOneRectangle, 0, 1, depthParameter: "Missing")) is not null &&
              session.Document.InPlaceFamilies.Single().Extrusions.Count == 1,
            "An extrusion bound to a missing parameter is rejected without changing the project");
        var boundPath = Path.Combine(Path.GetTempPath(), $"guanacaste-inplace-bound-{Guid.NewGuid():N}.g3");
        try
        {
            ProjectFile.Save(boundPath, session.Document);
            var reopened = ProjectFile.Load(boundPath).InPlaceFamilies.Single();
            var reopenedVolume = InPlaceFamilyRules.SignedVolume(InPlaceFamilyRules.Tessellate(reopened));
            check(reopened.Extrusions.Single().DepthParameter == "Profundidad" && Math.Abs(reopenedVolume - 1.0) < Tolerance,
                "A parameter-bound depth survives save and reopen and still regenerates from its parameter");
        }
        finally
        {
            if (File.Exists(boundPath)) File.Delete(boundPath);
        }
    }

    /// <summary>Verifies that every invalid profile, depth, or parameter is rejected with nothing changed.</summary>
    private static void CheckRejectedChangesLeaveProjectUnchanged(Action<bool, string> check)
    {
        var session = NewSession();
        var familyId = session.CreateInPlaceFamily(GenericModels, "Guarded");
        var plane = WorkPlane.Horizontal(0);
        string? Reject(Point2[] profile, double depth = 1) =>
            RejectionMessage(() => session.AddInPlaceExtrusion(familyId, plane, profile, 0, depth));

        check(Reject([new(0, 0), new(1, 0)]) is not null, "An open two-point profile is rejected");
        check(Reject([new(0, 0), new(1, 0), new(1, 0), new(1, 1)]) is not null,
            "A profile with two coincident adjacent vertices is rejected");
        check(Reject([new(0, 0), new(1, 0), new(2, 0)]) is not null,
            "A collinear profile that encloses no area is rejected");
        check(Reject([new(0, 0), new(double.NaN, 0), new(1, 1)]) is not null,
            "A profile with a non-finite coordinate is rejected");
        check(Reject([new(0, 0), new(6, 0), new(0, 3), new(4, 3)]) is { } crossing &&
              crossing.Contains("cross each other"),
            "A self-crossing profile is rejected and the crossing edges are named");
        check(Reject(TwoByOneRectangle, depth: 0) is not null && Reject(TwoByOneRectangle, depth: 2000) is not null,
            "Depths outside 1 mm to 1 km are rejected");
        check(RejectionMessage(() => session.SetInPlaceParameter(familyId, "Ancho", 0)) is not null,
            "A zero length parameter is rejected");
        check(session.Document.InPlaceFamilies.Single().Extrusions.Count == 0 &&
              session.Document.InPlaceFamilies.Single().Parameters.Count == 0,
            "Every rejected change leaves the family unchanged");
    }

    /// <summary>Verifies undo and redo of an edit, then a save, reopen and regeneration round trip.</summary>
    private static void CheckUndoRedoAndPersistence(Action<bool, string> check)
    {
        var session = NewSession();
        var familyId = session.CreateInPlaceFamily(GenericModels, "Persisted");
        session.SetInPlaceParameter(familyId, "Ancho", 2.0);
        session.AddInPlaceExtrusion(familyId, WorkPlane.Horizontal(0), TwoByOneRectangle, 0, 1.0, name: "Base");
        var extrusionId = session.Document.InPlaceFamilies.Single().Extrusions.Single().Id;
        var tilted = new WorkPlane(new Point3(0, 0, 0.5), new Point3(0, 1, 0), new Point3(1, 0, 0));
        session.UpdateInPlaceExtrusion(familyId, extrusionId, tilted, TwoByOneRectangle, 0.25, 2.0, null, "Steel");
        var edited = session.Document.InPlaceFamilies.Single().Extrusions.Single();
        check(edited.Id == extrusionId && edited.Plane == tilted && edited.Depth == 2.0 && edited.MaterialName == "Steel",
            "Editing an extrusion changes it in place and keeps its identifier");
        session.Undo();
        var undone = session.Document.InPlaceFamilies.Single().Extrusions.Single();
        check(undone.Depth == 1.0 && undone.MaterialName == string.Empty && undone.Plane == WorkPlane.Horizontal(0),
            "Undo restores the previous extrusion definition");
        session.Redo();
        check(session.Document.InPlaceFamilies.Single().Extrusions.Single().Depth == 2.0,
            "Redo reapplies the extrusion edit");

        var filename = Path.Combine(Path.GetTempPath(), $"guanacaste-inplace-{Guid.NewGuid():N}.g3");
        try
        {
            var original = InPlaceFamilyRules.SignedVolume(
                InPlaceFamilyRules.Tessellate(session.Document.InPlaceFamilies.Single()));
            ProjectFile.Save(filename, session.Document);
            var reloaded = ProjectFile.Load(filename);
            var reloadedFamily = reloaded.InPlaceFamilies.Single();
            var reloadedExtrusion = reloadedFamily.Extrusions.Single();
            check(reloaded.SchemaVersion == 9 && reloadedFamily.Id == familyId && reloadedFamily.Name == "Persisted" &&
                  reloadedFamily.CategoryId == GenericModels && reloadedFamily.Parameters.Single().Value == 2.0 &&
                  reloadedExtrusion.Id == extrusionId && reloadedExtrusion.Plane == tilted &&
                  reloadedExtrusion.Profile.SequenceEqual(TwoByOneRectangle) &&
                  reloadedExtrusion.MaterialName == "Steel",
                "Saving and reopening keeps the family, parameters, work plane, profile, material and identifiers");
            check(Math.Abs(InPlaceFamilyRules.SignedVolume(InPlaceFamilyRules.Tessellate(reloadedFamily)) - original) < Tolerance,
                "Regeneration after reopening produces the same solid");
        }
        finally
        {
            if (File.Exists(filename)) File.Delete(filename);
        }
    }

    /// <summary>Verifies that the shared scene mesh includes in-place solids, so 3D views and mesh exports show them.</summary>
    private static void CheckSceneIntegration(Action<bool, string> check)
    {
        var session = NewSession();
        var baseline = BuildingMesher.Build(session.Document).TriangleCount;
        var familyId = session.CreateInPlaceFamily(GenericModels, "Scene block");
        session.AddInPlaceExtrusion(familyId, WorkPlane.Horizontal(0), TwoByOneRectangle, 0, 1.5);
        var added = InPlaceFamilyRules.Tessellate(session.Document.InPlaceFamilies.Single()).TriangleCount;
        check(added == 12 && BuildingMesher.Build(session.Document).TriangleCount == baseline + added,
            "The shared scene mesh includes in-place solids, so 3D views and mesh exports show them");
    }

    /// <summary>Verifies that a schema 8 file loads and migrates to schema 9 with no in-place families.</summary>
    private static void CheckSchemaEightMigration(Action<bool, string> check)
    {
        var legacyPath = Path.Combine(Path.GetTempPath(), $"guanacaste-inplace-legacy-{Guid.NewGuid():N}.g3");
        try
        {
            var node = JsonNode.Parse(JsonSerializer.Serialize(BuildingDocument.Create("Legacy")))!.AsObject();
            node["SchemaVersion"] = 8;
            node.Remove("InPlaceFamilies");
            File.WriteAllText(legacyPath, "G3-PROJECT/1\n" + node.ToJsonString(), new UTF8Encoding(false));
            var migrated = ProjectFile.Load(legacyPath);
            check(migrated.SchemaVersion == 9 && migrated.InPlaceFamilies.Count == 0,
                "A schema 8 project loads, migrates to schema 9 and has no in-place families");
        }
        finally
        {
            if (File.Exists(legacyPath)) File.Delete(legacyPath);
        }
    }
}
