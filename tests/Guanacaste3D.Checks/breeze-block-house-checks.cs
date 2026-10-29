using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

/// <summary>Checks the breeze-block house sample and the families and materials it introduces.</summary>
static class BreezeBlockHouseChecks
{
    public static void Run(Action<bool, string> check)
    {
        var document = BreezeBlockHouseShowcase.Create();
        document.Validate();
        var path = Path.Combine(Path.GetTempPath(), $"cobogo-{Guid.NewGuid():N}.g3");
        ProjectFile.Save(path, document);
        ProjectFile.Load(path).Validate();
        File.Delete(path);
        check(true, "Breeze-block house validates and survives a save and reload");

        var screen = document.StructuralElements.Single(e => e.Style == ElementStyle.BreezeBlock);
        var cells = ElementGeometry.Parts(screen).ToArray();
        check(screen.Kind == StructuralElementKind.CurtainWall && cells.Length > 20 && cells.All(part => part.Kind == SurfaceKind.Wall) &&
              cells.All(part => part.Kind != SurfaceKind.Glass),
            "The cobogó is a curtain wall of open masonry webs without glass");

        var cars = document.StructuralElements.Where(e => e.ComponentShape is ResidentialComponentShape.Car or ResidentialComponentShape.SilverCar).ToArray();
        check(cars.Length == 2 && cars.All(car => car.Depth > 4), "Two full-size cars are parked in the carport");
        var carParts = cars.SelectMany(ElementGeometry.Parts).ToArray();
        check(new[] { SurfaceKind.CarPaint, SurfaceKind.CarPaintSilver, SurfaceKind.Rubber, SurfaceKind.Glass }
                .All(kind => carParts.Any(part => part.Kind == kind)) &&
              carParts.All(part => part.Top <= cars[0].Height + 1e-9),
            "Cars have painted bodies, tyres and glass within their family height");

        var door = document.Openings.Single(opening => opening.Kind == OpeningKind.Door);
        check(document.OpeningTypes.Single(type => type.Id == door.OpeningTypeId).FamilyName == "Pivot Door" && door.Height > 2.7,
            "The entrance is a tall timber pivot door");

        var mesh = BuildingMesher.Build(document, includeGround: true);
        foreach (var kind in new[] { SurfaceKind.Asphalt, SurfaceKind.WallConcrete, SurfaceKind.WallWood, SurfaceKind.Ground,
                     SurfaceKind.Glass, SurfaceKind.CarPaint })
            check(mesh.TriangleKinds.Contains(kind), $"The house meshes {kind} surfaces");
        check(MaterialLibrary.ForKind(SurfaceKind.CarPaint).Roughness < 0.15f &&
              MaterialLibrary.ForKind(SurfaceKind.Asphalt).BaseColor.X < 0.2f,
            "Car paint is glossy and asphalt is dark");

        var canopy = document.Floors.Single(f => f.Finish == FinishKind.ExposedConcrete && f.ElevationOffset > 3.5);
        var volume = document.Floors.Where(f => f.Finish == FinishKind.Paint).ToArray();
        check(volume.Length >= 2 && canopy.Boundary.Min(p => p.Y) < volume.Min(f => f.Boundary.Min(p => p.Y)),
            "A fair-faced canopy overhangs the painted white volume toward the street");
        check(BudgetCalculator.Build(document, UnitPriceList.Default()).Total > 0, "The house can be priced");

        // The street view of the photograph travels with the project and is checked like any other data.
        check(document.RenderSetup == BreezeBlockHouseShowcase.StreetView && document.Clone().RenderSetup == document.RenderSetup &&
              ProjectFileRoundTrip(document).RenderSetup == document.RenderSetup,
            "The house keeps its render camera, sun and filters through clone and save");
        var sun = SolarPosition.Direction(document.RenderSetup!.Latitude, document.RenderSetup.DayOfYear, document.RenderSetup.SolarHour);
        check(sun.X < 0 && sun.Y < 0 && SolarPosition.AltitudeDegrees(sun) is > 25 and < 45,
            "The saved sun shines from the front left, so the tree's shadow falls across the white volume");
        var broken = document.Clone();
        broken.RenderSetup = broken.RenderSetup! with { FieldOfViewDegrees = 0 };
        var rejected = false;
        try { broken.Validate(); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "An invalid saved render setup is rejected");
    }

    private static BuildingDocument ProjectFileRoundTrip(BuildingDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"render-setup-{Guid.NewGuid():N}.g3");
        try
        {
            ProjectFile.Save(path, document);
            return ProjectFile.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
