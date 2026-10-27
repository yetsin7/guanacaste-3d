using System.Text.Json.Serialization;

namespace Guanacaste3D.Core;

/// <summary>Kinds of electrical devices placed in a building, grouped as receptacles, switches, luminaires and equipment.</summary>
public enum ElectricalDeviceKind
{
    /// <summary>Duplex receptacle, 120 V 15 A.</summary>
    Outlet,
    /// <summary>Ground-fault protected receptacle for kitchens, baths and outdoors.</summary>
    GfciOutlet,
    /// <summary>Duplex receptacle with USB charging ports.</summary>
    UsbOutlet,
    /// <summary>240 V receptacle for an air conditioner, range or dryer.</summary>
    Outlet240,
    /// <summary>Single-pole switch.</summary>
    Switch,
    /// <summary>Three-way switch: two of them control the same lights from two places.</summary>
    ThreeWaySwitch,
    /// <summary>Dimmer: switches and dims the lights it controls.</summary>
    Dimmer,
    /// <summary>Surface-mounted ceiling fixture.</summary>
    CeilingFixture,
    /// <summary>Recessed downlight in the ceiling.</summary>
    Downlight,
    /// <summary>Wall sconce.</summary>
    WallSconce,
    /// <summary>Pendant hanging from the ceiling.</summary>
    Pendant,
    /// <summary>Load center (breaker panel).</summary>
    Panel,
    /// <summary>Ceiling fan with a light kit: switched and dimmed like a luminaire, it also draws its motor load.</summary>
    CeilingFan,
    /// <summary>
    /// Ductless split air conditioner: the wall-mounted indoor unit, with its outdoor condenser outside the wall and the
    /// refrigerant line set between them. It runs on 240 V.
    /// </summary>
    MiniSplit
}

/// <summary>
/// One electrical device. Wall devices stand on a wall face at <see cref="Elevation"/> above their level and look out
/// along <see cref="Facing"/> (radians, plan); luminaires hang from <see cref="Elevation"/> (the ceiling). The device is
/// fed by the circuit <see cref="CircuitId"/>; a switch controls the luminaires (or lighting components) listed in
/// <see cref="Controls"/>.
/// </summary>
public sealed record ElectricalDevice(
    Guid Id,
    Guid LevelId,
    ElectricalDeviceKind Kind,
    string Name,
    Point2 Position,
    double Elevation)
{
    public double Facing { get; init; }

    public Guid? CircuitId { get; init; }

    /// <summary>Connected load in watts (volt-amperes): lamp power for a luminaire, planned load for a receptacle.</summary>
    public double Watts { get; init; }

    /// <summary>Switches: closed (on) or open. Panel: main breaker closed.</summary>
    public bool IsOn { get; init; } = true;

    /// <summary>Dimmers: output from 0.1 to 1.</summary>
    public double DimLevel { get; init; } = 1;

    /// <summary>Switches: the luminaires and lighting components they control.</summary>
    public List<Guid> Controls { get; init; } = [];

    /// <summary>Panels: rating of the main breaker in amperes.</summary>
    public double MainBreakerAmps { get; init; }

    /// <summary>Air conditioners: nominal cooling capacity in BTU per hour.</summary>
    public double CoolingBtu { get; init; }

    /// <summary>Air conditioners: where the outdoor condenser stands, outside the wall behind the indoor unit.</summary>
    public Point2? OutdoorPosition { get; init; }

    /// <summary>Equipment that runs only on 240 V; everything else is a 120 V device.</summary>
    [JsonIgnore] public bool Needs240 => Kind is ElectricalDeviceKind.Outlet240 or ElectricalDeviceKind.MiniSplit;

    [JsonIgnore] public bool IsSwitch => Kind is ElectricalDeviceKind.Switch or ElectricalDeviceKind.ThreeWaySwitch or ElectricalDeviceKind.Dimmer;

    [JsonIgnore] public bool IsLuminaire => Kind is ElectricalDeviceKind.CeilingFixture or ElectricalDeviceKind.Downlight
        or ElectricalDeviceKind.WallSconce or ElectricalDeviceKind.Pendant or ElectricalDeviceKind.CeilingFan;

    [JsonIgnore] public bool IsReceptacle => Kind is ElectricalDeviceKind.Outlet or ElectricalDeviceKind.GfciOutlet
        or ElectricalDeviceKind.UsbOutlet or ElectricalDeviceKind.Outlet240;
}

/// <summary>A branch circuit of a panel, protected by one breaker.</summary>
public sealed record ElectricalCircuit(Guid Id, Guid PanelId, int Number, string Name)
{
    public double BreakerAmps { get; init; } = 15;

    /// <summary>120 V for lighting and receptacles, 240 V (two poles) for large appliances.</summary>
    public double Voltage { get; init; } = 120;

    /// <summary>The breaker handle: off when the user opens it. A breaker also trips by itself when overloaded.</summary>
    public bool BreakerOn { get; init; } = true;
}

/// <summary>Default sizes and loads of electrical devices.</summary>
public static class ElectricalStandards
{
    /// <summary>Mounting height above the floor (or ceiling height for luminaires, resolved by the caller).</summary>
    public static double MountingHeight(ElectricalDeviceKind kind) => kind switch
    {
        ElectricalDeviceKind.Outlet or ElectricalDeviceKind.GfciOutlet or ElectricalDeviceKind.UsbOutlet => 0.30,
        ElectricalDeviceKind.Outlet240 => 0.60,
        ElectricalDeviceKind.Switch or ElectricalDeviceKind.ThreeWaySwitch or ElectricalDeviceKind.Dimmer => 1.20,
        ElectricalDeviceKind.WallSconce => 2.00,
        // The centre of a minisplit's indoor unit, a little below a 2.4–2.7 m ceiling.
        ElectricalDeviceKind.MiniSplit => 2.15,
        ElectricalDeviceKind.Panel => 1.50,
        _ => 0
    };

    /// <summary>Default connected load: lamp watts for LED luminaires, 180 VA per receptacle (NEC 220.14(I)), 3.5 kW for a 240 V outlet.</summary>
    public static double DefaultWatts(ElectricalDeviceKind kind) => kind switch
    {
        ElectricalDeviceKind.CeilingFixture => 18,
        ElectricalDeviceKind.Downlight => 12,
        ElectricalDeviceKind.WallSconce => 9,
        ElectricalDeviceKind.Pendant => 15,
        // A 52" fan draws about 60 W at high speed, plus its 15 W LED light kit.
        ElectricalDeviceKind.CeilingFan => 75,
        ElectricalDeviceKind.Outlet or ElectricalDeviceKind.GfciOutlet or ElectricalDeviceKind.UsbOutlet => 180,
        ElectricalDeviceKind.Outlet240 => 3500,
        ElectricalDeviceKind.MiniSplit => CoolingWatts(12000),
        _ => 0
    };

    /// <summary>Capacities in which minisplits are sold, in BTU/h (9000 is ¾ ton, 12000 one ton).</summary>
    public static IReadOnlyList<double> CoolingCapacities { get; } = [9000, 12000, 18000, 24000, 36000];

    /// <summary>
    /// Cooling a hot, humid lowland room (Guanacaste, the coasts) takes about 600 BTU/h per square meter of floor:
    /// one ton (12000 BTU/h) for 20 m².
    /// </summary>
    public const double CoolingPerSquareMeter = 600;

    /// <summary>Electrical input of an air conditioner at an EER of 11 BTU/Wh, rounded to 10 W.</summary>
    public static double CoolingWatts(double btu) => Math.Round(btu / 11 / 10) * 10;

    /// <summary>The smallest standard capacity that cools a room of the given area, or the largest one.</summary>
    public static double CoolingCapacityFor(double area) =>
        CoolingCapacities.FirstOrDefault(capacity => capacity >= area * CoolingPerSquareMeter, CoolingCapacities[^1]);

    public static bool MountsOnWall(ElectricalDeviceKind kind) =>
        kind is not (ElectricalDeviceKind.CeilingFixture or ElectricalDeviceKind.Downlight or ElectricalDeviceKind.Pendant
            or ElectricalDeviceKind.CeilingFan);

    /// <summary>Breaker ratings offered for branch circuits.</summary>
    public static IReadOnlyList<double> BreakerRatings { get; } = [15, 20, 30, 40, 50, 60];

    /// <summary>
    /// Smallest copper conductor (THHN, 75 °C) allowed for a breaker rating, by NEC 240.4(D) and table 310.16: its AWG
    /// size, cross-section in mm² and resistance in ohms per kilometer.
    /// </summary>
    public static (string Awg, double SquareMillimeters, double OhmsPerKilometer) Conductor(double breakerAmps) => breakerAmps switch
    {
        <= 15 => ("14", 2.08, 8.28),
        <= 20 => ("12", 3.31, 5.21),
        <= 30 => ("10", 5.26, 3.28),
        <= 40 => ("8", 8.37, 2.06),
        <= 55 => ("6", 13.3, 1.30),
        _ => ("4", 21.2, 0.82)
    };
}
