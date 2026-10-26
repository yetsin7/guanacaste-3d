using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

/// <summary>Checks the built-in home component catalog, geometry, plan footprint, and native persistence.</summary>
static class ResidentialComponentChecks
{
    /// <summary>Runs all residential component contract checks.</summary>
    public static void Run(Action<bool, string> check)
    {
        var presets = ResidentialComponentCatalog.Presets;
        check(presets.Count >= 35 && presets.Select(preset => preset.Name).Distinct().Count() == presets.Count &&
              presets.All(preset => preset.Kind == StructuralElementKind.Component && preset.Depth > 0 &&
                  preset.ComponentCategory != ResidentialComponentCategory.None &&
                  !string.IsNullOrWhiteSpace(preset.LocalizationKey)),
            "The built-in residential catalog has 35+ distinct, localizable, ready-to-place component families");
        check(Enum.GetValues<ResidentialComponentCategory>().Where(category => category != ResidentialComponentCategory.None)
                .All(category => presets.Any(preset => preset.ComponentCategory == category)),
            "The residential catalog covers every home category");

        var session = new DocumentSession(BuildingDocument.Create("Residential components"));
        var level = session.Document.Levels[0].Id;
        for (var index = 0; index < presets.Count; index++)
        {
            var preset = presets[index];
            var position = new Point2(index % 8 * 3, index / 8 * 4);
            session.AddElementFromPreset(level, preset, position, position);
        }
        session.Document.Validate();
        var elements = session.Document.StructuralElements;
        check(elements.Count == presets.Count && elements.All(element => ElementGeometry.Parts(element).Any()),
            "Every residential family produces coordinated solid geometry");
        check(elements.Zip(presets).All(pair => HasExpectedFootprint(pair.First, pair.Second)),
            "Residential family footprints preserve independent width and depth");

        var mesh = BuildingMesher.Build(session.Document);
        check(mesh.TriangleCount > presets.Count * 20,
            "Residential families participate in the shared 3D mesh with detailed silhouettes");
        var reloaded = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(session.Document));
        check(reloaded.StructuralElements.Select(element => (element.Depth, element.ComponentShape, element.ComponentCategory))
                .SequenceEqual(elements.Select(element => (element.Depth, element.ComponentShape, element.ComponentCategory))),
            "Residential family type and dimensions persist in native project documents");
    }

    /// <summary>Tests the independent width and depth of a placed component's rectangular plan footprint.</summary>
    private static bool HasExpectedFootprint(StructuralElement element, ElementPreset preset) =>
        Math.Abs((element.Footprint.Max(point => point.X) - element.Footprint.Min(point => point.X)) - preset.Width) < 1e-9 &&
        Math.Abs((element.Footprint.Max(point => point.Y) - element.Footprint.Min(point => point.Y)) - preset.Depth) < 1e-9;
}
