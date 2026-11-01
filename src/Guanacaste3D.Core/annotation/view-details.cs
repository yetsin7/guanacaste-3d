namespace Guanacaste3D.Core;

/// <summary>Dash treatment for a drawing-only segment.</summary>
public enum DetailStrokeStyle { Solid, Dashed, Dotted, Center }

/// <summary>Reusable hatch choices for view-only regions.</summary>
public enum DetailFillPattern { Solid, Diagonal, Crosshatch, Herringbone, Dots }

/// <summary>
/// A view-scoped drawing primitive: never meshed into 3D geometry.
/// All coordinates are in the owning view's metric XY plane.
/// </summary>
public sealed record DetailLine(Guid Id, Guid ViewId, Point2 Start, Point2 End)
{
    public string StyleName { get; init; } = "Línea de detalle";
    public string Color { get; init; } = "#202020";
    public double StrokeWidth { get; init; } = 1.0;
    public DetailStrokeStyle StrokeStyle { get; init; }
}

/// <summary>A filled polygon annotation belonging exclusively to one architectural view.</summary>
public sealed record FilledRegion(Guid Id, Guid ViewId, List<Point2> Boundary)
{
    public DetailFillPattern Pattern { get; init; } = DetailFillPattern.Diagonal;
    public string Foreground { get; init; } = "#454545";
    public string Background { get; init; } = "#FFFFFF";
    public double PatternSpacing { get; init; } = 0.10;
    public bool ShowBoundary { get; init; } = true;
}

/// <summary>Validation and copy rules for drafting-only shapes.</summary>
public static class ViewDetailRules
{
    private static bool ValidPoint(Point2 point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) &&
        Math.Abs(point.X) < 1_000_000 && Math.Abs(point.Y) < 1_000_000;

    public static bool ValidColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color[1..].All(Uri.IsHexDigit);

    public static void Validate(BuildingDocument document)
    {
        var ids = document.Views.Where(view => view.Kind == ProjectViewKind.Plan)
            .Select(view => view.Id).ToHashSet();
        if (document.DetailLines is null || document.FilledRegions is null ||
            document.DetailLines.Count > 200000 || document.FilledRegions.Count > 50000)
            throw new InvalidDataException("Invalid view detail collections.");
        var elements = document.DetailLines.Select(line => line.Id)
            .Concat(document.FilledRegions.Select(region => region.Id)).ToArray();
        if (elements.Any(id => id == Guid.Empty) || elements.Distinct().Count() != elements.Length)
            throw new InvalidDataException("Duplicate view annotation identifiers.");
        foreach (var line in document.DetailLines)
            if (!ids.Contains(line.ViewId) || !ValidPoint(line.Start) || !ValidPoint(line.End) ||
                line.Start.DistanceTo(line.End) < 0.0001 ||
                !ValidColor(line.Color) || !double.IsFinite(line.StrokeWidth) ||
                line.StrokeWidth is < 0.1 or > 20 || !Enum.IsDefined(line.StrokeStyle) ||
                string.IsNullOrWhiteSpace(line.StyleName) || line.StyleName.Length > 100)
                throw new InvalidDataException("Invalid view detail line.");
        foreach (var region in document.FilledRegions)
        {
            if (!ids.Contains(region.ViewId) || region.Boundary is null ||
                region.Boundary.Count is < 3 or > 10000 ||
                region.Boundary.Any(point => !ValidPoint(point)) ||
                !ValidColor(region.Foreground) || !ValidColor(region.Background) ||
                !double.IsFinite(region.PatternSpacing) || region.PatternSpacing is < 0.005 or > 10 ||
                !Enum.IsDefined(region.Pattern))
                throw new InvalidDataException("Invalid filled region.");
            var area = 0.0;
            for (var i = 0; i < region.Boundary.Count; i++)
            {
                var a = region.Boundary[i];
                var b = region.Boundary[(i + 1) % region.Boundary.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            if (Math.Abs(area) < 0.000001)
                throw new InvalidDataException("Filled region cannot have zero area.");
        }
    }
}
