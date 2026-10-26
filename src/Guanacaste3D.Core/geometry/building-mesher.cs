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
    Structure,
    /// <summary>Leaves of trees and shrubs.</summary>
    Foliage,
    /// <summary>Trunks and limbs of trees.</summary>
    Bark
}

/// <summary>An indexed triangle soup in Z-up model coordinates (meters).</summary>
public sealed class SceneMesh
{
    public List<Vector3> Positions { get; } = [];
    public List<int> Indices { get; } = [];
    public List<SurfaceKind> TriangleKinds { get; } = [];

    /// <summary>
    /// Per-vertex shading normals, aligned with <see cref="Positions"/>, or null when every triangle is flat. A zero
    /// normal marks a flat vertex. Created by the first <see cref="AddSmoothTriangle"/>.
    /// </summary>
    public List<Vector3>? Normals { get; private set; }

    public int TriangleCount => TriangleKinds.Count;

    /// <summary>Appends one triangle with its own three vertices.</summary>
    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, SurfaceKind kind)
    {
        var start = Positions.Count;
        if (Normals is { } normals)
        {
            normals.Add(Vector3.Zero);
            normals.Add(Vector3.Zero);
            normals.Add(Vector3.Zero);
        }
        Positions.Add(a);
        Positions.Add(b);
        Positions.Add(c);
        Indices.Add(start);
        Indices.Add(start + 1);
        Indices.Add(start + 2);
        TriangleKinds.Add(kind);
    }

    /// <summary>Appends a triangle shaded with interpolated vertex normals, for curved surfaces such as foliage.</summary>
    public void AddSmoothTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normalA, Vector3 normalB, Vector3 normalC,
        SurfaceKind kind)
    {
        if (Normals is null)
        {
            Normals = new List<Vector3>(Positions.Count + 3);
            for (var index = 0; index < Positions.Count; index++) Normals.Add(Vector3.Zero);
        }
        AddTriangle(a, b, c, kind);
        var start = Positions.Count - 3;
        Normals[start] = normalA;
        Normals[start + 1] = normalB;
        Normals[start + 2] = normalC;
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
        {
            var slabTop = elevations[floor.LevelId] + floor.ElevationOffset;
            if (floor.IsPitched) AddPitchedRoof(mesh, floor, slabTop);
            else AddSlab(mesh, floor.Boundary, slabTop, floor.Thickness, FloorOpenings(document, floor, elevations));
        }

        foreach (var column in document.Columns)
            AddColumn(mesh, column, elevations[column.LevelId], document.Levels);

        foreach (var element in document.StructuralElements.Where(element => element.HasSolid))
        {
            var datum = elevations[element.LevelId];
            if (element.Kind == StructuralElementKind.Planting)
            {
                PlantGeometry.AppendTo(mesh, element, datum);
                continue;
            }
            foreach (var part in ElementGeometry.Parts(element))
                AddPart(mesh, part, datum);
        }

        foreach (var family in document.InPlaceFamilies)
            FamilyGeometry.AppendTo(mesh, family);

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
        // A wall opening is a plain hole: no lining, sash, leaf or sill.
        if (type.Operation == OpeningOperation.Void) return;
        var bottom = elevation + opening.Sill;
        var top = bottom + opening.Height;
        var lining = Math.Min(type.FrameWidth, Math.Min(opening.Width, opening.Height) / 6);
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
            if (type.Operation is OpeningOperation.Standard or OpeningOperation.Casement or OpeningOperation.Awning)
                AddWindowSash(mesh, wall, opening, type, lining, bottom, top);
            else
                AddOperatedWindow(mesh, wall, opening, type, lining, bottom, top);
            if (type.Operation is OpeningOperation.Casement or OpeningOperation.Awning)
                AddWindowHandles(mesh, wall, opening, type, lining, bottom, top);
            AddWindowSill(mesh, wall, opening, bottom);
            return;
        }
        var depth = type.Operation == OpeningOperation.Revolving ? type.Depth : WallGeometry.LeafDepth(type, wall);
        if (depth <= 0) return;
        if (type.Operation == OpeningOperation.Standard)
            AddDoorLeaves(mesh, wall, opening, depth, lining, bottom, top, type.GlazedLeaf);
        else
            AddOperatedDoor(mesh, wall, opening, type, depth, lining, bottom, top);
    }

    /// <summary>
    /// Adds the leaves of a door that does not swing: sliding panels on two tracks, a closed pocket leaf, narrow
    /// folding leaves, overhead sections, or the glass wings of a revolving door.
    /// </summary>
    private static void AddOperatedDoor(SceneMesh mesh, Wall wall, WallOpening opening, OpeningType type, double depth,
        double lining, double bottom, double top)
    {
        const double clearance = 0.004;
        const double thresholdGap = 0.012;
        var left = opening.Begin + lining + clearance;
        var right = opening.End - lining - clearance;
        var low = bottom + thresholdGap;
        var high = top - lining - clearance;
        var panelKind = type.GlazedLeaf ? SurfaceKind.Glass : SurfaceKind.DoorLeaf;
        switch (type.Operation)
        {
            case OpeningOperation.Sliding:
            {
                // Two panels overlap by one stile and run on separate tracks, one in front of the other.
                const double overlap = 0.05;
                var panelWidth = (right - left + overlap) / 2;
                var half = Math.Min(depth, 0.06) / 2;
                AddWallBlock(mesh, wall, left, right, -half * 2, half * 2, high, high + lining, SurfaceKind.OpeningFrame);
                for (var panel = 0; panel < 2; panel++)
                {
                    var from = panel == 0 ? left : right - panelWidth;
                    var shift = panel == 0 ? -half : half;
                    AddSlidingPanel(mesh, wall, from, from + panelWidth, shift - half, shift + half, low, high, panelKind);
                }
                break;
            }
            case OpeningOperation.Pocket:
                AddDoorPanel(mesh, wall, left, right, Math.Min(depth, 0.035), low, high);
                break;
            case OpeningOperation.Folding:
            {
                var leaves = Math.Max(2, (int)Math.Round((right - left) / 0.45 / 2) * 2);
                var leafWidth = (right - left) / leaves;
                for (var leaf = 0; leaf < leaves; leaf++)
                {
                    var from = left + leaf * leafWidth;
                    var shift = leaf % 2 == 0 ? -0.012 : 0.012;
                    AddWallBlock(mesh, wall, from + 0.002, from + leafWidth - 0.002, shift - 0.015, shift + 0.015,
                        low, high, SurfaceKind.DoorLeaf);
                }
                break;
            }
            case OpeningOperation.Overhead:
            {
                var sections = Math.Max(4, (int)Math.Round((high - low) / 0.5));
                var sectionHeight = (high - low) / sections;
                var half = Math.Min(depth, wall.Thickness) / 2;
                for (var section = 0; section < sections; section++)
                {
                    var from = low + section * sectionHeight;
                    AddWallBlock(mesh, wall, left, right, -half, half, from + 0.005, from + sectionHeight - 0.005,
                        SurfaceKind.DoorLeaf);
                }
                break;
            }
            case OpeningOperation.Revolving:
            {
                var center = (opening.Begin + opening.End) / 2;
                var radius = Math.Min((right - left) / 2, depth / 2);
                AddWallBlock(mesh, wall, center - radius, center + radius, -0.02, 0.02, low, high, SurfaceKind.Glass);
                AddWallBlock(mesh, wall, center - 0.02, center + 0.02, -radius, radius, low, high, SurfaceKind.Glass);
                AddWallBlock(mesh, wall, center - 0.05, center + 0.05, -0.05, 0.05, low, high, SurfaceKind.OpeningFrame);
                break;
            }
        }
    }

    /// <summary>One sliding panel: a frame of four members around a pane that is glass or timber.</summary>
    private static void AddSlidingPanel(SceneMesh mesh, Wall wall, double from, double to, double sideA, double sideB,
        double bottom, double top, SurfaceKind pane)
    {
        const double member = 0.05;
        AddWallBlock(mesh, wall, from, from + member, sideA, sideB, bottom, top, SurfaceKind.OpeningFrame);
        AddWallBlock(mesh, wall, to - member, to, sideA, sideB, bottom, top, SurfaceKind.OpeningFrame);
        AddWallBlock(mesh, wall, from + member, to - member, sideA, sideB, top - member, top, SurfaceKind.OpeningFrame);
        AddWallBlock(mesh, wall, from + member, to - member, sideA, sideB, bottom, bottom + member, SurfaceKind.OpeningFrame);
        var middle = (sideA + sideB) / 2;
        AddWallBlock(mesh, wall, from + member, to - member, middle - 0.003, middle + 0.003, bottom + member, top - member, pane);
    }

    /// <summary>Adds the sashes of sliding, hung and louvered windows inside the lining.</summary>
    private static void AddOperatedWindow(SceneMesh mesh, Wall wall, WallOpening opening, OpeningType type,
        double lining, double bottom, double top)
    {
        var member = Math.Min(type.FrameWidth, 0.07);
        const double sashHalf = 0.02;
        const double glassHalf = 0.003;
        const double plane = 0.0225;
        var from = opening.Begin + lining;
        var to = opening.End - lining;
        var sashBottom = bottom + lining;
        var sashTop = top - lining;
        var columns = type.Operation == OpeningOperation.Sliding ? Math.Max(2, type.GlazingLightCount) : 1;
        var rows = type.Operation == OpeningOperation.Hung ? 2 : 1;
        var cellWidth = (to - from) / columns;
        var cellHeight = (sashTop - sashBottom) / rows;
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < columns; column++)
        {
            var x0 = from + column * cellWidth;
            var x1 = x0 + cellWidth;
            var y0 = sashBottom + row * cellHeight;
            var y1 = y0 + cellHeight;
            // Neighbouring sashes sit on alternate tracks so they can pass one another.
            var shift = type.Operation == OpeningOperation.Louvered ? 0 : ((row + column) % 2 == 0 ? -plane : plane);
            AddWallBlock(mesh, wall, x0, x0 + member, shift - sashHalf, shift + sashHalf, y0, y1, SurfaceKind.OpeningFrame);
            AddWallBlock(mesh, wall, x1 - member, x1, shift - sashHalf, shift + sashHalf, y0, y1, SurfaceKind.OpeningFrame);
            AddWallBlock(mesh, wall, x0 + member, x1 - member, shift - sashHalf, shift + sashHalf, y1 - member, y1,
                SurfaceKind.OpeningFrame);
            AddWallBlock(mesh, wall, x0 + member, x1 - member, shift - sashHalf, shift + sashHalf, y0, y0 + member,
                SurfaceKind.OpeningFrame);
            if (type.Operation == OpeningOperation.Louvered)
            {
                // Overlapping glass blades, each a thin horizontal slat.
                const double pitch = 0.07;
                const double blade = 0.045;
                for (var level = y0 + member; level + blade <= y1 - member + 1e-9; level += pitch)
                    AddWallBlock(mesh, wall, x0 + member, x1 - member, -0.035, 0.035, level, level + 0.006, SurfaceKind.Glass);
            }
            else
            {
                AddWallBlock(mesh, wall, x0 + member, x1 - member, shift - glassHalf, shift + glassHalf,
                    y0 + member, y1 - member, SurfaceKind.Glass);
            }
        }
    }

    /// <summary>Adds an operating handle on the free stile of each casement light, or at the base of an awning sash.</summary>
    private static void AddWindowHandles(SceneMesh mesh, Wall wall, WallOpening opening, OpeningType type, double lining,
        double bottom, double top)
    {
        var member = Math.Min(type.FrameWidth, 0.07);
        var from = opening.Begin + lining;
        var to = opening.End - lining;
        var lights = type.GlazingLightCount > 0 ? type.GlazingLightCount : opening.Width > 1.0 ? 2 : 1;
        var lightWidth = (to - from) / lights;
        if (type.Operation == OpeningOperation.Awning)
        {
            var center = (from + to) / 2;
            AddWallBlock(mesh, wall, center - 0.04, center + 0.04, 0.02, 0.04, bottom + lining + member / 2 - 0.01,
                bottom + lining + member / 2 + 0.01, SurfaceKind.OpeningFrame);
            return;
        }
        var handleHeight = bottom + (top - bottom) / 2;
        for (var light = 0; light < lights; light++)
        {
            // Handles alternate sides so adjacent casements meet at their handles.
            var edge = light % 2 == 0 ? from + (light + 1) * lightWidth - member : from + light * lightWidth + member;
            AddWallBlock(mesh, wall, edge - 0.01, edge + 0.01, 0.02, 0.04, handleHeight - 0.06, handleHeight + 0.06,
                SurfaceKind.OpeningFrame);
        }
    }

    /// <summary>Adds the door leaves, one per half of a wide opening, each clear of the lining by a few millimeters.</summary>
    private static void AddDoorLeaves(SceneMesh mesh, Wall wall, WallOpening opening, double depth, double lining,
        double bottom, double top, bool glazed = false)
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
            AddDoorPanel(mesh, wall, from, from + leafWidth, depth, bottom + thresholdGap, top - clearance, glazed);
        }
    }

    /// <summary>
    /// Adds one door panel: a full-depth frame of hinge and latch stiles, a head rail and a kick rail,
    /// with a thinner raised panel between them that sits recessed inside the frame.
    /// </summary>
    private static void AddDoorPanel(SceneMesh mesh, Wall wall, double from, double to, double depth,
        double bottom, double top, bool glazed = false)
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
            bottom + kickRail, top - headRail, glazed ? SurfaceKind.Glass : kind);
    }

    /// <summary>
    /// Adds the sash frame and glass inside the lining. A wide window is split into two lights by a
    /// mullion, the way a sliding window carries two sashes.
    /// </summary>
    private static void AddWindowSash(SceneMesh mesh, Wall wall, WallOpening opening, OpeningType type, double lining,
        double bottom, double top)
    {
        var member = Math.Min(type.FrameWidth, 0.07);
        const double sashHalf = 0.025;
        const double glassHalf = 0.003;
        var from = opening.Begin + lining;
        var to = opening.End - lining;
        var sashBottom = bottom + lining;
        var sashTop = top - lining;
        var lights = type.GlazingLightCount > 0 ? type.GlazingLightCount : opening.Width > 1.0 ? 2 : 1;
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
                (element.Kind is StructuralElementKind.Dormer or StructuralElementKind.Skylight && floor.Kind == FloorKind.Roof &&
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

    /// <summary>Adds one element part as a prism, or as a sloped wedge when it carries an end height.</summary>
    private static void AddPart(SceneMesh mesh, ElementPart part, double datum)
    {
        var outline = part.Footprint.Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray();
        if (part.EndTop is not double endTop || outline.Length != 4)
        {
            AddPrism(mesh, outline, (float)(datum + part.Bottom), (float)(datum + part.Top), part.Kind);
            return;
        }
        // Vertices 0 and 3 sit at the start (Top); vertices 1 and 2 at the end (EndTop).
        var bottom = (float)(datum + part.Bottom);
        var endBottom = (float)(datum + (part.EndBottom ?? part.Bottom));
        float[] bottoms = [bottom, endBottom, endBottom, bottom];
        float[] tops = [(float)(datum + part.Top), (float)(datum + endTop), (float)(datum + endTop), (float)(datum + part.Top)];
        var counterClockwise = PolygonTriangulator.SignedDoubleArea(
            outline.Select(point => new Point2(point.X, point.Y)).ToArray()) > 0;
        var order = counterClockwise ? new[] { 0, 1, 2, 3 } : new[] { 3, 2, 1, 0 };
        Vector3 Top(int index) => new(outline[order[index]].X, outline[order[index]].Y, tops[order[index]]);
        Vector3 Bottom(int index) => new(outline[order[index]].X, outline[order[index]].Y, bottoms[order[index]]);
        mesh.AddQuad(Top(0), Top(1), Top(2), Top(3), part.Kind);
        mesh.AddQuad(Bottom(3), Bottom(2), Bottom(1), Bottom(0), part.Kind);
        for (var index = 0; index < 4; index++)
        {
            var next = (index + 1) % 4;
            mesh.AddQuad(Bottom(index), Bottom(next), Top(next), Top(index), part.Kind);
        }
    }

    /// <summary>
    /// Adds a pitched roof as planar facets. The eave sits at <paramref name="eaveTop"/>; each facet rises linearly,
    /// its underside follows it a slab thickness lower, and the exterior edges close the section.
    /// </summary>
    private static void AddPitchedRoof(SceneMesh mesh, FloorSlab roof, double eaveTop)
    {
        var boundary = PolygonTriangulator.SignedDoubleArea(roof.Boundary) > 0 ? roof.Boundary : [.. roof.Boundary.AsEnumerable().Reverse()];
        Vector3 Surface(Point2 point, double drop) =>
            new((float)point.X, (float)point.Y, (float)(eaveTop + RoofGeometry.RiseAt(roof, point) - drop));
        foreach (var facet in RoofGeometry.Facets(roof))
        {
            IReadOnlyList<(int A, int B, int C)> triangles;
            try { triangles = PolygonTriangulator.Triangulate(facet.Outline); }
            catch (ArgumentException) { continue; }
            foreach (var (a, b, c) in triangles)
            {
                mesh.AddTriangle(Surface(facet.Outline[a], 0), Surface(facet.Outline[b], 0), Surface(facet.Outline[c], 0),
                    SurfaceKind.Floor);
                mesh.AddTriangle(Surface(facet.Outline[c], roof.Thickness), Surface(facet.Outline[b], roof.Thickness),
                    Surface(facet.Outline[a], roof.Thickness), SurfaceKind.Floor);
            }
            for (var index = 0; index < facet.Outline.Count; index++)
            {
                var current = facet.Outline[index];
                var next = facet.Outline[(index + 1) % facet.Outline.Count];
                if (!LiesOnBoundary(boundary, new Point2((current.X + next.X) / 2, (current.Y + next.Y) / 2))) continue;
                mesh.AddQuad(Surface(current, roof.Thickness), Surface(next, roof.Thickness), Surface(next, 0),
                    Surface(current, 0), SurfaceKind.Floor);
            }
        }
    }

    /// <summary>Tests whether a point lies on any edge of a closed polygon, within a micrometer.</summary>
    private static bool LiesOnBoundary(IReadOnlyList<Point2> polygon, Point2 point)
    {
        for (var index = 0; index < polygon.Count; index++)
        {
            var a = polygon[index];
            var b = polygon[(index + 1) % polygon.Count];
            var edge = a.DistanceTo(b);
            if (edge < 1e-12) continue;
            var cross = Math.Abs((b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X)) / edge;
            var along = ((point.X - a.X) * (b.X - a.X) + (point.Y - a.Y) * (b.Y - a.Y)) / edge;
            if (cross < 1e-6 && along >= -1e-6 && along <= edge + 1e-6) return true;
        }
        return false;
    }

    /// <summary>Raises a plan point to a model elevation.</summary>
    private static Vector3 LiftPoint(Point2 point, float z) => new((float)point.X, (float)point.Y, z);

    /// <summary>Adds a rectangular column standing on its level, up to the resolved column top.</summary>
    private static void AddColumn(SceneMesh mesh, Column column, double elevation, IReadOnlyList<Level> levels)
    {
        var outline = column.Outline.Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray();
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
