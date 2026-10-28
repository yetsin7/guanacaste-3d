using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>Resolves the same level-based stair height for placement and its transient preview.</summary>
public partial class MainWindow
{
    /// <summary>Reports the actual generated rise and going rather than a generic structural section.</summary>
    private string StairProperties(StructuralElement stair)
    {
        var count = StairGeometry.RiserCount(stair.Height);
        if (stair.Style == ElementStyle.Spiral)
            return FormatText("Properties.SpiralStairSummary", stair.Name, stair.Height, count,
                stair.Height / count, Math.Max(1.2, stair.Width), stair.Offset);
        var flights = StairGeometry.Flights(stair);
        var goings = flights.Select(flight => flight.Start.DistanceTo(flight.End) / flight.Risers).ToArray();
        return FormatText("Properties.StairSummary", stair.Name, stair.Height, count, stair.Height / count,
            stair.Width, flights.Count, goings.DefaultIfEmpty(0).Min(), goings.DefaultIfEmpty(0).Max(), stair.Offset);
    }

    /// <summary>Uses the nearest higher level when available, otherwise keeps the selected type's default rise.</summary>
    private double StairPlacementHeight(double defaultHeight)
    {
        var elevation = _session.Document.Levels.First(level => level.Id == _activeLevelId).Elevation;
        return _session.Document.Levels.Where(level => level.Elevation > elevation + 1e-6)
            .OrderBy(level => level.Elevation).Select(level => level.Elevation - elevation)
            .FirstOrDefault(defaultHeight);
    }
}
