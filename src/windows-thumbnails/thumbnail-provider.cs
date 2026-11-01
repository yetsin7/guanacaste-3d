using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Guanacaste3D.Thumbnails;

/// <summary>Windows stream initialization contract; paths and external references are never followed.</summary>
[ComVisible(true), Guid("B824B49D-22AC-4161-AC8A-9916E8FA3F7F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IInitializeWithStream
{
    /// <summary>Copies a bounded read-only snapshot supplied by the Shell.</summary>
    void Initialize(IStream stream, uint accessMode);
}

/// <summary>Windows thumbnail contract with bitmap ownership transferred to the caller.</summary>
[ComVisible(true), Guid("E357FCCD-A995-4576-B01F-234630154E96"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IThumbnailProvider
{
    /// <summary>Returns an opaque GDI bitmap no larger than the requested edge.</summary>
    void GetThumbnail(uint size, out IntPtr bitmap, out uint alphaType);
}

/// <summary>Generates native document previews for Windows Shell thumbnail clients.</summary>
[ComVisible(true), Guid(ClassId), ClassInterface(ClassInterfaceType.None)]
public sealed class ThumbnailProvider : IInitializeWithStream, IThumbnailProvider
{
    public const string ClassId = "2175B67E-0798-4A06-B104-4F1B6E9C42D8";
    private byte[]? _snapshot;

    /// <summary>Allows COM activation without starting the desktop application.</summary>
    public ThumbnailProvider() { }

    /// <summary>Reads at most 256 MiB plus the native header and rejects repeated initialization.</summary>
    public void Initialize(IStream stream, uint accessMode)
    {
        if (_snapshot is not null) throw new COMException("The provider is already initialized.", unchecked((int)0x800704DF));
        stream.Stat(out var statistics, 1);
        if (statistics.cbSize is < 1 or > 268435537)
            throw new InvalidDataException("The thumbnail input size is invalid.");
        stream.Seek(0, 0, IntPtr.Zero);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        var countPointer = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
            while (output.Length < statistics.cbSize)
            {
                var requested = (int)Math.Min(buffer.Length, statistics.cbSize - output.Length);
                stream.Read(buffer, requested, countPointer);
                var count = Marshal.ReadInt32(countPointer);
                if (count <= 0 || count > requested) throw new EndOfStreamException("The thumbnail stream is truncated.");
                output.Write(buffer, 0, count);
            }
            _snapshot = output.ToArray();
        }
        finally { Marshal.FreeCoTaskMem(countPointer); }
    }

    /// <summary>Renders on an STA thread because WPF drawing cannot run on a Shell MTA worker.</summary>
    public void GetThumbnail(uint size, out IntPtr bitmap, out uint alphaType)
    {
        bitmap = IntPtr.Zero;
        alphaType = 0;
        if (_snapshot is null) throw new InvalidOperationException("The thumbnail provider has not been initialized.");
        if (size == 0) throw new ArgumentOutOfRangeException(nameof(size));
        IntPtr rendered = IntPtr.Zero;
        Exception? failure = null;
        var snapshot = _snapshot;
        var thread = new Thread(() =>
        {
            try { rendered = ThumbnailBitmap.Create(DocumentPreview.Render(snapshot, (int)Math.Min(size, 1024))); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new COMException("The native document thumbnail could not be generated.", failure);
        bitmap = rendered;
        alphaType = 1;
    }
}
