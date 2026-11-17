namespace Guanacaste3D.Core;

/// <summary>How a room's air supply compares with what its use needs.</summary>
public enum VentilationStatus
{
    /// <summary>Natural openings or extraction cover the required airflow.</summary>
    Ok,

    /// <summary>The room has some openings or ducts but not enough for its required airflow.</summary>
    Insufficient,

    /// <summary>The room has neither an openable window nor an extraction duct.</summary>
    None
}

/// <summary>One room's ventilation: the air it needs, the openable area it has and the extraction that serves it.</summary>
public sealed record RoomVentilation(Room Room, double Area, double Volume, double AirChangesRequired, double RequiredFlow,
    double OpenableArea, double RequiredOpenableArea, double DuctFlow, int Ducts, VentilationStatus Status)
{
    /// <summary>Air changes per hour that the room's extraction ducts alone give.</summary>
    public double ExtractionAirChanges => Volume > 0 ? DuctFlow / Volume : 0;
}

/// <summary>
/// Room ventilation by air changes per hour (ACH): the required airflow is the room volume times the changes its use
/// asks for (bathrooms 10, kitchens 15, laundries 10, bedrooms and living rooms 4 to 6, others 3), after ASHRAE 62.2 and
/// the usual residential practice. A room passes when its openable window area reaches one twentieth of its floor area
/// (a common building-code minimum for natural ventilation) or when its extraction ducts carry the required flow. A
/// duct's flow is its cross section times 4 m/s, the velocity recommended for quiet residential ducts.
/// </summary>
public static class VentilationAnalysis
{
    public const double NaturalOpenableRatio = 1.0 / 20;
    public const double DuctVelocity = 4.0;
    public const double DefaultCeilingHeight = 2.7;

    /// <summary>Openable share of a window's glazed area: louvres open almost fully, sliders and casements about half.</summary>
    public static double OpenableFraction(OpeningOperation operation) => operation switch
    {
        OpeningOperation.Louvered => 0.9,
        OpeningOperation.Sliding => 0.5,
        _ => 0.6
    };

    /// <summary>Air changes per hour a room's use asks for, from its name.</summary>
    public static double AirChanges(string name)
    {
        bool Has(params string[] words) => words.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));
        if (Has("cocina", "kitchen")) return 15;
        if (Has("baño", "bano", "bath", "servicio sanitario", "lavander", "laundry")) return 10;
        if (Has("dormitorio", "habitaci", "bedroom", "cuarto")) return 6;
        if (Has("sala", "living", "comedor", "dining", "estudio", "oficina", "study", "office")) return 4;
        if (Has("cochera", "garaje", "garage", "terraza", "balc", "pasillo", "hall", "corredor")) return 2;
        return 3;
    }

    public static IReadOnlyList<RoomVentilation> Analyze(BuildingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var walls = document.Walls.ToDictionary(wall => wall.Id);
        var types = document.OpeningTypes.ToDictionary(type => type.Id);
        return document.Rooms.Select(room =>
        {
            bool Near(Point2 point, double margin) =>
                point.X >= Math.Min(room.FirstCorner.X, room.SecondCorner.X) - margin && point.X <= Math.Max(room.FirstCorner.X, room.SecondCorner.X) + margin &&
                point.Y >= Math.Min(room.FirstCorner.Y, room.SecondCorner.Y) - margin && point.Y <= Math.Max(room.FirstCorner.Y, room.SecondCorner.Y) + margin;
            var area = room.Area;
            var height = document.Walls.Where(wall => wall.LevelId == room.LevelId).Select(wall => wall.Height).DefaultIfEmpty(DefaultCeilingHeight).Max();
            var volume = area * height;
            var changes = AirChanges(room.Name);
            var required = volume * changes;

            // Openable windows of the walls around the room.
            var openable = 0.0;
            foreach (var opening in document.Openings.Where(opening => opening.Kind == OpeningKind.Window))
            {
                if (!walls.TryGetValue(opening.WallId, out var wall) || wall.LevelId != room.LevelId) continue;
                var along = (X: wall.End.X - wall.Start.X, Y: wall.End.Y - wall.Start.Y);
                var length = Math.Max(wall.Start.DistanceTo(wall.End), 1e-9);
                var centre = new Point2(wall.Start.X + along.X * opening.Offset / length, wall.Start.Y + along.Y * opening.Offset / length);
                if (!Near(centre, wall.Thickness / 2 + 0.05)) continue;
                var operation = types.TryGetValue(opening.OpeningTypeId, out var type) ? type.Operation : OpeningOperation.Standard;
                openable += opening.Width * opening.Height * OpenableFraction(operation);
            }

            // Extraction: the ducts that start inside the room.
            var ducts = document.StructuralElements.Where(element => element.LevelId == room.LevelId &&
                                                                     element.Kind == StructuralElementKind.Duct && Near(element.Start, 0.2)).ToList();
            var flow = ducts.Sum(duct => duct.Width * duct.Height * DuctVelocity * 3600);

            var requiredOpenable = area * NaturalOpenableRatio;
            var status = openable >= requiredOpenable && openable > 0 || flow >= required && flow > 0 ? VentilationStatus.Ok
                : openable > 0 || ducts.Count > 0 ? VentilationStatus.Insufficient
                : VentilationStatus.None;
            return new RoomVentilation(room, area, volume, changes, required, openable, requiredOpenable, flow, ducts.Count, status);
        }).ToList();
    }
}
