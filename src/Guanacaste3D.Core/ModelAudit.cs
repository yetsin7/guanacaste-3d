using System.Text.Json;

namespace Guanacaste3D.Core;

/// <summary>
/// A deterministic, vendor-neutral observation of visible BIM geometry and
/// measurements. Intended for controlled black-box comparison experiments.
/// This is not an RVT converter or a serialization format for editing projects.
/// </summary>
public static class ModelAudit
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToJson(BuildingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();

        var levels = document.Levels.ToDictionary(l => l.Id);
        var types = document.WallTypes.ToDictionary(t => t.Id);

        var snapshot = new
        {
            Schema = "guanacaste-observation/1",
            Units = "meters",
            LevelCount = document.Levels.Count,
            WallCount = document.Walls.Count,
            SlabCount = document.Slabs.Count,
            OpeningCount = document.Openings.Count,
            Levels = document.Levels
                .OrderBy(l => l.Elevation).ThenBy(l => l.Name, StringComparer.Ordinal)
                .Select(l => new { l.Name, l.Elevation })
                .ToArray(),
            Walls = document.Walls
                .OrderBy(w => levels[w.LevelId].Elevation)
                .ThenBy(w => w.Start.X).ThenBy(w => w.Start.Y)
                .ThenBy(w => w.End.X).ThenBy(w => w.End.Y)
                .ThenBy(w => w.Thickness).ThenBy(w => w.Height)
                .Select(w => new
                {
                    Level = levels[w.LevelId].Name,
                    LevelElevation = levels[w.LevelId].Elevation,
                    w.Start,
                    w.End,
                    w.Length,
                    w.Thickness,
                    w.Height,
                    WallType = w.TypeId is Guid typeId ? types[typeId].Name : (string?)null,
                    GrossVolume = w.Length * w.Thickness * w.Height,
                    NetVolume = WallGeometry.NetVolume(w,
                        document.Openings.Where(o => o.WallId == w.Id)),
                    Openings = document.Openings.Where(o => o.WallId == w.Id)
                        .OrderBy(o => o.Offset).ThenBy(o => o.Kind)
                        .Select(o => new
                        {
                            Kind = o.Kind.ToString(),
                            o.Offset,
                            o.Width,
                            o.Sill,
                            o.Height
                        }).ToArray()
                }).ToArray(),
            Slabs = document.Slabs
                .OrderBy(s => levels[s.LevelId].Elevation)
                .ThenBy(s => s.Min.X).ThenBy(s => s.Min.Y)
                .ThenBy(s => s.Max.X).ThenBy(s => s.Max.Y)
                .ThenBy(s => s.Thickness)
                .Select(s => new
                {
                    Level = levels[s.LevelId].Name,
                    LevelElevation = levels[s.LevelId].Elevation,
                    s.Min,
                    s.Max,
                    s.Thickness,
                    s.Area,
                    s.Volume
                }).ToArray()
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }
}
