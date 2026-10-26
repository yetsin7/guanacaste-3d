using System.Numerics;
using Guanacaste3D.Core.Rendering;

/// <summary>Renders the demonstration house and the daylit room at presentation size, for judging the look of the renderer.</summary>
static class ShowcaseShot
{
    public static void Run(string folder, int width, int passes)
    {
        Directory.CreateDirectory(folder);
        var height = width * 9 / 16;
        var house = BenchmarkScenes.HouseExterior();
        var (min, max) = Guanacaste3D.Core.Geometry.BuildingMesher.Build(Guanacaste3D.Core.SampleProjects.CreateDemonstrationHouse()).Bounds();
        var eyeLevel = house with
        {
            Name = "house-eye-level",
            Camera = new RenderCamera(new Vector3(min.X - 6, min.Y - 9, 1.6f), new Vector3((min.X + max.X) / 2, (min.Y + max.Y) / 2, 1.4f), 55)
            {
                CorrectVerticals = true
            }
        };
        var garden = GardenScene(min, max);
        var scenes = Environment.GetEnvironmentVariable("G3_SHOT_ONLY") is { } only
            ? new[] { house, eyeLevel, garden, BenchmarkScenes.DaylitRoom() }.Where(scene => scene.Name == only).ToArray()
            : new[] { house, eyeLevel, garden, BenchmarkScenes.DaylitRoom() };
        foreach (var scene in scenes)
        {
            var builder = new RenderSceneBuilder().AddBuilding(scene.Mesh, scene.Materials);
            foreach (var light in scene.Lights) builder.AddLight(light);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var tracer = new PathTracer(builder.Build(), width, height, scene.Camera, scene.Settings);
            for (var i = 0; i < passes; i++) tracer.RenderPass();
            PngCodec.Save(Path.Combine(folder, $"shot-{scene.Name}.png"), RasterImage.FromBgra32(width, height, tracer.ToBgra32(true, new ImageFinish { AutoExposure = true })));
            PngCodec.Save(Path.Combine(folder, $"shot-{scene.Name}-aces.png"), RasterImage.FromBgra32(width, height,
                tracer.ToBgra32(true, new ImageFinish { AutoExposure = true, ToneMapper = ToneMapper.Aces })));
            Console.WriteLine($"{scene.Name}: {passes} passes in {watch.Elapsed.TotalSeconds:0.0} s");
        }
    }

    /// <summary>The demonstration house with one tree of every species around it, seen from the garden.</summary>
    private static BenchmarkScene GardenScene(Vector3 min, Vector3 max)
    {
        var session = new Guanacaste3D.Core.DocumentSession(Guanacaste3D.Core.SampleProjects.CreateDemonstrationHouse());
        var level = session.Document.Levels[0].Id;
        var species = Guanacaste3D.Core.ArchitecturalLibrary.PresetsFor(Guanacaste3D.Core.StructuralElementKind.Planting).ToArray();
        var spots = new (int Species, double X, double Y)[]
        {
            (1, min.X - 13, max.Y + 6), (0, max.X + 4, min.Y - 3), (2, min.X - 3, min.Y - 4), (2, min.X - 6, min.Y - 1),
            (3, max.X + 3, max.Y + 3), (3, max.X + 6, max.Y - 1), (4, min.X + 2, min.Y - 2.5), (4, min.X + 5, min.Y - 2.2),
            (4, max.X - 2, min.Y - 2.4), (0, max.X + 14, max.Y + 12)
        };
        foreach (var (index, x, y) in spots)
            session.AddElementFromPreset(level, species[index], new Guanacaste3D.Core.Point2(x, y), new Guanacaste3D.Core.Point2(x, y));
        if (Environment.GetEnvironmentVariable("G3_SHOT_TERRAIN") == "1")
        {
            var preset = Guanacaste3D.Core.ArchitecturalLibrary.SlabPresets.Single(slab => slab.Kind == Guanacaste3D.Core.FloorKind.Toposolid);
            var site = session.AddSlabFromPreset(level, [new(min.X - 40, min.Y - 30), new(max.X + 40, min.Y - 30),
                new(max.X + 40, max.Y + 45), new(min.X - 40, max.Y + 45)], preset, -0.05);
            foreach (var (x, y, z) in new[] { (min.X - 22.0, max.Y + 14, 4.0), (max.X + 25, max.Y + 30, 9.0), (max.X + 20, min.Y - 18, -2.5),
                         (min.X - 30, min.Y - 20, 1.5), (min.X - 1.0, min.Y - 1, -0.05), (max.X + 1, max.Y + 1, -0.05),
                         (min.X - 1.0, max.Y + 1, -0.05), (max.X + 1, min.Y - 1, -0.05) })
                session.AddTopoPoint(site, new Guanacaste3D.Core.Geometry.TopoPoint(x, y, z));
        }
        var mesh = Guanacaste3D.Core.Geometry.BuildingMesher.Build(session.Document, includeGround: true);
        var center = (min + max) / 2;
        if (Environment.GetEnvironmentVariable("G3_SHOT_TEXTURES") is { } textureFolder)
        {
            RenderTexture Load(string name) => RenderTexture.FromRaster(PngCodec.Load(Path.Combine(textureFolder, name + ".png")));
            var materials = new Dictionary<Guanacaste3D.Core.Geometry.SurfaceKind, RenderMaterial>
            {
                [Guanacaste3D.Core.Geometry.SurfaceKind.Wall] = MaterialLibrary.Textured(Guanacaste3D.Core.Geometry.SurfaceKind.Wall, Load("brick-red"), 1.0, 0.6),
                [Guanacaste3D.Core.Geometry.SurfaceKind.Floor] = MaterialLibrary.Textured(Guanacaste3D.Core.Geometry.SurfaceKind.Floor, Load("stone-paving"), 2.0, 0.6),
                [Guanacaste3D.Core.Geometry.SurfaceKind.DoorLeaf] = MaterialLibrary.Textured(Guanacaste3D.Core.Geometry.SurfaceKind.DoorLeaf, Load("wood-floor"), 1.0, 0.25),
            };
            return new BenchmarkScene("garden", mesh,
                new RenderCamera(new Vector3(center.X - 4, min.Y - 9, 1.9f), new Vector3(center.X - 1, center.Y, 1.6f), 62)
                {
                    CorrectVerticals = true
                }, new RenderSettings()) { Materials = kind => materials.TryGetValue(kind, out var material) ? material : MaterialLibrary.ForKind(kind) };
        }
        return new BenchmarkScene("garden", mesh,
            new RenderCamera(new Vector3(center.X - 4, min.Y - 17, 2.2f), new Vector3(center.X - 2, center.Y + 2, 3.2f), 62)
            {
                CorrectVerticals = true
            }, new RenderSettings());
    }
}
