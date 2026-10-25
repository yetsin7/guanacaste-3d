using System.Globalization;
using System.Windows;
using Guanacaste3D.Desktop.Controls;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Routes shared ribbon commands to implemented workflows while keeping planned
    /// tools visible with an explicit availability message.
    /// </summary>
    private void ProfessionalRibbon_CommandInvoked(object? sender, ProfessionalRibbonCommandEventArgs e)
    {
        var eventArguments = new RoutedEventArgs();
        switch (e.CommandId)
        {
            case "Modify": Select_Click(this, eventArguments); return;
            case "Wall": Wall_Click(this, eventArguments); return;
            case "Floor": Floor_Click(this, eventArguments); return;
            case "Level": AddLevel_Click(this, eventArguments); return;
            case "Grid": ToggleGrid_Click(this, eventArguments); return;
            case "Viewer": ShowModel_Click(this, eventArguments); return;
            case "Move": Move_Click(this, eventArguments); return;
            case "CopyElement": Duplicate_Click(this, eventArguments); return;
            case "Rotate": Rotate_Click(this, eventArguments); return;
            case "Delete": Delete_Click(this, eventArguments); return;
            case "Plan": ShowPlan_Click(this, eventArguments); return;
            case "Model3D": ShowModel_Click(this, eventArguments); return;
            case "Elevation": ShowElevation_Click(this, eventArguments); return;
            case "Section": ShowSection_Click(this, eventArguments); return;
            case "Schedule": ShowSchedule_Click(this, eventArguments); return;
            case "Sheet": AddSheet_Click(this, eventArguments); return;
            case "Render": StartRender_Click(this, eventArguments); return;
            case "Fit": FitPlan_Click(this, eventArguments); return;
            case "ProjectParameters": ProjectParameters_Click(this, eventArguments); return;
            case "Levels": Levels_Click(this, eventArguments); return;
            case "Schedules": Schedules_Click(this, eventArguments); return;
            case "OpeningTypes": OpeningTypes_Click(this, eventArguments); return;
            case "About": About_Click(this, eventArguments); return;
            default: ShowPlannedRibbonCommand(e); return;
        }
    }

    /// <summary>
    /// Reports that a visible workspace command is not yet backed by a BIM operation.
    /// </summary>
    private void ShowPlannedRibbonCommand(ProfessionalRibbonCommandEventArgs command)
    {
        StatusText.Text = string.Format(
            CultureInfo.CurrentCulture,
            GetText("Status.RibbonCommandPlanned"),
            GetWorkspaceLabel(command.Workspace),
            command.DisplayName);
    }

    /// <summary>
    /// Resolves the localized tab label used in planned-command status feedback.
    /// </summary>
    private string GetWorkspaceLabel(RibbonWorkspace workspace) => GetText(workspace switch
    {
        RibbonWorkspace.Structure => "Ribbon.Structure",
        RibbonWorkspace.Concrete => "Ribbon.Concrete",
        RibbonWorkspace.Steel => "Ribbon.Steel",
        RibbonWorkspace.Systems => "Ribbon.Systems",
        RibbonWorkspace.Insert => "Ribbon.Insert",
        RibbonWorkspace.Annotate => "Ribbon.Annotate",
        RibbonWorkspace.Analyze => "Ribbon.Analyze",
        RibbonWorkspace.MassAndSite => "Ribbon.MassAndSite",
        RibbonWorkspace.Collaborate => "Ribbon.Collaborate",
        RibbonWorkspace.View => "Ribbon.View",
        RibbonWorkspace.Manage => "Ribbon.Manage",
        RibbonWorkspace.AddIns => "Ribbon.AddIns",
        RibbonWorkspace.Modify => "Ribbon.Modify",
        _ => "App.Name"
    });
}
