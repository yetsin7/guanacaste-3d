using System.Collections;
using System.Reflection;
using System.Windows;
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
        foreach (var texture in catalog)
        {
            var textureType = texture.GetType();
            var key = (string)textureType.GetProperty("Key")!.GetValue(texture)!;
            var label = (string)textureType.GetProperty("LabelKey")!.GetValue(texture)!;
            var path = resolve.Invoke(null, ["builtin:" + key, null]);
            Check(path is string && load.Invoke(null, [path]) is not null &&
                  Application.Current.TryFindResource(label) is string,
                $"Built-in texture {key} is available, decodable and localized");
            count++;
        }
        Check(count >= 19, "The built-in catalog includes the photographic texture collection");
        Check(resolve.Invoke(null, ["builtin:../outside", null]) is null,
            "Unknown built-in texture identifiers cannot escape the texture catalog");
    }
}
