using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Thumbnails;

namespace Guanacaste3D.ThumbnailChecks;

/// <summary>Exercises real native files, bounded parsing, rendering, and optional installed Shell extraction.</summary>
internal static class ThumbnailChecks
{
    /// <summary>Creates reviewable fixtures and fails the process on any invalid preview or accepted malformed input.</summary>
    [STAThread]
    private static int Main(string[] arguments)
    {
        var directory = Path.GetFullPath(arguments.FirstOrDefault(argument => !argument.StartsWith("--")) ?? "artifacts/thumbnail-checks");
        Directory.CreateDirectory(directory);
        var project = ArchitecturalShowcase.Create();
        var projectPath = Path.Combine(directory, "project.g3d");
        NativeDocumentStore.SaveProject(projectPath, project);
        var templatePath = Path.Combine(directory, "template.g3dt");
        NativeDocumentStore.SaveTemplate(templatePath, ProjectTemplateDocument.CreateMetricArchitecture());
        var familyPath = Path.Combine(directory, "sheet-family.g3df");
        NativeDocumentStore.SaveFamily(familyPath, SheetFamilies.Create(SheetPaperSize.A1));
        var operationPath = Path.Combine(directory, "operation-family.g3df");
        NativeDocumentStore.SaveFamily(operationPath, BuiltInFamilyLibrary.CreateArchitecturalDoor());
        var legacyFamily = Path.Combine(directory, "opening-family.g3df");
        FamilyFile.Save(legacyFamily, OpeningFamilyDocument.CreateStandard(OpeningKind.Window, "Thumbnail window"));
        var legacyTemplate = Path.Combine(directory, "legacy-template.g3dt");
        TemplateFile.Save(legacyTemplate, project);
        var solidPath = Path.Combine(directory, "solid-family.g3df");
        var solid = CreateSolidFamily();
        NativeDocumentStore.SaveFamily(solidPath, solid);
        var meshPath = Path.Combine(directory, "mesh-family.g3df");
        var geometry = FamilyGeometry.Tessellate(solid);
        solid.Mesh = FamilyMesh.Create(geometry.Positions, geometry.Indices, geometry.TriangleKinds);
        solid.Operations.Clear();
        NativeDocumentStore.SaveFamily(meshPath, solid);
        Require(NativeDocumentStore.LoadProject(projectPath).Id == project.Id, "Project round-trip failed.");
        Require(NativeDocumentStore.LoadFamily(meshPath).Mesh is not null, "Mesh family round-trip failed.");
        Require(NativeDocumentStore.LoadTemplate(templatePath).Levels.Count > 0, "Template round-trip failed.");
        var fixtures = new[] { projectPath, templatePath, familyPath, operationPath, legacyFamily, legacyTemplate, solidPath, meshPath };
        foreach (var path in fixtures)
        {
            var snapshot = File.ReadAllBytes(path);
            foreach (var size in new[] { 32, 96, 256, 512 })
            {
                var image = DocumentPreview.Render(snapshot, size);
                Require(image.PixelWidth == size && image.PixelHeight == size, "The preview does not respect the requested resolution.");
                var pixels = new byte[size * size * 4];
                image.CopyPixels(pixels, size * 4, 0);
                Require(pixels.Where((value, index) => index % 4 != 3).Any(value => value < 240), "The preview is blank.");
                Require(pixels.Where((value, index) => index % 4 == 3).All(value => value == 255), "The preview is not opaque.");
                if (size == 256) Save(image, Path.ChangeExtension(path, ".png"));
            }
            Require(File.ReadAllBytes(path).SequenceEqual(snapshot), "Previewing modified the original document.");
            Console.WriteLine($"PASS: content preview {Path.GetFileName(path)} at four resolutions");
            if (arguments.Contains("--shell")) ShellExtraction.Check(path);
        }
        var original = File.ReadAllBytes(projectPath);
        Reject(original[..40]);
        var damaged = original.ToArray();
        damaged[49] ^= 0xff;
        Reject(damaged);
        var oversized = original.ToArray();
        BitConverter.GetBytes(1L).CopyTo(oversized, 33);
        Reject(oversized);
        Reject("not a native document"u8.ToArray());
        VerifyRepeatedExtraction(original);
        VerifyBusyRenderRejected(original);
        VerifyMtaExtraction(original);
        Console.WriteLine("PASS: truncated, corrupt, forged-length, unknown input, STA/MTA extraction and bitmap ownership checks");
        if (arguments.Contains("--shell")) VerifyCacheRefresh(projectPath);
        ExchangeThumbnailChecks.Run(directory, arguments);
        return 0;
    }

    /// <summary>Verifies the Windows cache notices a saved geometry change without deleting the global cache.</summary>
    private static void VerifyCacheRefresh(string path)
    {
        var before = ShellExtraction.Check(path);
        var document = NativeDocumentStore.LoadProject(path);
        var session = new DocumentSession(document);
        session.AddWall(document.Levels[0].Id, new Point2(100, 0), new Point2(120, 0));
        NativeDocumentStore.SaveProject(path, session.Document);
        var after = ShellExtraction.Check(path);
        Require(!before.SequenceEqual(after), "Windows reused the old thumbnail after geometry changed.");
        Console.WriteLine("PASS: Windows thumbnail cache refresh after saving changed geometry");
    }

    /// <summary>Creates an actual extruded solid to distinguish geometric family rendering from operation-tree fallback.</summary>
    private static FamilyDocument CreateSolidFamily()
    {
        var plane = new FamilyWorkPlane(Guid.NewGuid(), "Base plane", new(0, 0, 0), new(0, 0, 1), new(1, 0, 0));
        return new FamilyDocument
        {
            Name = "Thumbnail solid", Types = [new(Guid.NewGuid(), "Default type", [])], WorkPlanes = [plane],
            Operations = [new(Guid.NewGuid(), "Extruded profile", FamilyOperationKind.SolidExtrusion)
            {
                WorkPlaneId = plane.Id, Profile = [new(0, 0), new(2, 0), new(2, 1), new(0, 1)],
                Expressions = new() { [FamilyGeometry.DepthKey] = "3" }
            }]
        };
    }

    /// <summary>Checks that corrupt files fail cleanly rather than yielding plausible but incorrect previews.</summary>
    private static void Reject(byte[] snapshot)
    {
        try { DocumentPreview.Render(snapshot, 256); }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException) { return; }
        throw new InvalidOperationException("Malformed thumbnail input was accepted.");
    }

    /// <summary>Uses a real COM IStream to verify repeated requests and caller-owned HBITMAP lifetime.</summary>
    private static void VerifyRepeatedExtraction(byte[] snapshot)
    {
        var stream = SHCreateMemStream(snapshot, (uint)snapshot.Length);
        try
        {
            var provider = new ThumbnailProvider();
            provider.Initialize(stream, 0);
            foreach (var size in new uint[] { 48, 256 })
            {
                provider.GetThumbnail(size, out var bitmap, out var alpha);
                Require(bitmap != IntPtr.Zero && alpha == 1, "The provider did not return an opaque HBITMAP.");
                Require(ShellExtraction.DeleteObject(bitmap), "The thumbnail bitmap was not caller-owned.");
            }
            try { provider.Initialize(stream, 0); }
            catch (COMException) { return; }
            throw new InvalidOperationException("Repeated stream initialization was accepted.");
        }
        finally { Marshal.ReleaseComObject(stream); }
    }

    /// <summary>Ensures a timed-out worker cannot be overlapped by another in-process render.</summary>
    private static void VerifyBusyRenderRejected(byte[] snapshot)
    {
        var stream = SHCreateMemStream(snapshot, (uint)snapshot.Length);
        try
        {
            var provider = new ThumbnailProvider();
            provider.Initialize(stream, 0);
            // Hold the admission slot deterministically instead of depending on CPU timing.
            var slot = typeof(ThumbnailProvider).GetField("_renderInProgress",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The render admission slot is missing.");
            IntPtr bitmap = IntPtr.Zero;
            uint alpha = 0;
            try
            {
                slot.SetValue(null, 1);
                try
                {
                    provider.GetThumbnail(96, out bitmap, out alpha);
                    throw new InvalidOperationException("A second thumbnail render was admitted.");
                }
                catch (COMException exception) when (exception.HResult == unchecked((int)0x800700AA))
                {
                    Require(bitmap == IntPtr.Zero && alpha == 0, "Busy extraction allocated a bitmap.");
                }
            }
            finally
            {
                if (bitmap != IntPtr.Zero) ShellExtraction.DeleteObject(bitmap);
                slot.SetValue(null, 0);
            }
            provider.GetThumbnail(96, out bitmap, out alpha);
            Require(bitmap != IntPtr.Zero && alpha == 1, "Extraction did not recover after the busy slot cleared.");
            Require(ShellExtraction.DeleteObject(bitmap), "Recovered bitmap was not caller-owned.");
            Console.WriteLine("PASS: overlapping thumbnail renders are rejected and recovery works");
        }
        finally { Marshal.ReleaseComObject(stream); }
    }

    /// <summary>Exercises the Shell's MTA calling pattern and cross-thread frozen bitmap transfer.</summary>
    private static void VerifyMtaExtraction(byte[] snapshot)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var stream = SHCreateMemStream(snapshot, (uint)snapshot.Length);
                try
                {
                    var provider = new ThumbnailProvider();
                    provider.Initialize(stream, 0);
                    provider.GetThumbnail(96, out var bitmap, out var alpha);
                    Require(bitmap != IntPtr.Zero && alpha == 1, "MTA extraction did not return an opaque bitmap.");
                    Require(ShellExtraction.DeleteObject(bitmap), "MTA extraction did not transfer bitmap ownership.");
                }
                finally { Marshal.ReleaseComObject(stream); }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new InvalidOperationException("MTA thumbnail extraction did not finish.");
        if (failure is not null) throw new InvalidOperationException("MTA thumbnail extraction failed.", failure);
    }

    /// <summary>Creates an OS stream to exercise the same ABI used by Explorer.</summary>
    [DllImport("shlwapi.dll")]
    [return: MarshalAs(UnmanagedType.Interface)]
    private static extern System.Runtime.InteropServices.ComTypes.IStream SHCreateMemStream(byte[] bytes, uint length);

    /// <summary>Writes the rendered evidence without holding a lock on the corresponding document.</summary>
    private static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(path);
        encoder.Save(output);
    }

    /// <summary>Reports a precise invariant failure through the test executable's exit code.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
