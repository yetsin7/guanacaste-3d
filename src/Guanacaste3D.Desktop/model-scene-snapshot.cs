using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Desktop;

/// <summary>A frozen interactive scene that can be constructed away from the WPF dispatcher.</summary>
internal sealed record ModelSceneSnapshot(IReadOnlyList<GeometryModel3D> Models, GeometryModel3D? Edges,
    (Vector3 Min, Vector3 Max) Bounds, bool HasGeometry)
{
    /// <summary>Meshes a document snapshot, prepares frozen visuals, and derives bounds without the infinite ground.</summary>
    public static ModelSceneSnapshot Build(BuildingDocument document, bool clay, bool xray, bool showEdges,
        IReadOnlyDictionary<SurfaceKind, (ImageSource Image, double Size)>? textures)
    {
        var scene = BuildingMesher.Build(document, includeGround: true);
        var indices = new List<int>();
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        for (var triangle = 0; triangle < scene.TriangleCount; triangle++)
        {
            if (scene.TriangleKinds[triangle] == SurfaceKind.Ground) continue;
            for (var corner = 0; corner < 3; corner++)
            {
                var index = scene.Indices[triangle * 3 + corner];
                indices.Add(index);
                minimum = Vector3.Min(minimum, scene.Positions[index]);
                maximum = Vector3.Max(maximum, scene.Positions[index]);
            }
        }
        var hasGeometry = indices.Count > 0;
        if (!hasGeometry) { minimum = new(-5, -5, 0); maximum = new(5, 5, 3); }
        var thickness = Math.Clamp(0.0012f * Math.Max(3, (maximum - minimum).Length() / 2), 0.0025f, 0.012f);
        var models = (xray ? ScenePresentation.ToWpfXRay(scene) : clay
            ? ScenePresentation.ToWpfClay(scene) : ScenePresentation.ToWpfModels(scene, textures)).ToArray();
        // Each edge becomes an eight-sided tube; bound the additional geometry on large models.
        var edgeLimit = scene.TriangleCount > 100_000 ? 10_000 : 80_000;
        var edges = showEdges && !xray
            ? ScenePresentation.ToWpfEdges(FeatureEdges.Extract(scene.Positions, indices, maximumEdges: edgeLimit), Matrix4x4.Identity,
                thickness, Color.FromRgb(52, 56, 62))
            : null;
        return new(models, edges, (minimum, maximum), hasGeometry);
    }
}
