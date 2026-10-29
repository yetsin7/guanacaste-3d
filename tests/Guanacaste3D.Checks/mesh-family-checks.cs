using System.Numerics;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

/// <summary>Checks mesh component families (.g3df): storage, placement, scaling, the viewport preview and the shipped files.</summary>
static class MeshFamilyChecks
{
    public static void Run(Action<bool, string> check)
    {
        // A 2 × 1 × 3 m box family, its base already centered, plus a one-triangle preview.
        Vector3[] corners = [new(-1, -0.5f, 0), new(1, -0.5f, 0), new(1, 0.5f, 0), new(-1, 0.5f, 0),
            new(-1, -0.5f, 3), new(1, -0.5f, 3), new(1, 0.5f, 3), new(-1, 0.5f, 3)];
        int[] faces = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7];
        var mesh = FamilyMesh.Create(corners, faces, Enumerable.Repeat(SurfaceKind.CarPaint, 12).ToArray());
        mesh.Preview = FamilyMesh.Create(corners, [0, 1, 6], [SurfaceKind.Metal]);
        var family = new FamilyDocument
        {
            Name = "Caja de prueba", Category = FamilyCategory.Entourage,
            Types = [new FamilyTypeDefinition(Guid.NewGuid(), "Estándar", [])], Mesh = mesh
        };
        family.Metadata["PlanShape"] = nameof(ResidentialComponentShape.Car);
        var path = Path.Combine(Path.GetTempPath(), $"mesh-family-{Guid.NewGuid():N}.g3df");
        NativeDocumentStore.SaveFamily(path, family);
        var loaded = NativeDocumentStore.LoadFamily(path);
        File.Delete(path);
        check(loaded.Mesh!.TriangleCount == 12 && loaded.Mesh.Preview!.TriangleCount == 1 &&
              Vector3.Distance(loaded.Mesh.Decode().Size, new Vector3(2, 1, 3)) < 1e-5f,
            "A mesh family survives a .g3df save and reload with its preview");

        var bad = new FamilyMesh { Positions = mesh.Positions, Indices = Convert.ToBase64String(BitConverter.GetBytes(99)), Kinds = mesh.Kinds };
        check(Throws(bad.Validate), "A mesh whose indices point past its vertices is rejected");

        var session = new DocumentSession(ArchitecturalLibrary.CreateProject("Familias de malla"));
        var level = session.Document.Levels[0].Id;
        var id = session.LoadComponentFamily(loaded);
        check(session.LoadComponentFamily(loaded) == id && session.Document.ComponentFamilies.Count == 1,
            "Loading a family twice replaces it instead of duplicating it");
        var placed = session.PlaceComponentFamily(level, id, new Point2(10, 5), Math.PI / 2);
        var element = session.Document.StructuralElements.Single(candidate => candidate.Id == placed);
        check(element.Kind == StructuralElementKind.Component && element.FamilyId == id &&
              element.ComponentShape == ResidentialComponentShape.Car && Math.Abs(element.Depth - 1) < 1e-6,
            "A placed mesh family is a component at its modeled size with its plan shape");

        var scene = BuildingMesher.Build(session.Document, plants: PlantDetail.Render);
        var (min, max) = scene.Bounds();
        // Turned a quarter, the 2 m width runs along Y and the 1 m depth along X.
        check(scene.TriangleKinds.Count(kind => kind == SurfaceKind.CarPaint) == 12 &&
              Math.Abs(max.X - min.X - 1) < 1e-4 && Math.Abs(max.Y - min.Y - 2) < 1e-4 && Math.Abs(max.Z - 3) < 1e-4 &&
              Math.Abs((min.X + max.X) / 2 - 10) < 1e-4,
            "The renderer draws the full mesh turned and placed at the insertion point");
        check(BuildingMesher.Build(session.Document, plants: PlantDetail.Model).TriangleKinds.SequenceEqual([SurfaceKind.Metal]),
            "The interactive view draws the light preview instead");

        session.UpdateStructuralElement(placed, element.Name, 4, 6, 0, element.Style);
        var stretched = BuildingMesher.Build(session.Document, plants: PlantDetail.Render).Bounds();
        check(Math.Abs(stretched.Max.Z - 6) < 1e-4 && Math.Abs(stretched.Max.Y - stretched.Min.Y - 4) < 1e-4,
            "Changing an instance's width and height stretches its mesh");

        var copy = session.Document.Clone();
        copy.Validate();
        var json = Path.Combine(Path.GetTempPath(), $"mesh-project-{Guid.NewGuid():N}.g3");
        ProjectFile.Save(json, copy);
        var reopened = ProjectFile.Load(json);
        File.Delete(json);
        check(reopened.ComponentFamilies.Single().Mesh!.TriangleCount == 12 && reopened.StructuralElements.Single().FamilyId == id,
            "A project keeps its loaded mesh families and their instances when saved");
        var orphan = copy.Clone();
        orphan.ComponentFamilies.Clear();
        check(Throws(orphan.Validate), "An instance of a family that is not loaded is rejected");

        Shipped(check);
    }

    /// <summary>The CC0 jacaranda and cars shipped as families, and the saved breeze-block house that uses them.</summary>
    private static void Shipped(Action<bool, string> check)
    {
        string? root = null;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Guanacaste3D.slnx"))) { root = directory.FullName; break; }
        if (root is null) return;
        var resources = Path.Combine(root, "src", "Guanacaste3D.Desktop", "resources");
        var families = Directory.GetFiles(Path.Combine(resources, "families"), "*.g3df").Select(NativeDocumentStore.LoadFamily).ToArray();
        check(new[] { BreezeBlockHouseShowcase.TreeFamily, BreezeBlockHouseShowcase.SedanFamily, BreezeBlockHouseShowcase.CoupeFamily }
                .All(name => families.Any(family => family.Name == name && family.Metadata.GetValueOrDefault("License") == "CC0-1.0")),
            "The detailed jacaranda and cars ship as CC0 mesh families");
        var tree = families.Single(family => family.Name == BreezeBlockHouseShowcase.TreeFamily).Mesh!;
        check(tree.Preview is { } preview && preview.TriangleCount < tree.TriangleCount / 10 &&
              tree.Decode().Kinds.Contains(SurfaceKind.Leaf) && tree.Decode().Kinds.Contains(SurfaceKind.Bark),
            "The jacaranda has leaves, bark and a light preview for the viewport");

        var sample = NativeDocumentStore.LoadProject(Path.Combine(resources, "samples", "casa-del-cobogo.g3d"));
        sample.Validate();
        check(sample.ComponentFamilies.Count == 3 &&
              sample.StructuralElements.Count(element => element.FamilyId is not null) == 6,
            "The saved breeze-block house opens with its jacarandas and two detailed cars");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }
}
