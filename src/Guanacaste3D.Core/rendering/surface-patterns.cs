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
                // Four scales of variation, as a mown lawn shows them: wide mowing patches, clumps, tufts and blades.
                // Each one is gentle, so together they read as grass rather than as stains on a green sheet.
                var patches = ValueNoise(u * 0.09f + 3, v * 0.09f + 11);
                var clumps = ValueNoise(u * 0.8f, v * 0.8f);
                var tufts = ValueNoise(u * 3.3f + 7, v * 3.3f - 3);
                var blades = 0.6f * ValueNoise(u * 21f, v * 21f) + 0.4f * ValueNoise(u * 57f + 5, v * 57f + 9);
                var tone = 0.80f + 0.14f * patches + 0.12f * clumps + 0.10f * tufts + 0.22f * (blades - 0.5f);
                // A little straw shows where the lawn is thin and in a few wide, dry areas; never a yellow blotch.
                var dryness = Math.Clamp((ValueNoise(u * 0.05f + 17, v * 0.05f - 5) - 0.6f) * 1.6f, 0f, 0.35f) +
                              Math.Clamp((0.32f - blades) * 0.6f, 0f, 0.12f);
                return Vector3.Lerp(new Vector3(0.90f, 1.05f, 0.82f), new Vector3(1.32f, 1.10f, 0.70f), dryness) * tone;
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
            case SurfacePattern.Foliage:
            {
                // Sunlit leaf clusters turn yellow-green, the gaps between them fall to a deep blue-green.
                var clusters = ValueNoise(u * 6f, v * 6f);
                var leaves = ValueNoise(u * 23f + 4, v * 23f - 7);
                var tone = 0.62f + 0.55f * clusters + 0.30f * (leaves - 0.5f);
                var light = Math.Clamp((clusters - 0.45f) * 1.6f, 0f, 1f);
                return Vector3.Lerp(new Vector3(0.82f, 0.95f, 1.05f), new Vector3(1.22f, 1.12f, 0.78f), light) * tone;
            }
            case SurfacePattern.Bark:
            {
                var furrows = ValueNoise(u * 18f, v * 1.6f);
                var grain = ValueNoise(u * 60f, v * 6f);
                return new Vector3(0.70f + 0.45f * furrows + 0.12f * grain);
            }
            default:
                return Vector3.One;
        }
    }

    /// <summary>
    /// Bends the shading normal by the relief of the pattern, so tile joints, mortar, board gaps, the trowel marks of
    /// plaster and lawn catch grazing sunlight as real surfaces do. The relief is a height field along the normal that
    /// faces the viewer (joints are recessed from whichever side is seen); its slope is found by central differences.
    /// Fine detail fades with <paramref name="distance"/>, where it would only sparkle.
    /// </summary>
    public static Vector3 Relief(SurfacePattern pattern, float scale, Vector3 position, Vector3 normal, float distance)
    {
        if (pattern is SurfacePattern.None or SurfacePattern.Checker) return normal;
        var fade = 1 / (1 + distance * distance * (1f / 900f));   // half strength at 30 m
        var (u, v) = PlaneCoordinates(position, normal);
        const float step = 0.0006f;
        var du = (Height(pattern, scale, u + step, v, fade) - Height(pattern, scale, u - step, v, fade)) / (2 * step);
        var dv = (Height(pattern, scale, u, v + step, fade) - Height(pattern, scale, u, v - step, fade)) / (2 * step);
        if (du == 0 && dv == 0) return normal;
        var (tangentU, tangentV) = PlaneTangents(normal);
        tangentU -= normal * Vector3.Dot(normal, tangentU);
        tangentV -= normal * Vector3.Dot(normal, tangentV);
        var bent = normal - tangentU * du - tangentV * dv;
        var length = bent.Length();
        return length > 1e-6f ? bent / length : normal;
    }

    /// <summary>Height of the relief in meters (negative is recessed) at plane coordinates in meters.</summary>
    private static float Height(SurfacePattern pattern, float scale, float worldU, float worldV, float fade)
    {
        var u = worldU / scale;
        var v = worldV / scale;
        switch (pattern)
        {
            case SurfacePattern.Tiles:
            {
                const float tile = 0.6f;
                var cu = u / tile;
                var cv = v / tile;
                var fu = cu - MathF.Floor(cu);
                var fv = cv - MathF.Floor(cv);
                var edge = MathF.Min(MathF.Min(fu, 1 - fu), MathF.Min(fv, 1 - fv)) * tile * scale;
                // A 1.5 mm grout line with eased tile edges, and the slight unevenness of tiles laid by hand.
                var lippage = (Hash01((int)MathF.Floor(cu), (int)MathF.Floor(cv)) - 0.5f) * 0.0004f;
                return -0.0015f * (1 - SmoothStep(0.0008f, 0.005f, edge)) + lippage * fu;
            }
            case SurfacePattern.Planks:
            {
                const float board = 0.14f;
                var row = MathF.Floor(v / board);
                var fv = v / board - row;
                var gap = MathF.Min(fv, 1 - fv) * board * scale;
                var cu = u + Hash01((int)row, 3) * 2.4f;
                var grain = ValueNoise(cu * 14f, v * 40f) * 0.00015f * fade;
                return -0.001f * (1 - SmoothStep(0.0004f, 0.0025f, gap)) + grain;
            }
            case SurfacePattern.Blocks:
            {
                const float courseHeight = 0.2f;
                const float blockLength = 0.4f;
                var course = MathF.Floor(v / courseHeight);
                var shifted = u + ((int)course & 1) * blockLength * 0.5f;
                var fu = shifted / blockLength - MathF.Floor(shifted / blockLength);
                var fv = v / courseHeight - course;
                var edge = MathF.Min(MathF.Min(fu * blockLength, (1 - fu) * blockLength),
                    MathF.Min(fv * courseHeight, (1 - fv) * courseHeight)) * scale;
                var face = ValueNoise(worldU * 30f, worldV * 30f) * 0.0006f * fade;
                return -0.004f * (1 - SmoothStep(0.001f, 0.009f, edge)) + face;
            }
            case SurfacePattern.Concrete:
            {
                // Trowelled plaster: soft undulation plus fine sand grain, both a fraction of a millimetre deep.
                var trowel = ValueNoise(worldU * 9f, worldV * 9f) * 0.0006f;
                var sand = (ValueNoise(worldU * 140f, worldV * 140f) - 0.5f) * 0.00012f;
                return (trowel + sand) * fade;
            }
            case SurfacePattern.Foliage:
            {
                // Clumps of leaves a few centimeters proud of the canopy: the sun picks out each cluster.
                var clumps = ValueNoise(worldU * 6f, worldV * 6f) * 0.03f;
                var leaves = ValueNoise(worldU * 23f, worldV * 23f) * 0.008f * fade;
                return clumps + leaves;
            }
            case SurfacePattern.Bark:
                return ValueNoise(worldU * 18f, worldV * 1.6f) * 0.012f + ValueNoise(worldU * 60f, worldV * 6f) * 0.002f * fade;
            case SurfacePattern.Grass:
            {
                var blades = ValueNoise(worldU * 26f, worldV * 26f) * 0.004f + ValueNoise(worldU * 70f, worldV * 70f) * 0.0015f;
                var tufts = ValueNoise(worldU * 3.3f, worldV * 3.3f) * 0.01f;
                return blades * fade + tufts;
            }
            default:
                return 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SmoothStep(float low, float high, float value)
    {
        var t = Math.Clamp((value - low) / (high - low), 0f, 1f);
        return t * t * (3 - 2 * t);
    }

    /// <summary>World directions of increasing u and v for the plane chosen by <see cref="PlaneCoordinates"/>.</summary>
    private static (Vector3 U, Vector3 V) PlaneTangents(Vector3 normal)
    {
        var ax = MathF.Abs(normal.X);
        var ay = MathF.Abs(normal.Y);
        var az = MathF.Abs(normal.Z);
        if (az >= ax && az >= ay) return (Vector3.UnitX, Vector3.UnitY);
        return ax >= ay ? (Vector3.UnitY, Vector3.UnitZ) : (Vector3.UnitX, Vector3.UnitZ);
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
