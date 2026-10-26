using System.Text;
using System.Text.Json;

namespace Guanacaste3D.Core;

/// <summary>Versioned on-disk projects, with validation and a previous-copy backup.</summary>
public static class ProjectFile
{
    private const long MaximumBytes = 20L * 1024 * 1024;
    private const string NativeHeader = "G3-PROJECT/1\n";
    private static readonly string[] RequiredFields = ["SchemaVersion", "Id", "Name", "Levels", "Walls"];
    public const string NativeExtension = ".g3d";
    public const string PreviousNativeExtension = ".g3";
    public const string LegacyExtension = ".g3d.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    /// <summary>Returns whether a path uses the native Guanacaste 3D extension.</summary>
    public static bool IsNativePath(string path) =>
        string.Equals(Path.GetExtension(path), NativeExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Recognizes earlier native projects for read-only migration to the current extension.</summary>
    public static bool IsPreviousNativePath(string path) =>
        string.Equals(Path.GetExtension(path), PreviousNativeExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether a path uses the import-only preview extension.</summary>
    public static bool IsLegacyPath(string path) =>
        path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Loads, migrates, and validates a native or legacy project document.</summary>
    public static BuildingDocument Load(string path)
    {
        if (!IsNativePath(path) && !IsPreviousNativePath(path) && !IsLegacyPath(path))
            throw new InvalidDataException("Guanacaste 3D projects must use .g3d, previous .g3, or legacy .g3d.json extension.");
        if (new FileInfo(path).Length > MaximumBytes)
            throw new InvalidDataException("The project file exceeds 20 MB.");
        var content = File.ReadAllText(path, Encoding.UTF8);
        var hasNativeHeader = IsNativePath(path) || IsPreviousNativePath(path);
        if (hasNativeHeader && !content.StartsWith(NativeHeader, StringComparison.Ordinal))
            throw new InvalidDataException("The native project header is missing or unsupported.");
        var json = hasNativeHeader ? content[NativeHeader.Length..] : content;
        return DeserializeDocument(json);
    }

    /// <summary>
    /// Parses, migrates, and validates a project document payload. Templates reuse
    /// this path so every accepted document follows one migration chain.
    /// </summary>
    public static BuildingDocument DeserializeDocument(string json)
    {
        NativeContainer.RequireObject(json, RequiredFields);
        var document = JsonSerializer.Deserialize<BuildingDocument>(json, Options)
            ?? throw new InvalidDataException("The project document is empty.");
        UpgradeLegacyDocument(document);
        document.Validate();
        return document;
    }

    /// <summary>Serializes a document with the project options used for every native kind.</summary>
    public static string SerializeDocument(BuildingDocument document) =>
        JsonSerializer.Serialize(document, Options);

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
        if (document.SchemaVersion == 4)
        {
            // Schema 5 moves door and window sizes onto reusable types. Each
            // existing opening is matched to a built-in type of the same size,
            // or receives a generated type, so geometry is preserved exactly.
            document.SchemaVersion = 5;
            document.OpeningTypes = BuiltInOpeningTypes.CreateDefaults();
            for (var index = 0; index < document.Openings.Count; index++)
            {
                var opening = document.Openings[index];
                var type = OpeningTypeRules.FindOrCreate(document.OpeningTypes, opening.Kind,
                    opening.Width, opening.Height, opening.Sill);
                document.Openings[index] = opening with { OpeningTypeId = type.Id };
            }
        }
        if (document.SchemaVersion == 5)
        {
            // Schema 6 adds optional parameter formulas. No data changes, but the
            // version bump stops older builds from silently dropping formulas.
            document.SchemaVersion = 6;
        }
        if (document.SchemaVersion == 6)
        {
            // Schema 7 adds optional wall top constraints; existing walls stay unconnected.
            document.SchemaVersion = 7;
        }
        if (document.SchemaVersion == 7)
        {
            // Schema 8 adds saved schedule definitions; earlier projects have none.
            document.SchemaVersion = 8;
            document.Schedules ??= [];
        }
        if (document.SchemaVersion == 8)
        {
            // Earlier built-in doors had no leaf. Preserve custom sizes, names, and nonzero depths.
            var defaults = BuiltInOpeningTypes.CreateDefaults().Where(type => type.Kind == OpeningKind.Door)
                .ToDictionary(type => type.Id);
            for (var index = 0; index < document.OpeningTypes.Count; index++)
            {
                var type = document.OpeningTypes[index];
                if (type.Kind == OpeningKind.Door && type.Depth == 0 && defaults.TryGetValue(type.Id, out var standard))
                    document.OpeningTypes[index] = type with { Depth = standard.Depth };
            }
            document.SchemaVersion = 9;
        }

        if (document.SchemaVersion == 9) document.SchemaVersion = 10;

    }

    /// <summary>Saves a validated project atomically and retains the previous file as a backup.</summary>
    public static void Save(string path, BuildingDocument document)
    {
        if (!IsNativePath(path))
            throw new InvalidDataException("Native Guanacaste 3D projects must use the .g3d extension.");
        document.Validate();
        var content = NativeHeader + SerializeDocument(document);
        NativeContainer.WriteAtomically(path, content);
    }
}
