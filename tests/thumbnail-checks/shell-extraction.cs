using System.Runtime.InteropServices;

namespace Guanacaste3D.ThumbnailChecks;

/// <summary>Checks actual Windows Shell extraction with icon fallback explicitly disabled.</summary>
internal static class ShellExtraction
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ImageSize { public int Width; public int Height; }

    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        /// <summary>Requests a thumbnail-only image, failing when Windows can only supply a file-type icon.</summary>
        void GetImage(ImageSize size, uint flags, out IntPtr bitmap);
    }

    /// <summary>Requests extraction through Windows and releases both COM and GDI resources.</summary>
    public static byte[] Check(string path)
    {
        var identifier = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(path, IntPtr.Zero, ref identifier, out var factory);
        try
        {
            factory.GetImage(new ImageSize { Width = 256, Height = 256 }, 0x8, out var bitmap);
            if (bitmap == IntPtr.Zero) throw new InvalidOperationException("Windows returned no document thumbnail.");
            try
            {
                var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero,
                    System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                var stride = (image.PixelWidth * image.Format.BitsPerPixel + 7) / 8;
                var pixels = new byte[stride * image.PixelHeight];
                image.CopyPixels(pixels, stride, 0);
                Console.WriteLine($"PASS: Windows thumbnail-only extraction {System.IO.Path.GetFileName(path)}");
                return System.Security.Cryptography.SHA256.HashData(pixels);
            }
            finally { DeleteObject(bitmap); }
        }
        finally { Marshal.ReleaseComObject(factory); }
    }

    /// <summary>Opens a Shell item using the installed per-user file association.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr binding, ref Guid identifier,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    /// <summary>Releases each caller-owned thumbnail immediately after the assertion.</summary>
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr bitmap);
}
