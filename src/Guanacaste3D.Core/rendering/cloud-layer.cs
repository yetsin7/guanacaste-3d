using System.Numerics;
using System.Runtime.CompilerServices;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// A procedural layer of fair-weather cumulus about 1.5 km up. A sky ray is followed to the layer, where warped fractal
/// noise decides how much cloud it meets; coverage moves the threshold, so 0 is a clear sky and 1 nearly overcast.
/// The clouds are lit by the sun (bright tops, a silver lining toward the sun, greyer where they are thick) and by the
/// sky, and they thin out toward the horizon, where the layer would be too far away to resolve.
/// </summary>
public sealed class CloudLayer
{
    private const float AltitudeKm = 1.5f;
    private readonly float _coverage;
    private readonly Vector2 _offset;

    /// <param name="coverage">Share of the sky covered, 0–1.</param>
    /// <param name="seed">Chooses the cloud pattern; the same seed always gives the same sky.</param>
    public CloudLayer(float coverage, uint seed = 0)
    {
        _coverage = float.IsFinite(coverage) ? Math.Clamp(coverage, 0f, 1f) : 0f;
        _offset = new Vector2(seed % 977 * 3.17f, seed % 613 * 5.31f);
    }

    /// <summary>Whether there are any clouds at all.</summary>
    public bool IsClear => _coverage <= 0.001f;

    /// <summary>
    /// Returns how much a sky ray in <paramref name="direction"/> is covered (0–1) and the thickness of the cloud it
    /// meets (0–1); zero for rays at or below the horizon.
    /// </summary>
    public (float Opacity, float Thickness) Sample(Vector3 direction)
    {
        if (IsClear || direction.Z <= 0.015f) return (0, 0);
        var distance = AltitudeKm / direction.Z;
        var point = new Vector2(direction.X, direction.Y) * distance + _offset;
        // Domain warping turns round noise blobs into the cauliflower outlines of cumulus.
        var warp = new Vector2(Fbm(point * 0.35f + new Vector2(5.2f, 1.3f), 3), Fbm(point * 0.35f + new Vector2(-3.7f, 8.1f), 3));
        var density = Fbm(point * 0.55f + warp * 1.6f, 5);
        var threshold = 0.78f - 0.48f * _coverage;
        var thickness = Math.Clamp((density - threshold) / 0.22f, 0f, 1f);
        var opacity = thickness * (2 - thickness);   // soft edges, solid cores
        // Far away the layer is seen edge-on and its detail is smaller than a pixel: let it fade into the haze.
        var horizonFade = SmoothStep(0.015f, 0.16f, direction.Z);
        return (opacity * horizonFade, thickness);
    }

    /// <summary>
    /// Radiance of the cloud seen in <paramref name="direction"/>, given the light that reaches it: the sun's
    /// irradiance over π (<paramref name="sunlight"/>), the direction to the sun and the sky's zenith radiance.
    /// </summary>
    public static Vector3 Shade(Vector3 direction, float thickness, Vector3 sunDirection, Vector3 sunlight, Vector3 skylight)
    {
        var towardSun = Math.Clamp(Vector3.Dot(direction, sunDirection), -1f, 1f);
        // Thin edges glow when the sun is behind them; thick bodies scatter light every way and darken underneath.
        var silver = MathF.Pow(MathF.Max(0, towardSun), 8) * (1 - thickness) * 1.2f;
        var body = 0.62f + 0.18f * (towardSun * 0.5f + 0.5f);
        var selfShadow = 1 - 0.42f * thickness;
        return sunlight * (0.78f * (body * selfShadow + silver)) + skylight * (1.6f + 0.6f * (1 - thickness));
    }

    private static float Fbm(Vector2 point, int octaves)
    {
        var sum = 0f;
        var amplitude = 0.5f;
        var total = 0f;
        for (var octave = 0; octave < octaves; octave++)
        {
            sum += amplitude * Noise(point);
            total += amplitude;
            // Rotate between octaves so the grid of the value noise never lines up.
            point = new Vector2(point.X * 1.6f - point.Y * 1.2f, point.X * 1.2f + point.Y * 1.6f) + new Vector2(1.7f, 9.2f);
            amplitude *= 0.5f;
        }
        return sum / total;
    }

    private static float Noise(Vector2 point)
    {
        var ix = (int)MathF.Floor(point.X);
        var iy = (int)MathF.Floor(point.Y);
        var fx = point.X - ix;
        var fy = point.Y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        var a = Hash(ix, iy);
        var b = Hash(ix + 1, iy);
        var c = Hash(ix, iy + 1);
        var d = Hash(ix + 1, iy + 1);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Hash(int x, int y)
    {
        var h = (uint)x * 374761393u + (uint)y * 668265263u;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) * (1f / 16777216f);
    }

    private static float SmoothStep(float low, float high, float value)
    {
        var t = Math.Clamp((value - low) / (high - low), 0f, 1f);
        return t * t * (3 - 2 * t);
    }
}
