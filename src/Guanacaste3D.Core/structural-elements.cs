using System.Text.Json.Serialization;

namespace Guanacaste3D.Core;

/// <summary>Identifies the structural, foundation, connection, or drafting role of an element.</summary>
public enum StructuralElementKind
{
    Beam,
    Brace,
    Truss,
    WallFoundation,
    IsolatedFoundation,
    Component,
    Connection,
    ModelLine,

    /// <summary>A rectangular hole through the floor slab of its own level.</summary>
    Opening,

    /// <summary>A rectangular hole through every floor slab at or above its level.</summary>
    Shaft,

    /// <summary>An annotation placed in the plan; its text is stored in the element name.</summary>
    ModelText,

    /// <summary>A box rising from a roof slab. It cuts an opening through that roof and stands on its top face.</summary>
    Dormer,

    /// <summary>A straight run of solid steps rising from the start to the end.</summary>
    Stair,

    /// <summary>A straight guard with posts and two horizontal rails.</summary>
    Railing
}

/// <summary>
/// A framing, foundation, or connection element tied to a building level. Line kinds run from
/// <see cref="Start"/> to <see cref="End"/>; point kinds share one position for both ends.
/// <see cref="Width"/> is the plan size across the element, <see cref="Height"/> its vertical size, and
/// <see cref="Offset"/> the elevation of its underside relative to the level datum.
/// </summary>
public sealed record StructuralElement(
    Guid Id,
    Guid LevelId,
    StructuralElementKind Kind,
    string Name,
    Point2 Start,
    Point2 End,
    double Width,
    double Height,
    double Offset)
{
    /// <summary>Members of one model group share this id; moves and deletes act on the whole group.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Drafting marks and openings have no solid of their own; openings only cut floor slabs.</summary>
    [JsonIgnore] public bool HasSolid => Kind is not (StructuralElementKind.ModelLine or StructuralElementKind.Opening
        or StructuralElementKind.Shaft or StructuralElementKind.ModelText);

    /// <summary>Plan outline: a rectangle along the axis for lines, or a square around the point for points.</summary>
    [JsonIgnore]
    public IReadOnlyList<Point2> Footprint
    {
        get
        {
            var half = Width / 2;
            var length = Start.DistanceTo(End);
            if (Kind is StructuralElementKind.Opening or StructuralElementKind.Shaft or StructuralElementKind.Dormer
                && length >= 1e-9)
                return Geometry.SlabOpenings.Corners(PlanExtent);
            if (length < 1e-9)
                return
                [
                    new(Start.X - half, Start.Y - half), new(Start.X + half, Start.Y - half),
                    new(Start.X + half, Start.Y + half), new(Start.X - half, Start.Y + half)
                ];

            var normalX = -(End.Y - Start.Y) / length * half;
            var normalY = (End.X - Start.X) / length * half;
            return
            [
                new(Start.X - normalX, Start.Y - normalY), new(End.X - normalX, End.Y - normalY),
                new(End.X + normalX, End.Y + normalY), new(Start.X + normalX, Start.Y + normalY)
            ];
        }
    }

    [JsonIgnore] public double Length => Start.DistanceTo(End);

    /// <summary>Bounding rectangle of the two corners of an opening or shaft drawn by its two clicks.</summary>
    [JsonIgnore]
    public Geometry.PlanRectangle PlanExtent => Geometry.PlanRectangle.Of([Start, End]);
}

/// <summary>Holds the default section, footprint, and elevation used when a tool places an element.</summary>
public static class StructuralElementStandards
{
    /// <summary>Returns the width, height, and underside offset for a new element of the given kind.</summary>
    public static (double Width, double Height, double Offset) For(StructuralElementKind kind) => kind switch
    {
        // Beams and trusses hang below the level so their top sits on it, like a framed floor.
        StructuralElementKind.Beam => (0.30, 0.50, -0.50),
        StructuralElementKind.Truss => (0.20, 0.80, -0.80),
        StructuralElementKind.Brace => (0.30, 0.30, -0.30),
        StructuralElementKind.WallFoundation => (0.40, 0.60, -0.60),
        StructuralElementKind.IsolatedFoundation => (1.20, 0.40, -0.40),
        StructuralElementKind.Component => (0.40, 0.40, 0),
        StructuralElementKind.Connection => (0.20, 0.02, 0),
        // Openings are placed as 1.20 m squares by a click, or as the rectangle between two clicks.
        StructuralElementKind.Opening => (1.20, 0, 0),
        StructuralElementKind.Shaft or StructuralElementKind.ModelText => (0, 0, 0),
        // A dormer's Height is its body height above the roof top; its Offset is set from the roof when placed.
        StructuralElementKind.Dormer => (0, 2.40, 0),
        StructuralElementKind.Stair => (1.20, 3.0, 0),
        StructuralElementKind.Railing => (0.06, 1.10, 0),
        _ => (0.02, 0, 0)
    };
}
