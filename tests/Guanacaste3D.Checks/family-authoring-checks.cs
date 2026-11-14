using Guanacaste3D.Core;

/// <summary>A parametric dining table made from scratch: parameters, two types, boxes driven by formulas, loaded and placed.</summary>
static class FamilyAuthoringChecks
{
    public static FamilyDocument DiningTable()
    {
        var table = FamilyAuthoring.Create("Mesa de comedor", FamilyCategory.Furniture);
        FamilyAuthoring.AddLengthParameter(table, "Ancho", 1.2);
        FamilyAuthoring.AddLengthParameter(table, "Fondo", 0.8);
        FamilyAuthoring.AddLengthParameter(table, "Alto", 0.75);
        FamilyAuthoring.AddLengthParameter(table, "Pata", 0.05);
        var small = table.Types[0].Id;
        table.Types[0] = table.Types[0] with { Name = "120 × 80" };
        var large = FamilyAuthoring.AddType(table, "160 × 90", small);
        FamilyAuthoring.SetValue(table, large, "Ancho", 1.6);
        FamilyAuthoring.SetValue(table, large, "Fondo", 0.9);
        FamilyAuthoring.AddBox(table, "Tablero", "0", "0", "Ancho", "Fondo", "Alto - 0.03", "0.03");
        foreach (var (x, y) in new[] { ("0.03", "0.03"), ("Ancho - Pata - 0.03", "0.03"), ("0.03", "Fondo - Pata - 0.03"), ("Ancho - Pata - 0.03", "Fondo - Pata - 0.03") })
            FamilyAuthoring.AddBox(table, "Pata", x, y, "Pata", "Pata", "0", "Alto - 0.03");
        return table;
    }

    public static void Run(Action<bool, string> check)
    {
        var table = DiningTable();
        var (small, large) = (table.Types[0].Id, table.Types[1].Id);
        System.Numerics.Vector3 Size(Guid type)
        {
            var mesh = FamilyGeometry.Tessellate(table, type);
            return new(mesh.Positions.Max(p => p.X) - mesh.Positions.Min(p => p.X), mesh.Positions.Max(p => p.Y) - mesh.Positions.Min(p => p.Y),
                mesh.Positions.Max(p => p.Z) - mesh.Positions.Min(p => p.Z));
        }
        bool Near(System.Numerics.Vector3 size, double x, double y, double z) =>
            Math.Abs(size.X - x) < 1e-4 && Math.Abs(size.Y - y) < 1e-4 && Math.Abs(size.Z - z) < 1e-4;
        check(table.Operations.Count == 5 && Near(Size(small), 1.2, 0.8, 0.75) && Near(Size(large), 1.6, 0.9, 0.75),
            "Families: a table built from boxes follows its parameters in each type (120×80 and 160×90)");

        var bad = DiningTable();
        var rejected = false;
        try { FamilyAuthoring.SetValue(bad, bad.Types[0].Id, "Ancho", 0); bad.Validate(); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "Families: a type that would make a box of zero width is refused");

        var baked = FamilyAuthoring.Bake(table, large);
        var mesh = baked.Mesh!.Decode();
        check(baked.Name == "Mesa de comedor : 160 × 90" && Math.Abs(mesh.Min.Z) < 1e-5 &&
              Math.Abs(mesh.Min.X + 0.8) < 1e-4 && Math.Abs(mesh.Max.X - 0.8) < 1e-4,
            "Families: loading a type regenerates it centred on the insertion point with its base on the floor");

        var session = new DocumentSession(BuildingDocument.Create("Comedor"));
        var level = session.Document.Levels[0].Id;
        var id = session.LoadComponentFamily(baked);
        var placed = session.PlaceComponentFamily(level, id, new Point2(3, 2));
        session.LoadComponentFamily(FamilyAuthoring.Bake(table, large));
        check(session.Document.ComponentFamilies.Count(family => family.Id == id) == 1 &&
              session.Document.StructuralElements.Any(element => element.Id == placed && element.FamilyId == id),
            "Families: a made-up table loads, is placed in the project, and loading it again replaces it");
        check(Guanacaste3D.Core.Geometry.BuildingMesher.Build(session.Document).TriangleCount > 0, "Families: the placed table appears in the 3D model");
    }
}
