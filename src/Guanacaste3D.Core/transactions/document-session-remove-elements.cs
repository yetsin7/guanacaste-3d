namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>
    /// Removes walls (with their openings), openings, slabs, columns and structural elements (with their groups and
    /// railings) as one undoable step. Returns how many of the given ids were found.
    /// </summary>
    public int RemoveElements(IReadOnlyCollection<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var chosen = ids.ToHashSet();
        // Deleting one member of a group deletes the whole group, as it does for the structural groups.
        foreach (var group in Document.ElementGroups.Where(item => item.Kind != ElementGroupKind.Assembly && item.MemberIds.Any(chosen.Contains)).ToArray())
            chosen.UnionWith(group.MemberIds);
        var found = Document.Walls.Count(item => chosen.Contains(item.Id)) +
                    Document.Openings.Count(item => chosen.Contains(item.Id)) +
                    Document.Floors.Count(item => chosen.Contains(item.Id)) +
                    Document.Columns.Count(item => chosen.Contains(item.Id)) +
                    Document.StructuralElements.Count(item => chosen.Contains(item.Id)) +
                    Document.Rooms.Count(item => chosen.Contains(item.Id)) +
                    Document.ElectricalDevices.Count(item => chosen.Contains(item.Id)) +
                    Document.PlumbingPoints.Count(item => chosen.Contains(item.Id));
        if (found == 0) return 0;
        foreach (var wall in Document.Walls.Where(item => chosen.Contains(item.Id)))
            EnsureNotPinned(Document, wall.Id);
        Commit("Transaction.RemoveElements", document =>
        {
            var levels = document.Walls.Where(item => chosen.Contains(item.Id)).Select(item => item.LevelId).ToHashSet();
            var groups = document.StructuralElements.Where(item => chosen.Contains(item.Id) && item.GroupId is not null)
                .Select(item => item.GroupId!.Value).ToHashSet();
            var removedStructural = document.StructuralElements
                .Where(item => chosen.Contains(item.Id) || (item.GroupId is Guid group && groups.Contains(group)))
                .Select(item => item.Id).ToHashSet();
            levels.UnionWith(document.StructuralElements
                .Where(item => removedStructural.Contains(item.Id) && item.Kind == StructuralElementKind.RoomSeparator)
                .Select(item => item.LevelId));

            document.Openings.RemoveAll(item => chosen.Contains(item.Id) || chosen.Contains(item.WallId));
            document.Walls.RemoveAll(item => chosen.Contains(item.Id));
            document.Floors.RemoveAll(item => chosen.Contains(item.Id));
            document.Columns.RemoveAll(item => chosen.Contains(item.Id));
            // Rooms selected with everything else (Ctrl+A, then Delete) go too, as in the reference application.
            document.Rooms.RemoveAll(item => chosen.Contains(item.Id));
            RemoveInstallation(document, chosen);
            document.StructuralElements.RemoveAll(item => removedStructural.Contains(item.Id) ||
                (item.RailingHostId is Guid host && removedStructural.Contains(host)));
            foreach (var level in levels) RefreshRoomBoundariesIfNeeded(document, level);
        });
        return found;
    }

    /// <summary>
    /// Removes the selected electrical devices and plumbing points with what depends on them, as their own delete
    /// commands do: a meter or outlet takes its system and its pump, a panel its circuits, and the devices left behind
    /// lose their links to what was removed.
    /// </summary>
    private static void RemoveInstallation(BuildingDocument document, IReadOnlySet<Guid> chosen)
    {
        var points = document.PlumbingPoints.Where(point => chosen.Contains(point.Id)).ToList();
        var devices = document.ElectricalDevices.Where(device => chosen.Contains(device.Id)).Select(device => device.Id).ToHashSet();
        foreach (var point in points)
            if (point.PumpDeviceId is Guid pump) devices.Add(pump);
        var pointIds = points.Select(point => point.Id).ToHashSet();
        document.PlumbingPoints.RemoveAll(point => pointIds.Contains(point.Id));
        document.PlumbingSystems.RemoveAll(system => pointIds.Contains(system.SourceId));
        if (devices.Count == 0) return;
        var circuits = document.ElectricalCircuits.Where(circuit => devices.Contains(circuit.PanelId)).Select(circuit => circuit.Id).ToHashSet();
        document.ElectricalCircuits.RemoveAll(circuit => circuits.Contains(circuit.Id));
        document.ElectricalDevices.RemoveAll(device => devices.Contains(device.Id));
        for (var index = 0; index < document.ElectricalDevices.Count; index++)
        {
            var device = document.ElectricalDevices[index];
            if (device.Controls.Any(devices.Contains) || device.CircuitId is Guid circuit && circuits.Contains(circuit))
                document.ElectricalDevices[index] = device with
                {
                    Controls = device.Controls.Where(id => !devices.Contains(id)).ToList(),
                    CircuitId = device.CircuitId is Guid kept && circuits.Contains(kept) ? null : device.CircuitId
                };
        }
    }
}
