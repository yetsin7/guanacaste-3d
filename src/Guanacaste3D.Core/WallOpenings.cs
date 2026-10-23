using System.Text.Json.Serialization;

namespace Guanacaste3D.Core;

/// <summary>Types of wall-hosted openings.</summary>
public enum OpeningKind { Door, Window }

/// <summary>Aperture dimensions are meters; offset is measured from wall start to opening center.</summary>
public sealed record WallOpening(Guid Id, Guid WallId, OpeningKind Kind,
    double Offset, double Width, double Sill, double Height)
{
    /// <summary>The door or window type whose width and height drive this instance.</summary>
    public Guid OpeningTypeId { get; init; }

    [JsonIgnore] public double Begin => Offset - Width / 2;
    [JsonIgnore] public double End => Offset + Width / 2;
}

/// <summary>A portion of solid wall excluding an opening.</summary>
public readonly record struct WallSolid(double From, double To, double Bottom, double Top);

/// <summary>Checks hosted openings and builds rectangular wall-solid sections.</summary>
public static class WallGeometry
{
    /// <summary>Validates hosting, dimensions, bounds, and collision rules for all openings.</summary>
    public static void Validate(IReadOnlyList<Wall> walls, IReadOnlyList<WallOpening> openings)
    {
        var hosts = walls.ToDictionary(w => w.Id);
        if (openings.Count > 200000 || openings.Select(o => o.Id).Distinct().Count() != openings.Count)
            throw new InvalidDataException("The opening count or identifiers are invalid.");
        foreach (var group in openings.GroupBy(o => o.WallId))
        {
            if (!hosts.TryGetValue(group.Key, out var host))
                throw new InvalidDataException("An opening has no host wall.");
            var sorted = group.OrderBy(o => o.Begin).ToArray();
            for (var i = 0; i < sorted.Length; i++)
            {
                var o = sorted[i];
                if (o.Id == Guid.Empty || !Enum.IsDefined(o.Kind) ||
                    !double.IsFinite(o.Offset) || !double.IsFinite(o.Width) ||
                    !double.IsFinite(o.Sill) || !double.IsFinite(o.Height) ||
                    o.Width < 0.3 || o.Height < 0.3 || o.Sill < 0 ||
                    o.Begin < 0.05 || o.End > host.Length - 0.05 ||
                    o.Sill + o.Height > host.Height - 0.05 ||
                    (o.Kind == OpeningKind.Door && o.Sill != 0))
                    throw new InvalidDataException("An opening exceeds its host wall bounds.");
                if (i > 0 && sorted[i - 1].End + 0.05 > o.Begin)
                    throw new InvalidDataException("Hosted openings cannot overlap.");
            }
        }
    }

    /// <summary>Segments a wall into rectangular solids that exclude its hosted openings.</summary>
    public static IReadOnlyList<WallSolid> Solids(Wall wall, IEnumerable<WallOpening> openings)
    {
        var hosted = openings.ToArray();
        if (hosted.Any(o => o.WallId != wall.Id))
            throw new ArgumentException("The opening does not belong to this wall.");
        var stops = new List<double> { 0, wall.Length };
        foreach (var o in hosted) { stops.Add(o.Begin); stops.Add(o.End); }
        stops.Sort();
        var parts = new List<WallSolid>();
        for (var i = 1; i < stops.Count; i++)
        {
            var a = stops[i - 1]; var b = stops[i];
            if (b - a < 0.000001) continue;
            var middle = (a + b) / 2;
            var opening = hosted.FirstOrDefault(o => o.Begin <= middle && o.End >= middle);
            if (opening is null) parts.Add(new WallSolid(a, b, 0, wall.Height));
            else
            {
                if (opening.Sill > 0) parts.Add(new WallSolid(a, b, 0, opening.Sill));
                var top = opening.Sill + opening.Height;
                if (top < wall.Height) parts.Add(new WallSolid(a, b, top, wall.Height));
            }
        }
        return parts;
    }

    /// <summary>Calculates net wall volume after subtracting hosted openings.</summary>
    public static double NetVolume(Wall wall, IEnumerable<WallOpening> openings) =>
        Solids(wall, openings).Sum(part =>
            (part.To - part.From) * (part.Top - part.Bottom)) * wall.Thickness;
}
