using Guanacaste3D.Core;

namespace Guanacaste3D.CrossPlatform;

/// <summary>
/// Device-independent drafting snaps. Pixel tolerance is converted to metres at the current zoom.
/// Endpoints win over the optional metric grid, and ties are deterministic.
/// No snap operation mutates the BIM document.
/// </summary>
public static class PlanSnapping
{
    public enum SnapKind { None, Endpoint, Grid, Orthogonal }

    public readonly record struct Result(Point2 Point, SnapKind Kind);

    public static Result Resolve(BuildingDocument document, Guid? activeLevelId,
        Point2 pointer, double pixelsPerMeter, bool useGrid, bool useEndpoints,
        Point2? firstPoint = null, bool orthogonal = false, double gridMeters = 0.25,
        double tolerancePixels = 12)
    {
        if (!double.IsFinite(pointer.X) || !double.IsFinite(pointer.Y) ||
            !double.IsFinite(pixelsPerMeter) || pixelsPerMeter <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointer), "The pointer or zoom is invalid.");
        if (!double.IsFinite(gridMeters) || gridMeters <= 0 ||
            !double.IsFinite(tolerancePixels) || tolerancePixels < 0)
            throw new ArgumentOutOfRangeException(nameof(gridMeters), "Invalid snap settings.");

        // Restrict orthogonal drawing to the axis with the greatest displacement.
        var constrained = pointer;
        if (orthogonal && firstPoint is Point2 origin)
        {
            var deltaX = pointer.X - origin.X;
            var deltaY = pointer.Y - origin.Y;
            constrained = Math.Abs(deltaX) >= Math.Abs(deltaY)
                ? new Point2(pointer.X, origin.Y)
                : new Point2(origin.X, pointer.Y);
        }

        var radius = tolerancePixels / pixelsPerMeter;
        if (useEndpoints)
        {
            Point2? closest = null;
            var bestSquared = radius * radius;
            var walls = document.Walls.Where(w => w.LevelId == activeLevelId)
                .SelectMany(w => new[] { w.Start, w.End });
            var axes = document.Grids.SelectMany(g => new[] { g.Start, g.End });
            foreach (var candidate in walls.Concat(axes))
            {
                if (orthogonal && firstPoint is Point2 fixedPoint &&
                    ((Math.Abs(constrained.Y - fixedPoint.Y) < 1e-9 && Math.Abs(candidate.Y - fixedPoint.Y) > 1e-8) ||
                     (Math.Abs(constrained.X - fixedPoint.X) < 1e-9 && Math.Abs(candidate.X - fixedPoint.X) > 1e-8)))
                    continue;
                var dx = candidate.X - constrained.X;
                var dy = candidate.Y - constrained.Y;
                var distanceSquared = dx * dx + dy * dy;
                if (distanceSquared < bestSquared)
                {
                    bestSquared = distanceSquared;
                    closest = candidate;
                }
            }
            if (closest is Point2 endpoint)
                return new Result(endpoint, SnapKind.Endpoint);
        }

        if (useGrid)
        {
            var snapX = Math.Round(constrained.X / gridMeters, MidpointRounding.AwayFromZero) * gridMeters;
            var snapY = Math.Round(constrained.Y / gridMeters, MidpointRounding.AwayFromZero) * gridMeters;
            if (orthogonal && firstPoint is Point2 anchor)
            {
                if (Math.Abs(constrained.X - anchor.X) < 1e-9) snapX = anchor.X;
                else snapY = anchor.Y;
            }
            var gridPoint = new Point2(snapX, snapY);
            if (gridPoint.DistanceTo(constrained) <= radius)
                return new Result(gridPoint, SnapKind.Grid);
        }

        return new Result(constrained,
            orthogonal && firstPoint is not null ? SnapKind.Orthogonal : SnapKind.None);
    }
}
