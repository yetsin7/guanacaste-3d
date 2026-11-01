using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Guanacaste3D.Core.Import.Revit;
using Guanacaste3D.ExchangePreview;
using Guanacaste3D.RevitBridge;
using Guanacaste3D.Thumbnails;

namespace Guanacaste3D.ThumbnailChecks;

/// <summary>Checks old exchange files, the exact export preview writer, and optional local customer files without publishing them.</summary>
internal static class ExchangeThumbnailChecks
{
    /// <summary>Verifies preview generation, embedding, fallback, interoperability, and installed Shell extraction.</summary>
    internal static void Run(string directory, string[] arguments)
    {
        var original = CreateExchange();
        VerifyChunkedStreamInitialization(original);
        var legacyPath = Path.Combine(directory, "legacy-exchange.g3dx");
        File.WriteAllText(legacyPath, original.ToJsonString());
        Verify(legacyPath, directory, arguments.Contains("--shell"));
        var embeddedPath = Path.Combine(directory, "embedded-exchange.g3dx");
        File.Copy(legacyPath, embeddedPath + ".partial", true);
        ExchangePreviewWriter.Complete(embeddedPath + ".partial", embeddedPath);
        var embedded = JsonNode.Parse(File.ReadAllBytes(embeddedPath))!;
        Require(embedded["thumbnailPng"]!.GetValue<string>().Length > 0, "The exporter did not embed a PNG.");
        foreach (var property in original)
            Require(JsonNode.DeepEquals(property.Value, embedded[property.Key]), "Preview embedding changed exchange content.");
        Require(ExchangeFile.Read(embeddedPath).Elements.Count == 1, "The existing importer rejected the updated exchange.");
        Verify(embeddedPath, directory, arguments.Contains("--shell"));
        embedded["thumbnailPng"] = "broken preview";
        DocumentPreview.Render(System.Text.Encoding.UTF8.GetBytes(embedded.ToJsonString()), 256);
        var oversizedBuffer = CreateExchange();
        // One extra base64 record beyond the 500k-triangle material budget must fail
        // before allocating or copying the binary buffer.
        oversizedBuffer["meshes"]![0]!["materials"] = new string('A', 2_666_672);
        RejectOversizedExchange(oversizedBuffer);
        var excessiveCollections = CreateExchange();
        var meshCollection = excessiveCollections["meshes"]!.AsArray();
        var sampleMesh = meshCollection[0]!.DeepClone();
        for (var index = 1; index <= 2048; index++)
            meshCollection.Add(sampleMesh.DeepClone());
        RejectOversizedExchange(excessiveCollections);
        original["meshes"]![0]!["indices"] = Convert.ToBase64String(MemoryMarshal.AsBytes(new[] { 99, 1, 2 }.AsSpan()));
        try { DocumentPreview.Render(System.Text.Encoding.UTF8.GetBytes(original.ToJsonString()), 256); }
        catch (InvalidDataException)
        {
            Console.WriteLine("PASS: exchange mesh bounds and allocation budgets, optional preview fallback, unchanged BIM payload and importer compatibility");
            foreach (var argument in arguments.Where(argument => argument.StartsWith("--exchange-file=")))
                Verify(argument["--exchange-file=".Length..], directory, arguments.Contains("--shell"));
            return;
        }
        throw new InvalidOperationException("Invalid exchange mesh indices were accepted.");
    }

    /// <summary>Checks that the Shell provider copies a valid stream spanning multiple read chunks.</summary>
    private static void VerifyChunkedStreamInitialization(JsonObject exchange)
    {
        // JSON whitespace is legal, so this exercises > 81,920 bytes without changing
        // the geometry or requiring a large test fixture in the repository.
        var padded = System.Text.Encoding.UTF8.GetBytes(new string(' ', 90_000) + exchange.ToJsonString());
        var stream = SHCreateMemStream(padded, (uint)padded.Length);
        try
        {
            var provider = new ThumbnailProvider();
            provider.Initialize(stream, 0);
            provider.GetThumbnail(96, out var bitmap, out var alpha);
            Require(bitmap != IntPtr.Zero && alpha == 1, "Multi-chunk Shell snapshot did not render.");
            Require(ShellExtraction.DeleteObject(bitmap), "Multi-chunk Shell bitmap ownership was not transferred.");
        }
        finally { Marshal.ReleaseComObject(stream); }
        Console.WriteLine("PASS: Shell stream snapshot read across multiple bounded chunks");
    }

    [DllImport("shlwapi.dll")]
    [return: MarshalAs(UnmanagedType.Interface)]
    private static extern System.Runtime.InteropServices.ComTypes.IStream SHCreateMemStream(byte[] bytes, uint length);

    /// <summary>Untrusted exchange metadata must fail within the thumbnail allocation budget.</summary>
    private static void RejectOversizedExchange(JsonObject exchange)
    {
        try { DocumentPreview.Render(System.Text.Encoding.UTF8.GetBytes(exchange.ToJsonString()), 256); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Oversized exchange thumbnail input was accepted.");
    }

    /// <summary>Renders a file read-only and optionally asks Windows for its actual cached thumbnail.</summary>
    private static void Verify(string path, string directory, bool shell)
    {
        var bytes = File.ReadAllBytes(path);
        var image = DocumentPreview.Render(bytes, 512);
        var pixels = new byte[512 * 512 * 4];
        image.CopyPixels(pixels, 512 * 4, 0);
        Require(pixels.Where((value, index) => index % 4 != 3).Count(value => value < 240) > 100, "The exchange preview is blank.");
        File.WriteAllBytes(Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + "-preview.png"), ExchangeThumbnail.EncodePng(image));
        if (shell) ShellExtraction.Check(Path.GetFullPath(path));
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Previewing modified the exchange file.");
        Console.WriteLine($"PASS: read-only exchange thumbnail {Path.GetFileName(path)}");
    }

    /// <summary>Builds a portable triangle fixture with an instance transform and a material color.</summary>
    private static JsonObject CreateExchange()
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(0, 1, 3) };
        return new JsonObject
        {
            ["format"] = "guanacaste-exchange", ["version"] = 1, ["projectName"] = "Exchange thumbnail test",
            ["materials"] = new JsonArray(new JsonObject { ["color"] = "#4384A0" }),
            ["levels"] = new JsonArray(),
            ["meshes"] = new JsonArray(new JsonObject
            {
                ["positions"] = Convert.ToBase64String(MemoryMarshal.AsBytes(positions.AsSpan())),
                ["indices"] = Convert.ToBase64String(MemoryMarshal.AsBytes(new[] { 0, 1, 2 }.AsSpan())),
                ["materials"] = Convert.ToBase64String(MemoryMarshal.AsBytes(new[] { 0 }.AsSpan()))
            }),
            ["elements"] = new JsonArray(new JsonObject
            {
                ["id"] = 1, ["mesh"] = 0, ["origin"] = new JsonArray(10, 2, 1), ["rotation"] = 0.3
            })
        };
    }

    /// <summary>Fails the executable regression suite with a developer-facing invariant description.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
