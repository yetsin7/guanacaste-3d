namespace Guanacaste3D.Core;

/// <summary>
/// Axis-aligned floor slab in metric project XY coordinates. Its top face is at
/// the level elevation and its bottom face is at elevation minus thickness.
/// </summary>
public sealed record Slab(Guid Id, Guid LevelId, Point2 Min, Point2 Max, double Thickness)
{
    public double Width => Max.X - Min.X;
    public double Depth => Max.Y - Min.Y;
    public double Area => Width * Depth;
    public double Volume => Area * Thickness;

    /// <summary>Normalize two opposite corners to the rectangle's lower/upper bounds.</summary>
    public static Slab FromCorners(Guid id, Guid levelId, Point2 first, Point2 second,
        double thickness = 0.2) =>
        new(id, levelId,
            new Point2(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y)),
            new Point2(Math.Max(first.X, second.X), Math.Max(first.Y, second.Y)),
            thickness);
}
