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
    Railing,

    /// <summary>A sloped walking surface rising from the start to the end by its height.</summary>
    Ramp,

    /// <summary>A rain gutter (canal) running along the eave between its two points.</summary>
    Gutter,

    /// <summary>A vertical rainwater pipe (bajante) at a point, running up from its offset by its height.</summary>
    Downspout,

    /// <summary>A glazed wall system with a mullion and transom grid between its two points.</summary>
    CurtainWall,

    /// <summary>A glazed rooflight in a rectangle of a roof; it cuts the roof and stands on its top face.</summary>
    Skylight,

    /// <summary>A datum line that hosts and dimensions can refer to. It has no solid.</summary>
    ReferencePlane,

    /// <summary>A line that divides a space into rooms without a wall. It has no solid.</summary>
    RoomSeparator,

    /// <summary>A line that bounds an area of an area scheme. It has no solid.</summary>
    AreaBoundary,

    /// <summary>A rectangular area of an area scheme, measured in plan. It has no solid.</summary>
    Area,

    /// <summary>A profile swept along both faces of a wall, such as a skirting or cornice.</summary>
    WallSweep,

    /// <summary>A shallow groove line drafted along both faces of a wall.</summary>
    WallReveal,

    /// <summary>A flat panel under the overhang of a roof.</summary>
    Soffit,

    /// <summary>A vertical board closing the edge of a roof.</summary>
    Fascia,

    /// <summary>A band running along the edge of a floor slab.</summary>
    SlabEdge
}

/// <summary>Variant of an element's geometry. Which values apply depends on the element kind.</summary>
public enum ElementStyle
{
    /// <summary>The standard form of the kind.</summary>
    Default,

    /// <summary>Round section for point elements (piers, foundations, downspouts).</summary>
    Round,

    /// <summary>Wide-flange section for beams, braces and trusses.</summary>
    IShape,

    /// <summary>Tee section for beams, braces and trusses.</summary>
    Tee,

    /// <summary>A guard with closely spaced vertical balusters.</summary>
    Pickets,

    /// <summary>A guard with a glass panel between the rails.</summary>
    GlassPanel,

    /// <summary>A guard with slim horizontal cables.</summary>
    Cable,

    /// <summary>A solid parapet with a cap.</summary>
    SolidParapet,

    /// <summary>A stair of treads on stringers, without risers.</summary>
    Open
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
    /// <summary>
    /// Plan depth for point-hosted components. Zero preserves the legacy square footprint whose depth equals
    /// <see cref="Width"/>.
    /// </summary>
    public double Depth { get; init; }

    /// <summary>Parametric residential geometry used by component instances; other element kinds keep the box default.</summary>
    public ResidentialComponentShape ComponentShape { get; init; }

    /// <summary>Residential library group retained on component instances for schedules and future type browsing.</summary>
    public ResidentialComponentCategory ComponentCategory { get; init; }

    /// <summary>Members of one model group share this id; moves and deletes act on the whole group.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Curtain wall only: center-to-center spacing of mullions in meters; zero means the 1.5 m default.</summary>
    public double GridWidth { get; init; }

    /// <summary>Curtain wall only: center-to-center spacing of transoms in meters; zero means the 1.2 m default.</summary>
    public double GridHeight { get; init; }

    /// <summary>Guards only: vertical rise from the start to the end, so a guard can follow a stair or ramp.</summary>
    public double Rise { get; init; }

    /// <summary>Geometry variant: beam section, guard type, stair type, or round point elements.</summary>
    public ElementStyle Style { get; init; }

    /// <summary>Drafting marks and openings have no solid of their own; openings only cut floor slabs.</summary>
    [JsonIgnore] public bool HasSolid => Kind is not (StructuralElementKind.ModelLine or StructuralElementKind.Opening
        or StructuralElementKind.Shaft or StructuralElementKind.ModelText or StructuralElementKind.ReferencePlane
        or StructuralElementKind.RoomSeparator or StructuralElementKind.AreaBoundary or StructuralElementKind.Area);

    /// <summary>Whether the kind is drafted as a single line between its two points.</summary>
    [JsonIgnore] public bool IsDatumLine => Kind is StructuralElementKind.ReferencePlane
        or StructuralElementKind.RoomSeparator or StructuralElementKind.AreaBoundary
        or StructuralElementKind.ModelLine;

    /// <summary>Plan area in square meters of an area rectangle; zero for every other kind.</summary>
    [JsonIgnore] public double PlanArea => Kind == StructuralElementKind.Area
        ? Math.Abs(End.X - Start.X) * Math.Abs(End.Y - Start.Y) : 0;

    /// <summary>Plan outline: a rectangle along the axis for lines, or a square around the point for points.</summary>
    [JsonIgnore]
    public IReadOnlyList<Point2> Footprint
    {
        get
        {
            var half = Width / 2;
            var halfDepth = (Depth > 0 ? Depth : Width) / 2;
            var length = Start.DistanceTo(End);
            if (Kind is StructuralElementKind.Opening or StructuralElementKind.Shaft or StructuralElementKind.Dormer
                    or StructuralElementKind.Skylight or StructuralElementKind.Area && length >= 1e-9)
                return Geometry.SlabOpenings.Corners(PlanExtent);
            if (length < 1e-9)
                return
                [
                    new(Start.X - half, Start.Y - halfDepth), new(Start.X + half, Start.Y - halfDepth),
                    new(Start.X + half, Start.Y + halfDepth), new(Start.X - half, Start.Y + halfDepth)
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
        // A ramp rises 0.60 m by default; its run is the distance between the clicks (1:12 is 7.2 m).
        StructuralElementKind.Ramp => (1.50, 0.60, 0),
        // Gutters hang at the eave of a 3 m wall; the channel is 12 cm wide and 10 cm deep.
        StructuralElementKind.Gutter => (0.12, 0.10, 2.85),
        StructuralElementKind.Downspout => (0.09, 2.85, 0),
        StructuralElementKind.CurtainWall => (0.10, 3.0, 0),
        StructuralElementKind.Skylight => (0, 0.15, 0),
        StructuralElementKind.ReferencePlane or StructuralElementKind.RoomSeparator
            or StructuralElementKind.AreaBoundary or StructuralElementKind.Area => (0, 0, 0),
        // A sweep's width is the host wall's thickness; a reveal is a 2 cm groove at 1 m.
        StructuralElementKind.WallSweep => (0.20, 0.10, 0),
        StructuralElementKind.WallReveal => (0.20, 0.02, 1.0),
        StructuralElementKind.Soffit => (0.60, 0.02, 2.95),
        StructuralElementKind.Fascia => (0.03, 0.25, 2.80),
        StructuralElementKind.SlabEdge => (0.20, 0.30, -0.30),
        _ => (0.02, 0, 0)
    };
}
