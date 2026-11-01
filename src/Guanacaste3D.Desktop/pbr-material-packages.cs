using System.IO;
using System.Text.Json;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Portable PBR package manifest. The first version supports textured base colour and uniform
/// physically-based roughness/metallicity (not unsupported per-pixel maps).
/// </summary>
internal sealed record PbrMaterialManifest
{
    public int Version { get; init; } = 1;
    public string Name { get; init; } = "Material PBR";
    public string Albedo { get; init; } = "";
    public string? Normal { get; init; }
    public string? RoughnessMap { get; init; }
    public string? MetallicMap { get; init; }
    public double Roughness { get; init; } = 0.7;
    public double Metallic { get; init; }
    public double Ior { get; init; } = 1.5;
    public double TextureSize { get; init; } = 1;
    public double Relief { get; init; } = 0.2;
}

/// <summary>
/// Safe local importer for .g3dm JSON sidecars. Rejects paths escaping the manifest directory;
/// projects retain the manifest path so a material can be changed without embedding large images.
/// </summary>
internal static class PbrMaterialPackages
{
    public const string Prefix = "pbr:";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static bool IsPackage(string? path) =>
        path?.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) == true;

    public static string? ResolveManifest(string assignment, string? projectFile)
    {
        if (!IsPackage(assignment)) return null;
        var raw = assignment[Prefix.Length..];
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            if (Path.IsPathRooted(raw)) return Path.GetFullPath(raw);
            var projectFolder = projectFile is null ? null : Path.GetDirectoryName(Path.GetFullPath(projectFile));
            return projectFolder is null ? null : Path.GetFullPath(Path.Combine(projectFolder, raw));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static (PbrMaterialManifest Manifest, string AlbedoPath)? Read(string? path)
    {
        if (path is null || !File.Exists(path) ||
            !string.Equals(Path.GetExtension(path), ".g3dm", StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            if (new FileInfo(path).Length > 65536) return null;
            var data = File.ReadAllText(path);
            var manifest = JsonSerializer.Deserialize<PbrMaterialManifest>(data, JsonOptions);
            if (manifest is null || manifest.Version != 1 ||
                string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 120 ||
                string.IsNullOrWhiteSpace(manifest.Albedo) || Path.IsPathRooted(manifest.Albedo) ||
                !double.IsFinite(manifest.Roughness) || manifest.Roughness is < 0.02 or > 1 ||
                !double.IsFinite(manifest.Metallic) || manifest.Metallic is < 0 or > 1 ||
                !double.IsFinite(manifest.Ior) || manifest.Ior is < 1 or > 3 ||
                !double.IsFinite(manifest.TextureSize) || manifest.TextureSize is < 0.01 or > 1000 ||
                !double.IsFinite(manifest.Relief) || manifest.Relief is < 0 or > 1)
                return null;
            var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var image = SafeImagePath(directory, manifest.Albedo);
            if (image is null ||
                (manifest.Normal is not null && SafeImagePath(directory, manifest.Normal) is null) ||
                (manifest.RoughnessMap is not null && SafeImagePath(directory, manifest.RoughnessMap) is null) ||
                (manifest.MetallicMap is not null && SafeImagePath(directory, manifest.MetallicMap) is null))
                return null;
            return (manifest, image);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? SafeImagePath(string directory, string relativeName)
    {
        if (string.IsNullOrWhiteSpace(relativeName) || Path.IsPathRooted(relativeName)) return null;
        var image = Path.GetFullPath(Path.Combine(directory, relativeName));
        var relative = Path.GetRelativePath(directory, image);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative) ||
            !new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" }
                .Contains(Path.GetExtension(image), StringComparer.OrdinalIgnoreCase) ||
            !File.Exists(image)) return null;
        return image;
    }

    /// <summary>
    /// Creates a self-contained folder material from one raster photograph. Both files
    /// use the same basename and existing packages are never overwritten silently.
    /// </summary>
    public static string CreateFromImage(string sourceImage, string destinationManifest,
        string name, double roughness, double metallic, double size, double relief)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 ||
            !double.IsFinite(roughness) || roughness is < 0.02 or > 1 ||
            !double.IsFinite(metallic) || metallic is < 0 or > 1 ||
            !double.IsFinite(size) || size is < 0.01 or > 1000 ||
            !double.IsFinite(relief) || relief is < 0 or > 1)
            throw new ArgumentException("Invalid PBR material parameters.");
        if (!File.Exists(sourceImage) ||
            !new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" }
                .Contains(Path.GetExtension(sourceImage), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected PBR albedo image is invalid.");
        if (!string.Equals(Path.GetExtension(destinationManifest), ".g3dm",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Material packages must have a .g3dm extension.");
        var destination = Path.GetFullPath(destinationManifest);
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        if (File.Exists(destination))
            throw new IOException("The material manifest already exists.");
        var imageName = Path.GetFileNameWithoutExtension(destination) + "-albedo" +
            Path.GetExtension(sourceImage).ToLowerInvariant();
        var targetImage = Path.Combine(directory, imageName);
        if (File.Exists(targetImage))
            throw new IOException("The material albedo already exists.");
        // Validate that source is decodable before copying. The renderer will use its
        // existing WPF decoder and downsampling limits after the import.
        if (RenderTextureLibrary.Load(sourceImage) is null)
            throw new InvalidDataException("The selected PBR image cannot be decoded.");
        var manifest = new PbrMaterialManifest
        {
            Name = name.Trim(), Albedo = imageName,
            Roughness = roughness, Metallic = metallic,
            TextureSize = size, Relief = relief
        };
        File.Copy(sourceImage, targetImage);
        try
        {
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(destination, json);
            return destination;
        }
        catch
        {
            File.Delete(targetImage);
            throw;
        }
    }

    public static RenderMaterial? Build(string assignment, string? projectFile, double size, double relief)
    {
        var imported = Read(ResolveManifest(assignment, projectFile));
        if (imported is not { } package) return null;
        var texture = RenderTextureLibrary.Load(package.AlbedoPath);
        if (texture is null) return null;
        var manifest = package.Manifest;
        return new RenderMaterial
        {
            Name = manifest.Name, Texture = texture,
            TextureSize = (float)size, TextureRelief = (float)relief,
            Roughness = (float)manifest.Roughness, Metallic = (float)manifest.Metallic,
            Ior = (float)manifest.Ior, BaseColor = System.Numerics.Vector3.One,
            NormalMap = manifest.Normal is null ? null : RenderTextureLibrary.LoadNormal(
                SafeImagePath(Path.GetDirectoryName(package.AlbedoPath)!, manifest.Normal)),
            RoughnessMap = manifest.RoughnessMap is null ? null : RenderTextureLibrary.LoadScalar(
                SafeImagePath(Path.GetDirectoryName(package.AlbedoPath)!, manifest.RoughnessMap)),
            MetallicMap = manifest.MetallicMap is null ? null : RenderTextureLibrary.LoadScalar(
                SafeImagePath(Path.GetDirectoryName(package.AlbedoPath)!, manifest.MetallicMap))
        }.Sanitized();
    }
}
