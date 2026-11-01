using System.Numerics;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;

/// <summary>Exports and renders the shared reference house without maintaining a competing architectural recipe.</summary>
internal static class Program
{
    /// <summary>Accepts an output folder and the desktop resource folder, preserving embedded detailed families in the native project.</summary>
    public static void Main(string[] arguments)
    {
        if (arguments.Length != 2) throw new ArgumentException("Provide output and desktop resource directories.");
        var output = Path.GetFullPath(arguments[0]);
        Directory.CreateDirectory(output);
        var families = Directory.GetFiles(Path.Combine(arguments[1], "families"), "*.g3df")
            .Select(NativeDocumentStore.LoadFamily).ToArray();
        var document = BreezeBlockHouseShowcase.Create(families);
        // The shared camera targets a landscape viewport; a portrait crop needs a wider field to retain both facade ends.
        document.RenderSetup = document.RenderSetup! with { FieldOfViewDegrees = 56, Width = 735, Height = 910 };
        document.Validate();
        var path = Path.Combine(output, "courtyard-house" + NativeDocumentStore.ProjectExtension);
        NativeDocumentStore.SaveProject(path, document);
        var reloaded = NativeDocumentStore.LoadProject(path);
        reloaded.Validate();
        if (reloaded.ComponentFamilies.Count != document.ComponentFamilies.Count || reloaded.Rooms.Count != document.Rooms.Count)
            throw new InvalidDataException("Native project round trip changed families or rooms.");
        Console.WriteLine($"Reloaded {reloaded.Rooms.Count} rooms and {reloaded.ComponentFamilies.Count} embedded families.");
        var setup = reloaded.RenderSetup!;
        var navigation = new NavigationCamera
        {
            Position = new Vector3((float)setup.CameraX, (float)setup.CameraY, (float)setup.CameraZ),
            YawDegrees = (float)setup.YawDegrees, PitchDegrees = (float)setup.PitchDegrees,
            FieldOfViewDegrees = (float)setup.FieldOfViewDegrees
        };
        var settings = RenderPresets.Lighting(setup.Scenario, setup.Latitude, setup.DayOfYear, setup.SolarHour, RenderQuality.High)
            with { CloudCover = (float)setup.CloudCover, Clouds = setup.Clouds, AerialPerspective = setup.Haze ? 1 : 0 };
        var mesh = BuildingMesher.Build(reloaded, includeGround: true, plants: PlantDetail.Render);
        var scene = new RenderSceneBuilder().AddBuilding(mesh).Build();
        var camera = navigation.ToRenderCamera(20) with { CorrectVerticals = setup.CorrectVerticals };
        const int width = 735, height = 910;
        var tracer = new PathTracer(scene, width, height, camera, settings);
        for (var pass = 0; pass < 48; pass++)
        {
            tracer.RenderPass();
            if ((pass + 1) % 8 == 0) Console.WriteLine($"Render pass {pass + 1}/48.");
        }
        PngCodec.Save(Path.Combine(output, "courtyard-house.png"), RasterImage.FromBgra32(width, height,
            tracer.ToBgra32(true, new ImageFinish { AutoExposure = setup.AutoExposure, ToneMapper = setup.Look })));
        Console.WriteLine($"Saved {scene.TriangleCount} rendered triangles to {output}.");
    }
}
