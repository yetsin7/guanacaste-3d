using System.Text;
using System.Text.Json;

namespace Guanacaste3D.Core;

/// <summary>Versioned on-disk projects, with validation and a previous-copy backup.</summary>
public static class ProjectFile
{
    private const long MaximumBytes = 20L * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    public static BuildingDocument Load(string path)
    {
        if (new FileInfo(path).Length > MaximumBytes)
            throw new InvalidDataException("El proyecto excede 20 MB.");
        var json = File.ReadAllText(path, Encoding.UTF8);
        var document = JsonSerializer.Deserialize<BuildingDocument>(json, Options)
            ?? throw new InvalidDataException("Documento vacío.");
        document.Validate();
        return document;
    }

    public static void Save(string path, BuildingDocument document)
    {
        document.Validate();
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)
            ?? throw new InvalidDataException("Ruta de archivo inválida.");
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);
        var content = JsonSerializer.Serialize(document, Options);
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
