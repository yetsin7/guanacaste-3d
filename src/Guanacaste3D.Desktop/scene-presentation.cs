using System.Windows.Media;
using System.Windows.Media.Media3D;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop;

/// <summary>Maps renderer-neutral Z-up scene data into WPF's Y-up 3D presentation.</summary>
internal static class ScenePresentation
{
    /// <summary>Model (x, y, z) to WPF (x, z, -y): a proper rotation, so triangle winding is preserved.</summary>
    public static Point3D ToWpf(System.Numerics.Vector3 point) => new(point.X, point.Z, -point.Y);

    /// <summary>Builds one frozen model per surface kind; transparent glass is emitted last.</summary>
    public static IEnumerable<GeometryModel3D> ToWpfModels(SceneMesh scene)
    {
        var meshes = new Dictionary<SurfaceKind, MeshGeometry3D>();
        for (var triangle = 0; triangle < scene.TriangleCount; triangle++)
        {
            var kind = scene.TriangleKinds[triangle];
            if (!meshes.TryGetValue(kind, out var mesh)) meshes[kind] = mesh = new MeshGeometry3D();
            for (var corner = 0; corner < 3; corner++)
            {
                mesh.TriangleIndices.Add(mesh.Positions.Count);
                mesh.Positions.Add(ToWpf(scene.Positions[scene.Indices[triangle * 3 + corner]]));
            }
        }
        foreach (var kind in new[] { SurfaceKind.Ground, SurfaceKind.Floor, SurfaceKind.Wall, SurfaceKind.DoorLeaf, SurfaceKind.OpeningFrame, SurfaceKind.Structure, SurfaceKind.Glass })
        {
            if (!meshes.TryGetValue(kind, out var mesh)) continue;
            mesh.Freeze();
            var material = MaterialFor(kind);
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            model.Freeze();
            yield return model;
        }
    }

    /// <summary>Converts a renderer camera so the interactive view and the ray-traced image match.</summary>
    public static PerspectiveCamera ToWpfCamera(RenderCamera camera, double aspectRatio)
    {
        var position = ToWpf(camera.Position);
        var target = ToWpf(camera.Target);
        // WPF measures the field of view horizontally; the renderer measures it vertically.
        var halfVertical = camera.VerticalFieldOfViewDegrees * Math.PI / 360;
        var horizontal = 2 * Math.Atan(Math.Tan(halfVertical) * Math.Max(0.1, aspectRatio)) * 180 / Math.PI;
        return new PerspectiveCamera(position, target - position, new Vector3D(0, 1, 0), horizontal);
    }

    private static Material MaterialFor(SurfaceKind kind) => kind switch
    {
        SurfaceKind.DoorLeaf => Diffuse(Color.FromRgb(154, 99, 55)),
        SurfaceKind.OpeningFrame => Diffuse(Color.FromRgb(65, 69, 73)),
        SurfaceKind.Wall => Diffuse(Color.FromRgb(214, 210, 198)),
        SurfaceKind.Floor => Diffuse(Color.FromRgb(150, 148, 140)),
        SurfaceKind.Structure => Diffuse(Color.FromRgb(118, 110, 100)),
        SurfaceKind.Ground => Diffuse(Color.FromRgb(118, 140, 104)),
        SurfaceKind.Glass => new MaterialGroup
        {
            Children =
            {
                Diffuse(Color.FromArgb(90, 150, 190, 215)),
                new SpecularMaterial(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 60)
            }
        },
        _ => Diffuse(Colors.LightGray)
    };

    private static DiffuseMaterial Diffuse(Color color) => new(new SolidColorBrush(color));
}
