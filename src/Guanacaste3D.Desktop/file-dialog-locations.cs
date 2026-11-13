using System.IO;

namespace Guanacaste3D.Desktop;

/// <summary>Resolves saved file locations without requiring unavailable folders to exist.</summary>
internal static class FileDialogLocations
{
    /// <summary>Returns the configured project folder when it is available, otherwise lets Windows choose.</summary>
    public static string ProjectFolder() => ExistingFolder(DesktopOptions.Load().DefaultProjectFolder);

    /// <summary>Returns the configured family folder when it is available, otherwise lets Windows choose.</summary>
    public static string FamilyFolder() => ExistingFolder(DesktopOptions.Load().DefaultFamilyFolder);

    /// <summary>Ignores stale, empty, or inaccessible locations so file dialogs remain usable.</summary>
    private static string ExistingFolder(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) ? folder : string.Empty;
}
