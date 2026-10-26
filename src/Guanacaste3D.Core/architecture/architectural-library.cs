using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>A named slab recipe: a floor, ceiling or roof with its thickness, shape, pitch and finish.</summary>
public sealed record SlabPreset(string Name, FloorKind Kind, double Thickness, RoofShape Shape, double Pitch,
    FinishKind Finish);

/// <summary>A named column section.</summary>
public sealed record ColumnPreset(string Name, ColumnShape Shape, double Width, double Depth);

/// <summary>A named structural or architectural element section, with the style that shapes its geometry.</summary>
public sealed record ElementPreset(string Name, StructuralElementKind Kind, ElementStyle Style, double Width,
    double Height, double Offset);

/// <summary>
/// The standard catalog of architectural types that ships with Guanacaste 3D: wall assemblies, door and window
/// families, slab, roof and ceiling recipes, column sections, and framing, guard, stair and drainage sections.
/// Identifiers derive from the type name, so loading the library twice never duplicates a type.
/// </summary>
public static class ArchitecturalLibrary
{
    /// <summary>A stable identifier derived from a type's category and name.</summary>
    public static Guid StableId(string category, string name)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"guanacaste3d:{category}:{name}"));
        return new Guid(hash);
    }

    /// <summary>A new project that already holds the whole standard wall, door and window catalog.</summary>
    public static BuildingDocument CreateProject(string name = "Untitled Project")
    {
        var document = BuildingDocument.Create(name);
        document.WallTypes.AddRange(WallTypes().Where(type => document.WallTypes.All(existing => existing.Id != type.Id)));
        document.OpeningTypes.AddRange(OpeningTypes().Where(type => document.OpeningTypes.All(existing => existing.Id != type.Id)));
        return document;
    }

    /// <summary>Wall assemblies: partitions, masonry, concrete, regional earth walls, foundations and retaining walls.</summary>
    public static IReadOnlyList<WallType> WallTypes() =>
    [
        Wall("Gypsum Partition 75 mm", WallFunction.Interior, 0.075, "Gypsum on steel studs", "#8C959B", WallFillPattern.Solid),
        Wall("Gypsum Partition 150 mm", WallFunction.Interior, 0.15, "Insulated gypsum on steel studs", "#8C959B", WallFillPattern.Solid),
        Wall("Fire-Rated Partition 200 mm", WallFunction.Interior, 0.20, "Fire-rated gypsum assembly", "#A0654F", WallFillPattern.Crosshatch),
        Wall("Wood Stud Partition 140 mm", WallFunction.Interior, 0.14, "Wood studs and board", "#A9835A", WallFillPattern.Diagonal),
        Wall("Glass Partition 50 mm", WallFunction.Interior, 0.05, "Tempered glass", "#9CC7D6", WallFillPattern.Solid),
        Wall("Concrete Block 150 mm", WallFunction.Interior, 0.15, "Concrete masonry", "#7D8185", WallFillPattern.Crosshatch),
        Wall("Concrete Block 250 mm", WallFunction.Exterior, 0.25, "Concrete masonry", "#7D8185", WallFillPattern.Crosshatch),
        Wall("Clay Brick 120 mm", WallFunction.Interior, 0.12, "Clay brick", "#A8553C", WallFillPattern.Diagonal),
        Wall("Brick Cavity Wall 300 mm", WallFunction.Exterior, 0.30, "Brick cavity wall", "#A8553C", WallFillPattern.Diagonal),
        Wall("Reinforced Concrete 200 mm", WallFunction.Exterior, 0.20, "Cast-in-place concrete", "#6E7378", WallFillPattern.Solid),
        Wall("Reinforced Concrete 300 mm", WallFunction.Exterior, 0.30, "Cast-in-place concrete", "#6E7378", WallFillPattern.Solid),
        Wall("Stone Wall 400 mm", WallFunction.Exterior, 0.40, "Natural stone", "#8E8677", WallFillPattern.Crosshatch),
        Wall("Bahareque 150 mm", WallFunction.Exterior, 0.15, "Cane and earth (bahareque)", "#B08968", WallFillPattern.Diagonal),
        Wall("Adobe 400 mm", WallFunction.Exterior, 0.40, "Adobe", "#B8895A", WallFillPattern.Solid),
        Wall("Insulated Metal Panel 100 mm", WallFunction.Exterior, 0.10, "Insulated metal panel", "#B7BEC4", WallFillPattern.Solid),
        Wall("Parapet 150 mm", WallFunction.Exterior, 0.15, "Masonry parapet", "#7D8185", WallFillPattern.Solid),
        Wall("Foundation Wall 300 mm", WallFunction.Foundation, 0.30, "Reinforced concrete", "#5D6166", WallFillPattern.Solid),
        Wall("Foundation Wall 400 mm", WallFunction.Foundation, 0.40, "Reinforced concrete", "#5D6166", WallFillPattern.Solid),
        Wall("Retaining Wall 300 mm", WallFunction.Retaining, 0.30, "Reinforced concrete", "#5D6166", WallFillPattern.Crosshatch),
        Wall("Retaining Wall 500 mm", WallFunction.Retaining, 0.50, "Reinforced concrete", "#5D6166", WallFillPattern.Crosshatch)
    ];

    private static WallType Wall(string name, WallFunction function, double thickness, string material, string color,
        WallFillPattern pattern) =>
        new(StableId("wall", name), name, function, thickness, material)
        {
            PlanFillColor = color, PlanFillPattern = pattern, SectionFillColor = color, SectionFillPattern = pattern
        };

    /// <summary>Door families (swing, double, sliding, pocket, folding, garage, revolving) and window families.</summary>
    public static IReadOnlyList<OpeningType> OpeningTypes()
    {
        var types = new List<OpeningType>();
        void Door(string family, double width, double height, OpeningOperation operation, double depth = 0.04,
            bool glazed = false, string material = "Timber") =>
            types.Add(new OpeningType(StableId("opening", $"{family}/{Size(width, height)}"), OpeningKind.Door,
                family, Size(width, height), width, height, 0)
            { Depth = depth, Operation = operation, GlazedLeaf = glazed, MaterialName = material });
        void Window(string family, double width, double height, double sill, OpeningOperation operation, int lights = 0,
            string material = "Aluminium") =>
            types.Add(new OpeningType(StableId("opening", $"{family}/{Size(width, height)}"), OpeningKind.Window,
                family, Size(width, height), width, height, sill)
            { Operation = operation, GlazingLightCount = lights, MaterialName = material });

        foreach (var width in new[] { 0.7, 1.0 }) Door("Single Door", width, 2.1, OpeningOperation.Standard);
        foreach (var width in new[] { 1.4, 1.8, 2.0 }) Door("Double Door", width, 2.1, OpeningOperation.Standard);
        foreach (var width in new[] { 1.5, 1.8 }) Door("French Door", width, 2.1, OpeningOperation.Standard, glazed: true);
        Door("Glazed Entry Door", 1.0, 2.4, OpeningOperation.Standard, glazed: true, material: "Aluminium");
        foreach (var width in new[] { 1.8, 2.4 }) Door("Sliding Door", width, 2.1, OpeningOperation.Sliding, glazed: true, material: "Aluminium");
        Door("Sliding Door", 3.0, 2.4, OpeningOperation.Sliding, glazed: true, material: "Aluminium");
        foreach (var width in new[] { 0.8, 0.9 }) Door("Pocket Door", width, 2.1, OpeningOperation.Pocket);
        foreach (var width in new[] { 1.2, 1.8, 2.4 }) Door("Bi-Fold Door", width, 2.1, OpeningOperation.Folding);
        foreach (var width in new[] { 2.4, 2.7, 4.8 }) Door("Garage Door", width, 2.1, OpeningOperation.Overhead, 0.05, material: "Steel");
        Door("Revolving Door", 2.0, 2.2, OpeningOperation.Revolving, 0.9, glazed: true, material: "Glass and steel");

        foreach (var (width, height, sill) in new[] { (1.8, 1.2, 0.9), (2.4, 1.5, 0.9) })
            Window("Picture Window", width, height, sill, OpeningOperation.Standard, 1);
        foreach (var (width, height, sill) in new[] { (1.2, 1.2, 0.9), (2.0, 1.4, 0.9) })
            Window("Sliding Window", width, height, sill, OpeningOperation.Sliding, 2);
        foreach (var (width, height, lights) in new[] { (0.6, 1.2, 1), (0.9, 1.2, 1), (1.2, 1.2, 2), (1.8, 1.2, 3) })
            Window("Casement Window", width, height, 0.9, OpeningOperation.Casement, lights);
        foreach (var (width, height, sill) in new[] { (0.9, 0.6, 1.8), (1.2, 0.6, 1.5) })
            Window("Awning Window", width, height, sill, OpeningOperation.Awning, 1);
        foreach (var width in new[] { 0.9, 1.2 }) Window("Hung Window", width, 1.5, 0.9, OpeningOperation.Hung, 1);
        foreach (var width in new[] { 0.6, 1.2 }) Window("Louvered Window", width, 1.2, 0.9, OpeningOperation.Louvered, 1, "Glass blades");
        Window("Clerestory Window", 1.8, 0.5, 2.2, OpeningOperation.Standard, 3);
        return types;
    }

    private static string Size(double width, double height) =>
        string.Create(CultureInfo.InvariantCulture, $"{width * 1000:0} × {height * 1000:0} mm");

    /// <summary>Floor, ceiling and roof recipes.</summary>
    public static IReadOnlyList<SlabPreset> SlabPresets { get; } =
    [
        new("Concrete Slab 150 mm", FloorKind.Floor, 0.15, RoofShape.Flat, 0, FinishKind.ExposedConcrete),
        new("Concrete Slab 200 mm", FloorKind.Floor, 0.20, RoofShape.Flat, 0, FinishKind.None),
        new("Ceramic Tile Floor 200 mm", FloorKind.Floor, 0.20, RoofShape.Flat, 0, FinishKind.Ceramic),
        new("Porcelain Tile Floor 200 mm", FloorKind.Floor, 0.20, RoofShape.Flat, 0, FinishKind.Tile),
        new("Timber Joist Floor 250 mm", FloorKind.Floor, 0.25, RoofShape.Flat, 0, FinishKind.None),
        new("Gypsum Ceiling 15 mm", FloorKind.Ceiling, 0.015, RoofShape.Flat, 0, FinishKind.Paint),
        new("Suspended Tile Ceiling 20 mm", FloorKind.Ceiling, 0.02, RoofShape.Flat, 0, FinishKind.None),
        new("Timber Plank Ceiling 20 mm", FloorKind.Ceiling, 0.02, RoofShape.Flat, 0, FinishKind.None),
        new("Flat Concrete Roof 150 mm", FloorKind.Roof, 0.15, RoofShape.Flat, 0, FinishKind.None),
        new("Shed Roof 10°", FloorKind.Roof, 0.10, RoofShape.Shed, 10, FinishKind.None),
        new("Gable Roof 20°", FloorKind.Roof, 0.10, RoofShape.Gable, 20, FinishKind.None),
        new("Gable Roof 30°", FloorKind.Roof, 0.10, RoofShape.Gable, 30, FinishKind.None),
        new("Hip Roof 25°", FloorKind.Roof, 0.10, RoofShape.Hip, 25, FinishKind.None),
        new("Hip Roof 35°", FloorKind.Roof, 0.10, RoofShape.Hip, 35, FinishKind.None)
    ];

    /// <summary>Column sections.</summary>
    public static IReadOnlyList<ColumnPreset> ColumnPresets { get; } =
    [
        new("Column 300 × 300 mm", ColumnShape.Rectangular, 0.30, 0.30),
        new("Column 400 × 400 mm", ColumnShape.Rectangular, 0.40, 0.40),
        new("Column 300 × 600 mm", ColumnShape.Rectangular, 0.30, 0.60),
        new("Round Column Ø300 mm", ColumnShape.Round, 0.30, 0.30),
        new("Round Column Ø500 mm", ColumnShape.Round, 0.50, 0.50),
        new("Octagonal Column 400 mm", ColumnShape.Octagonal, 0.40, 0.40),
        new("L-Shaped Column 600 mm", ColumnShape.LShaped, 0.60, 0.60),
        new("T-Shaped Column 600 mm", ColumnShape.TShaped, 0.60, 0.60)
    ];

    /// <summary>Beams, guards, stairs, ramps and rainwater drainage.</summary>
    public static IReadOnlyList<ElementPreset> ElementPresets { get; } =
    [
        new("Concrete Beam 250 × 400 mm", StructuralElementKind.Beam, ElementStyle.Default, 0.25, 0.40, -0.40),
        new("Concrete Beam 300 × 500 mm", StructuralElementKind.Beam, ElementStyle.Default, 0.30, 0.50, -0.50),
        new("Steel I-Beam 200 × 400 mm", StructuralElementKind.Beam, ElementStyle.IShape, 0.20, 0.40, -0.40),
        new("Steel Tee 200 × 300 mm", StructuralElementKind.Beam, ElementStyle.Tee, 0.20, 0.30, -0.30),
        new("Steel I-Brace 150 × 200 mm", StructuralElementKind.Brace, ElementStyle.IShape, 0.15, 0.20, -0.20),
        new("Steel Truss 200 × 800 mm", StructuralElementKind.Truss, ElementStyle.IShape, 0.20, 0.80, -0.80),
        new("Round Pier Ø500 mm", StructuralElementKind.IsolatedFoundation, ElementStyle.Round, 0.50, 0.80, -0.80),
        new("Guard with Posts and Rails 1100 mm", StructuralElementKind.Railing, ElementStyle.Default, 0.06, 1.10, 0),
        new("Guard with Balusters 1100 mm", StructuralElementKind.Railing, ElementStyle.Pickets, 0.06, 1.10, 0),
        new("Glass Guard 1100 mm", StructuralElementKind.Railing, ElementStyle.GlassPanel, 0.06, 1.10, 0),
        new("Cable Guard 1100 mm", StructuralElementKind.Railing, ElementStyle.Cable, 0.06, 1.10, 0),
        new("Solid Parapet 1000 mm", StructuralElementKind.Railing, ElementStyle.SolidParapet, 0.15, 1.00, 0),
        new("Straight Stair 1200 mm", StructuralElementKind.Stair, ElementStyle.Default, 1.20, 3.00, 0),
        new("Open Stair 1000 mm", StructuralElementKind.Stair, ElementStyle.Open, 1.00, 3.00, 0),
        new("Ramp 1:12", StructuralElementKind.Ramp, ElementStyle.Default, 1.50, 0.60, 0),
        new("Box Gutter 120 × 100 mm", StructuralElementKind.Gutter, ElementStyle.Default, 0.12, 0.10, 2.85),
        new("Wide Gutter 200 × 120 mm", StructuralElementKind.Gutter, ElementStyle.Default, 0.20, 0.12, 2.85),
        new("Square Downspout 90 mm", StructuralElementKind.Downspout, ElementStyle.Default, 0.09, 2.85, 0),
        new("Round Downspout Ø100 mm", StructuralElementKind.Downspout, ElementStyle.Round, 0.10, 2.85, 0),
        new("Curtain Wall 100 mm", StructuralElementKind.CurtainWall, ElementStyle.Default, 0.10, 3.00, 0)
    ];

    /// <summary>Presets that apply to one kind of element, in catalog order.</summary>
    public static IEnumerable<ElementPreset> PresetsFor(StructuralElementKind kind) =>
        ElementPresets.Where(preset => preset.Kind == kind);
}
