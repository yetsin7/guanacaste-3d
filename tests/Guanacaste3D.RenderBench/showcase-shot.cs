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
        foreach (var scene in new[] { house, eyeLevel, BenchmarkScenes.DaylitRoom() })
        {
            var builder = new RenderSceneBuilder().AddBuilding(scene.Mesh);
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
}
