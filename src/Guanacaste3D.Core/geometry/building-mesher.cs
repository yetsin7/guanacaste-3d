using System.Numerics;

namespace Guanacaste3D.Core.Geometry;

/// <summary>Physical surface categories that renderers map to visual materials.</summary>
public enum SurfaceKind : byte
{
    Wall,
    Floor,
    Glass,
    Ground,
    DoorLeaf,
    OpeningFrame,
    Structure
}

/// <summary>An indexed triangle soup in Z-up model coordinates (meters).</summary>
public sealed class SceneMesh
{
    public List<Vector3> Positions { get; } = [];
    public List<int> Indices { get; } = [];
    public List<SurfaceKind> TriangleKinds { get; } = [];

    public int TriangleCount => TriangleKinds.Count;

    /// <summary>Appends one triangle with its own three vertices.</summary>
    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, SurfaceKind kind)
    {
        var start = Positions.Count;
        Positions.Add(a);
        Positions.Add(b);
        Positions.Add(c);
        Indices.Add(start);
        Indices.Add(start + 1);
        Indices.Add(start + 2);
        TriangleKinds.Add(kind);
    }

    /// <summary>Appends a planar quad given in counter-clockwise order seen from its front side.</summary>
    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, SurfaceKind kind)
    {
        AddTriangle(a, b, c, kind);
        AddTriangle(a, c, d, kind);
    }

    /// <summary>Returns the axis-aligned bounds of all vertices, or a unit box when empty.</summary>
    public (Vector3 Min, Vector3 Max) Bounds()
    {
        if (Positions.Count == 0) return (Vector3.Zero, Vector3.One);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in Positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }
        return (min, max);
    }
}

/// <summary>Converts the BIM document into renderer-neutral triangle geometry.</summary>
public static class BuildingMesher
{
    /// <summary>Builds closed solids for walls (split around openings), slabs, and glazing panes.</summary>
    public static SceneMesh Build(BuildingDocument document, bool includeGround = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        var elevations = document.Levels.ToDictionary(level => level.Id, level => level.Elevation);
        var mesh = new SceneMesh();
        var typesById = document.OpeningTypes.ToDictionary(type => type.Id);
        var joins = WallJoinIndex.Build(document.Walls);

        foreach (var wall in document.Walls)
        {
            var z = elevations[wall.LevelId];
            var openings = document.Openings.Where(opening => opening.WallId == wall.Id).ToArray();
            foreach (var solid in WallGeometry.Solids(wall, openings))
                AddWallSection(mesh, joins.SectionOutline(wall, solid.From, solid.To),
                    z + solid.Bottom, z + solid.Top, SurfaceKind.Wall);
            foreach (var opening in openings)
            {
                if (!typesById.TryGetValue(opening.OpeningTypeId, out var type)) continue;
                AddOpeningAssembly(mesh, wall, opening, type, z);
            }
        }

        foreach (var floor in document.Floors)
            AddSlab(mesh, floor.Boundary, elevations[floor.LevelId] + floor.ElevationOffset, floor.Thickness,
                FloorOpenings(document, floor, elevations));

        foreach (var column in document.Columns)
            AddColumn(mesh, column, elevations[column.LevelId], document.Levels);

        foreach (var element in document.StructuralElements.Where(element => element.HasSolid))
        {
            var bottom = elevations[element.LevelId] + element.Offset;
            AddPrism(mesh, element.Footprint.Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray(),
                (float)bottom, (float)(bottom + element.Height), SurfaceKind.Structure);
        }

        if (includeGround)
        {
            var (min, max) = mesh.Bounds();
            var center = (min + max) / 2;
            // Large enough that the ground meets the sky at the horizon from typical orbit distances.
            var extent = MathF.Max(500, (max - min).Length() * 20);
            var z = MathF.Min(0, min.Z) - 0.01f;
            mesh.AddQuad(
                new(center.X - extent, center.Y - extent, z), new(center.X + extent, center.Y - extent, z),
                new(center.X + extent, center.Y + extent, z), new(center.X - extent, center.Y + extent, z),
                SurfaceKind.Ground);
        }
        return mesh;
    }

    /// <summary>
    /// Builds a hosted opening in layers, like a detailed family: a lining (mocheta) recessed inside
    /// the rough opening, then a door leaf with stiles, rails and a recessed panel, or a window sash
    /// with glass and a projecting sill. Each member is its own solid so its profile reads on screen.
    /// </summary>
    private static void AddOpeningAssembly(SceneMesh mesh, Wall wall, WallOpening opening, OpeningType type,
        double elevation)
    {
        var bottom = elevation + opening.Sill;
        var top = bottom + opening.Height;
        var lining = Math.Min(0.06, Math.Min(opening.Width, opening.Height) / 6);
        // The lining sits 1 cm inside both wall faces, so the reveal reads as a step rather than a flush cut.
        var liningHalf = wall.Thickness / 2 - Math.Min(0.01, wall.Thickness / 4);
        AddWallBlock(mesh, wall, opening.Begin, opening.Begin + lining, -liningHalf, liningHalf, bottom, top,
            SurfaceKind.OpeningFrame);
        AddWallBlock(mesh, wall, opening.End - lining, opening.End, -liningHalf, liningHalf, bottom, top,
            SurfaceKind.OpeningFrame);
        AddWallBlock(mesh, wall, opening.Begin + lining, opening.End - lining, -liningHalf, liningHalf,
            top - lining, top, SurfaceKind.OpeningFrame);
        if (opening.Kind == OpeningKind.Window)
        {
            AddWindowSash(mesh, wall, opening, lining, bottom, top);
            AddWindowSill(mesh, wall, opening, bottom);
            return;
        }
        var depth = WallGeometry.LeafDepth(type, wall);
        if (depth <= 0) return;
        AddDoorLeaves(mesh, wall, opening, depth, lining, bottom, top);
    }

    /// <summary>Adds the door leaves, one per half of a wide opening, each clear of the lining by a few millimeters.</summary>
    private static void AddDoorLeaves(SceneMesh mesh, Wall wall, WallOpening opening, double depth, double lining,
        double bottom, double top)
    {
        const double clearance = 0.004;
        const double thresholdGap = 0.012;
        var leafCount = opening.Width > 1.2 ? 2 : 1;
        var left = opening.Begin + lining + clearance;
        var right = opening.End - lining - clearance;
        var leafWidth = (right - left) / leafCount;
        for (var leaf = 0; leaf < leafCount; leaf++)
        {
            var from = left + leaf * leafWidth;
            AddDoorPanel(mesh, wall, from, from + leafWidth, depth, bottom + thresholdGap, top - clearance);
        }
    }

    /// <summary>
    /// Adds one door panel: a full-depth frame of hinge and latch stiles, a head rail and a kick rail,
    /// with a thinner raised panel between them that sits recessed inside the frame.
    /// </summary>
    private static void AddDoorPanel(SceneMesh mesh, Wall wall, double from, double to, double depth,
        double bottom, double top)
    {
        const double stile = 0.07;
        const double headRail = 0.09;
        const double kickRail = 0.10;
        var half = depth / 2;
        var panelHalf = depth * 0.15;
        var kind = SurfaceKind.DoorLeaf;
        AddWallBlock(mesh, wall, from, from + stile, -half, half, bottom, top, kind);
        AddWallBlock(mesh, wall, to - stile, to, -half, half, bottom, top, kind);
        AddWallBlock(mesh, wall, from + stile, to - stile, -half, half, top - headRail, top, kind);
        AddWallBlock(mesh, wall, from + stile, to - stile, -half, half, bottom, bottom + kickRail, kind);
        AddWallBlock(mesh, wall, from + stile, to - stile, -panelHalf, panelHalf,
            bottom + kickRail, top - headRail, kind);
    }

    /// <summary>
    /// Adds the sash frame and glass inside the lining. A wide window is split into two lights by a
    /// mullion, the way a sliding window carries two sashes.
    /// </summary>
    private static void AddWindowSash(SceneMesh mesh, Wall wall, WallOpening opening, double lining,
        double bottom, double top)
    {
        const double member = 0.05;
        const double sashHalf = 0.025;
        const double glassHalf = 0.003;
        var from = opening.Begin + lining;
        var to = opening.End - lining;
        var sashBottom = bottom + lining;
        var sashTop = top - lining;
        var lights = opening.Width > 1.0 ? 2 : 1;
        var lightWidth = (to - from) / lights;
        for (var light = 0; light < lights; light++)
        {
            var lightFrom = from + light * lightWidth;
            var lightTo = lightFrom + lightWidth;
            AddWallBlock(mesh, wall, lightFrom, lightFrom + member, -sashHalf, sashHalf, sashBottom, sashTop,
                SurfaceKind.OpeningFrame);
            AddWallBlock(mesh, wall, lightTo - member, lightTo, -sashHalf, sashHalf, sashBottom, sashTop,
                SurfaceKind.OpeningFrame);
            AddWallBlock(mesh, wall, lightFrom + member, lightTo - member, -sashHalf, sashHalf,
                sashTop - member, sashTop, SurfaceKind.OpeningFrame);
            AddWallBlock(mesh, wall, lightFrom + member, lightTo - member, -sashHalf, sashHalf,
                sashBottom, sashBottom + member, SurfaceKind.OpeningFrame);
            AddWallBlock(mesh, wall, lightFrom + member, lightTo - member, -glassHalf, glassHalf,
                sashBottom + member, sashTop - member, SurfaceKind.Glass);
        }
    }

    /// <summary>Adds a projecting window sill that overhangs both wall faces and the opening by 3 cm.</summary>
    private static void AddWindowSill(SceneMesh mesh, Wall wall, WallOpening opening, double bottom)
    {
        const double overhang = 0.03;
        const double thickness = 0.03;
        var projection = wall.Thickness / 2 + 0.02;
        AddWallBlock(mesh, wall, opening.Begin - overhang, opening.End + overhang, -projection, projection,
            bottom - thickness, bottom, SurfaceKind.OpeningFrame);
    }

    /// <summary>
    /// Adds an oriented box in wall coordinates: <paramref name="from"/> and <paramref name="to"/> run
    /// along the wall axis and <paramref name="sideA"/> and <paramref name="sideB"/> measure across it,
    /// all in meters from the wall centerline. Negative and positive sides may be given in any order.
    /// </summary>
    private static void AddWallBlock(SceneMesh mesh, Wall wall, double from, double to, double sideA, double sideB,
        double bottom, double top, SurfaceKind kind)
    {
        var length = wall.Length;
        var direction = new Vector2((float)((wall.End.X - wall.Start.X) / length),
            (float)((wall.End.Y - wall.Start.Y) / length));
        var normal = new Vector2(-direction.Y, direction.X);
        var origin = new Vector2((float)wall.Start.X, (float)wall.Start.Y);
        var a = origin + direction * (float)from;
        var b = origin + direction * (float)to;
        var near = normal * (float)sideA;
        var far = normal * (float)sideB;
        AddPrism(mesh, [a + near, b + near, b + far, a + far], (float)bottom, (float)top, kind);
    }

    /// <summary>Adds a full-thickness wall section whose plan outline is already mitered at joined corners.</summary>
    private static void AddWallSection(SceneMesh mesh, IReadOnlyList<Point2> outline, double bottom, double top,
        SurfaceKind kind) =>
        AddPrism(mesh, outline.Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray(),
            (float)bottom, (float)top, kind);

    /// <summary>
    /// Adds a slab whose top face lies on the level elevation. Openings that fit inside the boundary are cut
    /// out: the top and bottom faces are built from convex pieces, and each opening gets its own inner walls.
    /// </summary>
    private static void AddSlab(SceneMesh mesh, IReadOnlyList<Point2> boundary, double elevation, double thickness,
        IReadOnlyList<Geometry.PlanRectangle> openings)
    {
        if (openings.Count == 0)
        {
            var outline = boundary.Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray();
            AddPrism(mesh, outline, (float)(elevation - thickness), (float)elevation, SurfaceKind.Floor);
            return;
        }

        var bottom = (float)(elevation - thickness);
        var top = (float)elevation;
        var ordered = PolygonTriangulator.SignedDoubleArea(boundary) > 0 ? boundary.ToArray() : boundary.Reverse().ToArray();
        foreach (var piece in SlabOpenings.Pieces(ordered, openings))
        {
            for (var corner = 1; corner < piece.Length - 1; corner++)
            {
                mesh.AddTriangle(LiftPoint(piece[0], top), LiftPoint(piece[corner], top),
                    LiftPoint(piece[corner + 1], top), SurfaceKind.Floor);
                mesh.AddTriangle(LiftPoint(piece[corner + 1], bottom), LiftPoint(piece[corner], bottom),
                    LiftPoint(piece[0], bottom), SurfaceKind.Floor);
            }
        }
        AddSideWalls(mesh, ordered, bottom, top);
        foreach (var opening in openings)
            // Walking the opening clockwise makes its walls face into the hole, away from the slab.
            AddSideWalls(mesh, Geometry.SlabOpenings.Corners(opening).Reverse().ToArray(), bottom, top);
    }

    /// <summary>Returns the openings that cut a floor: its own level's openings, and shafts from at or below it.</summary>
    private static IReadOnlyList<Geometry.PlanRectangle> FloorOpenings(BuildingDocument document, FloorSlab floor,
        IReadOnlyDictionary<Guid, double> elevations)
    {
        var floorElevation = elevations[floor.LevelId];
        return document.StructuralElements
            .Where(element => (element.Kind == StructuralElementKind.Opening && element.LevelId == floor.LevelId) ||
                (element.Kind == StructuralElementKind.Shaft && elevations[element.LevelId] <= floorElevation) ||
                // A dormer opens the roof it stands on; other slabs keep their solid top.
                (element.Kind == StructuralElementKind.Dormer && floor.Kind == FloorKind.Roof &&
                    element.LevelId == floor.LevelId))
            .Select(element => Geometry.PlanRectangle.Of(element.Footprint))
            .Where(rectangle => Geometry.SlabOpenings.ContainsRectangle(floor.Boundary, rectangle))
            .ToArray();
    }

    /// <summary>Adds the vertical quads of a closed plan outline, each facing outward from the enclosed area.</summary>
    private static void AddSideWalls(SceneMesh mesh, IReadOnlyList<Point2> outline, float bottom, float top)
    {
        for (var index = 0; index < outline.Count; index++)
        {
            var current = outline[index];
            var next = outline[(index + 1) % outline.Count];
            mesh.AddQuad(LiftPoint(current, bottom), LiftPoint(next, bottom),
                LiftPoint(next, top), LiftPoint(current, top), SurfaceKind.Floor);
        }
    }

    /// <summary>Raises a plan point to a model elevation.</summary>
    private static Vector3 LiftPoint(Point2 point, float z) => new((float)point.X, (float)point.Y, z);

    /// <summary>Adds a rectangular column standing on its level, up to the resolved column top.</summary>
    private static void AddColumn(SceneMesh mesh, Column column, double elevation, IReadOnlyList<Level> levels)
    {
        var halfWidth = column.Width / 2;
        var halfDepth = column.Depth / 2;
        var x = column.Position.X;
        var y = column.Position.Y;
        var outline = new[]
        {
            new Vector2((float)(x - halfWidth), (float)(y - halfDepth)),
            new Vector2((float)(x + halfWidth), (float)(y - halfDepth)),
            new Vector2((float)(x + halfWidth), (float)(y + halfDepth)),
            new Vector2((float)(x - halfWidth), (float)(y + halfDepth))
        };
        AddPrism(mesh, outline, (float)elevation, (float)ColumnTop(column, elevation, levels), SurfaceKind.Wall);
    }

    /// <summary>Resolves the top elevation, in meters, of a column whose base sits at the given elevation.</summary>
    private static double ColumnTop(Column column, double baseElevation, IReadOnlyList<Level> levels)
    {
        // TODO(human): decide how the column top is resolved. The default below trusts the stored height.
        return baseElevation + column.Height;
    }

    /// <summary>Extrudes a simple polygon vertically into a closed, outward-facing prism.</summary>
    private static void AddPrism(SceneMesh mesh, IReadOnlyList<Vector2> outline, float bottom, float top,
        SurfaceKind kind)
    {
        var plan = outline.Select(point => new Point2(point.X, point.Y)).ToArray();
        var counterClockwise = PolygonTriangulator.SignedDoubleArea(plan) > 0;
        var ordered = counterClockwise ? outline.ToArray() : outline.Reverse().ToArray();
        var orderedPlan = ordered.Select(point => new Point2(point.X, point.Y)).ToArray();
        foreach (var (ia, ib, ic) in PolygonTriangulator.Triangulate(orderedPlan))
        {
            mesh.AddTriangle(Lift(ordered[ia], top), Lift(ordered[ib], top), Lift(ordered[ic], top), kind);
            mesh.AddTriangle(Lift(ordered[ic], bottom), Lift(ordered[ib], bottom), Lift(ordered[ia], bottom), kind);
        }
        for (var index = 0; index < ordered.Length; index++)
        {
            var current = ordered[index];
            var next = ordered[(index + 1) % ordered.Length];
            mesh.AddQuad(Lift(current, bottom), Lift(next, bottom), Lift(next, top), Lift(current, top), kind);
        }
    }

    /// <summary>Raises a plan point to a model elevation for prism faces.</summary>
    private static Vector3 Lift(Vector2 point, float z) => new(point.X, point.Y, z);
}
