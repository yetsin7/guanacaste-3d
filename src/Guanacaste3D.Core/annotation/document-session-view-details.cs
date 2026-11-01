namespace Guanacaste3D.Core;

/// <summary>Undoable per-view 2D drafting, deliberately excluded from world geometry.</summary>
public sealed partial class DocumentSession
{
    public Guid AddDetailLine(Guid viewId, Point2 start, Point2 end,
        string styleName = "Línea de detalle", string color = "#202020",
        DetailStrokeStyle style = DetailStrokeStyle.Solid, double width = 1)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.DetailLine", document =>
            document.DetailLines.Add(new DetailLine(id, viewId, start, end)
            {
                StyleName = styleName, Color = color, StrokeStyle = style, StrokeWidth = width
            }));
        return id;
    }

    public Guid AddFilledRegion(Guid viewId, IEnumerable<Point2> points,
        DetailFillPattern pattern = DetailFillPattern.Diagonal,
        string foreground = "#454545", string background = "#FFFFFF", double spacing = 0.10)
    {
        var id = Guid.NewGuid();
        var boundary = points.ToList();
        Commit("Transaction.FilledRegion", document =>
            document.FilledRegions.Add(new FilledRegion(id, viewId, boundary)
            {
                Pattern = pattern, Foreground = foreground, Background = background,
                PatternSpacing = spacing
            }));
        return id;
    }

    public void DeleteViewDetail(Guid id) =>
        Commit("Transaction.RemoveDetail", document =>
        {
            document.DetailLines.RemoveAll(line => line.Id == id);
            document.FilledRegions.RemoveAll(region => region.Id == id);
        });

    public Guid CopyViewDetail(Guid id, Guid destinationViewId, double offsetX = 0, double offsetY = 0)
    {
        var newId = Guid.NewGuid();
        Commit("Transaction.CopyDetail", document =>
        {
            Point2 Shift(Point2 point) => new(point.X + offsetX, point.Y + offsetY);
            if (document.DetailLines.FirstOrDefault(line => line.Id == id) is { } line)
                document.DetailLines.Add(line with
                {
                    Id = newId, ViewId = destinationViewId,
                    Start = Shift(line.Start), End = Shift(line.End)
                });
            else if (document.FilledRegions.FirstOrDefault(region => region.Id == id) is { } region)
                document.FilledRegions.Add(region with
                {
                    Id = newId, ViewId = destinationViewId,
                    Boundary = region.Boundary.Select(Shift).ToList()
                });
            else throw new InvalidDataException("The drawing detail does not exist.");
        });
        return newId;
    }
}
