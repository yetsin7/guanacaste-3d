namespace Guanacaste3D.Desktop;

/// <summary>Resolves the same level-based stair height for placement and its transient preview.</summary>
public partial class MainWindow
{
    /// <summary>Uses the nearest higher level when available, otherwise keeps the selected type's default rise.</summary>
    private double StairPlacementHeight(double defaultHeight)
    {
        var elevation = _session.Document.Levels.First(level => level.Id == _activeLevelId).Elevation;
        return _session.Document.Levels.Where(level => level.Elevation > elevation + 1e-6)
            .OrderBy(level => level.Elevation).Select(level => level.Elevation - elevation)
            .FirstOrDefault(defaultHeight);
    }
}
