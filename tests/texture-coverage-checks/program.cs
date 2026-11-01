using Guanacaste3D.Core.Rendering;

/// <summary>Checks filtering against analytic coverage rather than a second implementation of the filter.</summary>
internal static class Program
{
    /// <summary>Fails when edge texels vanish, texture seams disagree, or mask memory is omitted.</summary>
    public static void Main()
    {
        var edge = RenderTexture.FromRgba(3, 1, new byte[]
        { 255, 255, 255, 0, 255, 255, 255, 0, 255, 255, 255, 255 }, 1);
        Require(Math.Abs(edge.Alpha(0.5f, 0.5f) - 1f / 3) < 0.005f, "Odd-sized edge coverage must survive reduction.");
        var pair = RenderTexture.FromRgba(2, 1, new byte[] { 255, 255, 255, 0, 255, 255, 255, 255 });
        Require(pair.Alpha(0.25f, 0.5f) == 0 && pair.Alpha(0.75f, 0.5f) == 1, "Texel centers must retain coverage.");
        Require(Math.Abs(pair.Alpha(0.5f, 0.5f) - 0.5f) < 0.0001f, "Coverage must interpolate across a contour.");
        Require(Math.Abs(pair.Alpha(0, 0.5f) - pair.Alpha(1, 0.5f)) < 0.0001f, "Repeat seam must be continuous.");
        Require(pair.Bytes == 12, "Texture memory must include the alpha mask.");
        Require(pair.Alpha(float.NaN, 0) == 1, "Invalid coordinates must fail opaque.");
        var foliage = RenderTexture.FromRgba(2, 1, new byte[] { 0, 255, 0, 255, 255, 0, 0, 0 });
        var boundary = foliage.Sample(0.5f, 0.5f, 0);
        var distant = foliage.Sample(0.5f, 0.5f, 1);
        Require(boundary.Y > 0.99f && boundary.X < 0.01f, "Invisible pixels must not tint interpolated edges.");
        Require(distant.Y > 0.99f && distant.X < 0.01f && foliage.Mean.Y > 0.99f,
            "Invisible pixels must not tint reduced or indirect foliage color.");
        var reduced = RenderTexture.FromRgba(2, 1, new byte[] { 0, 255, 0, 255, 255, 0, 0, 0 }, 1);
        Require(reduced.Sample(0.5f, 0.5f, 0).Y > 0.99f && reduced.Sample(0.5f, 0.5f, 0).X < 0.01f,
            "Reducing oversized textures must preserve visible color.");
        Console.WriteLine("PASS: nine texture coverage checks.");
    }

    /// <summary>Raises a diagnostic suitable for CI when a coverage invariant fails.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
