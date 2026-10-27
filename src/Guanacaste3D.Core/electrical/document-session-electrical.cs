namespace Guanacaste3D.Core;

/// <summary>Electrical design edits; each public method is one undoable transaction.</summary>
public sealed partial class DocumentSession
{
    /// <summary>Adds a device with its standard mounting height and load; luminaires hang at <paramref name="ceiling"/>.</summary>
    public Guid AddElectricalDevice(Guid levelId, ElectricalDeviceKind kind, string name, Point2 position, double facing = 0,
        double? ceiling = null)
    {
        var id = Guid.NewGuid();
        var elevation = ElectricalStandards.MountsOnWall(kind) ? ElectricalStandards.MountingHeight(kind) : ceiling ?? WallTopOf(levelId);
        Commit("Transaction.AddElectricalDevice", document => document.ElectricalDevices.Add(
            new ElectricalDevice(id, levelId, kind, name, position, elevation)
            {
                Facing = facing,
                Watts = ElectricalStandards.DefaultWatts(kind),
                MainBreakerAmps = kind == ElectricalDeviceKind.Panel ? 100 : 0
            }));
        return id;
    }

    /// <summary>Removes a device, its circuits if it is a panel, and every switch link to it.</summary>
    public void RemoveElectricalDevice(Guid deviceId)
    {
        if (Document.ElectricalDevices.All(device => device.Id != deviceId)) return;
        Commit("Transaction.RemoveElectricalDevice", document =>
        {
            var circuits = document.ElectricalCircuits.Where(circuit => circuit.PanelId == deviceId).Select(circuit => circuit.Id).ToHashSet();
            document.ElectricalCircuits.RemoveAll(circuit => circuits.Contains(circuit.Id));
            document.ElectricalDevices.RemoveAll(device => device.Id == deviceId);
            for (var index = 0; index < document.ElectricalDevices.Count; index++)
            {
                var device = document.ElectricalDevices[index];
                if (device.Controls.Contains(deviceId) || device.CircuitId is Guid circuit && circuits.Contains(circuit))
                    document.ElectricalDevices[index] = device with
                    {
                        Controls = device.Controls.Where(id => id != deviceId).ToList(),
                        CircuitId = device.CircuitId is Guid c && circuits.Contains(c) ? null : device.CircuitId
                    };
            }
        });
    }

    /// <summary>Flips a switch (or a panel's main breaker); the lights it controls follow at once.</summary>
    public void ToggleSwitch(Guid deviceId)
    {
        var device = Document.ElectricalDevices.FirstOrDefault(candidate => candidate.Id == deviceId)
            ?? throw new InvalidDataException("The electrical device does not exist.");
        if (!device.IsSwitch && device.Kind != ElectricalDeviceKind.Panel) return;
        Update("Transaction.ToggleSwitch", deviceId, existing => existing with { IsOn = !existing.IsOn });
    }

    /// <summary>Sets a dimmer's output from 10 % to 100 %.</summary>
    public void SetDimLevel(Guid deviceId, double level) =>
        Update("Transaction.SetDimLevel", deviceId, existing => existing with { DimLevel = Math.Clamp(level, 0.1, 1) });

    /// <summary>Sets a device's connected load in watts.</summary>
    public void SetDeviceLoad(Guid deviceId, double watts)
    {
        if (!double.IsFinite(watts) || watts is < 0 or > 50000) throw new InvalidDataException("Invalid electrical device.");
        Update("Transaction.SetDeviceLoad", deviceId, existing => existing with { Watts = watts });
    }

    /// <summary>Links a switch to a luminaire or lighting component, or unlinks it when already linked. Returns the new state.</summary>
    public bool ToggleSwitchLink(Guid switchId, Guid targetId)
    {
        var device = Document.ElectricalDevices.FirstOrDefault(candidate => candidate.Id == switchId && candidate.IsSwitch)
            ?? throw new InvalidDataException("The electrical device does not exist.");
        var linked = !device.Controls.Contains(targetId);
        Update(linked ? "Transaction.LinkSwitch" : "Transaction.UnlinkSwitch", switchId, existing => existing with
        {
            Controls = linked ? [.. existing.Controls, targetId] : existing.Controls.Where(id => id != targetId).ToList()
        });
        return linked;
    }

    /// <summary>Adds the next numbered circuit to a panel: 20 A for receptacles, 15 A for lighting, 30 A at 240 V.</summary>
    public Guid AddCircuit(Guid panelId, string name, double breakerAmps = 15, double voltage = 120)
    {
        if (Document.ElectricalDevices.All(device => device.Id != panelId || device.Kind != ElectricalDeviceKind.Panel))
            throw new InvalidDataException("The electrical device does not exist.");
        var id = Guid.NewGuid();
        var number = Document.ElectricalCircuits.Where(circuit => circuit.PanelId == panelId).Select(circuit => circuit.Number)
            .DefaultIfEmpty(0).Max() + 1;
        Commit("Transaction.AddCircuit", document => document.ElectricalCircuits.Add(
            new ElectricalCircuit(id, panelId, number, name) { BreakerAmps = breakerAmps, Voltage = voltage }));
        return id;
    }

    /// <summary>Feeds a device from a circuit (or disconnects it with null).</summary>
    public void AssignCircuit(Guid deviceId, Guid? circuitId)
    {
        if (circuitId is Guid circuit && Document.ElectricalCircuits.All(candidate => candidate.Id != circuit))
            throw new InvalidDataException("The circuit does not exist.");
        Update("Transaction.AssignCircuit", deviceId, existing => existing with { CircuitId = circuitId });
    }

    /// <summary>Changes a circuit's breaker: its rating and its handle.</summary>
    public void SetBreaker(Guid circuitId, double amps, bool on)
    {
        if (!ElectricalStandards.BreakerRatings.Contains(amps)) throw new InvalidDataException("Invalid circuit.");
        Commit("Transaction.SetBreaker", document =>
        {
            var index = document.ElectricalCircuits.FindIndex(circuit => circuit.Id == circuitId);
            if (index < 0) throw new InvalidDataException("The circuit does not exist.");
            document.ElectricalCircuits[index] = document.ElectricalCircuits[index] with { BreakerAmps = amps, BreakerOn = on };
        });
    }

    /// <summary>Renames a circuit.</summary>
    public void RenameCircuit(Guid circuitId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80) throw new InvalidDataException("Invalid circuit.");
        Commit("Transaction.RenameCircuit", document =>
        {
            var index = document.ElectricalCircuits.FindIndex(circuit => circuit.Id == circuitId);
            if (index < 0) throw new InvalidDataException("The circuit does not exist.");
            document.ElectricalCircuits[index] = document.ElectricalCircuits[index] with { Name = name.Trim() };
        });
    }

    /// <summary>Removes a circuit; its devices are left unconnected.</summary>
    public void RemoveCircuit(Guid circuitId)
    {
        if (Document.ElectricalCircuits.All(circuit => circuit.Id != circuitId)) return;
        Commit("Transaction.RemoveCircuit", document =>
        {
            document.ElectricalCircuits.RemoveAll(circuit => circuit.Id == circuitId);
            for (var index = 0; index < document.ElectricalDevices.Count; index++)
                if (document.ElectricalDevices[index].CircuitId == circuitId)
                    document.ElectricalDevices[index] = document.ElectricalDevices[index] with { CircuitId = null };
        });
    }

    /// <summary>Moves an electrical device in plan.</summary>
    public void MoveElectricalDevice(Guid deviceId, double deltaX, double deltaY) =>
        Update("Transaction.MoveElectricalDevice", deviceId, existing => existing with
        {
            Position = new Point2(existing.Position.X + deltaX, existing.Position.Y + deltaY)
        });

    private void Update(string transaction, Guid deviceId, Func<ElectricalDevice, ElectricalDevice> change)
    {
        if (Document.ElectricalDevices.All(device => device.Id != deviceId))
            throw new InvalidDataException("The electrical device does not exist.");
        Commit(transaction, document =>
        {
            var index = document.ElectricalDevices.FindIndex(device => device.Id == deviceId);
            document.ElectricalDevices[index] = change(document.ElectricalDevices[index]);
        });
    }
}

/// <summary>Validation rules for the electrical system.</summary>
public static class ElectricalRules
{
    public static void Validate(BuildingDocument document)
    {
        if (document.ElectricalDevices is null || document.ElectricalCircuits is null ||
            document.ElectricalDevices.Count > 20000 || document.ElectricalCircuits.Count > 2000)
            throw new InvalidDataException("Invalid electrical device.");
        var levels = document.Levels.Select(level => level.Id).ToHashSet();
        var circuits = document.ElectricalCircuits.Select(circuit => circuit.Id).ToHashSet();
        var panels = document.ElectricalDevices.Where(device => device.Kind == ElectricalDeviceKind.Panel).Select(device => device.Id).ToHashSet();
        if (document.ElectricalDevices.Select(device => device.Id).Distinct().Count() != document.ElectricalDevices.Count)
            throw new InvalidDataException("Invalid electrical device.");
        foreach (var device in document.ElectricalDevices)
            if (device.Id == Guid.Empty || !levels.Contains(device.LevelId) || !Enum.IsDefined(device.Kind) ||
                string.IsNullOrWhiteSpace(device.Name) || device.Name.Length > 120 ||
                !double.IsFinite(device.Position.X) || !double.IsFinite(device.Position.Y) ||
                !double.IsFinite(device.Elevation) || device.Elevation is < -10 or > 100 || !double.IsFinite(device.Facing) ||
                !double.IsFinite(device.Watts) || device.Watts is < 0 or > 50000 ||
                !double.IsFinite(device.DimLevel) || device.DimLevel is < 0.1 or > 1 ||
                device.CircuitId is Guid circuit && !circuits.Contains(circuit) ||
                device.Controls is null || device.Controls.Count > 200 || device.Controls.Count > 0 && !device.IsSwitch ||
                !double.IsFinite(device.MainBreakerAmps) || device.MainBreakerAmps is < 0 or > 1200)
                throw new InvalidDataException("Invalid electrical device.");
        foreach (var circuit in document.ElectricalCircuits)
            if (circuit.Id == Guid.Empty || !panels.Contains(circuit.PanelId) || circuit.Number is < 1 or > 200 ||
                string.IsNullOrWhiteSpace(circuit.Name) || circuit.Name.Length > 80 ||
                !double.IsFinite(circuit.BreakerAmps) || circuit.BreakerAmps is < 1 or > 400 ||
                circuit.Voltage is not (120 or 127 or 208 or 220 or 230 or 240))
                throw new InvalidDataException("Invalid circuit.");
    }
}
