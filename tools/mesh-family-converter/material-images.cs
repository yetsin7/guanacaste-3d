using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Rendering;

/// <summary>
/// Reads the base colour image of each material of a glTF model and, for cut-out materials (alpha mode MASK or BLEND),
/// the coverage: from the image's own alpha channel, or from the separate alpha map an asset library ships beside it
/// (<c>*_diff_*</c> → <c>*_alpha_*.png</c>). Color and normal maps use the requested texture resolution.
/// </summary>
internal static class MaterialImages
{
    /// <summary>Largest image side kept (--texture-size); 512 suits small plants, 1024 trunks and close-up foliage.</summary>
    public static int MaximumSide { get; set; } = 512;

    /// <summary>Colour saturation per material name pattern (--saturation bark=0.6), for images too vivid in sunlight.</summary>
    public static List<(string Pattern, float Saturation)> Saturation { get; } = [];

    /// <summary>The images by material index of the imported model; materials without an image are left out.</summary>
    public static Dictionary<int, FamilyMeshTexture> Read(string gltfPath, IReadOnlyList<RenderMaterial> materials)
    {
        var result = new Dictionary<int, FamilyMeshTexture>();
        if (!gltfPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)) return result;
        using var document = JsonDocument.Parse(File.ReadAllText(gltfPath));
        var root = document.RootElement;
        var folder = Path.GetDirectoryName(Path.GetFullPath(gltfPath))!;
        string? Uri(JsonElement reference)
        {
            var texture = root.GetProperty("textures")[reference.GetProperty("index").GetInt32()];
            var image = root.GetProperty("images")[texture.GetProperty("source").GetInt32()];
            return image.TryGetProperty("uri", out var uri) ? Path.Combine(folder, System.Uri.UnescapeDataString(uri.GetString()!)) : null;
        }
        string? ImageOf(JsonElement material) =>
            material.TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty("baseColorTexture", out var reference)
                ? Uri(reference) : null;
        string? NormalOf(JsonElement material) =>
            material.TryGetProperty("normalTexture", out var reference) ? Uri(reference) : null;
        var byName = root.TryGetProperty("materials", out var list)
            ? list.EnumerateArray().Where(material => material.TryGetProperty("name", out _))
                .GroupBy(material => material.GetProperty("name").GetString()!).ToDictionary(group => group.Key, group => group.First())
            : [];
        for (var index = 0; index < materials.Count; index++)
        {
            if (!byName.TryGetValue(materials[index].Name, out var material) || ImageOf(material) is not { } path || !File.Exists(path)) continue;
            var mode = material.TryGetProperty("alphaMode", out var alphaMode) ? alphaMode.GetString() : "OPAQUE";
            var cutout = mode is "MASK" or "BLEND";
            var cutoff = material.TryGetProperty("alphaCutoff", out var value) ? value.GetSingle() : 0.5f;
            var (width, height, rgba) = Decode(path);
            if (Saturation.FirstOrDefault(rule => materials[index].Name.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)) is
                { Pattern: not null } grade)
                for (var pixel = 0; pixel < width * height; pixel++)
                {
                    var (r, g, b) = (rgba[pixel * 4], rgba[pixel * 4 + 1], rgba[pixel * 4 + 2]);
                    var grey = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                    byte Mix(byte channel) => (byte)Math.Clamp(grey + (channel - grey) * grade.Saturation, 0, 255);
                    (rgba[pixel * 4], rgba[pixel * 4 + 1], rgba[pixel * 4 + 2]) = (Mix(r), Mix(g), Mix(b));
                }
            if (cutout)
            {
                var name = Path.GetFileName(path);
                var mask = Path.Combine(Path.GetDirectoryName(path)!, Path.ChangeExtension(name.Replace("_diff_", "_alpha_"), ".png"));
                if (File.Exists(mask) && mask != path)
                {
                    var (maskWidth, maskHeight, maskPixels) = Decode(mask, width, height);
                    if (maskWidth == width && maskHeight == height)
                        for (var pixel = 0; pixel < width * height; pixel++) rgba[pixel * 4 + 3] = maskPixels[pixel * 4];
                }
            }
            // Keep the normal map at the color map's resolution so close views retain fine bark relief.
            byte[]? normal = null;
            var (normalWidth, normalHeight) = (width, height);
            if (NormalOf(material) is { } normalPath && File.Exists(normalPath))
            {
                var (_, _, pixels) = Decode(normalPath, normalWidth, normalHeight);
                normal = new byte[normalWidth * normalHeight * 3];
                for (var pixel = 0; pixel < normalWidth * normalHeight; pixel++)
                    (normal[pixel * 3], normal[pixel * 3 + 1], normal[pixel * 3 + 2]) = (pixels[pixel * 4], pixels[pixel * 4 + 1], pixels[pixel * 4 + 2]);
            }
            result[index] = FamilyMeshTexture.Create(Path.GetFileNameWithoutExtension(path), width, height, rgba, cutout ? cutoff : 0, normal,
                normal is null ? 0 : normalWidth, normal is null ? 0 : normalHeight);
        }
        return result;
    }

    /// <summary>Decodes an image (JPEG or PNG) to RGBA, scaled to a given size or to at most <see cref="MaximumSide"/> a side.</summary>
    private static (int Width, int Height, byte[] Rgba) Decode(string path, int width = 0, int height = 0)
    {
        BitmapFrame frame;
        try
        {
            using var stream = File.OpenRead(path);
            frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }
        catch (ArgumentException) when (File.Exists(Path.ChangeExtension(path, ".png")))
        {
            // Some JPEGs carry metadata the Windows decoder rejects; a PNG copy beside them is read instead.
            using var stream = File.OpenRead(Path.ChangeExtension(path, ".png"));
            frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }
        if (width == 0)
        {
            var scale = Math.Min(1.0, (double)MaximumSide / Math.Max(frame.PixelWidth, frame.PixelHeight));
            (width, height) = (Math.Max(1, (int)(frame.PixelWidth * scale)), Math.Max(1, (int)(frame.PixelHeight * scale)));
        }
        BitmapSource scaled = new TransformedBitmap(frame, new ScaleTransform((double)width / frame.PixelWidth, (double)height / frame.PixelHeight));
        var bgra = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
        for (var pixel = 0; pixel < pixels.Length; pixel += 4) (pixels[pixel], pixels[pixel + 2]) = (pixels[pixel + 2], pixels[pixel]);
        return (bgra.PixelWidth, bgra.PixelHeight, pixels);
    }
}
