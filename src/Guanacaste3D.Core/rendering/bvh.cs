using System.Numerics;
using System.Runtime.CompilerServices;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core.Rendering;

/// <summary>Closest-hit result of a ray query.</summary>
public struct RayHit
{
    public float Distance;
    public int Triangle;
    public float U;
    public float V;
}

/// <summary>
/// Bounding-volume hierarchy over a triangle mesh, built with a binned surface-area heuristic.
/// Nodes are stored flat; leaves reference a contiguous range of reordered triangles.
/// </summary>
public sealed class Bvh
{
    private const int BinCount = 12;
    private const int MaxLeafSize = 4;

    private struct Node
    {
        public Vector3 Min;
        public Vector3 Max;
        public int LeftOrFirst; // left child index for interior nodes, first triangle for leaves
        public int Count;       // zero for interior nodes
    }

    private readonly Vector3[] _vertex0;
    private readonly Vector3[] _edge1;
    private readonly Vector3[] _edge2;
    private readonly int[] _order;
    private readonly Node[] _nodes;
    private int _nodeCount;

    public int TriangleCount => _order.Length;
    public int NodeCount => _nodeCount;

    /// <summary>Builds the hierarchy; construction is O(n log n) in the triangle count.</summary>
    public Bvh(SceneMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var count = mesh.TriangleCount;
        _vertex0 = new Vector3[count];
        _edge1 = new Vector3[count];
        _edge2 = new Vector3[count];
        _order = new int[count];
        var centroids = new Vector3[count];
        var minimums = new Vector3[count];
        var maximums = new Vector3[count];
        for (var triangle = 0; triangle < count; triangle++)
        {
            var a = mesh.Positions[mesh.Indices[triangle * 3]];
            var b = mesh.Positions[mesh.Indices[triangle * 3 + 1]];
            var c = mesh.Positions[mesh.Indices[triangle * 3 + 2]];
            _vertex0[triangle] = a;
            _edge1[triangle] = b - a;
            _edge2[triangle] = c - a;
            _order[triangle] = triangle;
            minimums[triangle] = Vector3.Min(a, Vector3.Min(b, c));
            maximums[triangle] = Vector3.Max(a, Vector3.Max(b, c));
            centroids[triangle] = (a + b + c) / 3;
        }
        _nodes = new Node[Math.Max(1, count * 2)];
        _nodes[0] = new Node { LeftOrFirst = 0, Count = count };
        _nodeCount = 1;
        if (count == 0) return;
        UpdateBounds(0, minimums, maximums);
        Subdivide(0, centroids, minimums, maximums);
    }

    /// <summary>Returns the geometric normal of a triangle (not normalized to face the ray).</summary>
    public Vector3 Normal(int triangle) => Vector3.Normalize(Vector3.Cross(_edge1[triangle], _edge2[triangle]));

    /// <summary>Recomputes a node's bounds from the triangles it references.</summary>
    private void UpdateBounds(int nodeIndex, Vector3[] minimums, Vector3[] maximums)
    {
        ref var node = ref _nodes[nodeIndex];
        node.Min = new Vector3(float.MaxValue);
        node.Max = new Vector3(float.MinValue);
        for (var i = 0; i < node.Count; i++)
        {
            var triangle = _order[node.LeftOrFirst + i];
            node.Min = Vector3.Min(node.Min, minimums[triangle]);
            node.Max = Vector3.Max(node.Max, maximums[triangle]);
        }
    }

    /// <summary>Recursively splits a node where the binned SAH cost is lowest.</summary>
    private void Subdivide(int nodeIndex, Vector3[] centroids, Vector3[] minimums, Vector3[] maximums)
    {
        var node = _nodes[nodeIndex];
        if (node.Count <= MaxLeafSize) return;

        var (axis, position, cost) = FindBestSplit(node, centroids, minimums, maximums);
        var parentCost = node.Count * SurfaceArea(node.Min, node.Max);
        if (axis < 0 || cost >= parentCost) return;

        var i = node.LeftOrFirst;
        var j = i + node.Count - 1;
        while (i <= j)
        {
            if (Component(centroids[_order[i]], axis) < position)
            {
                i++;
                continue;
            }
            (_order[i], _order[j]) = (_order[j], _order[i]);
            j--;
        }
        var leftCount = i - node.LeftOrFirst;
        if (leftCount == 0 || leftCount == node.Count) return;

        var left = _nodeCount++;
        var right = _nodeCount++;
        _nodes[left] = new Node { LeftOrFirst = node.LeftOrFirst, Count = leftCount };
        _nodes[right] = new Node { LeftOrFirst = i, Count = node.Count - leftCount };
        _nodes[nodeIndex].LeftOrFirst = left;
        _nodes[nodeIndex].Count = 0;
        UpdateBounds(left, minimums, maximums);
        UpdateBounds(right, minimums, maximums);
        Subdivide(left, centroids, minimums, maximums);
        Subdivide(right, centroids, minimums, maximums);
    }

    /// <summary>Evaluates BinCount-1 candidate planes per axis over the centroid bounds.</summary>
    private (int Axis, float Position, float Cost) FindBestSplit(Node node, Vector3[] centroids,
        Vector3[] minimums, Vector3[] maximums)
    {
        var bestAxis = -1;
        var bestPosition = 0f;
        var bestCost = float.MaxValue;
        Span<Vector3> binMin = stackalloc Vector3[BinCount];
        Span<Vector3> binMax = stackalloc Vector3[BinCount];
        Span<int> binCount = stackalloc int[BinCount];
        Span<float> leftArea = stackalloc float[BinCount - 1];
        Span<int> leftCount = stackalloc int[BinCount - 1];

        for (var axis = 0; axis < 3; axis++)
        {
            var low = float.MaxValue;
            var high = float.MinValue;
            for (var i = 0; i < node.Count; i++)
            {
                var value = Component(centroids[_order[node.LeftOrFirst + i]], axis);
                low = MathF.Min(low, value);
                high = MathF.Max(high, value);
            }
            if (high - low < 1e-6f) continue;

            binMin.Fill(new Vector3(float.MaxValue));
            binMax.Fill(new Vector3(float.MinValue));
            binCount.Clear();
            var scale = BinCount / (high - low);
            for (var i = 0; i < node.Count; i++)
            {
                var triangle = _order[node.LeftOrFirst + i];
                var bin = Math.Min(BinCount - 1, (int)((Component(centroids[triangle], axis) - low) * scale));
                binCount[bin]++;
                binMin[bin] = Vector3.Min(binMin[bin], minimums[triangle]);
                binMax[bin] = Vector3.Max(binMax[bin], maximums[triangle]);
            }

            var runningMin = new Vector3(float.MaxValue);
            var runningMax = new Vector3(float.MinValue);
            var runningCount = 0;
            for (var bin = 0; bin < BinCount - 1; bin++)
            {
                runningCount += binCount[bin];
                runningMin = Vector3.Min(runningMin, binMin[bin]);
                runningMax = Vector3.Max(runningMax, binMax[bin]);
                leftCount[bin] = runningCount;
                leftArea[bin] = runningCount == 0 ? 0 : SurfaceArea(runningMin, runningMax);
            }
            runningMin = new Vector3(float.MaxValue);
            runningMax = new Vector3(float.MinValue);
            runningCount = 0;
            for (var bin = BinCount - 1; bin > 0; bin--)
            {
                runningCount += binCount[bin];
                runningMin = Vector3.Min(runningMin, binMin[bin]);
                runningMax = Vector3.Max(runningMax, binMax[bin]);
                var rightArea = runningCount == 0 ? 0 : SurfaceArea(runningMin, runningMax);
                var cost = leftCount[bin - 1] * leftArea[bin - 1] + runningCount * rightArea;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestAxis = axis;
                    bestPosition = low + bin / scale;
                }
            }
        }
        return (bestAxis, bestPosition, bestCost);
    }

    /// <summary>Finds the closest intersection within (minDistance, hit.Distance).</summary>
    public bool Intersect(Vector3 origin, Vector3 direction, float minDistance, ref RayHit hit)
    {
        if (_order.Length == 0) return false;
        var inverse = new Vector3(1 / direction.X, 1 / direction.Y, 1 / direction.Z);
        Span<int> stack = stackalloc int[64];
        var top = 0;
        var nodeIndex = 0;
        var found = false;
        while (true)
        {
            ref readonly var node = ref _nodes[nodeIndex];
            if (node.Count > 0)
            {
                for (var i = 0; i < node.Count; i++)
                {
                    var triangle = _order[node.LeftOrFirst + i];
                    if (IntersectTriangle(triangle, origin, direction, minDistance, ref hit)) found = true;
                }
                if (top == 0) break;
                nodeIndex = stack[--top];
                continue;
            }
            var first = node.LeftOrFirst;
            var second = first + 1;
            var firstDistance = SlabTest(_nodes[first], origin, inverse, hit.Distance);
            var secondDistance = SlabTest(_nodes[second], origin, inverse, hit.Distance);
            if (firstDistance > secondDistance)
            {
                (first, second) = (second, first);
                (firstDistance, secondDistance) = (secondDistance, firstDistance);
            }
            if (firstDistance == float.MaxValue)
            {
                if (top == 0) break;
                nodeIndex = stack[--top];
                continue;
            }
            nodeIndex = first;
            if (secondDistance != float.MaxValue && top < stack.Length) stack[top++] = second;
        }
        return found;
    }

    /// <summary>Returns true when anything blocks the segment (used for shadow rays).</summary>
    public bool Occluded(Vector3 origin, Vector3 direction, float minDistance, float maxDistance)
    {
        var hit = new RayHit { Distance = maxDistance };
        return Intersect(origin, direction, minDistance, ref hit);
    }

    /// <summary>Möller–Trumbore ray/triangle test, two-sided.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IntersectTriangle(int triangle, Vector3 origin, Vector3 direction, float minDistance,
        ref RayHit hit)
    {
        var edge1 = _edge1[triangle];
        var edge2 = _edge2[triangle];
        var p = Vector3.Cross(direction, edge2);
        var determinant = Vector3.Dot(edge1, p);
        if (MathF.Abs(determinant) < 1e-9f) return false;
        var inverseDeterminant = 1 / determinant;
        var t = origin - _vertex0[triangle];
        var u = Vector3.Dot(t, p) * inverseDeterminant;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(t, edge1);
        var v = Vector3.Dot(direction, q) * inverseDeterminant;
        if (v < 0 || u + v > 1) return false;
        var distance = Vector3.Dot(edge2, q) * inverseDeterminant;
        if (distance <= minDistance || distance >= hit.Distance) return false;
        hit.Distance = distance;
        hit.Triangle = triangle;
        hit.U = u;
        hit.V = v;
        return true;
    }

    /// <summary>Returns the entry distance into a node's box, or MaxValue when it is missed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SlabTest(in Node node, Vector3 origin, Vector3 inverse, float maxDistance)
    {
        var t1 = (node.Min - origin) * inverse;
        var t2 = (node.Max - origin) * inverse;
        var near = Vector3.Min(t1, t2);
        var far = Vector3.Max(t1, t2);
        var entry = MathF.Max(MathF.Max(near.X, near.Y), near.Z);
        var exit = MathF.Min(MathF.Min(far.X, far.Y), far.Z);
        return exit >= MathF.Max(entry, 0) && entry < maxDistance ? entry : float.MaxValue;
    }

    private static float SurfaceArea(Vector3 min, Vector3 max)
    {
        var size = max - min;
        return size.X * size.Y + size.Y * size.Z + size.Z * size.X;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Component(Vector3 vector, int axis) =>
        axis == 0 ? vector.X : axis == 1 ? vector.Y : vector.Z;
}
