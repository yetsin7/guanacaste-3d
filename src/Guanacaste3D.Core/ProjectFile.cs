using System.Text;
using System.Text.Json;

namespace Guanacaste3D.Core;

/// <summary>Versioned on-disk projects, with validation and a previous-copy backup.</summary>
public static class ProjectFile
{
    private const long MaximumBytes = 20L * 1024 * 1024;
    private const string NativeHeader = "G3-PROJECT/1\n";
    public const string NativeExtension = ".g3";
    public const string LegacyExtension = ".g3d.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    /// <summary>Returns whether a path uses the native Guanacaste 3D extension.</summary>
    public static bool IsNativePath(string path) =>
        string.Equals(Path.GetExtension(path), NativeExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether a path uses the import-only preview extension.</summary>
    public static bool IsLegacyPath(string path) =>
        path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Loads, migrates, and validates a native or legacy project document.</summary>
    public static BuildingDocument Load(string path)
    {
        if (!IsNativePath(path) && !IsLegacyPath(path))
            throw new InvalidDataException("Guanacaste 3D projects must use .g3 or the legacy .g3d.json extension.");
        if (new FileInfo(path).Length > MaximumBytes)
            throw new InvalidDataException("The project file exceeds 20 MB.");
        var content = File.ReadAllText(path, Encoding.UTF8);
        if (IsNativePath(path) && !content.StartsWith(NativeHeader, StringComparison.Ordinal))
            throw new InvalidDataException("The .g3 project header is missing or unsupported.");
        var json = IsNativePath(path) ? content[NativeHeader.Length..] : content;
        using (var parsed = JsonDocument.Parse(json))
        {
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The project document must be a JSON object.");
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in parsed.RootElement.EnumerateObject())
                if (!fields.Add(field.Name))
                    throw new InvalidDataException("The project document contains duplicate fields.");
            foreach (var required in new[] { "SchemaVersion", "Id", "Name", "Levels", "Walls" })
                if (!fields.Contains(required))
                    throw new InvalidDataException($"The required field is missing: {required}.");
        }
        var document = JsonSerializer.Deserialize<BuildingDocument>(json, Options)
            ?? throw new InvalidDataException("The project document is empty.");
        UpgradeLegacyDocument(document);
        document.Validate();
        return document;
    }

    /// <summary>Upgrades earlier schemas to current BIM catalogs and coordinated views.</summary>
    private static void UpgradeLegacyDocument(BuildingDocument document)
    {
        if (document.SchemaVersion == 1)
        {
            document.SchemaVersion = 2;
            document.WallTypes = BuiltInWallTypes.CreateDefaults();
            document.Rooms ??= [];
            document.Floors ??= [];
            document.Sheets ??= [];
            for (var index = 0; index < document.Walls.Count; index++)
            {
                var wall = document.Walls[index];
                var closestType = document.WallTypes.MinBy(type => Math.Abs(type.Thickness - wall.Thickness))!;
                document.Walls[index] = wall with { WallTypeId = closestType.Id };
            }
        }
        if (document.SchemaVersion == 2)
        {
            document.SchemaVersion = 3;
            document.Views = BuiltInProjectViews.CreateDefaults(document.Levels);
        }
        if (document.SchemaVersion == 3)
        {
            // Schema 4 adds project parameters; earlier projects start with none.
            document.SchemaVersion = 4;
            document.ParameterDefinitions ??= [];
            document.ParameterValues ??= [];
        }
    }

    /// <summary>Saves a validated project atomically and retains the previous file as a backup.</summary>
    public static void Save(string path, BuildingDocument document)
    {
        if (!IsNativePath(path))
            throw new InvalidDataException("Native Guanacaste 3D projects must use the .g3 extension.");
        document.Validate();
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)
            ?? throw new InvalidDataException("The file path is invalid.");
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);
        var json = JsonSerializer.Serialize(document, Options);
        var content = NativeHeader + json;
        if (Encoding.UTF8.GetByteCount(content) > MaximumBytes)
            throw new InvalidDataException("The project exceeds the 20 MB limit.");
        var temp = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(full))
                File.Copy(full, full + ".bak", overwrite: true);
            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
