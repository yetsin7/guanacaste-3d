using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core;

/// <summary>
/// One closed solid of an element: a vertical prism over <see cref="Footprint"/> from <see cref="Bottom"/> to
/// <see cref="Top"/>, both measured from the element's level. When <see cref="EndTop"/> is set, the footprint is a
/// quad whose second and third vertices rise to that height, which gives a ramp its slope.
/// </summary>
public sealed record ElementPart(IReadOnlyList<Point2> Footprint, double Bottom, double Top, SurfaceKind Kind)
{
    public double? EndTop { get; init; }
}

/// <summary>Breaks every solid structural and architectural element into the parts that the 3D model and plan draw.</summary>
public static class ElementGeometry
{
    private const double RailPost = 0.06;

    /// <summary>Returns the solids of an element; drafting marks, openings and shafts have none.</summary>
    public static IEnumerable<ElementPart> Parts(StructuralElement element)
    {
        if (!element.HasSolid) return [];
        return element.Kind switch
        {
            StructuralElementKind.Stair => StairParts(element),
            StructuralElementKind.Railing => RailingParts(element),
            StructuralElementKind.Ramp => RampParts(element),
            StructuralElementKind.Gutter => GutterParts(element),
            StructuralElementKind.CurtainWall => CurtainWallParts(element),
            StructuralElementKind.Skylight => SkylightParts(element),
            StructuralElementKind.Downspout or StructuralElementKind.Component or
                StructuralElementKind.IsolatedFoundation when element.Style == ElementStyle.Round => RoundPointParts(element),
            StructuralElementKind.Beam or StructuralElementKind.Brace or StructuralElementKind.Truss
                when element.Style is ElementStyle.IShape or ElementStyle.Tee && element.Length >= 0.05 =>
                SectionedMemberParts(element),
            _ => [Block(element, element.Footprint, element.Offset, element.Offset + element.Height)]
        };
    }

    private static ElementPart Block(StructuralElement element, IReadOnlyList<Point2> footprint, double bottom,
        double top, SurfaceKind kind = SurfaceKind.Structure) => new(footprint, bottom, top, kind);

    /// <summary>Straight stairs: solid steps by default, or treads on stepped stringers when open.</summary>
    private static IEnumerable<ElementPart> StairParts(StructuralElement element)
    {
        if (element.Length < 0.05) yield break;
        if (element.Style != ElementStyle.Open)
        {
            foreach (var step in CirculationGeometry.Parts(element))
                yield return Block(element, step.Footprint, step.Offset, step.Offset + step.Height);
            yield break;
        }
        var count = (int)Math.Ceiling(element.Height / 0.175);
        const double tread = 0.04;
        const double stringer = 0.06;
        for (var index = 0; index < count; index++)
        {
            var from = ArchitecturalShapes.Lerp(element.Start, element.End, (double)index / count);
            var to = ArchitecturalShapes.Lerp(element.Start, element.End, (double)(index + 1) / count);
            var top = element.Offset + element.Height * (index + 1) / count;
            yield return Block(element, ArchitecturalShapes.Strip(from, to, 0, element.Width), top - tread, top);
            foreach (var side in new[] { -1, 1 })
                yield return Block(element, ArchitecturalShapes.Strip(from, to,
                    side * (element.Width / 2 - stringer / 2), stringer), element.Offset, top - tread);
        }
    }

    /// <summary>Guards: posts and two rails by default, or balusters, glass, cables, or a solid parapet.</summary>
    private static IEnumerable<ElementPart> RailingParts(StructuralElement element)
    {
        if (element.Length < 0.05) yield break;
        var bottom = element.Offset;
        var top = element.Offset + element.Height;
        switch (element.Style)
        {
            case ElementStyle.Default:
                foreach (var part in CirculationGeometry.Parts(element))
                    yield return Block(element, part.Footprint, part.Offset, part.Offset + part.Height);
                yield break;
            case ElementStyle.SolidParapet:
                const double thickness = 0.15;
                yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, thickness),
                    bottom, top - 0.05);
                yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, thickness + 0.06),
                    top - 0.05, top);
                yield break;
        }

        // Posts every 1.8 m (1.2 m for cables), always at both ends, then the style's infill and a top rail.
        var postSpacing = element.Style == ElementStyle.Cable ? 1.2 : 1.8;
        var intervals = (int)Math.Ceiling(element.Length / postSpacing);
        for (var index = 0; index <= intervals; index++)
        {
            var position = ArchitecturalShapes.Lerp(element.Start, element.End, (double)index / intervals);
            yield return Block(element, ArchitecturalShapes.PointFootprint(position, Math.Max(element.Width, RailPost), false),
                bottom, top);
        }
        yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, element.Width + 0.02),
            top - 0.05, top);
        switch (element.Style)
        {
            case ElementStyle.Pickets:
                yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, element.Width),
                    bottom + 0.08, bottom + 0.13);
                const double picket = 0.02;
                var spacing = Math.Max(0.11, element.Length / 2000);
                var count = (int)Math.Floor(element.Length / spacing);
                for (var index = 1; index < count; index++)
                    yield return Block(element, ArchitecturalShapes.PointFootprint(
                        ArchitecturalShapes.Lerp(element.Start, element.End, index * spacing / element.Length), picket, false),
                        bottom + 0.13, top - 0.05);
                break;
            case ElementStyle.GlassPanel:
                yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, 0.012),
                    bottom + 0.10, top - 0.05, SurfaceKind.Glass);
                break;
            case ElementStyle.Cable:
                for (var index = 1; index <= 5; index++)
                {
                    var height = bottom + 0.12 + (element.Height - 0.22) * (index - 1) / 4.0;
                    yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, 0.012),
                        height, height + 0.012);
                }
                break;
        }
    }

    /// <summary>A wedge rising from the start (thin) to the end (full height).</summary>
    private static IEnumerable<ElementPart> RampParts(StructuralElement element)
    {
        if (element.Length < 0.05) yield break;
        var toe = Math.Min(0.08, element.Height / 2);
        yield return new ElementPart(ArchitecturalShapes.Strip(element.Start, element.End, 0, element.Width),
            element.Offset, element.Offset + toe, SurfaceKind.Structure) { EndTop = element.Offset + element.Height };
    }

    /// <summary>A U-shaped channel: a thin bottom and two thin sides, 8 mm thick.</summary>
    private static IEnumerable<ElementPart> GutterParts(StructuralElement element)
    {
        if (element.Length < 0.05) yield break;
        const double sheet = 0.008;
        yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, element.Width),
            element.Offset, element.Offset + sheet);
        foreach (var side in new[] { -1, 1 })
            yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End,
                side * (element.Width / 2 - sheet / 2), sheet), element.Offset, element.Offset + element.Height);
    }

    private static IEnumerable<ElementPart> RoundPointParts(StructuralElement element)
    {
        var footprint = ArchitecturalShapes.PointFootprint(element.Start, element.Width, true);
        yield return Block(element, footprint, element.Offset, element.Offset + element.Height);
    }

    /// <summary>Wide-flange and tee members as stacked flange and web boxes along the axis.</summary>
    private static IEnumerable<ElementPart> SectionedMemberParts(StructuralElement element)
    {
        var flange = Math.Clamp(element.Height * 0.12, 0.01, element.Height / 3);
        var web = Math.Max(0.01, element.Width * 0.12);
        var bottom = element.Offset;
        var top = element.Offset + element.Height;
        if (element.Style == ElementStyle.IShape)
            yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, element.Width),
                bottom, bottom + flange);
        yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, web),
            element.Style == ElementStyle.IShape ? bottom + flange : bottom, top - flange);
        yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, element.Width),
            top - flange, top);
    }

    /// <summary>A pane of glass in a grid of mullions (every 1.5 m) and transoms (every 1.2 m).</summary>
    private static IEnumerable<ElementPart> CurtainWallParts(StructuralElement element)
    {
        if (element.Length < 0.05) yield break;
        const double member = 0.06;
        var bottom = element.Offset;
        var top = element.Offset + element.Height;
        yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, 0.012),
            bottom, top, SurfaceKind.Glass);
        var columns = Math.Max(1, (int)Math.Ceiling(element.Length / (element.GridWidth > 0 ? element.GridWidth : 1.5)));
        for (var index = 0; index <= columns; index++)
        {
            var fraction = (double)index / columns;
            var from = ArchitecturalShapes.Lerp(element.Start, element.End,
                Math.Max(0, fraction - member / 2 / element.Length));
            var to = ArchitecturalShapes.Lerp(element.Start, element.End,
                Math.Min(1, fraction + member / 2 / element.Length));
            yield return Block(element, ArchitecturalShapes.Strip(from, to, 0, element.Width), bottom, top);
        }
        var rows = Math.Max(1, (int)Math.Ceiling(element.Height / (element.GridHeight > 0 ? element.GridHeight : 1.2)));
        for (var index = 0; index <= rows; index++)
        {
            var center = bottom + element.Height * index / rows;
            yield return Block(element, ArchitecturalShapes.Strip(element.Start, element.End, 0, element.Width),
                Math.Max(bottom, center - member / 2), Math.Min(top, center + member / 2));
        }
    }

    /// <summary>A curb of four bars around a glass pane.</summary>
    private static IEnumerable<ElementPart> SkylightParts(StructuralElement element)
    {
        var extent = element.PlanExtent;
        const double bar = 0.08;
        var width = extent.MaxX - extent.MinX;
        var depth = extent.MaxY - extent.MinY;
        if (width <= 2 * bar + 0.05 || depth <= 2 * bar + 0.05) yield break;
        var bottom = element.Offset;
        var top = element.Offset + element.Height;
        IReadOnlyList<Point2> Rectangle(double x0, double y0, double x1, double y1) =>
            [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
        yield return Block(element, Rectangle(extent.MinX, extent.MinY, extent.MaxX, extent.MinY + bar), bottom, top);
        yield return Block(element, Rectangle(extent.MinX, extent.MaxY - bar, extent.MaxX, extent.MaxY), bottom, top);
        yield return Block(element, Rectangle(extent.MinX, extent.MinY + bar, extent.MinX + bar, extent.MaxY - bar), bottom, top);
        yield return Block(element, Rectangle(extent.MaxX - bar, extent.MinY + bar, extent.MaxX, extent.MaxY - bar), bottom, top);
        yield return Block(element, Rectangle(extent.MinX + bar, extent.MinY + bar, extent.MaxX - bar,
            extent.MaxY - bar), top - 0.03, top - 0.015, SurfaceKind.Glass);
    }
}
