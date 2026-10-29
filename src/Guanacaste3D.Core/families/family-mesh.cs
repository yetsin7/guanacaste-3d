using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core;

/// <summary>
/// The triangles of a mesh family: a detailed model (a tree, a car) converted from glTF or OBJ into a loadable .g3df
/// component. Coordinates are meters, Z up, with the origin at the middle of the base and the front toward -Y, like
/// every other component family. Each triangle carries the render surface it is made of, so it takes part in the
/// project's materials (foliage, bark, car paint, glass...). Arrays are stored as base64 little-endian binary.
/// </summary>
public sealed class FamilyMesh
{
    /// <summary>Largest mesh a family may carry; enough for a scanned tree once reduced.</summary>
    public const int MaximumTriangles = 1_500_000;

    private Decoded? _decoded;

    /// <summary>Vertex positions, three float32 each.</summary>
    public string Positions { get; set; } = string.Empty;

    /// <summary>Three int32 vertex indices per triangle.</summary>
    public string Indices { get; set; } = string.Empty;

    /// <summary>One <see cref="SurfaceKind"/> byte per triangle.</summary>
    public string Kinds { get; set; } = string.Empty;

    /// <summary>Optional smooth shading normals, three float32 per vertex.</summary>
    public string? Normals { get; set; }

    /// <summary>
    /// Optional light version for interactive views (a few tens of thousands of triangles); the renderer always uses
    /// the full mesh. It is scaled with the full mesh's bounds, so both versions fill the same box.
    /// </summary>
    public FamilyMesh? Preview { get; set; }

    /// <summary>The decoded arrays of a mesh, ready to transform into a scene.</summary>
    public sealed record Decoded(Vector3[] Positions, int[] Indices, SurfaceKind[] Kinds, Vector3[]? Normals, Vector3 Min, Vector3 Max)
    {
        public int TriangleCount => Kinds.Length;
        public Vector3 Size => Max - Min;
    }

    /// <summary>Packs arrays into a family mesh.</summary>
    public static FamilyMesh Create(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices, IReadOnlyList<SurfaceKind> kinds,
        IReadOnlyList<Vector3>? normals = null)
    {
        var mesh = new FamilyMesh
        {
            Positions = Convert.ToBase64String(MemoryMarshal.AsBytes(positions.ToArray().AsSpan())),
            Indices = Convert.ToBase64String(MemoryMarshal.AsBytes(indices.ToArray().AsSpan())),
            Kinds = Convert.ToBase64String(kinds.Select(kind => (byte)kind).ToArray()),
            Normals = normals is null ? null : Convert.ToBase64String(MemoryMarshal.AsBytes(normals.ToArray().AsSpan()))
        };
        mesh.Validate();
        return mesh;
    }

    /// <summary>The decoded arrays, computed once and then cached; the stored strings are treated as immutable.</summary>
    public Decoded Decode()
    {
        if (_decoded is { } cached) return cached;
        try
        {
            var positions = MemoryMarshal.Cast<byte, Vector3>(Convert.FromBase64String(Positions)).ToArray();
            var indices = MemoryMarshal.Cast<byte, int>(Convert.FromBase64String(Indices)).ToArray();
            var kinds = Convert.FromBase64String(Kinds).Select(value => (SurfaceKind)value).ToArray();
            var normals = Normals is null ? null : MemoryMarshal.Cast<byte, Vector3>(Convert.FromBase64String(Normals)).ToArray();
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var position in positions)
            {
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }
            return _decoded = new Decoded(positions, indices, kinds, normals, min, max);
        }
        catch (FormatException)
        {
            throw new InvalidDataException("The family mesh is not valid.");
        }
    }

    /// <summary>Rejects truncated arrays, indices out of range, unknown surfaces and non-finite or empty geometry.</summary>
    public void Validate()
    {
        if (Positions is null || Indices is null || Kinds is null || Preview?.Preview is not null)
            throw new InvalidDataException("The family mesh is not valid.");
        Preview?.Validate();
        var mesh = Decode();
        if (mesh.Kinds.Length is < 1 or > MaximumTriangles || mesh.Indices.Length != mesh.Kinds.Length * 3 ||
            (mesh.Normals is not null && mesh.Normals.Length != mesh.Positions.Length) ||
            mesh.Positions.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)) ||
            mesh.Indices.Any(index => (uint)index >= (uint)mesh.Positions.Length) ||
            mesh.Kinds.Any(kind => !Enum.IsDefined(kind)) ||
            mesh.Size.X <= 0.001f || mesh.Size.Y <= 0.001f || mesh.Size.Z <= 0.001f)
            throw new InvalidDataException("The family mesh is not valid.");
    }

    /// <summary>
    /// Appends the mesh to a scene, scaled so its bounds match the instance's width, depth and height, turned by its
    /// rotation about the insertion point and standing at <paramref name="baseElevation"/>. With
    /// <paramref name="preview"/> it appends the light version when the family has one.
    /// </summary>
    public void AppendTo(SceneMesh scene, Point2 at, double rotation, double width, double depth, double height, double baseElevation,
        bool preview = false)
    {
        var bounds = Decode();
        var mesh = preview && Preview is { } light ? light.Decode() : bounds;
        var size = bounds.Size;
        var scale = new Vector3((float)(width / size.X), (float)(depth / size.Y), (float)(height / size.Z));
        var center = new Vector3((bounds.Min.X + bounds.Max.X) / 2, (bounds.Min.Y + bounds.Max.Y) / 2, bounds.Min.Z);
        var (cos, sin) = ((float)Math.Cos(rotation), (float)Math.Sin(rotation));
        var origin = new Vector3((float)at.X, (float)at.Y, (float)baseElevation);
        Vector3 Place(Vector3 point)
        {
            var local = (point - center) * scale;
            return origin + new Vector3(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos, local.Z);
        }
        Vector3 Turn(Vector3 normal)
        {
            // Normals transform by the inverse scale so they stay perpendicular to a stretched surface.
            var local = new Vector3(normal.X / scale.X, normal.Y / scale.Y, normal.Z / scale.Z);
            var turned = new Vector3(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos, local.Z);
            return turned.LengthSquared() > 0 ? Vector3.Normalize(turned) : turned;
        }
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle++)
        {
            var (a, b, c) = (mesh.Indices[triangle * 3], mesh.Indices[triangle * 3 + 1], mesh.Indices[triangle * 3 + 2]);
            if (mesh.Normals is { } normals)
                scene.AddSmoothTriangle(Place(mesh.Positions[a]), Place(mesh.Positions[b]), Place(mesh.Positions[c]),
                    Turn(normals[a]), Turn(normals[b]), Turn(normals[c]), mesh.Kinds[triangle]);
            else
                scene.AddTriangle(Place(mesh.Positions[a]), Place(mesh.Positions[b]), Place(mesh.Positions[c]), mesh.Kinds[triangle]);
        }
    }

    [JsonIgnore]
    public int TriangleCount => Decode().TriangleCount;
}
