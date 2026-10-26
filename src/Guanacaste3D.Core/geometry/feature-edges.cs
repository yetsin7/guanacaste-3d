using System.Numerics;

namespace Guanacaste3D.Core.Geometry;

/// <summary>A straight edge of the model that should be drawn as a line.</summary>
public readonly record struct EdgeSegment(Vector3 A, Vector3 B);

/// <summary>
/// Finds the edges that give a model its readable outline: boundary edges (used by one triangle) and creases where two
/// faces meet at a visible angle. Edges between nearly coplanar triangles, such as the seams left by splitting a wall
/// around its openings, are not reported, so flat surfaces stay clean.
/// </summary>
public static class FeatureEdges
{
    /// <summary>Vertices closer than this (in meters) are treated as one, since meshes often repeat each corner per triangle.</summary>
    private const float WeldTolerance = 1e-4f;

    /// <summary>Returns feature edges of an indexed triangle mesh, at most <paramref name="maximumEdges"/> of them.</summary>
    public static List<EdgeSegment> Extract(IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices, float creaseDegrees = 28,
        int maximumEdges = 80_000)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(indices);
        var result = new List<EdgeSegment>();
        var triangleCount = indices.Count / 3;
        if (triangleCount == 0) return result;

        var weld = new Dictionary<(int, int, int), int>(positions.Count / 2);
        var welded = new int[positions.Count];
        var representatives = new List<Vector3>();
        for (var i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            var key = ((int)MathF.Round(p.X / WeldTolerance), (int)MathF.Round(p.Y / WeldTolerance), (int)MathF.Round(p.Z / WeldTolerance));
            if (!weld.TryGetValue(key, out var index))
            {
                index = representatives.Count;
                weld[key] = index;
                representatives.Add(p);
            }
            welded[i] = index;
        }

        var cosineLimit = MathF.Cos(creaseDegrees * MathF.PI / 180);
        // Per edge: the normal of the first triangle that used it, and how many triangles used it.
        var edges = new Dictionary<(int, int), (Vector3 Normal, int Count, bool Sharp)>(triangleCount * 2);
        for (var t = 0; t < triangleCount; t++)
        {
            var a = welded[indices[t * 3]];
            var b = welded[indices[t * 3 + 1]];
            var c = welded[indices[t * 3 + 2]];
            if (a == b || b == c || a == c) continue;
            var cross = Vector3.Cross(representatives[b] - representatives[a], representatives[c] - representatives[a]);
            if (cross.LengthSquared() < 1e-14f) continue;
            var normal = Vector3.Normalize(cross);
            Add(edges, a, b, normal, cosineLimit);
            Add(edges, b, c, normal, cosineLimit);
            Add(edges, c, a, normal, cosineLimit);
        }

        foreach (var ((first, second), (_, count, sharp)) in edges)
        {
            if (count != 2 || sharp) // boundary, crease, or an edge shared by more than two faces
            {
                result.Add(new EdgeSegment(representatives[first], representatives[second]));
                if (result.Count >= maximumEdges) break;
            }
        }
        return result;
    }

    private static void Add(Dictionary<(int, int), (Vector3 Normal, int Count, bool Sharp)> edges, int a, int b, Vector3 normal,
        float cosineLimit)
    {
        var key = a < b ? (a, b) : (b, a);
        if (!edges.TryGetValue(key, out var existing))
        {
            edges[key] = (normal, 1, false);
            return;
        }
        // Compare against the first face by absolute angle: meshes with mixed winding must not turn every seam into a line.
        var sharp = existing.Sharp || MathF.Abs(Vector3.Dot(existing.Normal, normal)) < cosineLimit;
        edges[key] = (existing.Normal, existing.Count + 1, sharp);
    }
}
