namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// A linear 8-bit scalar texture for physically based roughness and metalness.
/// Unlike color images, scalar values are never transformed from sRGB.
/// Filtered mip levels avoid moire and material flicker on distant surfaces.
/// </summary>
public sealed class RenderScalarMap
{
    private readonly byte[][] _levels;
    private readonly int[] _widths;
    private readonly int[] _heights;

    private RenderScalarMap(byte[][] levels, int[] widths, int[] heights)
    {
        _levels = levels;
        _widths = widths;
        _heights = heights;
    }

    public int Width => _widths[0];
    public int Height => _heights[0];
    public int LevelCount => _levels.Length;

    public static RenderScalarMap FromGray(int width, int height, byte[] gray)
    {
        ArgumentNullException.ThrowIfNull(gray);
        if (width < 1 || height < 1 || (long)width * height > gray.Length)
            throw new ArgumentException("Scalar texture pixels do not match its dimensions.");
        var levels = new List<byte[]> { gray.AsSpan(0, width * height).ToArray() };
        var widths = new List<int> { width };
        var heights = new List<int> { height };
        while (width > 1 || height > 1)
        {
            var nextWidth = (width + 1) / 2;
            var nextHeight = (height + 1) / 2;
            var down = new byte[nextWidth * nextHeight];
            var source = levels[^1];
            for (var y = 0; y < nextHeight; y++)
                for (var x = 0; x < nextWidth; x++)
                {
                    var sum = 0;
                    var count = 0;
                    for (var yy = 2 * y; yy < Math.Min(height, 2 * y + 2); yy++)
                        for (var xx = 2 * x; xx < Math.Min(width, 2 * x + 2); xx++)
                        {
                            sum += source[yy * width + xx];
                            count++;
                        }
                    down[y * nextWidth + x] = (byte)((sum + count / 2) / count);
                }
            levels.Add(down);
            widths.Add(nextWidth);
            heights.Add(nextHeight);
            width = nextWidth;
            height = nextHeight;
        }
        return new RenderScalarMap([..levels], [..widths], [..heights]);
    }

    /// <summary>Samples a wrapping UV tile (v increases upward) with pixel footprint in tiles.</summary>
    public float Sample(float u, float v, float footprint)
    {
        if (!float.IsFinite(u) || !float.IsFinite(v)) return 0.5f;
        var level = float.IsFinite(footprint) && footprint > 0
            ? MathF.Log2(MathF.Max(footprint * Math.Max(Width, Height), 1e-6f)) : 0f;
        if (level <= 0) return Bilinear(0, u, v);
        var last = _levels.Length - 1;
        if (level >= last) return Bilinear(last, u, v);
        var low = (int)level;
        return MathF.Lerp(Bilinear(low, u, v), Bilinear(low + 1, u, v), level - low);
    }

    private float Bilinear(int level, float u, float v)
    {
        var width = _widths[level];
        var height = _heights[level];
        var x = (u - MathF.Floor(u)) * width - .5f;
        var y = (1 - (v - MathF.Floor(v))) * height - .5f;
        var left = (int)MathF.Floor(x);
        var top = (int)MathF.Floor(y);
        var fx = x - left;
        var fy = y - top;
        var a = Texel(level, left, top);
        var b = Texel(level, left + 1, top);
        var c = Texel(level, left, top + 1);
        var d = Texel(level, left + 1, top + 1);
        return ((a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy);
    }

    private float Texel(int level, int x, int y)
    {
        var width = _widths[level];
        var height = _heights[level];
        x = (x % width + width) % width;
        y = (y % height + height) % height;
        return _levels[level][y * width + x] / 255f;
    }
}
