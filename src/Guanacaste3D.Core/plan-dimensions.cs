namespace Guanacaste3D.Core;

/// <summary>Semantic measurements recomputed from model references after every edit.</summary>
public enum PlanDimensionKind { WallLength, OpeningWidth, OpeningStart, OpeningEnd, Left, Right, Below, Above }

/// <summary>A persistent measurement references model identities instead of a cached numeric label.</summary>
public sealed record PlanDimension(Guid Id, Guid LevelId, Guid OwnerId, Guid? ReferenceId, PlanDimensionKind Kind);

/// <summary>Resolved dimension line coordinates and its actual metric value.</summary>
public readonly record struct PlanMeasurement(Point2 From, Point2 To, double Value);

/// <summary>Builds and resolves plan dimensions for walls, openings and other supported BIM elements.</summary>
public static class PlanDimensions
{
    /// <summary>Gets the active-level bounding rectangle; opening bounds follow their host axis.</summary>
    public static (Guid LevelId, PlanBounds Bounds)? Bounds(BuildingDocument document, Guid id)
    {
        if (document.Walls.FirstOrDefault(wall => wall.Id == id) is { } wall)
            return (wall.LevelId, Enclose(WallPlacementGeometry.Footprint(wall.Start, wall.End, wall.Thickness,
                0, WallLocationReference.Centerline).Points));
        if (document.Openings.FirstOrDefault(opening => opening.Id == id) is { } opening)
        {
            var host = document.Walls.Single(wall => wall.Id == opening.WallId);
            return (host.LevelId, Enclose(new[] { At(host, opening.Begin, -host.Thickness / 2),
                At(host, opening.End, -host.Thickness / 2), At(host, opening.End, host.Thickness / 2),
                At(host, opening.Begin, host.Thickness / 2) }));
        }
        if (document.Columns.FirstOrDefault(column => column.Id == id) is { } column)
            return (column.LevelId, new PlanBounds(column.Position.X - column.Width / 2,
                column.Position.Y - column.Depth / 2, column.Position.X + column.Width / 2,
                column.Position.Y + column.Depth / 2));
        if (document.Rooms.FirstOrDefault(room => room.Id == id) is { } room)
            return (room.LevelId, Enclose(new[] { room.FirstCorner, room.SecondCorner }));
        if (document.Floors.FirstOrDefault(floor => floor.Id == id) is { } floor)
            return (floor.LevelId, Enclose(floor.Boundary));
        if (document.StructuralElements.FirstOrDefault(element => element.Id == id) is { } element)
            return (element.LevelId, Enclose(element.Footprint));
        return null;
    }

    /// <summary>Finds the nearest overlapping wall in each cardinal direction plus intrinsic measurements.</summary>
    public static IReadOnlyList<PlanDimension> ForSelection(BuildingDocument document, Guid ownerId)
    {
        if (Bounds(document, ownerId) is not { } owner) return [];
        var result = new List<PlanDimension>();
        if (document.Walls.Any(wall => wall.Id == ownerId))
            result.Add(new(Guid.Empty, owner.LevelId, ownerId, null, PlanDimensionKind.WallLength));
        var opening = document.Openings.FirstOrDefault(candidate => candidate.Id == ownerId);
        if (opening is not null)
            foreach (var kind in new[] { PlanDimensionKind.OpeningStart, PlanDimensionKind.OpeningEnd, PlanDimensionKind.OpeningWidth })
                result.Add(new(Guid.Empty, owner.LevelId, ownerId, opening.WallId, kind));
        foreach (var kind in new[] { PlanDimensionKind.Left, PlanDimensionKind.Right, PlanDimensionKind.Below, PlanDimensionKind.Above })
        {
            PlanDimension? nearest = null;
            var minimum = double.PositiveInfinity;
            foreach (var wall in document.Walls.Where(wall => wall.LevelId == owner.LevelId &&
                         wall.Id != ownerId && wall.Id != opening?.WallId))
            {
                var target = Bounds(document, wall.Id)!.Value.Bounds;
                var horizontal = kind is PlanDimensionKind.Left or PlanDimensionKind.Right;
                if (horizontal ? target.MaxY <= owner.Bounds.MinY || target.MinY >= owner.Bounds.MaxY
                    : target.MaxX <= owner.Bounds.MinX || target.MinX >= owner.Bounds.MaxX) continue;
                var candidate = new PlanDimension(Guid.Empty, owner.LevelId, ownerId, wall.Id, kind);
                var value = Resolve(document, candidate)?.Value ?? -1;
                if (value < 0.01 || value >= minimum) continue;
                nearest = candidate;
                minimum = value;
            }
            if (nearest is not null) result.Add(nearest);
        }
        return result;
    }

    /// <summary>Resolves current geometry; null means an owner, host or reference was deleted.</summary>
    public static PlanMeasurement? Resolve(BuildingDocument document, PlanDimension dimension)
    {
        if (Bounds(document, dimension.OwnerId) is not { } owner || owner.LevelId != dimension.LevelId) return null;
        if (dimension.Kind == PlanDimensionKind.WallLength)
        {
            var wall = document.Walls.FirstOrDefault(wall => wall.Id == dimension.OwnerId);
            return wall is null ? null : new(At(wall, 0, wall.Thickness / 2 + 0.45),
                At(wall, wall.Length, wall.Thickness / 2 + 0.45), wall.Length);
        }
        if (dimension.Kind is PlanDimensionKind.OpeningStart or PlanDimensionKind.OpeningEnd or PlanDimensionKind.OpeningWidth)
        {
            var opening = document.Openings.FirstOrDefault(opening => opening.Id == dimension.OwnerId);
            if (opening is null) return null;
            var host = document.Walls.First(wall => wall.Id == opening.WallId);
            var begin = dimension.Kind == PlanDimensionKind.OpeningStart ? 0 :
                dimension.Kind == PlanDimensionKind.OpeningEnd ? opening.End : opening.Begin;
            var end = dimension.Kind == PlanDimensionKind.OpeningEnd ? host.Length :
                dimension.Kind == PlanDimensionKind.OpeningStart ? opening.Begin : opening.End;
            var across = (host.Thickness / 2 + 0.45) * (dimension.Kind == PlanDimensionKind.OpeningWidth ? -1 : 1);
            return new(At(host, begin, across), At(host, end, across), end - begin);
        }
        if (dimension.ReferenceId is not Guid referenceId || Bounds(document, referenceId) is not { } reference ||
            reference.LevelId != owner.LevelId) return null;
        var bounds = owner.Bounds;
        var target = reference.Bounds;
        var x = (bounds.MinX + bounds.MaxX) / 2;
        var y = (bounds.MinY + bounds.MaxY) / 2;
        return dimension.Kind switch
        {
            PlanDimensionKind.Left => new(new(bounds.MinX, y), new(target.MaxX, y), bounds.MinX - target.MaxX),
            PlanDimensionKind.Right => new(new(bounds.MaxX, y), new(target.MinX, y), target.MinX - bounds.MaxX),
            PlanDimensionKind.Below => new(new(x, bounds.MinY), new(x, target.MaxY), bounds.MinY - target.MaxY),
            PlanDimensionKind.Above => new(new(x, bounds.MaxY), new(x, target.MinY), target.MinY - bounds.MaxY),
            _ => null
        };
    }

    /// <summary>Rejects invalid annotation identities, references and unsupported measurement kinds.</summary>
    public static void Validate(BuildingDocument document)
    {
        if (document.Dimensions is null || document.Dimensions.Count > 100000 ||
            document.Dimensions.Select(item => item?.Id).Distinct().Count() != document.Dimensions.Count ||
            document.Dimensions.Any(item => item is null || item.Id == Guid.Empty || !Enum.IsDefined(item.Kind) ||
                Resolve(document, item) is null))
            throw new InvalidDataException("Invalid plan dimension references.");
    }

    /// <summary>Returns the axis-aligned envelope used for cardinal clear-gap measurements.</summary>
    private static PlanBounds Enclose(IEnumerable<Point2> points)
    {
        var vertices = points.ToArray();
        return new(vertices.Min(point => point.X), vertices.Min(point => point.Y),
            vertices.Max(point => point.X), vertices.Max(point => point.Y));
    }

    /// <summary>Converts along/across coordinates into world coordinates for a wall.</summary>
    private static Point2 At(Wall wall, double along, double across)
    {
        var x = (wall.End.X - wall.Start.X) / wall.Length;
        var y = (wall.End.Y - wall.Start.Y) / wall.Length;
        return new(wall.Start.X + x * along - y * across, wall.Start.Y + y * along + x * across);
    }
}
