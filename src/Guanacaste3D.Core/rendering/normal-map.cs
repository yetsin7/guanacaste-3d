using System.Numerics;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// A tangent-space normal map (OpenGL convention: green points along increasing v): the fine relief of bark, leaf veins
/// and grass blades that the geometry is too coarse to carry. Its values are directions, not colours, so they are read
/// raw, without the sRGB curve, and bilinearly from the full-resolution image; the path tracer's many samples per pixel
/// average away the detail that a distant surface cannot resolve.
/// </summary>
public sealed class NormalMap
{
    private readonly byte[] _rgb;

    private NormalMap(int width, int height, byte[] rgb)
    {
        Width = width;
        Height = height;
        _rgb = rgb;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Builds a normal map from RGB pixels (rows top to bottom).</summary>
    public static NormalMap FromRgb(int width, int height, byte[] rgb)
    {
        if (width < 1 || height < 1 || rgb.Length < width * height * 3)
            throw new ArgumentException("The normal map pixels do not match its size.");
        return new NormalMap(width, height, rgb);
    }

    /// <summary>The tangent-space direction at texture coordinates (wrapping, v up as in <see cref="RenderTexture"/>).</summary>
    public Vector3 Sample(float u, float v)
    {
        var x = (u - MathF.Floor(u)) * Width - 0.5f;
        var y = (1 - (v - MathF.Floor(v))) * Height - 0.5f;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var (fx, fy) = (x - x0, y - y0);
        var top = Vector3.Lerp(Texel(x0, y0), Texel(x0 + 1, y0), fx);
        var bottom = Vector3.Lerp(Texel(x0, y0 + 1), Texel(x0 + 1, y0 + 1), fx);
        var value = Vector3.Lerp(top, bottom, fy) * (2f / 255) - Vector3.One;
        return value.LengthSquared() > 1e-8f ? Vector3.Normalize(value) : Vector3.UnitZ;
    }

    private Vector3 Texel(int x, int y)
    {
        x = ((x % Width) + Width) % Width;
        y = ((y % Height) + Height) % Height;
        var index = (y * Width + x) * 3;
        return new Vector3(_rgb[index], _rgb[index + 1], _rgb[index + 2]);
    }

    /// <summary>
    /// Bends a shading normal by the map, given the triangle's corners and their texture coordinates (v up): the
    /// tangent follows increasing u and the bitangent increasing v, made perpendicular to the normal.
    /// </summary>
    public static Vector3 Perturb(Vector3 normal, Vector3 sample, Vector3 p0, Vector3 p1, Vector3 p2, Vector2 t0, Vector2 t1, Vector2 t2,
        float strength = 1)
    {
        var (edge1, edge2) = (p1 - p0, p2 - p0);
        var (duv1, duv2) = (t1 - t0, t2 - t0);
        var determinant = duv1.X * duv2.Y - duv2.X * duv1.Y;
        if (MathF.Abs(determinant) < 1e-12f) return normal;
        var tangent = (edge1 * duv2.Y - edge2 * duv1.Y) / determinant;
        var bitangent = (edge2 * duv1.X - edge1 * duv2.X) / determinant;
        tangent -= normal * Vector3.Dot(normal, tangent);
        if (tangent.LengthSquared() < 1e-12f) return normal;
        tangent = Vector3.Normalize(tangent);
        var side = Vector3.Cross(normal, tangent);
        // Mirrored texture coordinates flip the bitangent; follow them.
        if (Vector3.Dot(side, bitangent) < 0) side = -side;
        var bent = tangent * (sample.X * strength) + side * (sample.Y * strength) + normal * sample.Z;
        return bent.LengthSquared() > 1e-12f ? Vector3.Normalize(bent) : normal;
    }
}
