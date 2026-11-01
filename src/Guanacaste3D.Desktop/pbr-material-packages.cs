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
    public double Roughness { get; init; } = 0.7;
    public double Metallic { get; init; }
    public double Ior { get; init; } = 1.5;
    public double TextureSize { get; init; } = 1;
    public double Relief { get; init; } = 0.2;
}

/// <summary>
/// Safe local importer for .g3dpbr JSON sidecars. Rejects paths escaping the manifest directory;
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
            !string.Equals(Path.GetExtension(path), ".g3dpbr", StringComparison.OrdinalIgnoreCase))
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
            var image = Path.GetFullPath(Path.Combine(directory, manifest.Albedo));
            var relative = Path.GetRelativePath(directory, image);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                Path.IsPathRooted(relative)) return null;
            if (!new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" }
                    .Contains(Path.GetExtension(image), StringComparer.OrdinalIgnoreCase) ||
                !File.Exists(image)) return null;
            return (manifest, image);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return null;
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
            Ior = (float)manifest.Ior, BaseColor = System.Numerics.Vector3.One
        }.Sanitized();
    }
}
