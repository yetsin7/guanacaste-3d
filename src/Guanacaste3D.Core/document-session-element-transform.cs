namespace Guanacaste3D.Core;

/// <summary>
/// A rigid plan transformation: where a point goes and what happens to an orientation (an angle in radians). Rotating,
/// reflecting about an axis and sliding are all one of these, so every element that has a position, an outline or a
/// facing answers to the same Modify tools.
/// </summary>
public sealed record PlanTransform(Func<Point2, Point2> Map, Func<double, double> Orientation, bool Reflects, int QuarterTurns = 0)
{
    /// <summary>Turns about a centre; a multiple of 90° also swaps the width and depth of rectangular footprints.</summary>
    public static PlanTransform Rotation(Point2 centre, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var quarters = (int)Math.Round(degrees / 90) is var q && Math.Abs(degrees - q * 90) < 1e-9 ? ((q % 4) + 4) % 4 : -1;
        return new(point => WallEditGeometry.Rotate(point, centre, degrees), angle => angle + radians, false, quarters);
    }

    /// <summary>Reflects about the line through two points.</summary>
    public static PlanTransform Reflection(Point2 axisStart, Point2 axisEnd)
    {
        var dx = axisEnd.X - axisStart.X;
        var dy = axisEnd.Y - axisStart.Y;
        var length2 = dx * dx + dy * dy;
        if (length2 < 1e-18) throw new InvalidDataException("The mirror axis has no length.");
        var axis = Math.Atan2(dy, dx);
        return new(point =>
        {
            var t = ((point.X - axisStart.X) * dx + (point.Y - axisStart.Y) * dy) / length2;
            var foot = new Point2(axisStart.X + t * dx, axisStart.Y + t * dy);
            return new Point2(2 * foot.X - point.X, 2 * foot.Y - point.Y);
        }, angle => 2 * axis - angle, true);
    }

    /// <summary>Slides by a distance.</summary>
    public static PlanTransform Translation(double dx, double dy) =>
        new(point => new Point2(point.X + dx, point.Y + dy), angle => angle, false);

    /// <summary>Whether the width and depth of a footprint trade places (a quarter or three-quarter turn).</summary>
    public bool SwapsSides => QuarterTurns is 1 or 3;
}

public sealed partial class DocumentSession
{
    /// <summary>
    /// Applies a transformation to columns, structural and furniture elements, slabs, rooms, electrical devices and
    /// plumbing points, in place or as copies, in one undoable step. Walls have their own tools. Returns the ids of the
    /// transformed (or new) elements.
    /// </summary>
    public IReadOnlyList<Guid> TransformPlanElements(IReadOnlyCollection<Guid> ids, PlanTransform transform, bool copy, string transactionKey)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(transform);
        var result = new List<Guid>();
        var rooms = Document.Rooms.Where(item => ids.Contains(item.Id)).ToList();
        var floors = Document.Floors.Where(item => ids.Contains(item.Id)).ToList();
        var columns = Document.Columns.Where(item => ids.Contains(item.Id)).ToList();
        var elements = Document.StructuralElements.Where(item => ids.Contains(item.Id)).ToList();
        var devices = Document.ElectricalDevices.Where(item => ids.Contains(item.Id)).ToList();
        var points = Document.PlumbingPoints.Where(item => ids.Contains(item.Id)).ToList();
        if (rooms.Count + floors.Count + columns.Count + elements.Count + devices.Count + points.Count == 0)
            throw new InvalidDataException("There is nothing to transform.");
        if (!transform.Reflects && transform.QuarterTurns < 0 && rooms.Count > 0)
            throw new InvalidDataException("A room turns only by quarter turns.");

        Commit(transactionKey, document =>
        {
            Guid Id(Guid original) => copy ? Guid.NewGuid() : original;
            void Put<T>(List<T> list, T original, T changed, Func<T, Guid> idOf)
            {
                if (copy) list.Add(changed);
                else list[list.FindIndex(item => idOf(item) == idOf(original))] = changed;
            }

            foreach (var column in columns)
            {
                var id = Id(column.Id);
                var swapped = transform.SwapsSides ? column with { Id = id, Position = transform.Map(column.Position), Width = column.Depth, Depth = column.Width }
                                                    : column with { Id = id, Position = transform.Map(column.Position) };
                Put(document.Columns, column, swapped, item => item.Id);
                result.Add(id);
            }

            foreach (var element in elements)
            {
                var id = Id(element.Id);
                var changed = element with
                {
                    Id = id, Start = transform.Map(element.Start), End = transform.Map(element.End),
                    Rotation = NormalizeTurn(transform.Orientation(element.Rotation)),
                    GroupId = copy ? null : element.GroupId, RailingHostId = copy ? null : element.RailingHostId,
                    Cuts = element.Cuts?.ToList()
                };
                Put(document.StructuralElements, element, changed, item => item.Id);
                result.Add(id);
            }

            foreach (var floor in floors)
            {
                var id = Id(floor.Id);
                Put(document.Floors, floor, floor with { Id = id, Boundary = floor.Boundary.Select(transform.Map).ToList() }, item => item.Id);
                result.Add(id);
            }

            foreach (var room in rooms)
            {
                var id = Id(room.Id);
                var first = transform.Map(room.FirstCorner);
                var second = transform.Map(room.SecondCorner);
                var changed = room with
                {
                    Id = id, FirstCorner = first, SecondCorner = second, IsAutomatic = !copy && room.IsAutomatic,
                    Boundary = room.Boundary?.Select(transform.Map).ToList(),
                    Holes = room.Holes?.Select(hole => hole.Select(transform.Map).ToList()).ToList()
                };
                Put(document.Rooms, room, changed, item => item.Id);
                result.Add(id);
            }

            foreach (var device in devices)
            {
                var id = Id(device.Id);
                var changed = device with
                {
                    Id = id, Position = transform.Map(device.Position), Facing = transform.Orientation(device.Facing),
                    OutdoorPosition = device.OutdoorPosition is { } outdoor ? transform.Map(outdoor) : null,
                    CircuitId = copy ? null : device.CircuitId, Controls = copy ? [] : device.Controls
                };
                Put(document.ElectricalDevices, device, changed, item => item.Id);
                result.Add(id);
            }

            foreach (var point in points)
            {
                var id = Id(point.Id);
                Put(document.PlumbingPoints, point, point with { Id = id, Position = transform.Map(point.Position) }, item => item.Id);
                result.Add(id);
            }
        });
        return result;
    }
}
