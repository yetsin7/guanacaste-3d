using System.Numerics;
using System.Runtime.CompilerServices;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// Procedural surface detail (tile joints, boards, masonry, lawn breakup) evaluated from world position and the
/// face normal. It returns a multiplier for the diffuse color, so the same code serves BIM surfaces and imported
/// meshes that have no texture coordinates. Patterns are sampled once per path vertex, so the jittered samples
/// of a pixel antialias them naturally.
/// </summary>
internal static class SurfacePatterns
{
    /// <summary>Returns the diffuse multiplier at <paramref name="position"/> on a face with the given normal.</summary>
    public static Vector3 Modulate(SurfacePattern pattern, float scale, Vector3 position, Vector3 normal)
    {
        var (u, v) = PlaneCoordinates(position, normal);
        u /= scale;
        v /= scale;
        switch (pattern)
        {
            case SurfacePattern.Checker:
                return (((int)MathF.Floor(u) + (int)MathF.Floor(v)) & 1) == 0 ? new Vector3(1.03f) : new Vector3(0.95f);
            case SurfacePattern.Grass:
            {
                var broad = ValueNoise(u * 0.35f, v * 0.35f);
                var fine = ValueNoise(u * 3.1f, v * 3.1f);
                var tone = 0.82f + 0.30f * broad + 0.14f * fine;
                // Patches drift toward dry brown, which reads as natural ground rather than a flat green sheet.
                var dry = Math.Clamp((ValueNoise(u * 0.12f + 17, v * 0.12f - 5) - 0.55f) * 2.2f, 0f, 1f);
                return Vector3.Lerp(new Vector3(tone), new Vector3(tone * 1.28f, tone * 1.04f, tone * 0.72f), dry);
            }
            case SurfacePattern.Tiles:
            {
                const float tile = 0.6f;
                var cu = u / tile;
                var cv = v / tile;
                var fu = cu - MathF.Floor(cu);
                var fv = cv - MathF.Floor(cv);
                var edge = MathF.Min(MathF.Min(fu, 1 - fu), MathF.Min(fv, 1 - fv)) * tile * scale;
                var tone = 0.96f + 0.08f * Hash01((int)MathF.Floor(cu), (int)MathF.Floor(cv));
                var joint = Math.Clamp(edge / 0.004f, 0f, 1f);
                return new Vector3(tone * (0.55f + 0.45f * joint));
            }
            case SurfacePattern.Planks:
            {
                const float board = 0.14f;
                var row = MathF.Floor(v / board);
                var fv = v / board - row;
                var cu = u + Hash01((int)row, 3) * 2.4f;
                var grain = 0.92f + 0.10f * ValueNoise(cu * 1.7f, row * 5.3f) + 0.05f * ValueNoise(cu * 14f, v * 40f);
                var gap = Math.Clamp(MathF.Min(fv, 1 - fv) * board * scale / 0.0015f, 0f, 1f);
                return new Vector3(grain * (0.45f + 0.55f * gap) * (0.94f + 0.12f * Hash01((int)row, 9)));
            }
            case SurfacePattern.Blocks:
            {
                const float courseHeight = 0.2f;
                const float blockLength = 0.4f;
                var course = MathF.Floor(v / courseHeight);
                var shifted = u + ((int)course & 1) * blockLength * 0.5f;
                var fu = shifted / blockLength - MathF.Floor(shifted / blockLength);
                var fv = v / courseHeight - course;
                var edge = MathF.Min(MathF.Min(fu * blockLength, (1 - fu) * blockLength), MathF.Min(fv * courseHeight, (1 - fv) * courseHeight)) * scale;
                var tone = 0.93f + 0.10f * Hash01((int)MathF.Floor(shifted / blockLength), (int)course);
                return new Vector3(tone * (0.6f + 0.4f * Math.Clamp(edge / 0.006f, 0f, 1f)));
            }
            case SurfacePattern.Concrete:
            {
                var blotch = ValueNoise(u * 0.9f, v * 0.9f);
                var grain = ValueNoise(u * 22f, v * 22f);
                return new Vector3(0.94f + 0.07f * blotch + 0.04f * grain);
            }
            default:
                return Vector3.One;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(SurfacePattern, float), float> Means = new();

    /// <summary>
    /// Mean luminance of the multiplier over many pattern periods. It stands in for the pattern where its detail
    /// cannot be seen, so the average brightness of a surface is the same with or without the noise function.
    /// </summary>
    public static float Average(SurfacePattern pattern, float scale)
    {
        if (pattern == SurfacePattern.None) return 1;
        return Means.GetOrAdd((pattern, scale), key =>
        {
            const int steps = 96;
            var span = 48f * key.Item2;
            double sum = 0;
            for (var j = 0; j < steps; j++)
                for (var i = 0; i < steps; i++)
                {
                    // Irrational strides decorrelate the grid from the pattern's own periods.
                    var position = new Vector3((i + 0.37f * j) * span / steps, (j + 0.61f * i) * span / steps, 0);
                    sum += Sampling.Luminance(Modulate(key.Item1, key.Item2, position, Vector3.UnitZ));
                }
            return (float)(sum / (steps * steps));
        });
    }

    /// <summary>Projects a point onto the plane best aligned with the face: floors use (x, y), walls use (along, height).</summary>
    private static (float U, float V) PlaneCoordinates(Vector3 position, Vector3 normal)
    {
        var ax = MathF.Abs(normal.X);
        var ay = MathF.Abs(normal.Y);
        var az = MathF.Abs(normal.Z);
        if (az >= ax && az >= ay) return (position.X, position.Y);
        return ax >= ay ? (position.Y, position.Z) : (position.X, position.Z);
    }

    /// <summary>Smooth 2D value noise in [0, 1].</summary>
    private static float ValueNoise(float x, float y)
    {
        var ix = (int)MathF.Floor(x);
        var iy = (int)MathF.Floor(y);
        var fx = x - ix;
        var fy = y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        var a = Hash01(ix, iy);
        var b = Hash01(ix + 1, iy);
        var c = Hash01(ix, iy + 1);
        var d = Hash01(ix + 1, iy + 1);
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
    }

    /// <summary>Deterministic hash of an integer cell to [0, 1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Hash01(int x, int y)
    {
        var h = (uint)x * 374761393u + (uint)y * 668265263u;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) * (1f / 16777216f);
    }
}
