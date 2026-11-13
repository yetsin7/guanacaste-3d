using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core;

/// <summary>Resolves roof-hosted elements using the same first-containing-roof rule as placement.</summary>
public static class RoofElementHost
{
    /// <summary>Finds the first roof on the level enclosing the complete footprint, or null when none does.</summary>
    public static FloorSlab? Resolve(BuildingDocument document, Guid levelId, PlanRectangle footprint) =>
        document.Floors.FirstOrDefault(floor => floor.LevelId == levelId && floor.Kind == FloorKind.Roof &&
            SlabOpenings.ContainsRectangle(floor.Boundary, footprint));
}
