using System.Numerics;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

/// <summary>Regression checks for toposolids, image textures, composite stairs and roof overhangs and gables.</summary>
static class SiteRoofStairChecks
{
    public static void Run(Action<bool, string> check)
    {
        Terrain(check);
        Textures(check);
        Stairs(check);
        Roofs(check);
    }

    private static void Terrain(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Site"));
        var level = session.Document.Levels[0].Id;
        var preset = ArchitecturalLibrary.SlabPresets.Single(slab => slab.Kind == FloorKind.Toposolid);
        var site = session.AddSlabFromPreset(level, [new(0, 0), new(40, 0), new(40, 30), new(0, 30)], preset,
            session.SlabElevation(level, preset));
        session.AddTopoPoint(site, new TopoPoint(20, 15, 4));
        session.AddTopoPoint(site, new TopoPoint(32, 8, -1.5));
        var toposolid = session.Document.Floors.Single(floor => floor.Id == site);
        var surface = new TopoSurface(toposolid);
        check(Math.Abs(surface.ElevationAt(20, 15) - 4) < 1e-6 && Math.Abs(surface.ElevationAt(32, 8) + 1.5) < 1e-6 &&
              Math.Abs(surface.ElevationAt(0, 0) - toposolid.ElevationOffset) < 1e-6,
            "The terrain passes exactly through its elevation points and rests on its corners");
        var mid = surface.ElevationAt(26, 12);
        check(mid < 4 && mid > -1.5, "Between points the terrain is a smooth slope, not a step");

        var mesh = BuildingMesher.Build(session.Document);
        var (min, max) = mesh.Bounds();
        check(max.Z > 3.9f && max.Z < 4.6f && min.Z < -1.5f - toposolid.Thickness + 0.01f &&
              mesh.TriangleKinds.Contains(SurfaceKind.Ground) && mesh.TriangleKinds.Contains(SurfaceKind.Earth),
            "A toposolid meshes as a lawn surface over earth sides down to its thickness below the lowest point");

        var contours = TopographyGeometry.Contours(toposolid, 0.5);
        check(contours.Any(segment => Math.Abs(segment.Elevation - 3.5) < 1e-9) &&
              contours.All(segment => TopographyGeometry.Contains(toposolid.Boundary, segment.Start) ||
                                      TopographyGeometry.Contains(toposolid.Boundary, segment.End)),
            "Contour lines are traced at every half meter inside the site");

        var plant = ArchitecturalLibrary.PresetsFor(StructuralElementKind.Planting).First(preset => preset.Style == ElementStyle.Shrub);
        session.AddElementFromPreset(level, plant, new Point2(20, 15), new Point2(20, 15));
        check(BuildingMesher.TerrainUnder(session.Document, level, new Point2(20, 15)) > 3.9,
            "A planting on the terrain stands on its surface");

        var rejected = false;
        try { session.AddTopoPoint(site, new TopoPoint(60, 60, 2)); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "An elevation point outside the toposolid is rejected");
        check(session.RemoveTopoPoint(site, new Point2(20.05, 15), 0.2) &&
              session.Document.Floors.Single(floor => floor.Id == site).TopoPoints!.Count == 1,
            "The nearest elevation point is removed");
        session.MoveFloor(site, 5, 0);
        check(session.Document.Floors.Single(floor => floor.Id == site).TopoPoints!.Single().X == 37,
            "Moving a toposolid moves its elevation points with it");

        var path = Path.Combine(Path.GetTempPath(), $"site-{Guid.NewGuid():N}.g3d");
        try
        {
            session.SetRenderMaterial(new RenderMaterialAssignment(SurfaceKind.Wall, "builtin:brick-red") { TextureSize = 1, Relief = 0.6 });
            ProjectFile.Save(path, session.Document);
            var reloaded = ProjectFile.Load(path);
            check(reloaded.Floors.Single(floor => floor.IsToposolid).TopoPoints!.Count == 1 &&
                  reloaded.RenderMaterials.Single().TexturePath == "builtin:brick-red",
                "Toposolid points and render materials survive a project round trip");
        }
        finally { File.Delete(path); }
    }

    private static void Textures(Action<bool, string> check)
    {
        // A 4 × 4 checker of black and white texels.
        var bgra = new byte[4 * 4 * 4];
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
            {
                var value = (byte)(((x + y) & 1) == 0 ? 255 : 0);
                var index = (y * 4 + x) * 4;
                bgra[index] = bgra[index + 1] = bgra[index + 2] = value;
                bgra[index + 3] = 255;
            }
        var texture = RenderTexture.FromBgra32(4, 4, bgra);
        var sharp = texture.Sample(0.125f, 0.875f, 1e-4f);
        var far = texture.Sample(0.125f, 0.875f, 10f);
        check(texture.LevelCount == 3 && sharp.X > 0.95f && Math.Abs(far.X - 0.5f) < 0.02f && Math.Abs(texture.Mean.X - 0.5f) < 0.02f,
            "Textures sample sharply up close and average to their mean color far away (mip levels)");
        check(Vector3.Distance(texture.Sample(0.125f, 0.875f, 1e-4f), texture.Sample(1.125f, -0.125f, 1e-4f)) < 1e-5,
            "Textures repeat seamlessly in both directions");

        var mesh = new SceneMesh();
        BenchmarkScenes.AddBox(mesh, new(-2, -0.1f, 0), new(2, 0.1f, 3), SurfaceKind.Wall);
        BenchmarkScenes.AddBox(mesh, new(-20, -20, -0.1f), new(20, 20, 0), SurfaceKind.Ground);
        var camera = new RenderCamera(new Vector3(0, -4, 1.5f), new Vector3(0, 0, 1.5f), 50);
        float Mean(Func<SurfaceKind, RenderMaterial>? materials)
        {
            var tracer = new PathTracer(new RenderSceneBuilder().AddBuilding(mesh, materials).Build(), 48, 32, camera,
                new RenderSettings { Seed = 3, CloudCover = 0 });
            for (var pass = 0; pass < 8; pass++) tracer.RenderPass();
            return tracer.LinearImage(false).Average(pixel => pixel.X - pixel.Z);
        }
        var red = new byte[2 * 2 * 4];
        for (var index = 0; index < 4; index++) { red[index * 4 + 2] = 200; red[index * 4 + 1] = 40; red[index * 4] = 30; red[index * 4 + 3] = 255; }
        var redTexture = RenderTexture.FromBgra32(2, 2, red);
        var plain = Mean(null);
        var textured = Mean(kind => kind == SurfaceKind.Wall ? MaterialLibrary.Textured(kind, redTexture, 1, 0) : MaterialLibrary.ForKind(kind));
        check(textured > plain + 0.02f, "A wall with a red image texture renders red");
    }

    private static void Stairs(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Stairs"));
        var level = session.Document.Levels[0].Id;
        var presets = ArchitecturalLibrary.PresetsFor(StructuralElementKind.Stair).ToArray();
        check(new[] { ElementStyle.LShaped, ElementStyle.UShaped, ElementStyle.Spiral }.All(style => presets.Any(preset => preset.Style == style)),
            "The stair catalog offers L, U and spiral stairs");
        foreach (var preset in presets)
        {
            var id = session.AddElementFromPreset(level, preset, new Point2(0, 0), new Point2(4, 0));
            var stair = session.Document.StructuralElements.Single(element => element.Id == id);
            var parts = ElementGeometry.Parts(stair).ToArray();
            var top = parts.Max(part => part.Top);
            var steps = StairGeometry.RiserCount(stair.Height);
            check(parts.Length >= steps && top >= stair.Height - 1e-6 && stair.Height / steps <= StairGeometry.MaximumRiser + 1e-9,
                $"A {preset.Style} stair climbs its full height with risers of at most 17.5 cm");
            var railings = session.AddRailingsOnHost(id);
            check(railings >= 2, $"A {preset.Style} stair receives its railings");
            check(StairGeometry.WalkLine(stair).Count >= 2, $"A {preset.Style} stair has a walking line for its plan arrow");
            session.RemoveStructuralElement(id);
        }
        var lShaped = session.AddElementFromPreset(level, presets.Single(preset => preset.Style == ElementStyle.LShaped),
            new(0, 0), new(4, 0));
        var flights = StairGeometry.Flights(session.Document.StructuralElements.Single(element => element.Id == lShaped));
        check(flights.Count == 2 && Math.Abs(flights[0].Rise + flights[1].Rise - 3) < 1e-9 &&
              Math.Abs(Vector2.Dot(Direction(flights[0]), Direction(flights[1]))) < 1e-9,
            "An L stair splits its rise between two flights at right angles");
    }

    private static Vector2 Direction(StairFlight flight) =>
        Vector2.Normalize(new Vector2((float)(flight.End.X - flight.Start.X), (float)(flight.End.Y - flight.Start.Y)));

    private static void Roofs(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Roof"));
        var level = session.Document.Levels[0].Id;
        var gable = ArchitecturalLibrary.SlabPresets.Single(slab => slab.Name == "Gable Roof 30°");
        var id = session.AddSlabFromPreset(level, [new(0, 0), new(10, 0), new(10, 6), new(0, 6)], gable, 3.1);
        var roof = session.Document.Floors.Single(floor => floor.Id == id);
        var built = RoofGeometry.WithOverhang(roof);
        var (minX, maxX, _, _) = RoofGeometry.Extent(built.Boundary);
        check(Math.Abs(roof.Overhang - 0.6) < 1e-9 && Math.Abs(minX + 0.6) < 1e-9 && Math.Abs(maxX - 10.6) < 1e-9,
            "A pitched roof reaches past its boundary by its overhang");
        var original = roof.ElevationOffset + RoofGeometry.RiseAt(roof, new Point2(5, 3));
        var grown = built.ElevationOffset + RoofGeometry.RiseAt(built, new Point2(5, 3));
        check(Math.Abs(original - grown) < 1e-9 && built.ElevationOffset < roof.ElevationOffset,
            "The overhang keeps the roof plane and ridge where they were and drops the eave");

        var mesh = BuildingMesher.Build(session.Document);
        check(mesh.TriangleKinds.Contains(SurfaceKind.Roof), "Pitched roofs are surfaced with roofing, not floor finish");
        var gableWall = mesh.TriangleKinds.Select((kind, index) => (kind, index)).Where(item => item.kind == SurfaceKind.Wall)
            .SelectMany(item => Enumerable.Range(0, 3).Select(corner => mesh.Positions[mesh.Indices[item.index * 3 + corner]]))
            .DefaultIfEmpty(Vector3.Zero).Max(position => position.Z);
        check(gableWall > 3.1f + 1.5f, "The gables between wall tops and the roof are closed with wall");
        session.SetRoofEdges(id, 0, false);
        check(!BuildingMesher.Build(session.Document).TriangleKinds.Contains(SurfaceKind.Wall),
            "A roof can leave its gables open and drop its overhang");
    }
}
