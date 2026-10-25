namespace Guanacaste3D.Core;

/// <summary>
/// A point or vector in model space, in meters, Z-up. Only methods (not computed properties)
/// are declared beyond the coordinates, so JSON output contains exactly X, Y and Z.
/// </summary>
public readonly record struct Point3(double X, double Y, double Z)
{
    public static Point3 operator +(Point3 a, Point3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Point3 operator *(Point3 a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);

    /// <summary>Returns the cross product a × b.</summary>
    public static Point3 Cross(Point3 a, Point3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public double Dot(Point3 other) => X * other.X + Y * other.Y + Z * other.Z;

    public double Norm() => Math.Sqrt(Dot(this));

    public bool IsFinite() => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

/// <summary>
/// A real reference plane in model space: an origin, a unit normal and a unit X axis.
/// The Y axis is normal × X, so (X, Y, normal) is right-handed and extrusions run along the normal.
/// </summary>
public sealed record WorkPlane(Point3 Origin, Point3 Normal, Point3 XAxis)
{
    /// <summary>Returns the in-plane Y axis. Derived on demand, so it is never persisted.</summary>
    public Point3 YAxis() => Point3.Cross(Normal, XAxis);

    /// <summary>Maps plane-local coordinates (meters) to a point in model space.</summary>
    public Point3 ToModel(Point2 local) => Origin + XAxis * local.X + YAxis() * local.Y;

    /// <summary>A horizontal plane at the given elevation, facing up.</summary>
    public static WorkPlane Horizontal(double elevation) =>
        new(new Point3(0, 0, elevation), new Point3(0, 0, 1), new Point3(1, 0, 0));
}

/// <summary>A named length parameter of an in-place family, in meters. Bound geometry follows its value.</summary>
public sealed record InPlaceParameter(string Name, double Value);

/// <summary>
/// A solid made by pushing a closed plan profile along its work-plane normal. The solid spans
/// from <see cref="StartOffset"/> to <see cref="StartOffset"/> + depth along the normal.
/// </summary>
public sealed record InPlaceExtrusion(
    Guid Id,
    string Name,
    WorkPlane Plane,
    List<Point2> Profile,
    double StartOffset,
    double Depth)
{
    /// <summary>
    /// Family parameter that drives the depth. When set, regeneration copies its value into
    /// <see cref="Depth"/>; when null the stored depth is used.
    /// </summary>
    public string? DepthParameter { get; init; }

    /// <summary>Material name assigned to the solid's faces.</summary>
    public string MaterialName { get; init; } = string.Empty;
}

/// <summary>
/// An in-place family: geometry created inside the project and kept as operations, so it can
/// be selected and edited later instead of being stored as a flat mesh.
/// </summary>
public sealed record InPlaceFamily(
    Guid Id,
    string Name,
    string CategoryId,
    List<InPlaceParameter> Parameters,
    List<InPlaceExtrusion> Extrusions);
