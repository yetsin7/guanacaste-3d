using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Numerics;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Import;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop;

/// <summary>Maps renderer-neutral Z-up scene data into WPF's Y-up 3D presentation.</summary>
internal static class ScenePresentation
{
    /// <summary>Model (x, y, z) to WPF (x, z, -y): a proper rotation, so triangle winding is preserved.</summary>
    public static Point3D ToWpf(System.Numerics.Vector3 point) => new(point.X, point.Z, -point.Y);

    /// <summary>
    /// Builds one frozen model per surface kind; transparent glass is emitted last. Kinds with an image in
    /// <paramref name="textures"/> are drawn with it, projected in world space exactly as the renderer projects it, so
    /// the interactive view previews the render's materials.
    /// </summary>
    public static IEnumerable<GeometryModel3D> ToWpfModels(SceneMesh scene,
        IReadOnlyDictionary<SurfaceKind, (ImageSource Image, double Size)>? textures = null)
    {
        var meshes = new Dictionary<SurfaceKind, MeshGeometry3D>();
        for (var triangle = 0; triangle < scene.TriangleCount; triangle++)
        {
            var kind = scene.TriangleKinds[triangle];
            if (!meshes.TryGetValue(kind, out var mesh)) meshes[kind] = mesh = new MeshGeometry3D();
            var textured = textures is not null && textures.TryGetValue(kind, out var texture) ? texture.Size : 0;
            var a = scene.Positions[scene.Indices[triangle * 3]];
            var normal = System.Numerics.Vector3.Cross(scene.Positions[scene.Indices[triangle * 3 + 1]] - a,
                scene.Positions[scene.Indices[triangle * 3 + 2]] - a);
            for (var corner = 0; corner < 3; corner++)
            {
                var position = scene.Positions[scene.Indices[triangle * 3 + corner]];
                mesh.TriangleIndices.Add(mesh.Positions.Count);
                mesh.Positions.Add(ToWpf(position));
                if (textured <= 0) continue;
                // Image rows run downward while v runs up a wall, hence the minus sign.
                var (u, v) = TextureProjection.PlaneUv(position, normal);
                mesh.TextureCoordinates.Add(new System.Windows.Point(u / textured, -v / textured));
            }
        }
        foreach (var kind in new[] { SurfaceKind.Ground, SurfaceKind.Floor, SurfaceKind.Wall, SurfaceKind.DoorLeaf, SurfaceKind.OpeningFrame, SurfaceKind.Structure, SurfaceKind.Bark, SurfaceKind.Foliage, SurfaceKind.Earth, SurfaceKind.Roof, SurfaceKind.Leaf, SurfaceKind.InteriorWall, SurfaceKind.Metal, SurfaceKind.LightEmitter, SurfaceKind.Upholstery, SurfaceKind.Bedding, SurfaceKind.Ceramic, SurfaceKind.LightOff, SurfaceKind.SpotEmitter, SurfaceKind.WallBrick, SurfaceKind.WallBlock,
            SurfaceKind.WallConcrete, SurfaceKind.WallStone, SurfaceKind.WallWood, SurfaceKind.WallMetal, SurfaceKind.Mirror, SurfaceKind.PipeCold, SurfaceKind.PipeHot, SurfaceKind.PipeDrain,
            SurfaceKind.PipeStorm, SurfaceKind.SolarCell, SurfaceKind.FireRed, SurfaceKind.Glass })
        {
            if (!meshes.TryGetValue(kind, out var mesh)) continue;
            mesh.Freeze();
            var material = textures is not null && textures.TryGetValue(kind, out var texture)
                ? Textured(texture.Image)
                : MaterialFor(kind);
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            model.Freeze();
            yield return model;
        }
    }

    /// <summary>A material that repeats an image once per texture-coordinate unit.</summary>
    private static Material Textured(ImageSource image)
    {
        var brush = new ImageBrush(image)
        {
            TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new System.Windows.Rect(0, 0, 1, 1), Stretch = Stretch.Fill
        };
        brush.Freeze();
        var material = new DiffuseMaterial(brush);
        material.Freeze();
        return material;
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

    /// <summary>
    /// Converts a linked model's proxy into WPF models, one per material color so the view stays light. With
    /// <paramref name="clay"/> every surface is the same neutral grey, like an architectural study model.
    /// </summary>
    public static IEnumerable<GeometryModel3D> ToWpfModels(ProxyMesh proxy, IReadOnlyList<RenderMaterial> materials,
        ModelPlacement placement, bool clay = false)
    {
        var matrix = placement.ToMatrix();
        var groups = new Dictionary<(byte R, byte G, byte B, bool Glass), (MeshGeometry3D Mesh, Dictionary<int, int> Remap)>();
        for (var t = 0; t < proxy.TriangleCount; t++)
        {
            var materialIndex = t < proxy.TriangleMaterial.Length ? proxy.TriangleMaterial[t] : 0;
            var material = (uint)materialIndex < (uint)materials.Count ? materials[materialIndex] : MaterialLibrary.Imported;
            var key = clay ? ((byte)200, (byte)200, (byte)204, material.IsTransmissive)
                : (ToByte(material.BaseColor.X), ToByte(material.BaseColor.Y), ToByte(material.BaseColor.Z), material.IsTransmissive);
            if (!groups.TryGetValue(key, out var group)) groups[key] = group = (new MeshGeometry3D(), []);
            for (var corner = 0; corner < 3; corner++)
            {
                var source = proxy.Indices[t * 3 + corner];
                if (!group.Remap.TryGetValue(source, out var index))
                {
                    index = group.Mesh.Positions.Count;
                    group.Mesh.Positions.Add(ToWpf(Vector3.Transform(proxy.Positions[source], matrix)));
                    group.Remap[source] = index;
                }
                group.Mesh.TriangleIndices.Add(index);
            }
        }
        foreach (var (key, (mesh, _)) in groups.OrderBy(pair => pair.Key.Glass))
        {
            mesh.Freeze();
            var color = Color.FromRgb(key.R, key.G, key.B);
            Material wpfMaterial = key.Glass
                ? new MaterialGroup { Children = { Diffuse(Color.FromArgb(90, key.R, key.G, key.B)), new SpecularMaterial(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 60) } }
                : Diffuse(color);
            var model = new GeometryModel3D(mesh, wpfMaterial) { BackMaterial = wpfMaterial };
            model.Freeze();
            yield return model;
        }
    }

    /// <summary>
    /// X-ray: the building services (pipes, fixtures of the electrical system, air conditioning, septic tank) in their
    /// own colours, and the architecture and the ground as faint glass, so pipes under the floor and in the walls show
    /// through. Opaque services are emitted first and the translucent shell last, as WPF draws transparency in order.
    /// </summary>
    public static IEnumerable<GeometryModel3D> ToWpfXRay(SceneMesh scene)
    {
        var services = new Dictionary<SurfaceKind, MeshGeometry3D>();
        var shell = new MeshGeometry3D();
        var ground = new MeshGeometry3D();
        for (var triangle = 0; triangle < scene.TriangleCount; triangle++)
        {
            var kind = scene.TriangleKinds[triangle];
            MeshGeometry3D target;
            if (scene.IsService(triangle))
            {
                if (!services.TryGetValue(kind, out target!)) services[kind] = target = new MeshGeometry3D();
            }
            else target = kind == SurfaceKind.Ground ? ground : shell;
            for (var corner = 0; corner < 3; corner++)
            {
                target.TriangleIndices.Add(target.Positions.Count);
                target.Positions.Add(ToWpf(scene.Positions[scene.Indices[triangle * 3 + corner]]));
            }
        }
        foreach (var (kind, mesh) in services)
        {
            mesh.Freeze();
            var material = MaterialFor(kind);
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            model.Freeze();
            yield return model;
        }
        foreach (var (mesh, color) in new[] { (ground, Color.FromArgb(40, 150, 140, 120)), (shell, Color.FromArgb(38, 120, 150, 190)) })
        {
            if (mesh.TriangleIndices.Count == 0) continue;
            mesh.Freeze();
            var material = Diffuse(color);
            var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            model.Freeze();
            yield return model;
        }
    }

    /// <summary>
    /// The BIM scene as a study model: every solid surface the same soft grey, glass left see-through so openings still read.
    /// </summary>
    public static IEnumerable<GeometryModel3D> ToWpfClay(SceneMesh scene)
    {
        var solid = new MeshGeometry3D();
        var glass = new MeshGeometry3D();
        var ground = new MeshGeometry3D();
        for (var triangle = 0; triangle < scene.TriangleCount; triangle++)
        {
            var kind = scene.TriangleKinds[triangle];
            var target = kind == SurfaceKind.Glass ? glass : kind == SurfaceKind.Ground ? ground : solid;
            for (var corner = 0; corner < 3; corner++)
            {
                target.TriangleIndices.Add(target.Positions.Count);
                target.Positions.Add(ToWpf(scene.Positions[scene.Indices[triangle * 3 + corner]]));
            }
        }
        if (solid.TriangleIndices.Count > 0)
        {
            solid.Freeze();
            var material = Diffuse(Color.FromRgb(214, 214, 218));
            var model = new GeometryModel3D(solid, material) { BackMaterial = material };
            model.Freeze();
            yield return model;
        }
        if (ground.TriangleIndices.Count > 0)
        {
            ground.Freeze();
            var material = Diffuse(Color.FromRgb(232, 233, 231));
            var model = new GeometryModel3D(ground, material) { BackMaterial = material };
            model.Freeze();
            yield return model;
        }
        if (glass.TriangleIndices.Count > 0)
        {
            glass.Freeze();
            var material = Diffuse(Color.FromArgb(70, 170, 195, 215));
            var model = new GeometryModel3D(glass, material) { BackMaterial = material };
            model.Freeze();
            yield return model;
        }
    }

    /// <summary>
    /// Draws edges as slim octagonal tubes, so perspective makes nearby edges slightly stronger while depth testing hides
    /// occluded edges. The round section and unlit material avoid the coarse, shaded appearance of square prisms.
    /// </summary>
    public static GeometryModel3D? ToWpfEdges(IEnumerable<EdgeSegment> edges, Matrix4x4 placement, float thickness, Color color)
    {
        var mesh = new MeshGeometry3D();
        const int sectionSides = 8;
        var radius = Math.Max(0.00075f, thickness / 2);
        foreach (var edge in edges)
        {
            var a = Vector3.Transform(edge.A, placement);
            var b = Vector3.Transform(edge.B, placement);
            var direction = b - a;
            var length = direction.Length();
            if (length < 1e-5f) continue;
            direction /= length;
            var helper = MathF.Abs(direction.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX;
            var u = Vector3.Normalize(Vector3.Cross(direction, helper));
            var v = Vector3.Normalize(Vector3.Cross(direction, u));
            var start = mesh.Positions.Count;
            foreach (var end in new[] { a, b })
            {
                for (var side = 0; side < sectionSides; side++)
                {
                    var angle = 2 * MathF.PI * side / sectionSides;
                    mesh.Positions.Add(ToWpf(end + radius * (MathF.Cos(angle) * u + MathF.Sin(angle) * v)));
                }
            }
            for (var side = 0; side < sectionSides; side++)
            {
                var next = (side + 1) % sectionSides;
                mesh.TriangleIndices.Add(start + side);
                mesh.TriangleIndices.Add(start + sectionSides + side);
                mesh.TriangleIndices.Add(start + sectionSides + next);
                mesh.TriangleIndices.Add(start + side);
                mesh.TriangleIndices.Add(start + sectionSides + next);
                mesh.TriangleIndices.Add(start + next);
            }
        }
        if (mesh.TriangleIndices.Count == 0) return null;
        mesh.Freeze();
        var material = new EmissiveMaterial(new SolidColorBrush(color));
        var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
        model.Freeze();
        return model;
    }

    /// <summary>Linear 0–1 to an 8-bit sRGB channel, for display colors.</summary>
    private static byte ToByte(float linear)
    {
        var v = Math.Clamp(linear, 0f, 1f);
        var srgb = v <= 0.0031308f ? 12.92f * v : 1.055f * MathF.Pow(v, 1 / 2.4f) - 0.055f;
        return (byte)(srgb * 255 + 0.5f);
    }

    /// <summary>Converts the shared navigation camera to the 3D view's camera, with planes sized to the model.</summary>
    public static PerspectiveCamera ToWpfCamera(NavigationCamera camera, double aspectRatio, float sceneRadius)
    {
        var converted = ToWpfCamera(camera.ToRenderCamera(10), aspectRatio);
        var up = ToWpf(camera.Up);
        converted.UpDirection = new Vector3D(up.X, up.Y, up.Z);
        // Flying through rooms needs a close near plane; a long far plane keeps the horizon visible from outside.
        converted.NearPlaneDistance = Math.Max(0.02, sceneRadius * 0.0005);
        converted.FarPlaneDistance = Math.Max(500, sceneRadius * 40);
        return converted;
    }

    private static Material MaterialFor(SurfaceKind kind) => kind switch
    {
        SurfaceKind.DoorLeaf => Diffuse(Color.FromRgb(154, 99, 55)),
        SurfaceKind.OpeningFrame => Diffuse(Color.FromRgb(65, 69, 73)),
        SurfaceKind.Wall => Diffuse(Color.FromRgb(214, 210, 198)),
        SurfaceKind.Floor => Diffuse(Color.FromRgb(150, 148, 140)),
        SurfaceKind.Structure => Diffuse(Color.FromRgb(118, 110, 100)),
        SurfaceKind.Ground => Diffuse(Color.FromRgb(118, 140, 104)),
        SurfaceKind.Foliage => Diffuse(Color.FromRgb(78, 116, 58)),
        SurfaceKind.Bark => Diffuse(Color.FromRgb(104, 86, 68)),
        SurfaceKind.Earth => Diffuse(Color.FromRgb(126, 98, 72)),
        SurfaceKind.Roof => Diffuse(Color.FromRgb(168, 86, 58)),
        SurfaceKind.Leaf => Diffuse(Color.FromRgb(70, 108, 50)),
        SurfaceKind.InteriorWall => Diffuse(Color.FromRgb(226, 222, 212)),
        SurfaceKind.Metal => Diffuse(Color.FromRgb(160, 162, 166)),
        SurfaceKind.Upholstery => Diffuse(Color.FromRgb(112, 106, 98)),
        SurfaceKind.Bedding => Diffuse(Color.FromRgb(232, 230, 224)),
        SurfaceKind.Ceramic => Diffuse(Color.FromRgb(240, 240, 236)),
        SurfaceKind.LightOff => Diffuse(Color.FromRgb(214, 212, 206)),
        SurfaceKind.WallBrick => Diffuse(Color.FromRgb(168, 86, 60)),
        SurfaceKind.WallBlock => Diffuse(Color.FromRgb(160, 160, 156)),
        SurfaceKind.WallConcrete => Diffuse(Color.FromRgb(168, 166, 160)),
        SurfaceKind.WallStone => Diffuse(Color.FromRgb(150, 140, 124)),
        SurfaceKind.WallWood => Diffuse(Color.FromRgb(160, 112, 72)),
        SurfaceKind.WallMetal => Diffuse(Color.FromRgb(190, 194, 198)),
        SurfaceKind.Mirror => Diffuse(Color.FromRgb(200, 214, 220)),
        SurfaceKind.PipeCold => Diffuse(Color.FromRgb(40, 100, 200)),
        SurfaceKind.PipeHot => Diffuse(Color.FromRgb(200, 45, 35)),
        SurfaceKind.PipeDrain => Diffuse(Color.FromRgb(150, 120, 80)),
        SurfaceKind.PipeStorm => Diffuse(Color.FromRgb(60, 150, 70)),
        SurfaceKind.FireRed => new MaterialGroup { Children = { Diffuse(Color.FromRgb(190, 20, 16)), new SpecularMaterial(new SolidColorBrush(Color.FromRgb(90, 60, 60)), 30) } },
        SurfaceKind.SolarCell => new MaterialGroup { Children = { Diffuse(Color.FromRgb(18, 28, 60)), new SpecularMaterial(new SolidColorBrush(Color.FromRgb(120, 130, 150)), 60) } },
        SurfaceKind.SpotEmitter => new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(255, 240, 210))),
        SurfaceKind.LightEmitter => new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(255, 236, 200))),
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
