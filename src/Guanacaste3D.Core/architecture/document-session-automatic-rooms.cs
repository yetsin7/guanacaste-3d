namespace Guanacaste3D.Core;

/// <summary>
/// Keeps automatically discovered rectangular rooms in sync with their current boundaries.
/// Manually placed rooms and older, unmarked project rooms are never changed by this service.
/// </summary>
public sealed partial class DocumentSession
{
    /// <summary>
    /// Discover new rooms and reconcile existing automatic rooms in one undoable transaction.
    /// Matching uses the largest shared floor area to preserve a room's GUID, number and edited name
    /// when a separator moves. When two rooms merge, the best-matching identity survives.
    /// </summary>
    public int SynchronizeAutomaticRooms(Guid levelId)
    {
        if (!Document.Levels.Any(level => level.Id == levelId))
            throw new InvalidDataException("The room level does not exist.");

        // Build a complete proposed result first, so an idempotent refresh adds no undo step.
        var proposed = Document.Clone();
        var created = RebuildAutomaticRooms(proposed, levelId);
        if (Document.Rooms.SequenceEqual(proposed.Rooms)) return 0;

        Commit("Transaction.SyncAutomaticRooms", document =>
        {
            document.Rooms = proposed.Rooms;
        });
        return created;
    }

    /// <summary>
    /// Reconciles automatic rooms directly within an already open atomic transaction, for changes
    /// to room separators and other enclosing elements. Never call Commit from this helper.
    /// </summary>
    private static int RebuildAutomaticRooms(BuildingDocument document, Guid levelId)
    {
        var protectedRooms = document.Rooms.Where(room => room.LevelId == levelId && !room.IsAutomatic).ToArray();
        var regions = RoomDetection.Detect(document, levelId)
            .Where(space => protectedRooms.All(room => !RoomDetection.Overlaps(room, space.Min, space.Max)))
            .ToArray();
        var automatic = document.Rooms.Where(room => room.LevelId == levelId && room.IsAutomatic).ToArray();

        double OverlapArea(Room room, Point2 low, Point2 high)
        {
            var minX = Math.Min(room.FirstCorner.X, room.SecondCorner.X);
            var maxX = Math.Max(room.FirstCorner.X, room.SecondCorner.X);
            var minY = Math.Min(room.FirstCorner.Y, room.SecondCorner.Y);
            var maxY = Math.Max(room.FirstCorner.Y, room.SecondCorner.Y);
            return Math.Max(0, Math.Min(maxX, high.X) - Math.Max(minX, low.X)) *
                   Math.Max(0, Math.Min(maxY, high.Y) - Math.Max(minY, low.Y));
        }

        var candidates = (
            from oldIndex in Enumerable.Range(0, automatic.Length)
            from regionIndex in Enumerable.Range(0, regions.Length)
            let area = OverlapArea(automatic[oldIndex], regions[regionIndex].Min, regions[regionIndex].Max)
            where area > 1e-8
            orderby area descending, oldIndex, regionIndex
            select (oldIndex, regionIndex)
        ).ToArray();

        var usedRooms = new HashSet<int>();
        var usedRegions = new HashSet<int>();
        var replacements = new Dictionary<Guid, Room>();
        foreach (var (oldIndex, regionIndex) in candidates)
        {
            if (!usedRooms.Add(oldIndex)) continue;
            if (!usedRegions.Add(regionIndex))
            {
                usedRooms.Remove(oldIndex);
                continue;
            }
            var old = automatic[oldIndex];
            replacements.Add(old.Id, old with
            {
                FirstCorner = regions[regionIndex].Min,
                SecondCorner = regions[regionIndex].Max
            });
        }

        // Only automatic rooms without a valid enclosure disappear. Manual room annotations stay.
        var kept = document.Rooms
            .Where(room => room.LevelId != levelId || !room.IsAutomatic || replacements.ContainsKey(room.Id))
            .Select(room => replacements.TryGetValue(room.Id, out var updated) ? updated : room)
            .ToList();
        var usedNumbers = kept.Select(room => room.Number).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextNumber = 1;
        var created = 0;
        for (var index = 0; index < regions.Length; index++)
        {
            if (usedRegions.Contains(index)) continue;
            string number;
            do
            {
                number = nextNumber.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
                nextNumber++;
            } while (!usedNumbers.Add(number));

            var (min, max) = regions[index];
            kept.Add(new Room(Guid.NewGuid(), levelId, number, $"Room {number}", min, max)
            {
                IsAutomatic = true
            });
            created++;
        }
        document.Rooms = kept;
        return created;
    }
}
