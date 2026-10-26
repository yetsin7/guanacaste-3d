using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core;

/// <summary>Builds lightweight, recognizable solids for the default residential component families.</summary>
public static class ResidentialComponentGeometry
{
    /// <summary>Returns the coordinated plan and 3D solids for one residential component instance.</summary>
    public static IEnumerable<ElementPart> Parts(StructuralElement element) => element.ComponentShape switch
    {
        ResidentialComponentShape.Table => Table(element),
        ResidentialComponentShape.Chair => Chair(element),
        ResidentialComponentShape.Sofa => Sofa(element),
        ResidentialComponentShape.Bed => Bed(element),
        ResidentialComponentShape.Plant => Plant(element),
        ResidentialComponentShape.Cabinet or ResidentialComponentShape.WallCabinet => Cabinet(element),
        ResidentialComponentShape.Refrigerator => Refrigerator(element),
        ResidentialComponentShape.Range => Range(element),
        ResidentialComponentShape.Dishwasher or ResidentialComponentShape.WasherDryer => Appliance(element),
        ResidentialComponentShape.Toilet => Toilet(element),
        ResidentialComponentShape.Vanity => Vanity(element),
        ResidentialComponentShape.Bathtub => Bathtub(element),
        ResidentialComponentShape.Shower => Shower(element),
        ResidentialComponentShape.Desk => Desk(element),
        ResidentialComponentShape.Dresser => Dresser(element),
        ResidentialComponentShape.Bookshelf => Bookshelf(element),
        ResidentialComponentShape.Television => Television(element),
        ResidentialComponentShape.Rug => [Block(element, 0, 0, Width(element), Depth(element), 0, element.Height, SurfaceKind.Upholstery)],
        ResidentialComponentShape.FloorLamp => FloorLamp(element),
        ResidentialComponentShape.CeilingLight => CeilingLight(element),
        ResidentialComponentShape.Barbecue => Barbecue(element),
        _ => [Block(element, 0, 0, Width(element), Depth(element), 0, element.Height)]
    };

    /// <summary>Builds a top and four legs, scaling all members to the family dimensions.</summary>
    private static IEnumerable<ElementPart> Table(StructuralElement element)
    {
        var top = Math.Clamp(element.Height * 0.08, 0.035, 0.08);
        var leg = Math.Clamp(Math.Min(Width(element), Depth(element)) * 0.08, 0.035, 0.09);
        yield return Block(element, 0, 0, Width(element), Depth(element), element.Height - top, element.Height,
            SurfaceKind.DoorLeaf);
        foreach (var (x, y) in Corners(element, leg * 1.7))
            yield return Block(element, x, y, leg, leg, 0, element.Height - top, SurfaceKind.DoorLeaf);
    }

    /// <summary>Builds a chair with a seat, four legs, and a back panel.</summary>
    private static IEnumerable<ElementPart> Chair(StructuralElement element)
    {
        var seatTop = Math.Min(0.48, element.Height * 0.55);
        var member = Math.Clamp(Math.Min(Width(element), Depth(element)) * 0.09, 0.025, 0.05);
        yield return Block(element, 0, 0, Width(element), Depth(element), seatTop - 0.07, seatTop, SurfaceKind.DoorLeaf);
        foreach (var (x, y) in Corners(element, member * 1.5))
            yield return Block(element, x, y, member, member, 0, seatTop - 0.07, SurfaceKind.DoorLeaf);
        yield return Block(element, 0, Depth(element) / 2 - member, Width(element), member * 1.4,
            seatTop, element.Height, SurfaceKind.DoorLeaf);
    }

    /// <summary>Builds upholstered seating with a base, back, arms, and separate seat cushions.</summary>
    private static IEnumerable<ElementPart> Sofa(StructuralElement element)
    {
        var width = Width(element);
        var depth = Depth(element);
        var arm = Math.Clamp(width * 0.08, 0.09, 0.18);
        const SurfaceKind fabric = SurfaceKind.Upholstery;
        yield return Soft(element, 0, 0, width, depth, 0.12, 0.38, 0.04, fabric);
        yield return Soft(element, 0, depth * 0.34, width, depth * 0.28, 0.32, element.Height, 0.06, fabric);
        foreach (var side in new[] { -1, 1 })
            yield return Soft(element, side * (width / 2 - arm / 2), 0, arm, depth, 0.20, element.Height * 0.72, 0.04, fabric);
        // Short dark legs lift the base, as on most contemporary sofas.
        foreach (var (x, y) in Corners(element, 0.08))
            yield return Block(element, x, y, 0.04, 0.04, 0, 0.12, SurfaceKind.DoorLeaf);
        var cushionCount = Math.Clamp((int)Math.Round(width / 0.70), 1, 3);
        var cushionWidth = (width - 2 * arm - 0.04 * (cushionCount - 1)) / cushionCount;
        for (var index = 0; index < cushionCount; index++)
        {
            var x = -width / 2 + arm + cushionWidth / 2 + index * (cushionWidth + 0.04);
            yield return Soft(element, x, -depth * 0.08, cushionWidth, depth * 0.58, 0.38, 0.52, 0.05, fabric);
            // Back cushion leaning on the back.
            yield return Soft(element, x, depth * 0.17, cushionWidth, 0.16, 0.50, element.Height - 0.04, 0.05, fabric);
        }
    }

    /// <summary>Builds a bed frame, mattress, headboard, and two pillows when the bed is wide enough.</summary>
    private static IEnumerable<ElementPart> Bed(StructuralElement element)
    {
        var width = Width(element);
        var depth = Depth(element);
        yield return Block(element, 0, 0, width, depth, 0.08, 0.25, SurfaceKind.DoorLeaf);
        yield return Soft(element, 0, 0, width * 0.96, depth * 0.91, 0.25, 0.48, 0.06, SurfaceKind.Bedding);
        // A duvet over the lower two thirds, hanging a little past the mattress.
        yield return Soft(element, 0, -depth * 0.12, width * 1.0, depth * 0.66, 0.30, 0.52, 0.05, SurfaceKind.Upholstery);
        yield return Block(element, 0, depth / 2 - 0.055, width, 0.11, 0.08, element.Height, SurfaceKind.DoorLeaf);
        var pillowCount = width >= 1.30 ? 2 : 1;
        for (var index = 0; index < pillowCount; index++)
            yield return Soft(element, pillowCount == 1 ? 0 : (index == 0 ? -1 : 1) * width * 0.24,
                depth * 0.30, width * (pillowCount == 1 ? 0.55 : 0.38), depth * 0.20, 0.48, 0.61, 0.06, SurfaceKind.Bedding);
    }

    /// <summary>Builds an octagonal pot, a trunk, and three offset foliage masses.</summary>
    private static IEnumerable<ElementPart> Plant(StructuralElement element)
    {
        var diameter = Math.Min(Width(element), Depth(element));
        var potTop = Math.Min(element.Height * 0.28, diameter * 0.75);
        yield return RoundBlock(element, 0, 0, diameter * 0.58, 0, potTop, SurfaceKind.DoorLeaf);
        yield return RoundBlock(element, 0, 0, Math.Max(0.025, diameter * 0.10), potTop * 0.7,
            element.Height * 0.78, SurfaceKind.DoorLeaf);
        yield return RoundBlock(element, 0, 0, diameter, element.Height * 0.48, element.Height);
        yield return RoundBlock(element, -diameter * 0.20, diameter * 0.12, diameter * 0.62,
            element.Height * 0.38, element.Height * 0.78);
        yield return RoundBlock(element, diameter * 0.22, -diameter * 0.10, diameter * 0.58,
            element.Height * 0.42, element.Height * 0.82);
    }

    /// <summary>Builds a cabinet carcass, toe kick, countertop or top cap, and paired door fronts.</summary>
    private static IEnumerable<ElementPart> Cabinet(StructuralElement element)
    {
        var width = Width(element);
        var depth = Depth(element);
        var toe = element.ComponentShape == ResidentialComponentShape.WallCabinet ? 0 : Math.Min(0.10, element.Height / 6);
        yield return Block(element, 0, 0, width, depth, toe, element.Height - 0.04, SurfaceKind.DoorLeaf);
        if (toe > 0) yield return Block(element, 0, depth * 0.08, width * 0.88, depth * 0.82, 0, toe, SurfaceKind.Structure);
        yield return Block(element, 0, 0, width + 0.04, depth + 0.04, element.Height - 0.04, element.Height, SurfaceKind.Floor);
        foreach (var side in new[] { -1, 1 })
            yield return Block(element, side * width * 0.25, -depth / 2 - 0.006, width * 0.47, 0.012,
                toe + 0.02, element.Height - 0.08, SurfaceKind.DoorLeaf);
    }

    /// <summary>Builds a refrigerator body with split doors and projecting handles.</summary>
    private static IEnumerable<ElementPart> Refrigerator(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element), Depth(element), 0, element.Height, SurfaceKind.OpeningFrame);
        foreach (var side in new[] { -1, 1 })
        {
            yield return Block(element, side * Width(element) * 0.25, -Depth(element) / 2 - 0.012,
                Width(element) * 0.48, 0.024, 0.05, element.Height - 0.05, SurfaceKind.Structure);
            yield return Block(element, side * Width(element) * 0.09, -Depth(element) / 2 - 0.035,
                0.025, 0.025, element.Height * 0.42, element.Height * 0.78, SurfaceKind.OpeningFrame);
        }
    }

    /// <summary>Builds a range body, cooktop, back control panel, and four burners.</summary>
    private static IEnumerable<ElementPart> Range(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element), Depth(element), 0, element.Height * 0.82, SurfaceKind.OpeningFrame);
        yield return Block(element, 0, 0, Width(element), Depth(element), element.Height * 0.82,
            element.Height * 0.87, SurfaceKind.Structure);
        yield return Block(element, 0, Depth(element) * 0.42, Width(element), Depth(element) * 0.14,
            element.Height * 0.87, element.Height, SurfaceKind.OpeningFrame);
        foreach (var (x, y) in FourQuadrants(element, 0.24))
            yield return RoundBlock(element, x, y, Math.Min(Width(element), Depth(element)) * 0.22,
                element.Height * 0.87, element.Height * 0.89, SurfaceKind.Structure);
    }

    /// <summary>Builds a freestanding appliance with a recessed front panel and round loading door.</summary>
    private static IEnumerable<ElementPart> Appliance(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element), Depth(element), 0, element.Height, SurfaceKind.OpeningFrame);
        yield return Block(element, 0, -Depth(element) / 2 - 0.012, Width(element) * 0.86, 0.024,
            element.Height * 0.12, element.Height * 0.82, SurfaceKind.Structure);
        if (element.ComponentShape == ResidentialComponentShape.WasherDryer)
            yield return RoundBlock(element, 0, -Depth(element) / 2 - 0.032, Width(element) * 0.52,
                element.Height * 0.22, element.Height * 0.68, SurfaceKind.Glass);
    }

    /// <summary>Builds a toilet bowl, pedestal, seat, and rear tank.</summary>
    private static IEnumerable<ElementPart> Toilet(StructuralElement element)
    {
        yield return Block(element, 0, 0.08, Width(element) * 0.58, Depth(element) * 0.48, 0, 0.34);
        yield return RoundBlock(element, 0, -Depth(element) * 0.08, Width(element), 0.28, 0.46, SurfaceKind.Ceramic);
        yield return Block(element, 0, Depth(element) * 0.34, Width(element), Depth(element) * 0.24,
            0.30, element.Height, SurfaceKind.Ceramic);
    }

    /// <summary>Builds a vanity cabinet, counter, basin, and faucet stem.</summary>
    private static IEnumerable<ElementPart> Vanity(StructuralElement element)
    {
        foreach (var part in Cabinet(element)) yield return part;
        yield return RoundBlock(element, 0, -Depth(element) * 0.08, Width(element) * 0.48,
            element.Height, element.Height + 0.04, SurfaceKind.Ceramic);
        yield return RoundBlock(element, 0, Depth(element) * 0.12, 0.035,
            element.Height, element.Height + 0.22, SurfaceKind.OpeningFrame);
    }

    /// <summary>Builds a bathtub from four rim panels around an open inner volume.</summary>
    private static IEnumerable<ElementPart> Bathtub(StructuralElement element) => RimmedContainer(element, 0.08, 0.12, element.Height);

    /// <summary>Builds a shower tray and two transparent enclosure panels.</summary>
    private static IEnumerable<ElementPart> Shower(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element), Depth(element), 0, 0.08, SurfaceKind.Ceramic);
        yield return Block(element, 0, Depth(element) / 2 - 0.01, Width(element), 0.02, 0.08, element.Height, SurfaceKind.Glass);
        yield return Block(element, Width(element) / 2 - 0.01, 0, 0.02, Depth(element), 0.08, element.Height, SurfaceKind.Glass);
    }

    /// <summary>Builds a desk top, two supports, and a shallow drawer bank.</summary>
    private static IEnumerable<ElementPart> Desk(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element), Depth(element), element.Height - 0.06, element.Height,
            SurfaceKind.DoorLeaf);
        foreach (var side in new[] { -1, 1 })
            yield return Block(element, side * Width(element) * 0.43, 0, 0.08, Depth(element) * 0.85,
                0, element.Height - 0.06, SurfaceKind.DoorLeaf);
        yield return Block(element, Width(element) * 0.24, -Depth(element) * 0.08, Width(element) * 0.34,
            Depth(element) * 0.72, element.Height * 0.46, element.Height - 0.08, SurfaceKind.DoorLeaf);
    }

    /// <summary>Builds a dresser or nightstand body with horizontal drawer fronts and a top cap.</summary>
    private static IEnumerable<ElementPart> Dresser(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element), Depth(element), 0.06, element.Height - 0.04, SurfaceKind.DoorLeaf);
        yield return Block(element, 0, 0, Width(element) + 0.03, Depth(element) + 0.03,
            element.Height - 0.04, element.Height, SurfaceKind.DoorLeaf);
        for (var index = 0; index < 3; index++)
            yield return Block(element, 0, -Depth(element) / 2 - 0.008, Width(element) * 0.90, 0.016,
                0.10 + index * (element.Height - 0.16) / 3, 0.10 + (index + 1) * (element.Height - 0.16) / 3 - 0.015,
                SurfaceKind.Structure);
    }

    /// <summary>Builds a bookshelf with side panels, back, and evenly spaced shelves.</summary>
    private static IEnumerable<ElementPart> Bookshelf(StructuralElement element)
    {
        const double panel = 0.035;
        foreach (var side in new[] { -1, 1 })
            yield return Block(element, side * (Width(element) / 2 - panel / 2), 0, panel, Depth(element),
                0, element.Height, SurfaceKind.DoorLeaf);
        yield return Block(element, 0, Depth(element) / 2 - panel / 2, Width(element), panel, 0, element.Height,
            SurfaceKind.DoorLeaf);
        var shelfCount = Math.Max(3, (int)Math.Round(element.Height / 0.38));
        for (var index = 0; index <= shelfCount; index++)
        {
            var level = element.Height * index / shelfCount;
            yield return Block(element, 0, 0, Width(element), Depth(element), Math.Max(0, level - panel), level,
                SurfaceKind.DoorLeaf);
        }
    }

    /// <summary>Builds a thin television screen with a central pedestal and base.</summary>
    private static IEnumerable<ElementPart> Television(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element), Depth(element), 0, element.Height, SurfaceKind.OpeningFrame);
        yield return Block(element, 0, -Depth(element) / 2 - 0.006, Width(element) * 0.94, 0.012,
            element.Height * 0.06, element.Height * 0.94, SurfaceKind.Glass);
        yield return Block(element, 0, 0, 0.05, Depth(element), -0.18, 0, SurfaceKind.OpeningFrame);
        yield return Block(element, 0, 0, Width(element) * 0.30, Depth(element) * 1.8, -0.20, -0.18,
            SurfaceKind.OpeningFrame);
    }

    /// <summary>Builds a floor lamp with a round base, slender stem, and octagonal shade.</summary>
    private static IEnumerable<ElementPart> FloorLamp(StructuralElement element)
    {
        var diameter = Math.Min(Width(element), Depth(element));
        yield return RoundBlock(element, 0, 0, diameter * 0.65, 0, 0.04, SurfaceKind.OpeningFrame);
        yield return RoundBlock(element, 0, 0, 0.025, 0.04, element.Height * 0.78, SurfaceKind.OpeningFrame);
        // The fabric shade glows with the bulb inside it, so it is the emitting surface.
        yield return RoundBlock(element, 0, 0, diameter, element.Height * 0.72, element.Height, SurfaceKind.LightEmitter);
    }

    /// <summary>Builds a shallow ceiling-mounted light: a metal canopy against the ceiling and a glowing diffuser under it.</summary>
    private static IEnumerable<ElementPart> CeilingLight(StructuralElement element)
    {
        var diameter = Math.Min(Width(element), Depth(element));
        yield return RoundBlock(element, 0, 0, diameter * 0.82, 0, element.Height * 0.6, SurfaceKind.LightEmitter);
        yield return RoundBlock(element, 0, 0, diameter, element.Height * 0.6, element.Height, SurfaceKind.Metal);
    }

    /// <summary>Builds a barbecue cabinet with a raised hood, cooking grate, side shelves, and legs.</summary>
    private static IEnumerable<ElementPart> Barbecue(StructuralElement element)
    {
        yield return Block(element, 0, 0, Width(element) * 0.62, Depth(element), 0.42, element.Height * 0.76,
            SurfaceKind.OpeningFrame);
        yield return Block(element, 0, Depth(element) * 0.10, Width(element) * 0.62, Depth(element) * 0.70,
            element.Height * 0.76, element.Height, SurfaceKind.Structure);
        foreach (var side in new[] { -1, 1 })
        {
            yield return Block(element, side * Width(element) * 0.41, 0, Width(element) * 0.18, Depth(element) * 0.72,
                element.Height * 0.62, element.Height * 0.68, SurfaceKind.OpeningFrame);
            yield return Block(element, side * Width(element) * 0.23, 0, 0.05, 0.05, 0, 0.42, SurfaceKind.OpeningFrame);
        }
    }

    /// <summary>Returns four wall solids around a rectangular container opening.</summary>
    private static IEnumerable<ElementPart> RimmedContainer(StructuralElement element, double wall, double rim,
        double top)
    {
        yield return Block(element, 0, -Depth(element) / 2 + wall / 2, Width(element), wall, 0, top, SurfaceKind.Ceramic);
        yield return Block(element, 0, Depth(element) / 2 - wall / 2, Width(element), wall, 0, top, SurfaceKind.Ceramic);
        yield return Block(element, -Width(element) / 2 + wall / 2, 0, wall, Depth(element) - 2 * wall, 0, top, SurfaceKind.Ceramic);
        yield return Block(element, Width(element) / 2 - wall / 2, 0, wall, Depth(element) - 2 * wall, 0, top, SurfaceKind.Ceramic);
        yield return Block(element, 0, 0, Width(element) - 2 * rim, Depth(element) - 2 * rim, 0, 0.05, SurfaceKind.Ceramic);
    }

    /// <summary>Returns four inset plan corner centers for legs or burners.</summary>
    private static IEnumerable<(double X, double Y)> Corners(StructuralElement element, double inset)
    {
        foreach (var x in new[] { -Width(element) / 2 + inset, Width(element) / 2 - inset })
        foreach (var y in new[] { -Depth(element) / 2 + inset, Depth(element) / 2 - inset })
            yield return (x, y);
    }

    /// <summary>Returns four symmetric positions at a fraction of the width and depth.</summary>
    private static IEnumerable<(double X, double Y)> FourQuadrants(StructuralElement element, double fraction)
    {
        foreach (var x in new[] { -Width(element) * fraction, Width(element) * fraction })
        foreach (var y in new[] { -Depth(element) * fraction, Depth(element) * fraction })
            yield return (x, y);
    }

    /// <summary>Creates one axis-aligned rectangular solid relative to the component insertion point and offset.</summary>
    private static ElementPart Block(StructuralElement element, double centerX, double centerY, double width,
        double depth, double bottom, double top, SurfaceKind kind = SurfaceKind.Structure) =>
        new(Rectangle(element.Start.X + centerX, element.Start.Y + centerY, width, depth),
            element.Offset + bottom, element.Offset + top, kind);

    /// <summary>
    /// An upholstered block: its plan corners are rounded (chamfered twice), so cushions, mattresses and arms read soft
    /// instead of boxy.
    /// </summary>
    private static ElementPart Soft(StructuralElement element, double centerX, double centerY, double width, double depth,
        double bottom, double top, double round, SurfaceKind kind)
    {
        var r = Math.Min(round, Math.Min(width, depth) * 0.3);
        var x = element.Start.X + centerX;
        var y = element.Start.Y + centerY;
        var points = new List<Point2>();
        foreach (var (cx, cy, start) in new[]
                 {
                     (x + width / 2 - r, y - depth / 2 + r, -Math.PI / 2), (x + width / 2 - r, y + depth / 2 - r, 0.0),
                     (x - width / 2 + r, y + depth / 2 - r, Math.PI / 2), (x - width / 2 + r, y - depth / 2 + r, Math.PI)
                 })
            for (var step = 0; step <= 2; step++)
            {
                var angle = start + step * Math.PI / 4;
                points.Add(new Point2(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
            }
        return new ElementPart(points, element.Offset + bottom, element.Offset + top, kind);
    }

    /// <summary>Creates one regular twelve-sided solid used for pots, stems, bowls, burners, and light fittings.</summary>
    private static ElementPart RoundBlock(StructuralElement element, double centerX, double centerY, double diameter,
        double bottom, double top, SurfaceKind kind = SurfaceKind.Structure) =>
        new(Round(element.Start.X + centerX, element.Start.Y + centerY, diameter, 12),
            element.Offset + bottom, element.Offset + top, kind);

    /// <summary>Returns the effective component width, bounded away from degenerate geometry.</summary>
    private static double Width(StructuralElement element) => Math.Max(0.02, element.Width);

    /// <summary>Returns the effective component depth, falling back to width for legacy square components.</summary>
    private static double Depth(StructuralElement element) => Math.Max(0.02, element.Depth > 0 ? element.Depth : element.Width);

    /// <summary>Creates a counter-clockwise rectangle centered on a plan coordinate.</summary>
    private static IReadOnlyList<Point2> Rectangle(double centerX, double centerY, double width, double depth) =>
    [
        new(centerX - width / 2, centerY - depth / 2), new(centerX + width / 2, centerY - depth / 2),
        new(centerX + width / 2, centerY + depth / 2), new(centerX - width / 2, centerY + depth / 2)
    ];

    /// <summary>Creates a counter-clockwise regular polygon centered on a plan coordinate.</summary>
    private static IReadOnlyList<Point2> Round(double centerX, double centerY, double diameter, int sides) =>
        Enumerable.Range(0, sides)
            .Select(index => 2 * Math.PI * index / sides)
            .Select(angle => new Point2(centerX + Math.Cos(angle) * diameter / 2,
                centerY + Math.Sin(angle) * diameter / 2)).ToArray();
}
