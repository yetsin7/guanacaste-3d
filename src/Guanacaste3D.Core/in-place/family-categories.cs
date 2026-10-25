namespace Guanacaste3D.Core;

/// <summary>Professional discipline used to group categories in the category picker.</summary>
public enum FamilyDiscipline
{
    Architectural,
    Structural,
    Mep,
    Other
}

/// <summary>
/// A family category. <see cref="Id"/> is a stable identifier stored in projects, while
/// <see cref="NameKey"/> and <see cref="DefaultName"/> are presentation data that may change.
/// </summary>
public sealed record FamilyCategory(
    string Id,
    string NameKey,
    string DefaultName,
    FamilyDiscipline Discipline,
    bool SupportsInPlace);

/// <summary>
/// The registered family categories. Pickers read this list, so new categories are added
/// here without changing any window. Only Generic Models accepts in-place families for now.
/// </summary>
public static class FamilyCategoryCatalog
{
    public const string GenericModelsId = "generic-models";

    public static IReadOnlyList<FamilyCategory> All { get; } =
    [
        new("duct-fittings", "FamilyCategory.DuctFittings", "Accesorios de conductos", FamilyDiscipline.Mep, false),
        new("pipe-fittings", "FamilyCategory.PipeFittings", "Accesorios de tuberías", FamilyDiscipline.Mep, false),
        new("vibration-management", "FamilyCategory.VibrationManagement", "Administración de vibración", FamilyDiscipline.Mep, false),
        new("electrical-equipment", "FamilyCategory.ElectricalEquipment", "Aparatos eléctricos", FamilyDiscipline.Mep, false),
        new("plumbing-fixtures", "FamilyCategory.PlumbingFixtures", "Aparatos sanitarios", FamilyDiscipline.Mep, false),
        new("parking", "FamilyCategory.Parking", "Aparcamiento", FamilyDiscipline.Architectural, false),
        new("mep-auxiliary-frame", "FamilyCategory.MepAuxiliaryFrame", "Armazón auxiliar MEP", FamilyDiscipline.Mep, false),
        new("structural-framing", "FamilyCategory.StructuralFraming", "Armazón estructural", FamilyDiscipline.Structural, false),
        new("railings", "FamilyCategory.Railings", "Barandillas", FamilyDiscipline.Architectural, false),
        new("structural-cables", "FamilyCategory.StructuralCables", "Cables de acero estructurales", FamilyDiscipline.Structural, false),
        new("bridge-cables", "FamilyCategory.BridgeCables", "Cables de puente", FamilyDiscipline.Structural, false),
        new("roads", "FamilyCategory.Roads", "Carreteras", FamilyDiscipline.Other, false),
        new("structural-foundations", "FamilyCategory.StructuralFoundations", "Cimentación estructural", FamilyDiscipline.Structural, false),
        new("vertical-circulation", "FamilyCategory.VerticalCirculation", "Circulación vertical", FamilyDiscipline.Architectural, false),
        new("structural-connections", "FamilyCategory.StructuralConnections", "Conexiones estructurales", FamilyDiscipline.Structural, false),
        new("buttresses", "FamilyCategory.Buttresses", "Contrafuertes", FamilyDiscipline.Structural, false),
        new("roofs", "FamilyCategory.Roofs", "Cubiertas", FamilyDiscipline.Architectural, false),
        new("audiovisual-devices", "FamilyCategory.AudiovisualDevices", "Dispositivos audiovisuales", FamilyDiscipline.Mep, false),
        new("fire-alarm-devices", "FamilyCategory.FireAlarmDevices", "Dispositivos de alarma de incendios", FamilyDiscipline.Mep, false),
        new("communication-devices", "FamilyCategory.CommunicationDevices", "Dispositivos de comunicación", FamilyDiscipline.Mep, false),
        new("mechanical-control-devices", "FamilyCategory.MechanicalControlDevices", "Dispositivos de control mecánico", FamilyDiscipline.Mep, false),
        new("data-devices", "FamilyCategory.DataDevices", "Dispositivos de datos", FamilyDiscipline.Mep, false),
        new("expansion-joints", "FamilyCategory.ExpansionJoints", "Juntas de expansión", FamilyDiscipline.Structural, false),
        new("lighting-fixtures", "FamilyCategory.LightingFixtures", "Luminarias", FamilyDiscipline.Mep, false),
        new("mass", "FamilyCategory.Mass", "Masa", FamilyDiscipline.Architectural, false),
        new("furniture", "FamilyCategory.Furniture", "Mobiliario", FamilyDiscipline.Architectural, false),
        new(GenericModelsId, "FamilyCategory.GenericModels", "Modelos genéricos", FamilyDiscipline.Other, true),
        new("casework", "FamilyCategory.Casework", "Muebles de obra", FamilyDiscipline.Architectural, false)
    ];

    /// <summary>Returns the category with the given stable identifier, or null when it is not registered.</summary>
    public static FamilyCategory? Find(string id) => All.FirstOrDefault(category => category.Id == id);
}
