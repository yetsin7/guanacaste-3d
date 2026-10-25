using System.Text;
using System.Text.Json;

namespace Guanacaste3D.Core;

/// <summary>
/// Versioned on-disk files, with validation and a previous-copy backup. A project is a
/// <c>.g3d</c> file, a reusable template is a <c>.g3dt</c> file, and a single in-place family is a
/// <c>.g3df</c> file. Each kind has its own header, so a renamed file is rejected rather than misread.
/// </summary>
public static class ProjectFile
{
    private const long MaximumBytes = 20L * 1024 * 1024;
    private const string ProjectHeader = "G3-PROJECT/1\n";
    private const string TemplateHeader = "G3-TEMPLATE/1\n";
    private const string FamilyHeader = "G3-FAMILY/1\n";

    /// <summary>Version of the .g3df envelope. The family inside is validated against the current schema.</summary>
    private const int FamilyFileVersion = 1;

    /// <summary>Extension of a project. <see cref="Save"/> writes only this extension.</summary>
    public const string NativeExtension = ".g3d";

    /// <summary>Extension of a reusable project template.</summary>
    public const string TemplateExtension = ".g3dt";

    /// <summary>Extension of a single in-place family.</summary>
    public const string FamilyExtension = ".g3df";

    /// <summary>Extension of projects written before .g3d. They open as imports and are never written.</summary>
    public const string LegacyNativeExtension = ".g3";

    /// <summary>Import-only preview extension of the earliest builds.</summary>
    public const string LegacyExtension = ".g3d.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    /// <summary>Returns whether a path uses the project extension.</summary>
    public static bool IsNativePath(string path) => HasExtension(path, NativeExtension);

    /// <summary>Returns whether a path uses the template extension.</summary>
    public static bool IsTemplatePath(string path) => HasExtension(path, TemplateExtension);

    /// <summary>Returns whether a path uses the in-place family extension.</summary>
    public static bool IsFamilyPath(string path) => HasExtension(path, FamilyExtension);

    /// <summary>Returns whether a path uses the earlier project extension, which is readable but not writable.</summary>
    public static bool IsLegacyNativePath(string path) => HasExtension(path, LegacyNativeExtension);

    /// <summary>Returns whether a path uses the import-only preview extension.</summary>
    public static bool IsLegacyPath(string path) =>
        path.EndsWith(LegacyExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Loads, migrates, and validates a project from .g3d, the earlier .g3, or the .g3d.json preview.</summary>
    public static BuildingDocument Load(string path)
    {
        if (!IsNativePath(path) && !IsLegacyNativePath(path) && !IsLegacyPath(path))
            throw new InvalidDataException(
                "Guanacaste 3D projects must use .g3d, the earlier .g3, or the legacy .g3d.json extension.");
        var json = IsLegacyPath(path)
            ? ReadText(path)
            : ReadPayload(path, ProjectHeader, "The project header is missing or unsupported.");
        return ParseDocument(json);
    }

    /// <summary>Loads, migrates, and validates a reusable template from a .g3dt file.</summary>
    public static BuildingDocument LoadTemplate(string path)
    {
        if (!IsTemplatePath(path))
            throw new InvalidDataException("Guanacaste 3D templates must use the .g3dt extension.");
        return ParseDocument(ReadPayload(path, TemplateHeader, "The template header is missing or unsupported."));
    }

    /// <summary>Loads and validates one in-place family from a .g3df file.</summary>
    public static InPlaceFamily LoadFamily(string path)
    {
        if (!IsFamilyPath(path))
            throw new InvalidDataException("Guanacaste 3D families must use the .g3df extension.");
        var envelope = JsonSerializer.Deserialize<InPlaceFamilyFile>(
            ReadPayload(path, FamilyHeader, "The family file header is missing or unsupported."), Options)
            ?? throw new InvalidDataException("The family file is empty.");
        if (envelope.SchemaVersion != FamilyFileVersion)
            throw new InvalidDataException("Unsupported family file version.");
        var family = envelope.Family ?? throw new InvalidDataException("The family file contains no family.");
        ValidateFamily(family);
        return family;
    }

    /// <summary>Saves a validated project atomically and retains the previous file as a backup.</summary>
    public static void Save(string path, BuildingDocument document)
    {
        if (!IsNativePath(path))
            throw new InvalidDataException("Native Guanacaste 3D projects must use the .g3d extension.");
        WriteDocument(path, document, ProjectHeader);
    }

    /// <summary>Saves a validated template atomically. A template is a complete document used as a starting point.</summary>
    public static void SaveTemplate(string path, BuildingDocument document)
    {
        if (!IsTemplatePath(path))
            throw new InvalidDataException("Guanacaste 3D templates must use the .g3dt extension.");
        WriteDocument(path, document, TemplateHeader);
    }

    /// <summary>Saves one validated in-place family atomically, so a family can be reused in other projects.</summary>
    public static void SaveFamily(string path, InPlaceFamily family)
    {
        if (!IsFamilyPath(path))
            throw new InvalidDataException("Guanacaste 3D families must use the .g3df extension.");
        ValidateFamily(family);
        var json = JsonSerializer.Serialize(new InPlaceFamilyFile(FamilyFileVersion, family), Options);
        WriteAtomically(path, FamilyHeader + json);
    }

    /// <summary>Returns whether a path ends with the given extension, ignoring case.</summary>
    private static bool HasExtension(string path, string extension) =>
        string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a file as UTF-8 text after checking its size.</summary>
    private static string ReadText(string path)
    {
        if (new FileInfo(path).Length > MaximumBytes)
            throw new InvalidDataException("The project file exceeds 20 MB.");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    /// <summary>Reads a file that must start with the given header and returns the JSON payload after it.</summary>
    private static string ReadPayload(string path, string header, string headerMessage)
    {
        var content = ReadText(path);
        if (!content.StartsWith(header, StringComparison.Ordinal))
            throw new InvalidDataException(headerMessage);
        return content[header.Length..];
    }

    /// <summary>Parses, upgrades, and validates a project document from its JSON payload.</summary>
    private static BuildingDocument ParseDocument(string json)
    {
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

    /// <summary>Validates a document and writes it with the given header.</summary>
    private static void WriteDocument(string path, BuildingDocument document, string header)
    {
        document.Validate();
        WriteAtomically(path, header + JsonSerializer.Serialize(document, Options));
    }

    /// <summary>
    /// Validates one family by placing it in an otherwise valid document, so the family
    /// rules are the same whether it is saved alone or inside a project.
    /// </summary>
    private static void ValidateFamily(InPlaceFamily family)
    {
        var probe = BuildingDocument.Create("Family");
        probe.InPlaceFamilies.Add(family);
        probe.Validate();
    }

    /// <summary>Writes content to a temporary file and moves it into place, keeping the previous file as a backup.</summary>
    private static void WriteAtomically(string path, string content)
    {
        if (Encoding.UTF8.GetByteCount(content) > MaximumBytes)
            throw new InvalidDataException("The project exceeds the 20 MB limit.");
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)
            ?? throw new InvalidDataException("The file path is invalid.");
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);
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
            // Schema 9 adds in-place families; earlier projects have none.
            document.SchemaVersion = 9;
            document.InPlaceFamilies ??= [];
        }
    }
}

/// <summary>The on-disk envelope of a .g3df file: a family-file version and one in-place family.</summary>
public sealed record InPlaceFamilyFile(int SchemaVersion, InPlaceFamily? Family);
