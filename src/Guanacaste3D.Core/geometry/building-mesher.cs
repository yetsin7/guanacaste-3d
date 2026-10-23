using System.Numerics;

namespace Guanacaste3D.Core.Geometry;

/// <summary>Physical surface categories that renderers map to visual materials.</summary>
public enum SurfaceKind : byte
{
    Wall,
    Floor,
    Glass,
    Ground
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

        foreach (var wall in document.Walls)
        {
            var z = elevations[wall.LevelId];
            var openings = document.Openings.Where(opening => opening.WallId == wall.Id).ToArray();
            foreach (var solid in WallGeometry.Solids(wall, openings))
                AddWallBox(mesh, wall, solid.From, solid.To, z + solid.Bottom, z + solid.Top, 0.5f, SurfaceKind.Wall);
            foreach (var window in openings.Where(opening => opening.Kind == OpeningKind.Window))
                AddWallBox(mesh, wall, window.Begin, window.End, z + window.Sill,
                    z + window.Sill + window.Height, 0.08f, SurfaceKind.Glass);
        }

        foreach (var floor in document.Floors)
            AddSlab(mesh, floor.Boundary, elevations[floor.LevelId], floor.Thickness);

        if (includeGround)
        {
            var (min, max) = mesh.Bounds();
            var center = (min + max) / 2;
            var extent = MathF.Max(50, (max - min).Length() * 4);
            var z = MathF.Min(0, min.Z) - 0.01f;
            mesh.AddQuad(
                new(center.X - extent, center.Y - extent, z), new(center.X + extent, center.Y - extent, z),
                new(center.X + extent, center.Y + extent, z), new(center.X - extent, center.Y + extent, z),
                SurfaceKind.Ground);
        }
        return mesh;
    }

    /// <summary>Adds an oriented box along a wall axis, with a thickness fraction on each side.</summary>
    private static void AddWallBox(SceneMesh mesh, Wall wall, double from, double to,
        double bottom, double top, float halfThicknessFraction, SurfaceKind kind)
    {
        var length = wall.Length;
        var direction = new Vector2((float)((wall.End.X - wall.Start.X) / length),
            (float)((wall.End.Y - wall.Start.Y) / length));
        var normal = new Vector2(-direction.Y, direction.X) * (float)wall.Thickness * halfThicknessFraction;
        var origin = new Vector2((float)wall.Start.X, (float)wall.Start.Y);
        var a = origin + direction * (float)from;
        var b = origin + direction * (float)to;
        AddPrism(mesh, [a - normal, b - normal, b + normal, a + normal], (float)bottom, (float)top, kind);
    }

    /// <summary>Adds a slab whose top face lies on the level elevation.</summary>
    private static void AddSlab(SceneMesh mesh, IReadOnlyList<Point2> boundary, double elevation, double thickness)
    {
        var outline = boundary.Select(point => new Vector2((float)point.X, (float)point.Y)).ToArray();
        AddPrism(mesh, outline, (float)(elevation - thickness), (float)elevation, SurfaceKind.Floor);
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

    private static Vector3 Lift(Vector2 point, float z) => new(point.X, point.Y, z);
}
