using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core.Interop;

namespace Guanacaste3D.Desktop;

/// <summary>
/// The .NET render workspace is hosted inside Guanacaste 3D. The external handoff remains
/// available as a separate, explicit workflow, without intercepting the integrated Render tab.
/// </summary>
public partial class MainWindow
{
    /// <summary>Set by <see cref="EnterRenderWorkspace"/>: this window is the render host and keeps its Render view.</summary>
    private bool _hostsRenderWorkspace;

    /// <summary>Where the render snapshot of the current model goes: beside the saved model, or in the user's
    /// Guanacaste Render folder while the model has never been saved.</summary>
    private string RenderHandoffPath()
    {
        var name = string.Concat(_session.Document.Name.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(name)) name = "Proyecto";
        var folder = _filePath is not null
            ? Path.GetDirectoryName(Path.GetFullPath(_filePath))!
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GuanacasteRender", "Desde Guanacaste 3D");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, name + "-render.g3d");
    }

    /// <summary>Sends the model to Guanacaste Render, or explains how to install it.</summary>
    private void OpenInGuanacasteRender()
    {
        var title = GetText("RenderHandoff.Title");
        var executable = RenderApplication.FindExecutable();
        if (executable is null)
        {
            MessageBox.Show(this, GetText("RenderHandoff.NotInstalled"), title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var exportPath = RenderHandoffPath();
        if (!WriteRenderExport(exportPath)) return;
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false };
            start.ArgumentList.Add(exportPath);
            System.Diagnostics.Process.Start(start)?.Dispose();
            StatusText.Text = string.Format(GetText("RenderHandoff.Opened"), Path.GetFileName(exportPath));
            if (_filePath is null)
                MessageBox.Show(this, GetText("RenderHandoff.SaveForLiveLink"), title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            MessageBox.Show(this, GetText("RenderExport.LaunchFailed"), title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Legacy ribbon handler now opens the integrated render workspace.</summary>
    private void OpenInGuanacasteRender_Click(object sender, RoutedEventArgs e) => ShowRender_Click(sender, e);
}
