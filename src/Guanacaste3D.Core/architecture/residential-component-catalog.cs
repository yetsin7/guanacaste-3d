namespace Guanacaste3D.Core;

/// <summary>Residential catalog group used to filter ready-to-place component families.</summary>
public enum ResidentialComponentCategory
{
    None,
    Seating,
    Tables,
    Bedroom,
    Storage,
    Kitchen,
    Bathroom,
    Appliances,
    Lighting,
    Decor,
    Outdoor
}

/// <summary>Recognizable parametric solid recipe used by a residential component instance.</summary>
public enum ResidentialComponentShape
{
    Box,
    Table,
    Chair,
    Sofa,
    Bed,
    Plant,
    Cabinet,
    WallCabinet,
    Refrigerator,
    Range,
    Dishwasher,
    WasherDryer,
    Toilet,
    Vanity,
    Bathtub,
    Shower,
    Desk,
    Dresser,
    Bookshelf,
    Television,
    Rug,
    FloorLamp,
    CeilingLight,
    Barbecue
}

/// <summary>
/// Ready-to-place residential component families supplied with every installation. Dimensions are metric and describe
/// width, overall height, underside offset, and depth; geometry stays lightweight enough for coordinated BIM views.
/// </summary>
public static class ResidentialComponentCatalog
{
    /// <summary>Complete default catalog for living, dining, bedroom, kitchen, bathroom, utility, and outdoor spaces.</summary>
    public static IReadOnlyList<ElementPreset> Presets { get; } =
    [
        Component("Dining Table 6 Seats", 1.80, 0.75, 0.90, ResidentialComponentShape.Table,
            ResidentialComponentCategory.Tables, "Residential.DiningTable6"),
        Component("Dining Table 4 Seats", 1.20, 0.75, 0.80, ResidentialComponentShape.Table,
            ResidentialComponentCategory.Tables, "Residential.DiningTable4"),
        Component("Coffee Table", 1.10, 0.42, 0.60, ResidentialComponentShape.Table,
            ResidentialComponentCategory.Tables, "Residential.CoffeeTable"),
        Component("Side Table", 0.50, 0.52, 0.50, ResidentialComponentShape.Table,
            ResidentialComponentCategory.Tables, "Residential.SideTable"),
        Component("Dining Chair", 0.48, 0.90, 0.52, ResidentialComponentShape.Chair,
            ResidentialComponentCategory.Seating, "Residential.DiningChair"),
        Component("Armchair", 0.90, 0.88, 0.85, ResidentialComponentShape.Sofa,
            ResidentialComponentCategory.Seating, "Residential.Armchair"),
        Component("Two Seat Sofa", 1.65, 0.88, 0.90, ResidentialComponentShape.Sofa,
            ResidentialComponentCategory.Seating, "Residential.Sofa2"),
        Component("Three Seat Sofa", 2.20, 0.88, 0.95, ResidentialComponentShape.Sofa,
            ResidentialComponentCategory.Seating, "Residential.Sofa3"),
        Component("Single Bed", 1.00, 0.95, 2.00, ResidentialComponentShape.Bed,
            ResidentialComponentCategory.Bedroom, "Residential.SingleBed"),
        Component("Double Bed", 1.40, 1.05, 2.00, ResidentialComponentShape.Bed,
            ResidentialComponentCategory.Bedroom, "Residential.DoubleBed"),
        Component("Queen Bed", 1.60, 1.10, 2.05, ResidentialComponentShape.Bed,
            ResidentialComponentCategory.Bedroom, "Residential.QueenBed"),
        Component("King Bed", 2.00, 1.10, 2.05, ResidentialComponentShape.Bed,
            ResidentialComponentCategory.Bedroom, "Residential.KingBed"),
        Component("Nightstand", 0.50, 0.58, 0.45, ResidentialComponentShape.Dresser,
            ResidentialComponentCategory.Bedroom, "Residential.Nightstand"),
        Component("Dresser", 1.40, 0.90, 0.50, ResidentialComponentShape.Dresser,
            ResidentialComponentCategory.Storage, "Residential.Dresser"),
        Component("Wardrobe", 1.80, 2.20, 0.62, ResidentialComponentShape.Cabinet,
            ResidentialComponentCategory.Storage, "Residential.Wardrobe"),
        Component("Bookshelf", 1.00, 2.00, 0.35, ResidentialComponentShape.Bookshelf,
            ResidentialComponentCategory.Storage, "Residential.Bookshelf"),
        Component("Desk", 1.40, 0.75, 0.70, ResidentialComponentShape.Desk,
            ResidentialComponentCategory.Tables, "Residential.Desk"),
        Component("Base Cabinet", 0.90, 0.90, 0.60, ResidentialComponentShape.Cabinet,
            ResidentialComponentCategory.Kitchen, "Residential.BaseCabinet"),
        Component("Wall Cabinet", 0.90, 0.75, 0.38, ResidentialComponentShape.WallCabinet,
            ResidentialComponentCategory.Kitchen, "Residential.WallCabinet", 1.45),
        Component("Kitchen Island", 1.80, 0.92, 0.90, ResidentialComponentShape.Cabinet,
            ResidentialComponentCategory.Kitchen, "Residential.KitchenIsland"),
        Component("Refrigerator", 0.90, 1.85, 0.75, ResidentialComponentShape.Refrigerator,
            ResidentialComponentCategory.Appliances, "Residential.Refrigerator"),
        Component("Cooking Range", 0.76, 0.92, 0.68, ResidentialComponentShape.Range,
            ResidentialComponentCategory.Appliances, "Residential.Range"),
        Component("Dishwasher", 0.60, 0.86, 0.62, ResidentialComponentShape.Dishwasher,
            ResidentialComponentCategory.Appliances, "Residential.Dishwasher"),
        Component("Washing Machine", 0.68, 0.90, 0.70, ResidentialComponentShape.WasherDryer,
            ResidentialComponentCategory.Appliances, "Residential.WashingMachine"),
        Component("Clothes Dryer", 0.68, 0.90, 0.70, ResidentialComponentShape.WasherDryer,
            ResidentialComponentCategory.Appliances, "Residential.ClothesDryer"),
        Component("Toilet", 0.42, 0.78, 0.72, ResidentialComponentShape.Toilet,
            ResidentialComponentCategory.Bathroom, "Residential.Toilet"),
        Component("Bathroom Vanity", 0.90, 0.90, 0.55, ResidentialComponentShape.Vanity,
            ResidentialComponentCategory.Bathroom, "Residential.Vanity"),
        Component("Bathtub", 0.80, 0.60, 1.70, ResidentialComponentShape.Bathtub,
            ResidentialComponentCategory.Bathroom, "Residential.Bathtub"),
        Component("Shower Enclosure", 0.90, 2.10, 0.90, ResidentialComponentShape.Shower,
            ResidentialComponentCategory.Bathroom, "Residential.Shower"),
        Component("Indoor Plant", 0.65, 1.35, 0.65, ResidentialComponentShape.Plant,
            ResidentialComponentCategory.Decor, "Residential.IndoorPlant"),
        Component("Small Plant", 0.32, 0.55, 0.32, ResidentialComponentShape.Plant,
            ResidentialComponentCategory.Decor, "Residential.SmallPlant"),
        Component("Television 55 Inch", 1.23, 0.78, 0.12, ResidentialComponentShape.Television,
            ResidentialComponentCategory.Decor, "Residential.Television55", 0.75),
        Component("Area Rug", 2.00, 0.02, 3.00, ResidentialComponentShape.Rug,
            ResidentialComponentCategory.Decor, "Residential.AreaRug"),
        Component("Floor Lamp", 0.40, 1.65, 0.40, ResidentialComponentShape.FloorLamp,
            ResidentialComponentCategory.Lighting, "Residential.FloorLamp"),
        Component("Ceiling Light", 0.45, 0.18, 0.45, ResidentialComponentShape.CeilingLight,
            ResidentialComponentCategory.Lighting, "Residential.CeilingLight", 2.70),
        Component("Outdoor Table", 1.50, 0.75, 0.85, ResidentialComponentShape.Table,
            ResidentialComponentCategory.Outdoor, "Residential.OutdoorTable"),
        Component("Barbecue Grill", 1.20, 1.15, 0.65, ResidentialComponentShape.Barbecue,
            ResidentialComponentCategory.Outdoor, "Residential.Barbecue")
    ];

    /// <summary>Creates one point-hosted family preset with a canonical English name and a localizable display key.</summary>
    private static ElementPreset Component(string name, double width, double height, double depth,
        ResidentialComponentShape shape, ResidentialComponentCategory category, string localizationKey,
        double offset = 0) =>
        new(name, StructuralElementKind.Component, ElementStyle.Default, width, height, offset, depth, shape,
            category, localizationKey);
}
