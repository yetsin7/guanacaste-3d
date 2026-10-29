using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop;

/// <summary>
/// The render textures the application ships, including original procedural and licensed photographic images, and
/// the loading of any image the user picks. Decoded textures are cached by file and modification time, so rebuilding
/// the scene after an edit does not decode them again.
/// </summary>
internal static class RenderTextureLibrary
{
    /// <summary>A shipped texture: its file key, label resource, real size of one tile in meters and suggested relief.</summary>
    public sealed record BuiltInTexture(string Key, string LabelKey, double Size, double Relief, string Extension = ".png");

    /// <summary>Paths of shipped textures are stored as this prefix plus the key, so projects stay portable.</summary>
    public const string BuiltInPrefix = "builtin:";

    public static IReadOnlyList<BuiltInTexture> BuiltIn { get; } =
    [
        new("brick-red", "Texture.Brick", 1.0, 0.6),
        new("wood-floor", "Texture.WoodFloor", 1.0, 0.25),
        new("stone-paving", "Texture.StonePaving", 2.0, 0.6),
        new("concrete", "Texture.Concrete", 1.2, 0.15),
        new("stucco-white", "Texture.Stucco", 1.0, 0.35),
        new("gravel", "Texture.Gravel", 1.0, 0.8),
        new("ambientcg-wood096", "Texture.AmbientWood096", 0.5, 0.15, ".jpg"),
        new("ambientcg-wood092", "Texture.AmbientWood092", 0.8, 0.15, ".jpg"),
        new("ambientcg-wood051", "Texture.AmbientWood051", 0.8, 0.15, ".jpg"),
        new("ambientcg-bricks105", "Texture.AmbientBricks105", 2.4, 0.3, ".jpg"),
        new("ambientcg-bricks113a", "Texture.AmbientBricks113A", 1.0, 0.3, ".jpg"),
        new("ambientcg-concrete046", "Texture.AmbientConcrete046", 2.4, 0.12, ".jpg"),
        new("ambientcg-concrete034", "Texture.AmbientConcrete034", 1.1, 0.12, ".jpg"),
        new("ambientcg-tiles141", "Texture.AmbientTiles141", 2.0, 0.2, ".jpg"),
        new("ambientcg-tiles107", "Texture.AmbientTiles107", 1.0, 0.2, ".jpg"),
        new("ambientcg-marble012", "Texture.AmbientMarble012", 1.0, 0.05, ".jpg"),
        new("ambientcg-plaster001", "Texture.AmbientPlaster001", 1.0, 0.1, ".jpg"),
        new("ambientcg-ground108", "Texture.AmbientGround108", 1.5, 0.25, ".jpg"),
        new("ambientcg-fabric085", "Texture.AmbientFabric085", 0.3, 0.1, ".jpg")
    ];

    /// <summary>The categories whose render material a project may replace, with their label resources.</summary>
    public static IReadOnlyList<(SurfaceKind Category, string LabelKey)> Categories { get; } =
    [
        (SurfaceKind.Wall, "Material.Walls"),
        (SurfaceKind.InteriorWall, "Material.InteriorWalls"),
        (SurfaceKind.Floor, "Material.Floors"),
        (SurfaceKind.Roof, "Material.Roof"),
        (SurfaceKind.Ground, "Material.Ground"),
        (SurfaceKind.Structure, "Material.Structure"),
        (SurfaceKind.DoorLeaf, "Material.Wood"),
        (SurfaceKind.Upholstery, "Material.Upholstery"),
        (SurfaceKind.Earth, "Material.Earth"),
        (SurfaceKind.WallConcrete, "Material.WallConcrete"),
        (SurfaceKind.WallWood, "Material.WallWood"),
        (SurfaceKind.WallBrick, "Material.WallBrick"),
        (SurfaceKind.WallStone, "Material.WallStone"),
        (SurfaceKind.Glass, "Material.Glass"),
        (SurfaceKind.OpeningFrame, "Material.Frames"),
        (SurfaceKind.Metal, "Material.Metal"),
        (SurfaceKind.Water, "Material.Water"),
        (SurfaceKind.Asphalt, "Material.Asphalt"),
        (SurfaceKind.Bedding, "Material.Bedding"),
        (SurfaceKind.Ceramic, "Material.Ceramic"),
        (SurfaceKind.CarPaint, "Material.CarPaint")
    ];

    private static readonly ConcurrentDictionary<string, (DateTime Stamp, RenderTexture Texture)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Full path of a stored texture path: a shipped texture, an absolute path, or one relative to the project.</summary>
    public static string? Resolve(string texturePath, string? projectFile)
    {
        if (texturePath.StartsWith(BuiltInPrefix, StringComparison.Ordinal))
        {
            var key = texturePath[BuiltInPrefix.Length..];
            var builtIn = BuiltIn.FirstOrDefault(texture => texture.Key == key);
            return builtIn is null ? null : Path.Combine(AppContext.BaseDirectory, "resources", "textures", key + builtIn.Extension);
        }
        if (Path.IsPathRooted(texturePath)) return texturePath;
        var folder = projectFile is null ? null : Path.GetDirectoryName(Path.GetFullPath(projectFile));
        return folder is null ? null : Path.GetFullPath(Path.Combine(folder, texturePath));
    }

    /// <summary>
    /// Decodes an image (PNG, JPEG, BMP, TIFF, GIF) into a render texture, or returns null when it is missing or
    /// unreadable; the scene then keeps the stock material rather than failing. Safe on worker threads.
    /// </summary>
    public static RenderTexture? Load(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (Cache.TryGetValue(path, out var cached) && cached.Stamp == stamp) return cached.Texture;
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource frame = decoder.Frames[0];
            // Very large photographs are reduced while decoding; the renderer keeps at most 2048 texels a side anyway.
            if (frame.PixelWidth > 4096 || frame.PixelHeight > 4096)
            {
                var factor = 4096.0 / Math.Max(frame.PixelWidth, frame.PixelHeight);
                frame = new TransformedBitmap(frame, new ScaleTransform(factor, factor));
            }
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            var texture = RenderTexture.FromBgra32(converted.PixelWidth, converted.PixelHeight, pixels);
            Cache[path] = (stamp, texture);
            return texture;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException
                                              or FileFormatException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static readonly ConcurrentDictionary<string, (DateTime Stamp, BitmapSource Image)> Previews = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The images of the project's assignments, reduced to 1024 pixels, for the interactive 3D view. Missing or
    /// unreadable images are left out, so those surfaces keep their plain color.
    /// </summary>
    public static IReadOnlyDictionary<SurfaceKind, (ImageSource Image, double Size)> Previews3D(
        IReadOnlyList<RenderMaterialAssignment> assignments, string? projectFile)
    {
        var result = new Dictionary<SurfaceKind, (ImageSource, double)>();
        foreach (var assignment in assignments)
        {
            var path = Resolve(assignment.TexturePath, projectFile);
            if (path is null || !File.Exists(path)) continue;
            try
            {
                var stamp = File.GetLastWriteTimeUtc(path);
                if (!Previews.TryGetValue(path, out var cached) || cached.Stamp != stamp)
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 1024;
                    image.UriSource = new Uri(path);
                    image.EndInit();
                    image.Freeze();
                    cached = (stamp, image);
                    Previews[path] = cached;
                }
                result[assignment.Category] = (cached.Image, assignment.TextureSize);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException
                                                  or FileFormatException or ArgumentException or InvalidOperationException)
            {
                // A broken image only loses its preview; the render reports the same file by falling back as well.
            }
        }
        return result;
    }

    /// <summary>
    /// The material of each surface category for the render: the project's texture where one is assigned and readable,
    /// the stock material everywhere else.
    /// </summary>
    public static Func<SurfaceKind, RenderMaterial> Resolver(IReadOnlyList<RenderMaterialAssignment> assignments, string? projectFile,
        bool lightsOn = true)
    {
        var textured = new Dictionary<SurfaceKind, RenderMaterial>();
        foreach (var assignment in assignments)
        {
            // A library preset brings its whole material (a shipped texture too, when it has one); glass may take one.
            if (MaterialPresets.FromPath(assignment.TexturePath) is { } preset)
            {
                var image = preset.TextureKey is { } key ? Load(Resolve(BuiltInPrefix + key, projectFile)) : null;
                textured[assignment.Category] = MaterialPresets.Build(preset, image);
                continue;
            }
            if (assignment.Category == SurfaceKind.Glass) continue;
            if (Load(Resolve(assignment.TexturePath, projectFile)) is not { } texture) continue;
            textured[assignment.Category] = MaterialLibrary.Textured(assignment.Category, texture,
                assignment.TextureSize, assignment.Relief);
        }
        // With the lights off a fixture is just its white diffuser.
        if (!lightsOn)
        {
            textured[SurfaceKind.LightEmitter] = MaterialLibrary.ForKind(SurfaceKind.LightEmitter) with { Emission = default };
            textured[SurfaceKind.SpotEmitter] = MaterialLibrary.ForKind(SurfaceKind.SpotEmitter) with { Emission = default };
        }
        return kind => textured.TryGetValue(kind, out var material) ? material : MaterialLibrary.ForKind(kind);
    }
}
