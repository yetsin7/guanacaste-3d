using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Duplicate view and view discipline, the basis of every engineering drawing: the electrical, plumbing, structural
/// and mechanical plans are copies of the architectural plan whose discipline draws the rest halftone.
/// </summary>
public partial class MainWindow
{
    /// <summary>How strongly the elements of other disciplines are drawn in a discipline view.</summary>
    private const double DisciplineHalftoneOpacity = 0.28;

    /// <summary>Draws the plan of the view in use, then dims what does not belong to its discipline.</summary>
    private void RenderPlan()
    {
        RenderPlanContent();
        ApplyDisciplineHalftone();
    }

    private void ApplyDisciplineHalftone()
    {
        if (PlanCanvas is null || _session is null || CurrentPlanTagView() is not { } view) return;
        if (view.Discipline is ViewDiscipline.Architectural or ViewDiscipline.Coordination) return;
        var cache = new Dictionary<Guid, ViewDiscipline?>();
        foreach (var child in PlanCanvas.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not Guid id) continue;
            if (!cache.TryGetValue(id, out var discipline)) cache[id] = discipline = ViewDisciplines.Of(_session.Document, id);
            if (discipline is { } own && ViewDisciplines.IsHalftone(view.Discipline, own)) child.Opacity *= DisciplineHalftoneOpacity;
        }
    }

    /// <summary>Whether the plan in use draws a discipline's analysis overlays: in that discipline's views and in coordination.</summary>
    private bool ShowsEngineeringOverlay(ViewDiscipline discipline) =>
        CurrentPlanTagView()?.Discipline is { } current && (current == discipline || current == ViewDiscipline.Coordination);

    /// <summary>Duplicates the plan in use, opens the copy and selects it in the browser so F2 renames it.</summary>
    private void DuplicateCurrentView()
    {
        if (CurrentPlanTagView() is not { } view)
        {
            StatusText.Text = GetText("Status.DuplicatePlanOnly");
            return;
        }
        try
        {
            var copy = _session.DuplicateView(view.Id);
            RefreshAll();
            SelectProjectView(copy);
            StatusText.Text = FormatText("Status.ViewDuplicated", _session.Document.Views.Single(candidate => candidate.Id == copy).Name);
        }
        catch (InvalidDataException exception) { StatusText.Text = LocalizedErrors.Translate(exception.Message); }
    }

    /// <summary>Shows the disciplines for the plan in use, with the current one checked.</summary>
    private void ShowViewDisciplineMenu()
    {
        if (CurrentPlanTagView() is not { } view)
        {
            StatusText.Text = GetText("Status.DuplicatePlanOnly");
            return;
        }
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        foreach (var discipline in Enum.GetValues<ViewDiscipline>())
        {
            var item = new MenuItem { Header = GetText("Discipline." + discipline), IsCheckable = true, IsChecked = view.Discipline == discipline, Tag = discipline };
            item.Click += (_, _) =>
            {
                _session.SetViewDiscipline(view.Id, discipline);
                RefreshAll();
                StatusText.Text = FormatText("Status.ViewDisciplineSet", view.Name, GetText("Discipline." + discipline));
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}
