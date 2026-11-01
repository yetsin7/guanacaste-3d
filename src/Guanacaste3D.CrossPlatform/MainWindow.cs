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
    private readonly PlanWorkspace _workspace = new();
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
        create.Click += async (_, _) =>
        {
            if (await ConfirmReplaceAsync()) CreateProject();
        };
        var open = new Button { Content = "Abrir .g3d", Padding = new Thickness(14, 9) };
        open.Click += async (_, _) => await OpenProjectAsync();
        var save = new Button { Content = "Guardar", Padding = new Thickness(14, 9) };
        save.Click += async (_, _) => await SaveProjectAsync(false);
        var saveAs = new Button { Content = "Guardar como...", Padding = new Thickness(14, 9) };
        saveAs.Click += async (_, _) => await SaveProjectAsync(true);
        menu.Children.Add(create);
        menu.Children.Add(open);
        menu.Children.Add(save);
        menu.Children.Add(saveAs);
        var select = new Button { Content = "Seleccionar" };
        select.Click += (_, _) => _workspace.SetTool(PlanWorkspace.EditTool.Select);
        var wall = new Button { Content = "Muro" };
        wall.Click += (_, _) => _workspace.SetTool(PlanWorkspace.EditTool.Wall);
        var axis = new Button { Content = "Eje" };
        axis.Click += (_, _) => _workspace.SetTool(PlanWorkspace.EditTool.GridAxis);
        var fit = new Button { Content = "Ajustar vista" };
        fit.Click += (_, _) => _workspace.FitToModel();
        menu.Children.Add(select);
        menu.Children.Add(wall);
        menu.Children.Add(axis);
        menu.Children.Add(fit);
        _workspace.ModelChanged += () =>
        {
            _document = _workspace.Session?.Document;
            Refresh();
        };
        _workspace.Message += message => _status.Text = message;

        var layout = new StackPanel { Spacing = 18, Margin = new Thickness(28) };
        layout.Children.Add(new TextBlock { Text = "GUANACASTE 3D", FontSize = 13,
            FontWeight = FontWeight.Bold, Foreground = Brushes.SteelBlue });
        layout.Children.Add(menu);
        layout.Children.Add(_title);
        layout.Children.Add(_summary);
        _status.Text = "Crea un proyecto nuevo o abre un archivo Guanacaste 3D.";
        layout.Children.Add(_status);
        KeyDown += async (_, e) =>
        {
            var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if ((e.KeyModifiers & modifier) == 0) return;
            if (e.Key == Key.S) { await SaveProjectAsync((e.KeyModifiers & KeyModifiers.Shift) != 0); e.Handled = true; }
            else if (e.Key == Key.Z)
            {
                _workspace.Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y) { _workspace.Redo(); e.Handled = true; }
        };
        Closing += (_, e) =>
        {
            if (_closingAuthorized || !_workspace.HasUnsavedChanges) return;
            // Avalonia Closing is synchronous; prevent accidental loss and request explicit
            // save/discard before the user uses the window close control.
            e.Cancel = true;
            _ = CloseSafelyAsync();
        };
        var root = new DockPanel();
        DockPanel.SetDock(layout, Dock.Top);
        root.Children.Add(layout);
        root.Children.Add(_workspace);
        Content = root;
        Refresh();
    }

    private void CreateProject()
    {
        _document = BuildingDocument.Create("Proyecto sin título");
        _workspace.LoadDocument(_document);
        _path = null;
        _status.Text = "Proyecto nuevo creado. Puedes guardarlo como .g3d.";
        Refresh();
    }

    private async Task OpenProjectAsync()
    {
        if (!await ConfirmReplaceAsync()) return;
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
            _workspace.LoadDocument(_document);
            _path = localPath;
            _status.Text = "Proyecto abierto: " + localPath;
            Refresh();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Text = "No se pudo abrir el proyecto: " + ex.Message;
        }
    }

    private async Task SaveProjectAsync(bool saveAs)
    {
        if (_document is null)
        {
            _status.Text = "Primero crea o abre un proyecto.";
            return;
        }
        if (!saveAs && _path is not null)
        {
            WriteProject(_path);
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
        WriteProject(localPath);
    }

    private void WriteProject(string localPath)
    {
        if (_document is null) return;
        try
        {
            ProjectFile.Save(localPath, _workspace.Session?.Document ?? _document);
            _workspace.MarkSaved();
            _path = localPath;
            _status.Text = "Proyecto guardado: " + localPath;
            Refresh();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Text = "No se pudo guardar: " + ex.Message;
        }
    }

    private bool _closingAuthorized;

    private async Task CloseSafelyAsync()
    {
        if (_closingAuthorized || await ConfirmReplaceAsync())
        {
            _closingAuthorized = true;
            Close();
        }
    }

    private async Task<bool> ConfirmReplaceAsync()
    {
        if (!_workspace.HasUnsavedChanges) return true;
        var dialog = new Window
        {
            Title = "Cambios sin guardar",
            Width = 420,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var body = new StackPanel { Spacing = 18, Margin = new Thickness(20) };
        body.Children.Add(new TextBlock
        {
            Text = "Hay cambios sin guardar. ¿Quieres descartarlos?\nCancelar conservará el proyecto abierto.",
            TextWrapping = TextWrapping.Wrap
        });
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12
        };
        var discard = new Button { Content = "Descartar cambios", MinWidth = 130 };
        var cancel = new Button { Content = "Cancelar", MinWidth = 90 };
        discard.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        actions.Children.Add(discard);
        actions.Children.Add(cancel);
        body.Children.Add(actions);
        dialog.Content = body;
        return await dialog.ShowDialog<bool>(this);
    }

    private void Refresh()
    {
        _title.Text = _document?.Name ?? "Bienvenido a Guanacaste 3D";
        Title = "Guanacaste 3D — " + (_document?.Name ?? "Inicio") +
            (_workspace.HasUnsavedChanges ? " *" : "");
        _summary.Text = _document is null
            ? "Motor BIM compartido para Windows, Linux y macOS."
            : $"Muros: {_document.Walls.Count}  |  Niveles: {_document.Levels.Count}  |  " +
              $"Ejes: {_document.Grids.Count}  |  Puertas y ventanas: {_document.Openings.Count}";

    }
}
