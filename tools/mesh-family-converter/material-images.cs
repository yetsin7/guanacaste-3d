using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Rendering;

/// <summary>
/// Reads the base colour image of each material of a glTF model and, for cut-out materials (alpha mode MASK or BLEND),
/// the coverage: from the image's own alpha channel, or from the separate alpha map an asset library ships beside it
/// (<c>*_diff_*</c> → <c>*_alpha_*.png</c>). Images are reduced to 512 texels a side, plenty for leaves and bark.
/// </summary>
internal static class MaterialImages
{
    private const int MaximumSide = 512;

    /// <summary>The images by material index of the imported model; materials without an image are left out.</summary>
    public static Dictionary<int, FamilyMeshTexture> Read(string gltfPath, IReadOnlyList<RenderMaterial> materials)
    {
        var result = new Dictionary<int, FamilyMeshTexture>();
        if (!gltfPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)) return result;
        using var document = JsonDocument.Parse(File.ReadAllText(gltfPath));
        var root = document.RootElement;
        var folder = Path.GetDirectoryName(Path.GetFullPath(gltfPath))!;
        string? ImageOf(JsonElement material)
        {
            if (!material.TryGetProperty("pbrMetallicRoughness", out var pbr) || !pbr.TryGetProperty("baseColorTexture", out var reference))
                return null;
            var texture = root.GetProperty("textures")[reference.GetProperty("index").GetInt32()];
            var image = root.GetProperty("images")[texture.GetProperty("source").GetInt32()];
            return image.TryGetProperty("uri", out var uri) ? Path.Combine(folder, Uri.UnescapeDataString(uri.GetString()!)) : null;
        }
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
            result[index] = FamilyMeshTexture.Create(Path.GetFileNameWithoutExtension(path), width, height, rgba, cutout ? cutoff : 0);
        }
        return result;
    }

    /// <summary>Decodes an image (JPEG or PNG) to RGBA, scaled to a given size or to at most 512 texels a side.</summary>
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
