using Guanacaste3D.Core;

/// <summary>Regression checks for true roof surface area in material takeoffs and budgets.</summary>
static class RoofTakeoffChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Roof quantities"));
        var level = session.Document.Levels[0].Id;
        var preset = ArchitecturalLibrary.SlabPresets.Single(slab => slab.Name == "Gable Roof 30°");
        var id = session.AddSlabFromPreset(level,
            [new(0, 0), new(10, 0), new(10, 6), new(0, 6)], preset, 3.1);
        var roof = session.Document.Floors.Single(floor => floor.Id == id);

        // 10 x 6 m plan with 0.6 m eaves on every side: 11.2 x 7.2 m projected.
        var projected = 11.2 * 7.2;
        var expected = projected / Math.Cos(Math.PI / 6);
        check(Math.Abs(RoofGeometry.SurfaceArea(roof) - expected) < 1e-8 &&
              Math.Abs(FinishRules.FloorFinishArea(roof) - expected) < 1e-8,
            "Roof finish quantities include pitched surface and overhanging eaves");

        check(Math.Abs(RoofGeometry.SurfaceArea(roof with { Overhang = 0 }) -
                       60 / Math.Cos(Math.PI / 6)) < 1e-8,
            "A roof without eaves includes its slope but not extra projected area");

        check(new[] { RoofShape.Shed, RoofShape.Gable, RoofShape.Hip }.All(shape =>
                Math.Abs(RoofGeometry.SurfaceArea(roof with { Shape = shape }) - expected) < 1e-8),
            "Shed, gable and hip roof facets have consistent slope-area quantities");

        check(Math.Abs(RoofGeometry.SurfaceArea(roof with { Shape = RoofShape.Flat, Pitch = 0 }) -
                       projected) < 1e-8 &&
              Math.Abs(FinishRules.FloorFinishArea(roof with
              {
                  Kind = FloorKind.Floor, Shape = RoofShape.Flat, Pitch = 0
              }) - 60) < 1e-8,
            "Flat roof eaves are counted, but regular floors keep their plan area");

        var budget = BudgetCalculator.Build(session.Document, UnitPriceList.Default(), 0);
        var roofLine = budget.Lines.Single(line => line.Code == UnitPriceList.RoofArea);
        check(Math.Abs(roofLine.Quantity - Math.Round(expected, 3)) < 1e-9 &&
              roofLine.Total == Math.Round((decimal)roofLine.Quantity * roofLine.UnitPrice, 2),
            "Roof budgets price the true covered surface rather than the smaller plan area");
    }
}
