using Guanacaste3D.Core;

/// <summary>Material takeoff and construction budget: model quantities become a priced purchase list.</summary>
static class MaterialTakeoffChecks
{
    public static void Run(Action<bool, string> check)
    {
        static bool Near(double a, double b, double tolerance = 1e-3) => Math.Abs(a - b) <= tolerance;

        // A 10 m rendered block wall, 0.20 m thick and 2.50 m high: 25 m² of 20 cm block.
        var session = new DocumentSession(BuildingDocument.Create("Takeoff"));
        var level = session.Document.Levels.OrderBy(l => l.Elevation).First().Id;
        var wallId = session.AddWall(level, new(0, 0), new(10, 0), 0.20, 2.50, BuiltInWallTypes.Basic200Id);
        var takeoff = MaterialTakeoff.Build(session.Document);
        var blocks = takeoff.Find(MaterialCatalog.Block20);
        check(blocks is not null && Near(blocks.NetQuantity, 312.5),
            "Takeoff: a 25 m² wall of 20 cm block needs 12.5 blocks per m²");
        check(blocks is not null && blocks.PurchaseQuantity >= 312.5 * 1.05 && blocks.PurchaseQuantity == Math.Floor(blocks.PurchaseQuantity),
            "Takeoff: blocks are bought whole, with the waste added");
        check(takeoff.Find(MaterialCatalog.Rebar3) is { NetQuantity: > 13 and < 14 },
            "Takeoff: block walls carry #3 bars every 60 cm both ways");
        check(takeoff.Find(MaterialCatalog.Paint) is not null && takeoff.Find(MaterialCatalog.FinePlaster) is not null &&
              takeoff.Assumptions.Count == 1,
            "Takeoff: a rendered wall without modelled layers gets repello, fino and paint, and says it assumed them");

        // A door takes area out of the wall and adds the door, three hinges and a lock.
        session.AddOpening(wallId, OpeningKind.Door, 5, 0.90, 0, 2.10);
        var withDoor = MaterialTakeoff.Build(session.Document);
        check(Near(withDoor.Find(MaterialCatalog.Block20)!.NetQuantity, (25 - 0.90 * 2.10) * 12.5),
            "Takeoff: openings are deducted from the wall area");
        check(withDoor.Find(MaterialCatalog.Door)?.NetQuantity == 1 && withDoor.Find(MaterialCatalog.Hinge)?.NetQuantity == 3 &&
              withDoor.Find(MaterialCatalog.Lockset)?.NetQuantity == 1,
            "Takeoff: each door adds its leaf, three hinges and a lockset");

        // A 5 × 4 m ground slab, 10 cm thick: 2 m³ of concrete on a 10 cm gravel base.
        var slabSession = new DocumentSession(BuildingDocument.Create("Slab"));
        var ground = slabSession.Document.Levels.OrderBy(l => l.Elevation).First().Id;
        slabSession.AddFloor(ground, [new(0, 0), new(5, 0), new(5, 4), new(0, 4)], 0.10);
        var slab = MaterialTakeoff.Build(slabSession.Document);
        check(Near(slab.Find(MaterialCatalog.Cement)!.NetQuantity, 17) && Near(slab.Find(MaterialCatalog.Sand)!.NetQuantity, 1.1) &&
              Near(slab.Find(MaterialCatalog.Stone)!.NetQuantity, 1.5),
            "Takeoff: concrete 210 is split into cement bags, sand and stone");
        check(Near(slab.Find(MaterialCatalog.BaseGravel)!.NetQuantity, 2) && slab.Find(MaterialCatalog.Mesh)!.PurchaseQuantity == 2,
            "Takeoff: a ground slab gets a gravel base and whole sheets of welded mesh");

        // A gypsum partition 4 m long, 2.44 m high, studs at 16": 11 studs, four framing screws each.
        var drywall = new DocumentSession(BuildingDocument.Create("Drywall"));
        var dl = drywall.Document.Levels.OrderBy(l => l.Elevation).First().Id;
        var typeIndex = drywall.Document.WallTypes.FindIndex(t => t.Id == BuiltInWallTypes.Interior100Id);
        drywall.Document.WallTypes[typeIndex] = drywall.Document.WallTypes[typeIndex] with { Construction = WallConstruction.Gypsum };
        drywall.AddWall(dl, new(0, 0), new(4, 0), 0.10, 2.44, BuiltInWallTypes.Interior100Id);
        var dry = MaterialTakeoff.Build(drywall.Document);
        check(dry.Find(MaterialCatalog.Stud)?.NetQuantity == 11 && dry.Find(MaterialCatalog.FramingScrew)?.NetQuantity == 44,
            "Takeoff: drywall studs follow the stud spacing, with framing screws per stud");
        check(Near(dry.Find(MaterialCatalog.DrywallScrew)!.NetQuantity, 2 * 4 * 2.44 * 30) && dry.Find(MaterialCatalog.Block20) is null,
            "Takeoff: drywall counts board screws on both faces and no masonry");

        // The budget prices the purchase quantities and puts labor and contingency on top.
        var settings = BudgetSettings.Default.WithPrice(MaterialCatalog.Block20, 1000m);
        var budget = ConstructionBudget.Build(session.Document, settings);
        var blockLine = budget.Lines.Single(line => line.Takeoff.Code == MaterialCatalog.Block20);
        check(blockLine.Total == (decimal)blockLine.Takeoff.PurchaseQuantity * 1000m,
            "Budget: a line costs its purchase quantity times its unit price");
        check(budget.Materials == budget.Lines.Sum(line => line.Total) && budget.Labor == Math.Round(budget.Materials * 0.40m, 2) &&
              budget.Total == budget.Materials + budget.Labor + budget.Contingency,
            "Budget: labor is a rate of materials and the total adds labor and contingency");
        check(budget.ToCsv().Contains("TOTAL_CRC", StringComparison.Ordinal) && takeoff.ToCsv().Split('\n').Length > 5,
            "Budget: takeoff and budget export to CSV");

        // Settings are saved in the project and come back with undo.
        session.SetBudgetSettings(settings with { LaborRate = 0.5m });
        check(session.GetBudgetSettings().LaborRate == 0.5m && session.GetBudgetSettings().PriceOf(MaterialCatalog.Block20) == 1000m,
            "Budget: prices and rates are saved in the project");
        session.Undo();
        check(session.GetBudgetSettings().LaborRate == BudgetSettings.Default.LaborRate,
            "Budget: changing the settings is one undoable step");
        check(BudgetSettings.FromJson("{\"Currency\":\"CRC\",\"Prices\":{\"NOT_A_CODE\":5},\"LaborRate\":0.3,\"ContingencyRate\":0.1}")
                .PriceOf(MaterialCatalog.Cement) == BudgetSettings.CostaRicaReferencePrices[MaterialCatalog.Cement],
            "Budget: unknown saved codes are ignored and missing ones take the reference price");
        var sampleHouse = ConstructionBudget.Build(TwoStoreyHouse.Create(), BudgetSettings.Default);
        check(sampleHouse.Total > 0 && sampleHouse.ByCategory().Count >= 4,
            "Budget: the two-storey sample house prices at least four trades");

        // Stairs, guards, electrical devices and fixtures are bought too: each has its own trade and a price.
        var fitted = new DocumentSession(BuildingDocument.Create("Fitted"));
        var floor0 = fitted.Document.Levels.OrderBy(l => l.Elevation).First().Id;
        fitted.Document.StructuralElements.Add(new StructuralElement(Guid.NewGuid(), floor0, StructuralElementKind.Stair, "Escalera", new(0, 0), new(4, 0), 1.2, 3.0, 0));
        fitted.Document.StructuralElements.Add(new StructuralElement(Guid.NewGuid(), floor0, StructuralElementKind.Railing, "Baranda", new(0, 0), new(5, 0), 0.05, 1.0, 0));
        fitted.AddElectricalDevice(floor0, ElectricalDeviceKind.Outlet, "Tomacorriente", new(1, 1));
        fitted.AddPlumbingPoint(floor0, PlumbingPointKind.WaterMeter, "Medidor", new(0, 0));
        var fittedBudget = ConstructionBudget.Build(fitted.Document, BudgetSettings.Default);
        check(Near(fittedBudget.Takeoff.Find(MaterialCatalog.Stair)?.NetQuantity ?? 0, 4.8) &&
              Near(fittedBudget.Takeoff.Find(MaterialCatalog.Guard)?.NetQuantity ?? 0, 5) &&
              fittedBudget.Takeoff.Find(MaterialCatalog.Outlet)?.NetQuantity == 1 &&
              fittedBudget.Takeoff.Find(MaterialCatalog.Conductor) is not null &&
              fittedBudget.Takeoff.Find(MaterialCatalog.NetworkPoint)?.NetQuantity == 1 &&
              fittedBudget.Lines.All(line => line.Total > 0),
            "Budget: stairs, guards, electrical devices and plumbing points are counted and priced");
    }
}
