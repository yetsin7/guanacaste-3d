using Guanacaste3D.Core;

/// <summary>The takeoff buys what a builder buys: formwork and nails, roof waterproofing, timber walls and cobogó apart from glass walls.</summary>
static class BudgetCompletenessChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Budget"));
        var level = session.Document.Levels[0].Id;
        session.AddColumn(level, new(1, 1));
        var beamSource = session.AddStructuralElement(level, StructuralElementKind.Beam, "Viga", new(0, 0), new(4, 0));
        var takeoff = MaterialTakeoff.Build(session.Document);
        check(takeoff.Find(MaterialCatalog.Formwork) is { NetQuantity: > 0 } && takeoff.Find(MaterialCatalog.Nails) is { NetQuantity: > 0 },
            "Budget: concrete columns and beams bring formwork wood and nails");

        session.AddSlabFromPreset(level, [new(0, 3), new(5, 3), new(5, 7), new(0, 7)],
            ArchitecturalLibrary.SlabPresets.First(preset => preset.Kind == FloorKind.Roof && preset.Shape == RoofShape.Flat), 3.0);
        takeoff = MaterialTakeoff.Build(session.Document);
        check(takeoff.Find(MaterialCatalog.Waterproofing) is { } membrane && Math.Abs(membrane.NetQuantity - 20) < 0.01,
            "Budget: a 20 m² flat concrete roof is waterproofed over its whole surface");

        var deck = session.AddSlabFromPreset(level, [new(20, 0), new(24, 0), new(24, 5), new(20, 5)],
            ArchitecturalLibrary.SlabPresets.First(preset => preset.Kind == FloorKind.Floor && preset.Shape == RoofShape.Flat), 3.3);
        takeoff = MaterialTakeoff.Build(session.Document);
        check(takeoff.Find(MaterialCatalog.Waterproofing) is { } deckMembrane && Math.Abs(deckMembrane.NetQuantity - 40) < 0.01,
            "Budget: a floor slab raised 3.30 m (a roof deck or canopy) is waterproofed like a roof");
        session.LoadArchitecturalLibrary();
        var timberType = session.Document.WallTypes.FirstOrDefault(type => type.Name == "Timber Clad Wall 200 mm");
        if (timberType is null)
        {
            check(false, "Budget: the timber wall type exists in the library");
            return;
        }
        var wall = session.AddWall(level, new(0, 10), new(5, 10), height: 3, wallTypeId: timberType.Id);
        takeoff = MaterialTakeoff.Build(session.Document);
        check(takeoff.Find(MaterialCatalog.TimberCladding) is { } timber && Math.Abs(timber.NetQuantity - 15) < 0.01 &&
              takeoff.Lines.All(line => !line.Sources.Any(source => source.Contains("Timber")) || line.Material.Code is MaterialCatalog.TimberCladding or MaterialCatalog.Sealer),
            "Budget: a timber wall is bought as timber cladding, not as plastered block");

        var screen = ArchitecturalLibrary.PresetsFor(StructuralElementKind.CurtainWall).First(preset => preset.Style == ElementStyle.BreezeBlock);
        session.AddElementFromPreset(level, screen, new(0, 12), new(2, 12));
        takeoff = MaterialTakeoff.Build(session.Document);
        var blocks = takeoff.Find(MaterialCatalog.BreezeBlock);
        check(blocks is { NetQuantity: > 0 } && takeoff.Find(MaterialCatalog.CurtainWall) is null,
            "Budget: a breeze block screen is counted in cobogó pieces, not as an aluminium and glass curtain wall");
    }
}
