using System.Text.Json.Serialization;

namespace Guanacaste3D.Core;

/// <summary>Represents a position in the model's XY ground plane, measured in meters.</summary>
public readonly record struct Point2(double X, double Y)
{
    public double DistanceTo(Point2 other) => double.Hypot(other.X - X, other.Y - Y);
}

/// <summary>A named vertical building datum, measured in meters from the project origin.</summary>
public sealed record Level(Guid Id, string Name, double Elevation);

/// <summary>A straight metric wall, tied to a building level.</summary>
public sealed record Wall(Guid Id, Guid LevelId, Point2 Start, Point2 End, double Thickness, double Height)
{
    [JsonIgnore] public double Length => Start.DistanceTo(End);
}

/// <summary>Owns the editable architectural source of truth for all 2D and 3D views.</summary>
public sealed class BuildingDocument
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Proyecto sin título";
    public List<Level> Levels { get; set; } = [];
    public List<Wall> Walls { get; set; } = [];
    public List<WallOpening> Openings { get; set; } = [];

    public static BuildingDocument Create() => new()
    {
        Levels = [new Level(Guid.NewGuid(), "Nivel 1", 0)]
    };

    /// <summary>Returns a deep-enough copy because contained entities are immutable records.</summary>
    public BuildingDocument Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        Id = Id,
        Name = Name,
        Levels = [.. Levels],
        Walls = [.. Walls],
        Openings = [.. Openings]
    };

    /// <summary>Rejects inconsistent projects before mutation, opening, or writing a file.</summary>
    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("Versión de proyecto no compatible.");
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 120)
            throw new InvalidDataException("Identidad o nombre de proyecto inválido.");
        if (Levels is null || Walls is null || Openings is null ||
            Levels.Count is < 1 or > 200 || Walls.Count > 100000 || Openings.Count > 200000)
            throw new InvalidDataException("El documento excede los límites de elementos.");
        if (Levels.Any(l => l.Id == Guid.Empty || string.IsNullOrWhiteSpace(l.Name) ||
             l.Name.Length > 100 || !double.IsFinite(l.Elevation)) ||
            Levels.Select(l => l.Id).Distinct().Count() != Levels.Count)
            throw new InvalidDataException("Niveles inválidos.");
        var levelIds = Levels.Select(l => l.Id).ToHashSet();
        if (Walls.Any(w => w is null) || Walls.Select(w => w.Id).Distinct().Count() != Walls.Count)
            throw new InvalidDataException("Hay elementos duplicados.");
        foreach (var wall in Walls)
        {
            if (wall.Id == Guid.Empty || !levelIds.Contains(wall.LevelId) ||
                !double.IsFinite(wall.Start.X) || !double.IsFinite(wall.Start.Y) ||
                !double.IsFinite(wall.End.X) || !double.IsFinite(wall.End.Y) ||
                !double.IsFinite(wall.Thickness) || !double.IsFinite(wall.Height) ||
                wall.Length < 0.05 || wall.Length > 100000 ||
                wall.Thickness is < 0.05 or > 5 || wall.Height is < 0.1 or > 1000)
                throw new InvalidDataException("Muro inválido o sin nivel asociado.");
        }
        if (Openings.Any(o => o is null) ||
            Levels.Select(l => l.Id).Intersect(Walls.Select(w => w.Id)).Any() ||
            Levels.Select(l => l.Id).Intersect(Openings.Select(o => o.Id)).Any() ||
            Walls.Select(w => w.Id).Intersect(Openings.Select(o => o.Id)).Any())
            throw new InvalidDataException("Identificadores de elementos repetidos.");
        WallGeometry.Validate(Walls, Openings);
    }
}
