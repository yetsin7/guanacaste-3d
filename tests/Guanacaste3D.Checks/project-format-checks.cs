using System.Text;
using System.Text.Json;
using Guanacaste3D.Core;

/// <summary>
/// Regression checks for the file formats: .g3d projects, .g3dt templates and .g3df in-place
/// families. They cover the extension each kind writes, reading of earlier .g3 projects, and
/// rejection of a file whose header or extension does not match its kind.
/// </summary>
static class ProjectFormatChecks
{
    private const string GenericModels = FamilyCategoryCatalog.GenericModelsId;

    /// <summary>A unit square in counter-clockwise order; its area is 1 m².</summary>
    private static readonly Point2[] UnitSquare = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    /// <summary>Runs every file-format check in sequence.</summary>
    public static void Run(Action<bool, string> check)
    {
        CheckProjectFiles(check);
        CheckEarlierProjectsStillOpen(check);
        CheckTemplateFiles(check);
        CheckFamilyFiles(check);
        CheckMismatchedKindsAreRejected(check);
    }

    /// <summary>Returns a unique temporary path with the given extension.</summary>
    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"guanacaste-format-{Guid.NewGuid():N}{extension}");

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

    /// <summary>Deletes a temporary file and its backup when they exist.</summary>
    private static void Delete(string path)
    {
        foreach (var file in new[] { path, path + ".bak" })
            if (File.Exists(file)) File.Delete(file);
    }

    /// <summary>Creates a valid family with one unit-square extrusion, for file tests.</summary>
    private static InPlaceFamily NewFamily()
    {
        var session = new DocumentSession(BuildingDocument.Create("Family source"));
        var familyId = session.CreateInPlaceFamily(GenericModels, "Pedestal");
        session.AddInPlaceExtrusion(familyId, WorkPlane.Horizontal(0), UnitSquare, 0, 0.9, name: "Body");
        return session.Document.InPlaceFamilies.Single();
    }

    /// <summary>Verifies that projects are written as .g3d and that other extensions are refused.</summary>
    private static void CheckProjectFiles(Action<bool, string> check)
    {
        var path = TempPath(ProjectFile.NativeExtension);
        try
        {
            ProjectFile.Save(path, BuildingDocument.Create("Formats"));
            check(ProjectFile.IsNativePath(path) && File.ReadLines(path).First() == "G3-PROJECT/1",
                "Projects are written as .g3d files with the project header");
            check(ProjectFile.Load(path).Name == "Formats", "A .g3d project reopens with its content");
            check(RejectionMessage(() => ProjectFile.Save(Path.ChangeExtension(path, ProjectFile.LegacyNativeExtension),
                      BuildingDocument.Create("Refused"))) is not null &&
                  !File.Exists(Path.ChangeExtension(path, ProjectFile.LegacyNativeExtension)),
                "Saving to the earlier .g3 extension is refused, so older files are never overwritten");
            check(RejectionMessage(() => ProjectFile.Save(Path.ChangeExtension(path, ProjectFile.TemplateExtension),
                      BuildingDocument.Create("Refused"))) is not null,
                "A project cannot be saved with the template extension");
        }
        finally
        {
            Delete(path);
        }
    }

    /// <summary>Verifies that a project saved as .g3 by an earlier build still opens.</summary>
    private static void CheckEarlierProjectsStillOpen(Action<bool, string> check)
    {
        var path = TempPath(ProjectFile.LegacyNativeExtension);
        try
        {
            var json = JsonSerializer.Serialize(BuildingDocument.Create("Earlier build"));
            File.WriteAllText(path, "G3-PROJECT/1\n" + json, new UTF8Encoding(false));
            var opened = ProjectFile.Load(path);
            check(opened.Name == "Earlier build" && opened.SchemaVersion == BuildingDocument.CurrentSchemaVersion,
                "A project saved as .g3 by an earlier build opens as an import");
        }
        finally
        {
            Delete(path);
        }
    }

    /// <summary>Verifies that a template keeps its families and is written with the template header.</summary>
    private static void CheckTemplateFiles(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Template source"));
        var familyId = session.CreateInPlaceFamily(GenericModels, "Reusable block");
        session.AddInPlaceExtrusion(familyId, WorkPlane.Horizontal(0), UnitSquare, 0, 1.0);
        var path = TempPath(ProjectFile.TemplateExtension);
        try
        {
            ProjectFile.SaveTemplate(path, session.Document);
            check(File.ReadLines(path).First() == "G3-TEMPLATE/1",
                "Templates are written as .g3dt files with the template header");
            var reopened = ProjectFile.LoadTemplate(path);
            check(reopened.Name == "Template source" && reopened.InPlaceFamilies.Single().Name == "Reusable block",
                "A template reopens with its families, so new projects can start from it");
            check(RejectionMessage(() => ProjectFile.SaveTemplate(Path.ChangeExtension(path, ProjectFile.NativeExtension),
                      session.Document)) is not null,
                "A template cannot be saved with the project extension");
        }
        finally
        {
            Delete(path);
        }
    }

    /// <summary>Verifies family round trips, regeneration, and rejection of wrong versions and invalid content.</summary>
    private static void CheckFamilyFiles(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Family source"));
        var familyId = session.CreateInPlaceFamily(GenericModels, "Pedestal");
        session.SetInPlaceParameter(familyId, "Altura", 0.9);
        session.AddInPlaceExtrusion(familyId, WorkPlane.Horizontal(0), UnitSquare, 0, 0.9,
            depthParameter: "Altura", materialName: "Concrete", name: "Body");
        var family = session.Document.InPlaceFamilies.Single();
        var path = TempPath(ProjectFile.FamilyExtension);
        try
        {
            ProjectFile.SaveFamily(path, family);
            check(File.ReadLines(path).First() == "G3-FAMILY/1",
                "Families are written as .g3df files with the family header");
            var reopened = ProjectFile.LoadFamily(path);
            var extrusion = reopened.Extrusions.Single();
            var volume = InPlaceFamilyRules.SignedVolume(InPlaceFamilyRules.Tessellate(reopened));
            check(reopened.Name == "Pedestal" && reopened.CategoryId == GenericModels &&
                  reopened.Parameters.Single().Value == 0.9 && extrusion.DepthParameter == "Altura" &&
                  extrusion.MaterialName == "Concrete" && extrusion.Plane == family.Extrusions.Single().Plane &&
                  extrusion.Profile.SequenceEqual(UnitSquare) && Math.Abs(volume - 0.9) < 1e-6,
                "A .g3df family reopens with its parameters, binding, material and work plane, and regenerates the same solid");
            check(RejectionMessage(() => ProjectFile.SaveFamily(Path.ChangeExtension(path, ProjectFile.NativeExtension),
                      family)) is not null,
                "A family cannot be saved with the project extension");

            var original = File.ReadAllText(path);
            File.WriteAllText(path, original.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 99"), new UTF8Encoding(false));
            check(RejectionMessage(() => ProjectFile.LoadFamily(path)) == "Unsupported family file version.",
                "A family file with an unknown envelope version is rejected on load");

            File.WriteAllText(path, original.Replace("\"Name\": \"Pedestal\"", "\"Name\": \"   \""), new UTF8Encoding(false));
            check(RejectionMessage(() => ProjectFile.LoadFamily(path)) is not null,
                "A family file with an invalid name is rejected on load, not silently accepted");
        }
        finally
        {
            Delete(path);
        }
    }

    /// <summary>Verifies that renaming a file to another kind's extension is rejected by its header.</summary>
    private static void CheckMismatchedKindsAreRejected(Action<bool, string> check)
    {
        var temporary = new List<string>();
        string CopyAs(string source, string extension)
        {
            var copy = TempPath(extension);
            File.Copy(source, copy, overwrite: true);
            temporary.Add(copy);
            return copy;
        }
        try
        {
            var project = TempPath(ProjectFile.NativeExtension);
            temporary.Add(project);
            ProjectFile.Save(project, BuildingDocument.Create("Mismatch"));
            var template = TempPath(ProjectFile.TemplateExtension);
            temporary.Add(template);
            ProjectFile.SaveTemplate(template, BuildingDocument.Create("Mismatch"));
            var family = TempPath(ProjectFile.FamilyExtension);
            temporary.Add(family);
            ProjectFile.SaveFamily(family, NewFamily());

            check(RejectionMessage(() => ProjectFile.LoadTemplate(CopyAs(project, ProjectFile.TemplateExtension))) is not null,
                "A project renamed to .g3dt is rejected by its header");
            check(RejectionMessage(() => ProjectFile.Load(CopyAs(template, ProjectFile.NativeExtension))) is not null,
                "A template renamed to .g3d is rejected by its header");
            check(RejectionMessage(() => ProjectFile.LoadFamily(CopyAs(project, ProjectFile.FamilyExtension))) is not null,
                "A project renamed to .g3df is rejected by its header");
            check(RejectionMessage(() => ProjectFile.Load(CopyAs(family, ProjectFile.NativeExtension))) is not null,
                "A family renamed to .g3d is rejected by its header");
        }
        finally
        {
            foreach (var file in temporary) Delete(file);
        }
    }
}
