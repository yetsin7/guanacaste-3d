using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>What the render panel needs from the window that hosts it.</summary>
internal interface IRenderSource
{
    /// <summary>The camera shared with the 3D view, so both always show the same place.</summary>
    NavigationCamera Camera { get; }

    string ProjectName { get; }

    (Vector3 Min, Vector3 Max) SceneBounds();

    /// <summary>The current scene, built in the background and cached; null when the project is empty.</summary>
    Task<RenderScene?> GetSceneAsync();

    /// <summary>Incremented whenever geometry, visibility, or materials invalidate the cached scene.</summary>
    int SceneVersion { get; }

    /// <summary>Ray queries for the current scene, or null until they are built.</summary>
    SceneQuery? Query { get; }

    /// <summary>Raised when geometry, links or materials change and the scene must be rebuilt.</summary>
    event Action? SceneChanged;

    /// <summary>Raised when the scene query becomes available or changes.</summary>
    event Action? QueryChanged;

    /// <summary>The project's render texture per surface category.</summary>
    IReadOnlyList<RenderMaterialAssignment> RenderMaterials { get; }

    /// <summary>Assigns a texture to a category, or returns it to its stock material when the path is null.</summary>
    void AssignRenderMaterial(SurfaceKind category, string? texturePath, double size, double relief, Guid? elementId = null);

    /// <summary>Set or remove independently loaded PBR maps, preserving the selected base material and undo history.</summary>
    void SetRenderMaterialMaps(SurfaceKind category, Guid? elementId,
        string? normalPath, string? roughnessPath, string? metallicPath);

    /// <summary>Whether light fixtures are on; changing it rebuilds the scene.</summary>
    bool ArtificialLights { get; set; }

    /// <summary>Whether the render uses neutral study materials instead of project assignments.</summary>
    bool ClayMode { get; set; }
}

