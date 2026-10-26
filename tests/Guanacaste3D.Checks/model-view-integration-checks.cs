/// <summary>Runs cross-view model, family, architectural, and rendering checks after foundational checks.</summary>
internal static class ModelViewIntegrationChecks
{
    /// <summary>Preserves the integration suite order while keeping the executable entry point focused.</summary>
    public static void Run(Action<bool, string> check)
    {
        InPlaceFamilyChecks.Run(check);
        FamilyPlanGeometryChecks.Run(check);
        WallAppearanceChecks.Run(check);
        WallJoinChecks.Run(check);
        PlanDimensionChecks.Run(check);
        DoorPlanSymbolChecks.Run(check);
        StructuralChecks.Run(check);
        FamilyTemplateChecks.Run(check);
        ArchitectureChecks.Run(check);
        ResidentialComponentChecks.Run(check);
        RenderInfrastructureChecks.Run(check);
        RenderRegressionChecks.Run(check);
        RenderEngineChecks.Run(check);
        ImportChecks.Run(check);
        NavigationChecks.Run(check);
    }
}
