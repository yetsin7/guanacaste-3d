namespace Guanacaste3D.Core;

/// <summary>Drafting role of one plan primitive, so a view can choose line weight and fill per role.</summary>
public enum DoorPlanPart { Frame, Casing, Leaf }

/// <summary>A closed outline in host-wall coordinates: u along the wall from its start, v across toward its left normal.</summary>
public sealed record DoorPlanOutline(DoorPlanPart Part, IReadOnlyList<(double U, double V)> Points);

/// <summary>A circular swing arc in host-wall coordinates; angles are radians measured from the +u axis.</summary>
public readonly record struct DoorPlanArc(double CenterU, double CenterV, double Radius, double FromAngle, double ToAngle);

/// <summary>A straight drafting line in host-wall coordinates.</summary>
public readonly record struct DoorPlanLine((double U, double V) From, (double U, double V) To);

/// <summary>Every primitive of one door's plan symbol, all in the host wall's local coordinates.</summary>
public sealed record DoorPlanSymbolGeometry(
    IReadOnlyList<DoorPlanOutline> Outlines,
    IReadOnlyList<DoorPlanArc> Swings,
    IReadOnlyList<DoorPlanLine> Thresholds);

/// <summary>
/// Builds the architectural plan symbol of a hosted door with the same frame and leaf rules the 3D
/// mesher uses: a lining at each jamb set 1 cm inside both wall faces, a stop on each jamb, casings on
/// both faces, one leaf (two above 1.2 m) drawn open at 90° on the left side of the wall, and its swing arc.
/// </summary>
public static class DoorPlanSymbol
{
    /// <summary>Width of a casing (architrave) along the wall, measured from the rough opening edge outward.</summary>
    public const double CasingWidth = 0.07;

    /// <summary>Thickness of a casing proud of the wall face.</summary>
    public const double CasingDepth = 0.015;

    /// <summary>Openings wider than this carry two leaves, matching the 3D door assembly.</summary>
    public const double DoubleLeafThreshold = 1.2;

    /// <summary>How far a door stop projects from its jamb into the clear opening.</summary>
    private const double StopProjection = 0.012;

    /// <summary>Depth of a door stop across the wall.</summary>
    private const double StopDepth = 0.02;

    /// <summary>Gap between a leaf edge and the lining, matching the 3D door assembly.</summary>
    private const double Clearance = 0.004;

    /// <summary>Returns the plan primitives for a door of the given rough opening inside a wall of the given thickness.</summary>
    public static DoorPlanSymbolGeometry Build(double begin, double end, double wallThickness, double leafDepth)
    {
        var width = end - begin;
        if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(wallThickness) || wallThickness <= 0)
            throw new ArgumentOutOfRangeException(nameof(end), "A door symbol needs a positive width and wall thickness.");
        var half = wallThickness / 2;
        var lining = Math.Min(0.06, width / 6);
        var liningHalf = half - Math.Min(0.01, wallThickness / 4);
        var outlines = new List<DoorPlanOutline>
        {
            Box(DoorPlanPart.Frame, begin, begin + lining, -liningHalf, liningHalf),
            Box(DoorPlanPart.Frame, end - lining, end, -liningHalf, liningHalf)
        };
        foreach (var side in new[] { -1, 1 })
        {
            var (inner, outer) = side > 0 ? (half, half + CasingDepth) : (-half - CasingDepth, -half);
            outlines.Add(Box(DoorPlanPart.Casing, begin - CasingWidth + lining, begin + lining, inner, outer));
            outlines.Add(Box(DoorPlanPart.Casing, end - lining, end + CasingWidth - lining, inner, outer));
        }

        var swings = new List<DoorPlanArc>();
        var depth = Math.Clamp(leafDepth, 0.02, Math.Max(0.02, 2 * liningHalf - StopDepth));
        // The leaf hinges at the swing-side face of the lining, so the open leaf starts flush with the wall.
        // When closed it would lie just inside that face, against a stop on each jamb.
        var hingeV = liningHalf;
        var stopFace = liningHalf - depth;
        foreach (var (from, to) in new[] { (begin + lining, begin + lining + StopProjection), (end - lining - StopProjection, end - lining) })
            outlines.Add(Box(DoorPlanPart.Frame, from, to, stopFace - StopDepth, stopFace));
        var left = begin + lining + Clearance;
        var right = end - lining - Clearance;
        var leafCount = width > DoubleLeafThreshold ? 2 : 1;
        var leafWidth = (right - left) / leafCount;
        for (var leaf = 0; leaf < leafCount; leaf++)
        {
            // A single leaf hinges at the begin jamb; a pair hinges at both jambs and meets in the middle.
            var fromBegin = leaf == 0;
            var hingeU = fromBegin ? left : right;
            var leafU = fromBegin ? (hingeU, hingeU + depth) : (hingeU - depth, hingeU);
            outlines.Add(Box(DoorPlanPart.Leaf, leafU.Item1, leafU.Item2, hingeV, hingeV + leafWidth));
            swings.Add(fromBegin
                ? new DoorPlanArc(hingeU, hingeV, leafWidth, 0, Math.PI / 2)
                : new DoorPlanArc(hingeU, hingeV, leafWidth, Math.PI / 2, Math.PI));
        }

        var thresholds = new[]
        {
            new DoorPlanLine((begin + lining, -liningHalf), (end - lining, -liningHalf)),
            new DoorPlanLine((begin + lining, liningHalf), (end - lining, liningHalf))
        };
        return new DoorPlanSymbolGeometry(outlines, swings, thresholds);
    }

    /// <summary>Returns the points of an arc, inclusive of both ends, for views that draw polylines.</summary>
    public static IEnumerable<(double U, double V)> Sample(DoorPlanArc arc, int segments = 32)
    {
        for (var step = 0; step <= segments; step++)
        {
            var angle = arc.FromAngle + (arc.ToAngle - arc.FromAngle) * step / segments;
            yield return (arc.CenterU + arc.Radius * Math.Cos(angle), arc.CenterV + arc.Radius * Math.Sin(angle));
        }
    }

    /// <summary>Returns an axis-aligned rectangle in host-wall coordinates.</summary>
    private static DoorPlanOutline Box(DoorPlanPart part, double u0, double u1, double v0, double v1) =>
        new(part, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]);
}
