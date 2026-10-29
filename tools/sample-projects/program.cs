using Guanacaste3D.Core;

/// <summary>
/// Regenerates the sample projects shipped with the desktop application from their DocumentSession recipes:
///   dotnet run --project tools/sample-projects
/// The breeze-block house loads the detailed mesh families from resources/families, so the saved project carries them
/// and users can reuse its jacaranda and cars in their own projects.
/// </summary>
internal static class Program
{
    public static void Main()
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Guanacaste3D.Desktop", "resources"));
        var families = Directory.GetFiles(Path.Combine(resources, "families"), "*" + NativeDocumentStore.FamilyExtension)
            .Select(NativeDocumentStore.LoadFamily).ToArray();
        var document = BreezeBlockHouseShowcase.Create(families);
        var path = Path.Combine(resources, "samples", "casa-del-cobogo" + NativeDocumentStore.ProjectExtension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        NativeDocumentStore.SaveProject(path, document);
        var reloaded = NativeDocumentStore.LoadProject(path);
        reloaded.Validate();
        Console.WriteLine($"{path}: {reloaded.ComponentFamilies.Count} mesh families, {reloaded.StructuralElements.Count} elements, " +
                          $"{new FileInfo(path).Length / 1024.0 / 1024:0.0} MB");
    }
}
