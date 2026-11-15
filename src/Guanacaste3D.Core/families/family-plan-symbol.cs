using System.Numerics;
using System.Runtime.CompilerServices;

namespace Guanacaste3D.Core;

/// <summary>
/// The plan symbol of a family made in the Family editor, drawn from its own geometry the way a plan sees it from above:
/// the edges where a face that looks up meets a fold (a table top's border, the rings of a turned lamp) and the edges
/// where the surface ends. Flat faces lose the diagonals of their triangles and smooth round faces lose their facets,
/// so the symbol reads like a drawing instead of a mesh.
/// </summary>
public static class FamilyPlanSymbol
{
    /// <summary>Faces turned at least this far up count as seen from above.</summary>
    private const double UpwardNormal = 0.2;

    /// <summary>Neighbouring faces meeting at a sharper angle than about 30° draw their shared edge.</summary>
    private const double CreaseCosine = 0.866;

    /// <summary>Above this the mesh is an imported model, not a drawn family; its light preview is used, or none.</summary>
    private const int MaximumTriangles = 20_000;

    /// <summary>A symbol never draws more segments than this, so a dense family cannot slow the plan down.</summary>
    private const int MaximumSegments = 4_000;

    private static readonly ConditionalWeakTable<FamilyMesh, IReadOnlyList<(Vector2 A, Vector2 B)>> Cache = new();

    /// <summary>
    /// The symbol of a placed component when it comes from a family made in the Family editor (one loaded with
    /// <see cref="FamilyAuthoring.Bake"/>); empty for everything else, which keeps its catalog symbol.
    /// </summary>
    public static IReadOnlyList<(Point2 A, Point2 B)> For(BuildingDocument document, StructuralElement element)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (element is not { Kind: StructuralElementKind.Component, FamilyId: Guid familyId }) return [];
        var family = document.ComponentFamilies.FirstOrDefault(candidate => candidate.Id == familyId);
        return family is not null && family.Metadata.ContainsKey("SourceFamily") ? Segments(family, element) : [];
    }

    /// <summary>
    /// The symbol's segments in model coordinates for one instance: stretched to its width and depth, turned by its
    /// rotation and centred on its insertion point, exactly as the 3D view places the same mesh. Empty when the family
    /// has no usable mesh, so the caller keeps its plain outline.
    /// </summary>
    public static IReadOnlyList<(Point2 A, Point2 B)> Segments(FamilyDocument family, StructuralElement element)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(element);
        if (family.Mesh is not { } mesh) return [];
        var bounds = mesh.Decode();
        var size = bounds.Size;
        if (size.X <= 1e-6 || size.Y <= 1e-6 || element.Width <= 0 || element.Depth <= 0) return [];
        var local = Cache.GetValue(mesh, Edges);
        if (local.Count == 0) return [];

        var scale = new Vector2((float)(element.Width / size.X), (float)(element.Depth / size.Y));
        var center = new Vector2((bounds.Min.X + bounds.Max.X) / 2, (bounds.Min.Y + bounds.Max.Y) / 2);
        var (cos, sin) = (Math.Cos(element.Rotation), Math.Sin(element.Rotation));
        Point2 Place(Vector2 point)
        {
            var offset = (point - center) * scale;
            return new Point2(element.Start.X + offset.X * cos - offset.Y * sin, element.Start.Y + offset.X * sin + offset.Y * cos);
        }
        return local.Select(segment => (Place(segment.A), Place(segment.B))).ToList();
    }

    /// <summary>The edges seen from above, in the mesh's own coordinates, projected onto the floor.</summary>
    private static IReadOnlyList<(Vector2 A, Vector2 B)> Edges(FamilyMesh source)
    {
        var mesh = source.Decode();
        if (mesh.TriangleCount > MaximumTriangles)
        {
            if (source.Preview is not { } light || light.Decode().TriangleCount > MaximumTriangles) return [];
            mesh = light.Decode();
        }

        // Faces are stored per triangle, often with their own copies of shared corners, so edges are matched by
        // position (to a hundredth of a millimetre), not by index.
        static (long, long, long) Key(Vector3 point) =>
            ((long)Math.Round(point.X * 1e5), (long)Math.Round(point.Y * 1e5), (long)Math.Round(point.Z * 1e5));
        var edges = new Dictionary<((long, long, long), (long, long, long)), (Vector3 A, Vector3 B, List<Vector3> Normals)>();
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle++)
        {
            var corners = new[]
            {
                mesh.Positions[mesh.Indices[triangle * 3]], mesh.Positions[mesh.Indices[triangle * 3 + 1]],
                mesh.Positions[mesh.Indices[triangle * 3 + 2]]
            };
            var cross = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
            if (cross.LengthSquared() < 1e-14f) continue;
            var normal = Vector3.Normalize(cross);
            for (var side = 0; side < 3; side++)
            {
                var (a, b) = (corners[side], corners[(side + 1) % 3]);
                var (keyA, keyB) = (Key(a), Key(b));
                var key = keyA.CompareTo(keyB) <= 0 ? (keyA, keyB) : (keyB, keyA);
                if (!edges.TryGetValue(key, out var edge)) edges[key] = edge = (a, b, []);
                edge.Normals.Add(normal);
            }
        }

        // Faces that look up hide what lies under them: a table top hides the tops of its legs, a lamp shade its base.
        var roofs = new List<(Vector3 A, Vector3 B, Vector3 C)>();
        if (mesh.TriangleCount <= OcclusionTriangles)
            for (var triangle = 0; triangle < mesh.TriangleCount; triangle++)
            {
                var (a, b, c) = (mesh.Positions[mesh.Indices[triangle * 3]], mesh.Positions[mesh.Indices[triangle * 3 + 1]],
                    mesh.Positions[mesh.Indices[triangle * 3 + 2]]);
                var cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() > 1e-14f && Vector3.Normalize(cross).Z > UpwardNormal) roofs.Add((a, b, c));
            }

        var segments = new List<(Vector2 A, Vector2 B)>();
        foreach (var (a, b, normals) in edges.Values)
        {
            if (!normals.Any(normal => normal.Z > UpwardNormal)) continue;
            var fold = normals.Count == 1 || normals.Any(first => normals.Any(second => Vector3.Dot(first, second) < CreaseCosine));
            if (!fold) continue;
            var (from, to) = (new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));
            if (Vector2.DistanceSquared(from, to) < 1e-8f) continue;
            if (Hidden((a + b) / 2, roofs)) continue;
            segments.Add((from, to));
            if (segments.Count >= MaximumSegments) break;
        }
        return segments;
    }

    /// <summary>Meshes up to this size are checked for hidden edges; past it every visible-kind edge is drawn.</summary>
    private const int OcclusionTriangles = 3_000;

    /// <summary>True when an upward face lies over the point, more than a millimetre higher.</summary>
    private static bool Hidden(Vector3 point, List<(Vector3 A, Vector3 B, Vector3 C)> roofs)
    {
        foreach (var (a, b, c) in roofs)
        {
            var denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(denominator) < 1e-12f) continue;
            var u = ((b.Y - c.Y) * (point.X - c.X) + (c.X - b.X) * (point.Y - c.Y)) / denominator;
            var v = ((c.Y - a.Y) * (point.X - c.X) + (a.X - c.X) * (point.Y - c.Y)) / denominator;
            var w = 1 - u - v;
            const float inside = 1e-4f;
            if (u < inside || v < inside || w < inside) continue;
            if (u * a.Z + v * b.Z + w * c.Z > point.Z + 1e-3f) return true;
        }
        return false;
    }
}
