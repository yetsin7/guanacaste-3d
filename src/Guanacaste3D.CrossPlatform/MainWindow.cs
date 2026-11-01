using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Guanacaste3D.Core;

namespace Guanacaste3D.CrossPlatform;

/// <summary>
/// First cross-platform BIM workspace. Uses the same native project document and domain model as Windows WPF.
/// Rendering and editing parity will be added in later migration stages; this UI does not pretend to provide them.
/// </summary>
public sealed class MainWindow : Window
{
    private BuildingDocument? _document;
    private string? _path;
    private readonly TextBlock _title = new() { FontSize = 24, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ListBox _levels = new() { Height = 145 };
    private readonly ListBox _axes = new() { Height = 145 };

    public MainWindow()
    {
        Title = "Guanacaste 3D — Multiplataforma";
        MinWidth = 720;
        MinHeight = 520;
        Width = 1050;
        Height = 710;

        var menu = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var create = new Button { Content = "Nuevo proyecto", Padding = new Thickness(14, 9) };
        create.Click += (_, _) => CreateProject();
        var open = new Button { Content = "Abrir .g3d", Padding = new Thickness(14, 9) };
        open.Click += async (_, _) => await OpenProjectAsync();
        var save = new Button { Content = "Guardar como...", Padding = new Thickness(14, 9) };
        save.Click += async (_, _) => await SaveProjectAsync();
        menu.Children.Add(create);
        menu.Children.Add(open);
        menu.Children.Add(save);

        var layout = new StackPanel { Spacing = 18, Margin = new Thickness(28) };
        layout.Children.Add(new TextBlock { Text = "GUANACASTE 3D", FontSize = 13,
            FontWeight = FontWeight.Bold, Foreground = Brushes.SteelBlue });
        layout.Children.Add(menu);
        layout.Children.Add(_title);
        layout.Children.Add(_summary);
        layout.Children.Add(new TextBlock { Text = "Niveles del proyecto", FontWeight = FontWeight.SemiBold });
        layout.Children.Add(_levels);
        layout.Children.Add(new TextBlock { Text = "Ejes del proyecto", FontWeight = FontWeight.SemiBold });
        layout.Children.Add(_axes);
        layout.Children.Add(new TextBlock
        {
            Text = "Vista preliminar multiplataforma: lectura y guardado nativos compartidos con el motor BIM. " +
                   "La edición geométrica y el render 3D se incorporarán durante la migración de la interfaz.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });
        _status.Text = "Crea un proyecto nuevo o abre un archivo Guanacaste 3D.";
        layout.Children.Add(_status);
        Content = new ScrollViewer { Content = layout };
        Refresh();
    }

    private void CreateProject()
    {
        _document = BuildingDocument.Create("Proyecto sin título");
        _path = null;
        _status.Text = "Proyecto nuevo creado. Puedes guardarlo como .g3d.";
        Refresh();
    }

    private async Task OpenProjectAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir proyecto de Guanacaste 3D",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Proyecto Guanacaste 3D")
            {
                Patterns = ["*.g3d", "*.g3", "*.g3d.json"]
            }]
        });
        if (files.Count == 0) return;
        var localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            _status.Text = "Este proveedor de archivos no ofrece una ruta local. Selecciona un archivo local.";
            return;
        }
        try
        {
            var loaded = ProjectFile.Load(localPath);
            loaded.Validate();
            _document = loaded;
            _path = localPath;
            _status.Text = "Proyecto abierto: " + localPath;
            Refresh();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Text = "No se pudo abrir el proyecto: " + ex.Message;
        }
    }

    private async Task SaveProjectAsync()
    {
        if (_document is null)
        {
            _status.Text = "Primero crea o abre un proyecto.";
            return;
        }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar proyecto de Guanacaste 3D",
            SuggestedFileName = _path is null ? "Proyecto.g3d" : System.IO.Path.GetFileName(_path),
            DefaultExtension = "g3d",
            FileTypeChoices = [new FilePickerFileType("Proyecto Guanacaste 3D") { Patterns = ["*.g3d"] }]
        });
        if (file is null) return;
        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            _status.Text = "Selecciona una ubicación de archivos local para guardar.";
            return;
        }
        try
        {
            ProjectFile.Save(localPath, _document);
            _path = localPath;
            _status.Text = "Proyecto guardado: " + localPath;
            Refresh();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Text = "No se pudo guardar: " + ex.Message;
        }
    }

    private void Refresh()
    {
        _title.Text = _document?.Name ?? "Bienvenido a Guanacaste 3D";
        _summary.Text = _document is null
            ? "Motor BIM compartido para Windows, Linux y macOS."
            : $"Muros: {_document.Walls.Count}  |  Niveles: {_document.Levels.Count}  |  " +
              $"Ejes: {_document.Grids.Count}  |  Puertas y ventanas: {_document.Openings.Count}";
        _levels.ItemsSource = _document?.Levels.Select(level =>
            $"{level.Name} — {level.Elevation:0.###} m").ToArray() ?? [];
        _axes.ItemsSource = _document?.Grids.Select(axis =>
            $"{axis.Name} — ({axis.Start.X:0.##}, {axis.Start.Y:0.##}) a " +
            $"({axis.End.X:0.##}, {axis.End.Y:0.##})").ToArray() ?? [];
    }
}
