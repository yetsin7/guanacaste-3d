using System.Numerics;
using Guanacaste3D.Core.Rendering;

/// <summary>Checks the picture styles, image effects, cloud types and the material library.</summary>
static class RenderLookChecks
{
    public static void Run(Action<bool, string> check)
    {
        const int width = 48, height = 32;
        var image = new Vector3[width * height];
        for (var index = 0; index < image.Length; index++)
        {
            var (x, y) = (index % width, index / width);
            image[index] = new Vector3(0.05f + 0.4f * x / width, 0.08f + 0.3f * y / height, 0.12f);
        }
        image[16 * width + 24] = new Vector3(60);
        var plain = new ImageFinish { Dither = false, Bloom = 0, Vignette = 0 };
        var neutral = ImageProcessor.ToBgra32(image, width, height, 1, plain);
        check(neutral.SequenceEqual(ImageProcessor.ToBgra32(image, width, height, 1, plain with { ShadowTint = Vector3.One })),
            "Effects left at their defaults leave the picture unchanged");

        foreach (var style in RenderLooks.Styles.Skip(1))
        {
            var styled = ImageProcessor.ToBgra32(image, width, height, 1, style.Apply(plain));
            check(styled.Length == neutral.Length && !styled.SequenceEqual(neutral), $"The {style.Id} style changes the picture");
        }
        check(RenderLooks.Styles.Select(style => style.Id).Distinct().Count() == RenderLooks.Styles.Count &&
              RenderLooks.Skies.All(sky => RenderLooks.Styles.Any(style => style.Id == sky.StyleId)),
            "Styles have unique identifiers and every sky names an existing style");

        var mono = ImageProcessor.ToBgra32(image, width, height, 1, plain with { Monochrome = true });
        check(Enumerable.Range(0, width * height).All(pixel => mono[pixel * 4] == mono[pixel * 4 + 1] && mono[pixel * 4 + 1] == mono[pixel * 4 + 2]),
            "Black and white has equal channels");

        // A star streak lights the row and column of a bright point, farther than bloom reaches.
        var glare = ImageProcessor.ToBgra32(image, width, height, 1, plain with { Glare = 1 });
        int Green(byte[] pixels, int x, int y) => pixels[(y * width + x) * 4 + 1];
        check(Green(glare, 4, 16) > Green(neutral, 4, 16) && Green(glare, 24, 2) > Green(neutral, 24, 2) &&
              Green(glare, 4, 2) <= Green(neutral, 4, 2) + 1,
            "Glare draws streaks along the bright point's row and column only");

        // Fog: a far pixel drifts toward the fog colour, a near one barely changes.
        var features = new FeatureBuffer(width, height);
        for (var index = 0; index < features.Depth.Length; index++) features.Depth[index] = index % width < width / 2 ? 2f : 300f;
        var foggy = ImageProcessor.ToBgra32(image, width, height, 1, plain with { Fog = 1 }, () => features);
        int Blue(byte[] pixels, int x, int y) => pixels[(y * width + x) * 4];
        check(Math.Abs(Blue(foggy, 2, 5) - Blue(neutral, 2, 5)) <= 3 && Blue(foggy, 40, 5) > Blue(neutral, 40, 5) + 20,
            "Fog thickens with distance");

        var sharp = ImageProcessor.ToBgra32(image, width, height, 1, plain with { Sharpen = 1, FilmGrain = 1, ChromaticAberration = 1 });
        check(!sharp.SequenceEqual(neutral) && sharp.Length == neutral.Length, "Sharpening, grain and aberration run together");

        // Clouds: every type covers part of a partly cloudy sky; cirrus is thin, storm clouds are deep.
        foreach (var style in Enum.GetValues<CloudStyle>())
        {
            var layer = new CloudLayer(0.6f, 7, style);
            var samples = Enumerable.Range(0, 400).Select(index =>
            {
                var azimuth = index * 0.37f;
                var elevation = 0.2f + index % 20 * 0.04f;
                return layer.Sample(Vector3.Normalize(new Vector3(MathF.Cos(azimuth), MathF.Sin(azimuth), elevation)));
            }).ToArray();
            var covered = samples.Count(sample => sample.Opacity > 0.05f);
            check(covered is > 10 and < 400, $"{style} clouds cover part of the sky");
            if (style == CloudStyle.Cirrus) check(samples.Max(sample => sample.Thickness) <= 0.2f, "Cirrus stays thin");
            if (style == CloudStyle.Storm) check(samples.Max(sample => sample.Thickness) > 0.9f, "Storm clouds are deep");
        }

        // Materials.
        var presets = MaterialPresets.All;
        check(presets.Count >= 60 && presets.Select(preset => preset.Id).Distinct().Count() == presets.Count &&
              presets.All(preset => preset.Material.Roughness is >= 0 and <= 1 && preset.Material.Metallic is >= 0 and <= 1 &&
                  preset.TextureSize > 0 && Enum.IsDefined(preset.Group)),
            "The material library has 60+ presets with unique ids and physical values");
        check(presets.Count >= 120 && presets.Count(preset => preset.Id.StartsWith("paint-satin-", StringComparison.Ordinal)) >= 12 &&
              presets.Count(preset => preset.Id.StartsWith("fabric-", StringComparison.Ordinal)) >= 12,
            "Expanded library includes 120+ presets and complete finish colour variants");
        check(RenderLooks.Styles.Count >= 20 &&
              RenderLooks.Styles.Select(style => style.Id).Distinct().Count() == RenderLooks.Styles.Count,
            "Architectural filter catalog includes at least twenty uniquely identified looks");
        check(Enum.GetValues<MaterialFamily>().All(group => presets.Any(preset => preset.Group == group)),
            "Every material family has presets");
        var oak = MaterialPresets.FromPath(MaterialPresets.Prefix + "oak-light")!;
        var chrome = MaterialPresets.FromPath(MaterialPresets.Prefix + "chrome")!;
        var texture = RenderTexture.FromRaster(new RasterImage(2, 2, [200, 150, 100, 200, 150, 100, 200, 150, 100, 200, 150, 100]));
        check(MaterialPresets.Build(oak, texture).Texture == texture && MaterialPresets.Build(oak, null).Texture is null &&
              MaterialPresets.Build(chrome, null).Metallic == 1 && MaterialPresets.FromPath("builtin:concrete") is null,
            "Presets resolve with or without their texture and are told apart from plain textures");

        var setup = new RenderSetup { StyleId = "cinematic", Clouds = CloudStyle.Cirrus, Glare = 0.4, Fog = 0.2 };
        setup.Validate();
        var rejected = false;
        try { (setup with { StyleId = "no-such-style" }).Validate(); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "A saved render setup must name an existing style");
    }
}
