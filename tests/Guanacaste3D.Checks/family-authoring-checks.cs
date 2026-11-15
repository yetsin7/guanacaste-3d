using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

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

        // The furniture the person made goes into the budget: one line per family, a piece per instance, no site labor.
        session.PlaceComponentFamily(level, id, new Point2(6, 2));
        var furnished = ConstructionBudget.Build(session.Document, BudgetSettings.Default);
        var tables = furnished.Lines.Single(line => line.Takeoff.Code == MaterialCatalog.FurnitureCode(id));
        check(tables.Takeoff.PurchaseQuantity == 2 && tables.Takeoff.Material.Category == MaterialCategory.Furnishings &&
              tables.Takeoff.Material.Description == baked.Name && tables.Total == 2 * MaterialCatalog.FurniturePrice(baked.Category) &&
              furnished.Labor == Math.Round((furnished.Materials - tables.Total) * BudgetSettings.Default.LaborRate, 2),
            "Budget: placed furniture is counted per piece at its reference price and carries no labor");
        var quoted = BudgetSettings.FromJson(BudgetSettings.Default.WithPrice(MaterialCatalog.FurnitureCode(id), 310_000m).ToJson());
        check(ConstructionBudget.Build(session.Document, quoted).Lines.Single(line => line.Takeoff.Code == MaterialCatalog.FurnitureCode(id)).Total == 620_000m,
            "Budget: a quoted price for a piece of furniture is saved and replaces the reference price");

        // A floor lamp turned about its axis: base, stem and shade, in metal and light.
        var lamp = FamilyAuthoring.Create("Lámpara de pie", FamilyCategory.LightingFixtures);
        var profile = new List<Point2> { new(0, 0), new(0.15, 0), new(0.15, 0.03), new(0.02, 0.03), new(0.02, 1.4), new(0.25, 1.4), new(0.18, 1.7), new(0, 1.7) };
        FamilyAuthoring.AddRevolution(lamp, "Cuerpo", profile, material: "Metal");
        var turned = FamilyGeometry.Tessellate(lamp);
        var width = turned.Positions.Max(p => p.X) - turned.Positions.Min(p => p.X);
        var tall = turned.Positions.Max(p => p.Z) - turned.Positions.Min(p => p.Z);
        check(Math.Abs(width - 0.5) < 0.01 && Math.Abs(tall - 1.7) < 1e-4 && FamilyGeometry.SignedVolume(turned) > 0 &&
              turned.TriangleKinds.All(kind => kind == SurfaceKind.Metal),
            "Families: a lamp turned about its axis has its size, faces outward and renders as metal");
        // In plan each piece draws itself as seen from above: the table its top (the legs hide under it), the lamp the
        // two rings of its shade (the base hides under the shade), not the generic box of a component.
        var tableSymbol = FamilyPlanSymbol.For(session.Document, session.Document.StructuralElements.Single(element => element.Id == placed));
        var tableCorners = tableSymbol.SelectMany(segment => new[] { segment.A, segment.B }).ToList();
        check(tableSymbol.Count == 4 && tableCorners.All(point => Math.Abs(Math.Abs(point.X - 3) - 0.8) < 1e-3 && Math.Abs(Math.Abs(point.Y - 2) - 0.45) < 1e-3),
            "Families: in plan the table draws its top, with the legs hidden under it");
        var lampId = session.LoadComponentFamily(FamilyAuthoring.Bake(lamp, lamp.Types[0].Id));
        var lampPlaced = session.PlaceComponentFamily(level, lampId, new Point2(8, 2));
        var lampSymbol = FamilyPlanSymbol.For(session.Document, session.Document.StructuralElements.Single(element => element.Id == lampPlaced));
        var radii = lampSymbol.SelectMany(segment => new[] { segment.A, segment.B }).Select(point => point.DistanceTo(new Point2(8, 2))).ToList();
        check(lampSymbol.Count == 2 * FamilyGeometry.RevolutionSegments &&
              radii.All(radius => Math.Abs(radius - 0.25) < 1e-3 || Math.Abs(radius - 0.18) < 1e-3),
            "Families: in plan the turned lamp draws the two rings of its shade, its base hidden under it");

        var reversed = FamilyAuthoring.Create("Lámpara invertida", FamilyCategory.LightingFixtures);
        FamilyAuthoring.AddRevolution(reversed, "Cuerpo", profile.AsEnumerable().Reverse().ToList());
        check(FamilyGeometry.SignedVolume(FamilyGeometry.Tessellate(reversed)) > 0, "Families: a profile drawn the other way round still faces outward");

        var parsed = FamilyAuthoring.ParseOutline("0 0; 0,3 0; 0.3 0,05; 0 0.05");
        check(parsed.Count == 4 && Math.Abs(parsed[2].X - 0.3) < 1e-12 && Math.Abs(parsed[2].Y - 0.05) < 1e-12,
            "Families: an outline typed with decimal commas or points is read the same");
        var shelf = FamilyAuthoring.Create("Repisa", FamilyCategory.Furniture);
        var board = FamilyAuthoring.AddExtrusion(shelf, "Tabla", parsed, "0.9", "0.05", "Madera");
        check(FamilyGeometry.Tessellate(shelf).TriangleKinds.All(kind => kind == SurfaceKind.WallWood),
            "Families: an extrusion of a drawn outline renders in its material");
        bool Refused(Action action) { try { action(); return false; } catch (InvalidDataException) { return true; } }
        check(Refused(() => FamilyAuthoring.SetMaterial(shelf, board, "Oro macizo")) &&
              Refused(() => FamilyAuthoring.AddRevolution(shelf, "Mal", [new(-0.1, 0), new(0.2, 0), new(0.2, 0.3)])) &&
              Refused(() => FamilyAuthoring.ParseOutline("0 0; 1")),
            "Families: unknown materials, profiles crossing the axis and bad outlines are refused");
    }
}
