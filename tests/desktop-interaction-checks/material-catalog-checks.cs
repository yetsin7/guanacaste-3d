using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;
using Guanacaste3D.Desktop;

internal static partial class InteractionChecks
{
    /// <summary>Checks every shipped texture resolves, decodes, and has a localized label in the running application.</summary>
    private static void CheckBuiltInTextures()
    {
        var library = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.RenderTextureLibrary")!;
        var catalog = (IEnumerable)library.GetProperty("BuiltIn", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var resolve = library.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)!;
        var load = library.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)!;
        var count = 0;
        var treeTextures = 0;
        foreach (var texture in catalog)
        {
            var textureType = texture.GetType();
            var key = (string)textureType.GetProperty("Key")!.GetValue(texture)!;
            var label = (string)textureType.GetProperty("LabelKey")!.GetValue(texture)!;
            var path = resolve.Invoke(null, ["builtin:" + key, null]);
            Check(path is string && load.Invoke(null, [path]) is not null &&
                  Application.Current.TryFindResource(label) is string,
                $"Built-in texture {key} is available, decodable and localized");
            if (key.StartsWith("tree-", StringComparison.Ordinal))
            {
                treeTextures++;
                var colorPath = path as string;
                var normalPath = colorPath is null ? null :
                    Path.Combine(Path.GetDirectoryName(colorPath)!, Path.GetFileNameWithoutExtension(colorPath) + "_NormalGL.png");
                Check(colorPath is not null && normalPath is not null && File.Exists(colorPath) && File.Exists(normalPath),
                    $"Nature material {key} generates a cached albedo and an OpenGL normal image");
            }
            count++;
        }
        Check(treeTextures == 24 && count >= 43,
            "The library includes twelve bark/trunk and twelve foliage textures in addition to the shipped photographic collection");
        Check(resolve.Invoke(null, ["builtin:../outside", null]) is null,
            "Unknown built-in texture identifiers cannot escape the texture catalog");
        // A missing library preset may remain in a legacy project. It is an
        // identifier, not a relative file path, and must safely use the stock finish.
        var projectFile = Path.Combine(Path.GetTempPath(), "legacy-material.g3d");
        Check(resolve.Invoke(null, ["material:removed-from-library", projectFile]) is null,
            "Unknown library material identifiers are never interpreted as filesystem paths");
        var missing = new[]
        {
            new RenderMaterialAssignment(SurfaceKind.Wall, "material:removed-from-library")
        };
        var missingResolver = (Func<SurfaceKind, RenderMaterial>)library.GetMethod("Resolver",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [missing, projectFile, true])!;
        Check(missingResolver(SurfaceKind.Wall) == MaterialLibrary.ForKind(SurfaceKind.Wall),
            "Missing library materials fall back to the stock finish without crashing");
        // Regression: editing the size/relief of a library preset must affect the actual
        // ray-traced material, not merely the saved project values.
        var assignment = new RenderMaterialAssignment(SurfaceKind.Wall, MaterialPresets.Prefix + "oak-light")
        {
            TextureSize = 0.73,
            Relief = 0.64
        };
        var assignments = new[] { assignment };
        var resolver = (Func<SurfaceKind, RenderMaterial>)library.GetMethod("Resolver",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [assignments, null, true])!;
        var rendered = resolver(SurfaceKind.Wall);
        Check(rendered.Texture is not null &&
              Math.Abs(rendered.TextureSize - 0.73f) < 0.0001f &&
              Math.Abs(rendered.TextureRelief - 0.64f) < 0.0001f,
            "Preset material edits affect texture scale and relief in the renderer");

        // The ordinary WPF 3D viewport should show the same photographic texture.
        var previews = (IReadOnlyDictionary<SurfaceKind, (ImageSource Image, double Size)>)
            library.GetMethod("Previews3D", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [assignments, null])!;
        Check(previews.TryGetValue(SurfaceKind.Wall, out var preview) &&
              preview.Image is not null && Math.Abs(preview.Size - 0.73) < 1e-9,
            "A textured preset appears in the interactive 3D viewport at its edited scale");

        var plain = new[] { new RenderMaterialAssignment(SurfaceKind.Wall, MaterialPresets.Prefix + "paint-white") };
        var plainPreviews = (IReadOnlyDictionary<SurfaceKind, (ImageSource Image, double Size)>)
            library.GetMethod("Previews3D", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [plain, null])!;
        Check(!plainPreviews.ContainsKey(SurfaceKind.Wall),
            "Untextured presets do not resolve as nonexistent image files");

        var renderPanel = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.Controls.RenderPanel")!;
        var resolveHdri = renderPanel.GetMethod("ResolveSavedHdri",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var file = Path.Combine(Path.GetTempPath(), "render-asset-test", "example.g3d");
        var expected = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "assets", "sky.hdr"));
        Check((string?)resolveHdri.Invoke(null, ["assets/sky.hdr", file]) == expected,
            "Project-relative HDRI references resolve in the project assets folder");
        Check(resolveHdri.Invoke(null, ["../other-project/secret.hdr", file]) is null &&
              resolveHdri.Invoke(null, [@"\\server\share\sky.hdr", file]) is null,
            "HDRI auto-restore rejects path traversal and UNC network references");
    }
}
