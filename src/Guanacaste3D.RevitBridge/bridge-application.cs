using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace Guanacaste3D.RevitBridge;

/// <summary>What Guanacaste 3D asks Revit to do: open <see cref="Input"/>, write <see cref="Output"/>, report in <see cref="Status"/>.</summary>
internal sealed record ExportJob(string Input, string Output, string Status);

/// <summary>
/// The add-in. Started by Guanacaste 3D with <c>G3D_REVIT_JOB</c> pointing at a job file, it opens the project as soon
/// as Revit is ready, writes the exchange file, closes Revit and answers every dialog on the way so nothing waits for
/// a person. Started by a Revit user, it adds a "Guanacaste 3D" ribbon tab with an export button.
/// </summary>
public sealed class BridgeApplication : IExternalApplication
{
    private ExportJob? _job;
    private bool _exitRequested;

    public Result OnStartup(UIControlledApplication application)
    {
        var jobPath = Environment.GetEnvironmentVariable("G3D_REVIT_JOB");
        if (!string.IsNullOrWhiteSpace(jobPath) && File.Exists(jobPath))
        {
            _job = JsonSerializer.Deserialize<ExportJob>(File.ReadAllText(jobPath));
            application.ControlledApplication.ApplicationInitialized += OnInitialized;
            application.ControlledApplication.FailuresProcessing += OnFailures;
            application.DialogBoxShowing += OnDialog;
            return Result.Succeeded;
        }
        const string tab = "Guanacaste 3D";
        try { application.CreateRibbonTab(tab); }
        catch (Autodesk.Revit.Exceptions.ArgumentException) { /* the tab exists already */ }
        var panel = application.CreateRibbonPanel(tab, "Exportar");
        panel.AddItem(new PushButtonData("G3DExport", "Exportar a\nGuanacaste 3D",
            typeof(BridgeApplication).Assembly.Location, typeof(ExportCommand).FullName)
        {
            ToolTip = "Guarda el modelo como archivo de intercambio .g3dx que Guanacaste 3D abre como proyecto editable."
        });
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private void OnInitialized(object? sender, ApplicationInitializedEventArgs e)
    {
        if (_job is null || sender is not Application application) return;
        var clock = Stopwatch.StartNew();
        Document? document = null;
        try
        {
            Report(new { state = "opening" });
            var path = ModelPathUtils.ConvertUserVisiblePathToModelPath(_job.Input);
            var options = new OpenOptions { Audit = false };
            if (BasicFileInfo.Extract(_job.Input).IsWorkshared)
            {
                options.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;
                options.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets));
            }
            document = application.OpenDocumentFile(path, options);
            Report(new { state = "exporting" });
            var exporter = new ExchangeExporter(document);
            exporter.Export(_job.Output, fraction => Report(new { state = "exporting", progress = fraction }));
            Report(new
            {
                state = "done", ok = true, elements = exporter.ElementCount, triangles = exporter.TriangleCount,
                seconds = clock.Elapsed.TotalSeconds
            });
        }
        catch (Exception ex)
        {
            Report(new { state = "done", ok = false, error = ex.GetType().Name + ": " + ex.Message });
        }
        finally
        {
            try { document?.Close(false); }
            catch (Exception) { /* Revit is closing anyway */ }
            _exitRequested = true;
        }
    }

    /// <summary>Writes the job status atomically so Guanacaste never reads half a file.</summary>
    private void Report(object status)
    {
        if (_job is null) return;
        var temporary = _job.Status + ".partial";
        File.WriteAllText(temporary, JsonSerializer.Serialize(status));
        File.Move(temporary, _job.Status, overwrite: true);
    }

    /// <summary>Upgrade notices, missing links and similar dialogs are accepted with their default answer.</summary>
    private void OnDialog(object? sender, DialogBoxShowingEventArgs e)
    {
        e.OverrideResult(e is TaskDialogShowingEventArgs ? (int)TaskDialogResult.Close : 1);
        if (_exitRequested && sender is UIApplication application) RequestExit(application);
    }

    /// <summary>Warnings raised while opening are dismissed; they do not change the geometry being read.</summary>
    private static void OnFailures(object? sender, FailuresProcessingEventArgs e)
    {
        var accessor = e.GetFailuresAccessor();
        accessor.DeleteAllWarnings();
        e.SetProcessingResult(FailureProcessingResult.Continue);
    }

    private static void RequestExit(UIApplication application)
    {
        try { application.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit)); }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException) { /* already exiting */ }
    }
}

/// <summary>The ribbon button: exports the active project to a .g3dx file the user chooses.</summary>
[Transaction(TransactionMode.ReadOnly)]
public sealed class ExportCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var document = commandData.Application.ActiveUIDocument?.Document;
        if (document is null || document.IsFamilyDocument)
        {
            message = "Abra un proyecto de Revit para exportarlo.";
            return Result.Failed;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Intercambio Guanacaste 3D (*.g3dx)|*.g3dx",
            FileName = Path.GetFileNameWithoutExtension(document.PathName is { Length: > 0 } path ? path : document.Title) + ".g3dx"
        };
        if (dialog.ShowDialog() != true) return Result.Cancelled;
        var exporter = new ExchangeExporter(document);
        exporter.Export(dialog.FileName);
        TaskDialog.Show("Guanacaste 3D",
            $"Se exportaron {exporter.ElementCount:N0} elementos ({exporter.TriangleCount:N0} triángulos).\n" +
            "Abra el archivo .g3dx en Guanacaste 3D para convertirlo en un proyecto editable.");
        return Result.Succeeded;
    }
}
