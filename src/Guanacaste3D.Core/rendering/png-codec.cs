using System.IO.Compression;
using System.Numerics;

namespace Guanacaste3D.Core.Rendering;

/// <summary>An 8-bit RGB image decoded from or destined for a PNG file; rows run top to bottom.</summary>
public sealed record RasterImage(int Width, int Height, byte[] Rgb)
{
    /// <summary>Returns display-referred pixels in [0, 1] (gamma-encoded, as stored in the file).</summary>
    public Vector3[] ToDisplay()
    {
        var pixels = new Vector3[Width * Height];
        for (var index = 0; index < pixels.Length; index++)
            pixels[index] = new Vector3(Rgb[index * 3], Rgb[index * 3 + 1], Rgb[index * 3 + 2]) / 255f;
        return pixels;
    }

    /// <summary>Converts the renderer's BGRA32 output (alpha ignored) into an RGB image.</summary>
    public static RasterImage FromBgra32(int width, int height, ReadOnlySpan<byte> bgra)
    {
        if (bgra.Length < width * height * 4) throw new ArgumentException("Pixel buffer is too small.");
        var rgb = new byte[width * height * 3];
        for (var index = 0; index < width * height; index++)
        {
            rgb[index * 3] = bgra[index * 4 + 2];
            rgb[index * 3 + 1] = bgra[index * 4 + 1];
            rgb[index * 3 + 2] = bgra[index * 4];
        }
        return new RasterImage(width, height, rgb);
    }
}

/// <summary>
/// Minimal PNG reader and writer for 8-bit images (no interlacing). It exists so renders, regression
/// references and procedural textures can be stored and compared without any external library.
/// </summary>
public static class PngCodec
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Largest image dimension decoded, which bounds memory for hostile or mistaken files.</summary>
    public const int MaximumDimension = 16384;

    /// <summary>Encodes an RGB image using the "Up" scanline filter and zlib compression.</summary>
    public static byte[] Encode(RasterImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Rgb.Length != image.Width * image.Height * 3) throw new ArgumentException("Pixel buffer size is wrong.");
        var stride = image.Width * 3;
        var raw = new byte[(stride + 1) * image.Height];
        for (var row = 0; row < image.Height; row++)
        {
            raw[row * (stride + 1)] = 2;
            for (var i = 0; i < stride; i++)
            {
                var above = row == 0 ? 0 : image.Rgb[(row - 1) * stride + i];
                raw[row * (stride + 1) + 1 + i] = (byte)(image.Rgb[row * stride + i] - above);
            }
        }
        using var output = new MemoryStream();
        output.Write(Signature);
        var header = new byte[13];
        WriteBigEndian(header, 0, (uint)image.Width);
        WriteBigEndian(header, 4, (uint)image.Height);
        header[8] = 8;
        header[9] = 2; // truecolor
        WriteChunk(output, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw);
            WriteChunk(output, "IDAT", compressed.ToArray());
        }
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    /// <summary>Writes an RGB image to a PNG file.</summary>
    public static void Save(string path, RasterImage image) => File.WriteAllBytes(path, Encode(image));

    /// <summary>Decodes 8-bit grayscale, RGB, or RGBA PNG data; alpha is dropped.</summary>
    public static RasterImage Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 33 || !data[..8].SequenceEqual(Signature)) throw new InvalidDataException("The image is not a PNG file.");
        int width = 0, height = 0, channels = 0;
        using var compressed = new MemoryStream();
        var position = 8;
        var sawEnd = false;
        while (position + 12 <= data.Length && !sawEnd)
        {
            var length = (int)ReadBigEndian(data, position);
            var type = System.Text.Encoding.ASCII.GetString(data.Slice(position + 4, 4));
            if (length < 0 || position + 12 + length > data.Length) throw new InvalidDataException("The PNG file is truncated.");
            var body = data.Slice(position + 8, length);
            if (ReadBigEndian(data, position + 8 + length) != Crc(data.Slice(position + 4, 4 + length)))
                throw new InvalidDataException("The PNG file is corrupt.");
            switch (type)
            {
                case "IHDR":
                    width = (int)ReadBigEndian(body, 0);
                    height = (int)ReadBigEndian(body, 4);
                    if (body[8] != 8 || body[12] != 0) throw new InvalidDataException("Only 8-bit non-interlaced PNG images are supported.");
                    channels = body[9] switch { 0 => 1, 2 => 3, 6 => 4, _ => throw new InvalidDataException("This PNG color type is not supported.") };
                    if (width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension)
                        throw new InvalidDataException("The PNG dimensions are out of range.");
                    break;
                case "IDAT":
                    compressed.Write(body);
                    break;
                case "IEND":
                    sawEnd = true;
                    break;
            }
            position += 12 + length;
        }
        if (width == 0 || compressed.Length == 0) throw new InvalidDataException("The PNG file has no image data.");

        var stride = width * channels;
        var raw = new byte[(stride + 1) * height];
        compressed.Position = 0;
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            var read = 0;
            while (read < raw.Length)
            {
                var count = zlib.Read(raw, read, raw.Length - read);
                if (count == 0) throw new InvalidDataException("The PNG image data is incomplete.");
                read += count;
            }
        }

        var pixels = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            var filter = raw[row * (stride + 1)];
            for (var i = 0; i < stride; i++)
            {
                int value = raw[row * (stride + 1) + 1 + i];
                int left = i >= channels ? pixels[row * stride + i - channels] : 0;
                int up = row > 0 ? pixels[(row - 1) * stride + i] : 0;
                int upLeft = row > 0 && i >= channels ? pixels[(row - 1) * stride + i - channels] : 0;
                pixels[row * stride + i] = (byte)(filter switch
                {
                    0 => value,
                    1 => value + left,
                    2 => value + up,
                    3 => value + ((left + up) >> 1),
                    4 => value + Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException("The PNG uses an unknown scanline filter.")
                });
            }
        }
        if (channels == 3) return new RasterImage(width, height, pixels);
        var rgb = new byte[width * height * 3];
        for (var index = 0; index < width * height; index++)
            for (var channel = 0; channel < 3; channel++)
                rgb[index * 3 + channel] = pixels[index * channels + (channels == 1 ? 0 : channel)];
        return new RasterImage(width, height, rgb);
    }

    /// <summary>Reads a PNG file.</summary>
    public static RasterImage Load(string path) => Decode(File.ReadAllBytes(path));

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, string type, byte[] body)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, (uint)body.Length);
        output.Write(length);
        var typed = new byte[4 + body.Length];
        System.Text.Encoding.ASCII.GetBytes(type, typed);
        body.CopyTo(typed, 4);
        output.Write(typed);
        var crc = new byte[4];
        WriteBigEndian(crc, 0, Crc(typed));
        output.Write(crc);
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint ReadBigEndian(ReadOnlySpan<byte> data, int offset) =>
        (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);

    private static void WriteBigEndian(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
