using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>Render-only AI handoff commands. No provider is invoked from the application.</summary>
public partial class RenderPanel
{
    private string? _projectFilePath;

    /// <summary>The sidecar render vault follows the currently opened or saved project.</summary>
    internal void SetProjectFilePath(string? projectPath) => _projectFilePath = projectPath;

    /// <summary>A stable copy of the actual rendered pixels, not a screenshot of ribbons or overlays.</summary>
    private BitmapSource? VisibleImageSnapshot()
    {
        if (PreviewImage.Source is not BitmapSource image) return null;
        var copy = image.CloneCurrentValue();
        copy.Freeze();
        return copy;
    }

    private void CopyCurrentView_Click(object sender, RoutedEventArgs e)
    {
        var image = VisibleImageSnapshot();
        if (image is null)
        {
            StatusText.Text = "Todavía no hay imagen: activa la vista previa o genera un render.";
            return;
        }
        try
        {
            Clipboard.SetImage(image);
            StatusText.Text = "Vista actual copiada. Pégala como imagen de referencia en tu IA.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            StatusText.Text = "No se pudo copiar: el portapapeles está ocupado.";
        }
    }

    private void AiRender_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = VisibleImageSnapshot();
        if (snapshot is null)
        {
            StatusText.Text = "Esperando la imagen de la escena. Activa la vista previa antes de usar Render con IA.";
            return;
        }
        var dialog = new AiRenderWindow(_projectFilePath, _source?.ProjectName ?? "Proyecto", snapshot)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }
}
