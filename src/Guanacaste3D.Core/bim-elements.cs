using System.Text.Json.Serialization;

namespace Guanacaste3D.Core;

/// <summary>Classifies a wall type by its primary building role.</summary>
public enum WallFunction
{
    Interior,
    Exterior,
    Foundation,
    Retaining
}

/// <summary>Defines reusable parameters shared by wall instances.</summary>
public sealed record WallType(
    Guid Id,
    string Name,
    WallFunction Function,
    double Thickness,
    string MaterialName);

/// <summary>Provides stable identifiers and defaults for built-in wall types.</summary>
public static class BuiltInWallTypes
{
    public static readonly Guid Interior100Id = Guid.Parse("ea403711-f44c-4cab-bf9c-f96ddb578659");
    public static readonly Guid Basic200Id = Guid.Parse("4ae439fa-5c6b-41f1-ae56-9cf417e567ad");
    public static readonly Guid Exterior300Id = Guid.Parse("a4fba685-e21b-4694-b8bc-e67dd513b996");

    /// <summary>Creates an independent default wall-type catalog for a new document.</summary>
    public static List<WallType> CreateDefaults() =>
    [
        new(Interior100Id, "Interior 100 mm", WallFunction.Interior, 0.10, "Gypsum assembly"),
        new(Basic200Id, "Basic 200 mm", WallFunction.Interior, 0.20, "Concrete masonry"),
        new(Exterior300Id, "Exterior 300 mm", WallFunction.Exterior, 0.30, "Insulated masonry")
    ];
}

/// <summary>Represents a rectangular room annotation and its calculated area.</summary>
public sealed record Room(
    Guid Id,
    Guid LevelId,
    string Number,
    string Name,
    Point2 FirstCorner,
    Point2 SecondCorner)
{
    [JsonIgnore]
    public double Area => Math.Abs(
        (SecondCorner.X - FirstCorner.X) * (SecondCorner.Y - FirstCorner.Y));

    [JsonIgnore]
    public Point2 Center => new(
        (FirstCorner.X + SecondCorner.X) / 2,
        (FirstCorner.Y + SecondCorner.Y) / 2);
}

/// <summary>Represents a horizontal slab with a closed polygonal boundary.</summary>
public sealed record FloorSlab(
    Guid Id,
    Guid LevelId,
    string Name,
    List<Point2> Boundary,
    double Thickness)
{
    /// <summary>Calculates plan area using the shoelace formula.</summary>
    public double CalculateArea()
    {
        if (Boundary.Count < 3) return 0;
        var doubledArea = 0d;
        for (var index = 0; index < Boundary.Count; index++)
        {
            var current = Boundary[index];
            var next = Boundary[(index + 1) % Boundary.Count];
            doubledArea += current.X * next.Y - next.X * current.Y;
        }
        return Math.Abs(doubledArea) / 2;
    }
}

/// <summary>Supported physical paper sizes for drawing sheets.</summary>
public enum SheetPaperSize
{
    A0,
    A1,
    A2,
    A3,
    A4
}

/// <summary>Represents a numbered drawing sheet in the project document set.</summary>
public sealed record DrawingSheet(
    Guid Id,
    string Number,
    string Name,
    SheetPaperSize PaperSize);
