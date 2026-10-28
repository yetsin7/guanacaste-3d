using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Hides, isolates and restores elements for the view on screen. Each view keeps its own choice in the project, so
/// hiding something in a plan does not hide it in the 3D view, and the other way round.
/// </summary>
public partial class MainWindow
{
    /// <summary>Elements selected together with Ctrl+A in the active view; null when no such selection is active.</summary>
    private HashSet<Guid>? _viewSelection;

    /// <summary>The project's 3D view, or null when the project does not have one.</summary>
    private ProjectView? Model3DView() => _session?.Document.Views.FirstOrDefault(view => view.Id == BuiltInProjectViews.Model3DId);

    /// <summary>The view the user is looking at: the plan of the active level, the 3D or render view, or an elevation or section.</summary>
    private ProjectView? ActiveProjectView() => ViewTabs.SelectedIndex switch
    {
        0 => _session.Document.Views.FirstOrDefault(view => view.Kind == ProjectViewKind.Plan && view.LevelId == _activeLevelId),
        1 or RenderTabIndex => Model3DView(),
        4 => ProjectViewById(_elevationViewId),
        5 => ProjectViewById(_sectionViewId),
        _ => null
    };

    /// <summary>The ids to act on: the Ctrl+A selection when there is one, otherwise the single selected element.</summary>
    private List<Guid> SelectedIds()
    {
        if (_viewSelection is { Count: > 0 } set) return set.ToList();
        return CurrentSelection() is { } selection ? [selection.Id] : [];
    }

    /// <summary>Selects every wall, floor, column and structural element the active view shows (only the level in a plan).</summary>
    private void SelectAllInView()
    {
        if (ActiveProjectView() is not { } view) return;
        var document = ViewVisibility.Apply(_session.Document, view);
        bool OnLevel(Guid levelId) => view.Kind != ProjectViewKind.Plan || levelId == _activeLevelId;
        _viewSelection = document.Walls.Where(wall => OnLevel(wall.LevelId)).Select(wall => wall.Id)
            .Concat(document.Floors.Where(floor => OnLevel(floor.LevelId)).Select(floor => floor.Id))
            .Concat(document.Columns.Where(column => OnLevel(column.LevelId)).Select(column => column.Id))
            .Concat(document.StructuralElements.Where(element => OnLevel(element.LevelId)).Select(element => element.Id))
            .ToHashSet();
        StatusText.Text = string.Format(GetText("ViewSelection.Count"), _viewSelection.Count);
    }

    /// <summary>Hides the selected elements in the active view only.</summary>
    private void HideSelectionInView()
    {
        if (ActiveProjectView() is not { } view || SelectedIds() is not { Count: > 0 } ids) return;
        _session.SetViewVisibility(view.Id, [.. view.HiddenElementIds, .. ids], view.IsolatedElementIds);
        _viewSelection = null;
        ApplyViewVisibility();
    }

    /// <summary>Shows only the selected elements in the active view.</summary>
    private void IsolateSelectionInView()
    {
        if (ActiveProjectView() is not { } view || SelectedIds() is not { Count: > 0 } ids) return;
        _session.SetViewVisibility(view.Id, view.HiddenElementIds, ids);
        _viewSelection = null;
        ApplyViewVisibility();
    }

    /// <summary>Clears the hiding and isolation of the active view only.</summary>
    private void ShowAllInView()
    {
        if (ActiveProjectView() is not { } view) return;
        _session.SetViewVisibility(view.Id, [], []);
        ApplyViewVisibility();
    }

    /// <summary>Asks for a name and saves the active view's hiding and isolation as a template.</summary>
    private void SaveViewTemplateFromView()
    {
        if (ActiveProjectView() is not { } view) return;
        var name = PromptWindow.Ask(this, GetText("ViewTemplate.SaveTitle"), GetText("ViewTemplate.DefaultName"));
        if (_session.CreateViewTemplate(view, name ?? string.Empty) is null) return;
        StatusText.Text = GetText("ViewTemplate.Saved");
    }

    /// <summary>Adds one item per saved template to a menu; each applies the template to the given views.</summary>
    private void AddTemplateItems(ItemsControl menu, IReadOnlyCollection<Guid> viewIds)
    {
        if (_session.Document.ViewTemplates.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = GetText("ViewTemplate.None"), IsEnabled = false });
            return;
        }
        foreach (var template in _session.Document.ViewTemplates)
        {
            var templateId = template.Id;
            var item = new MenuItem { Header = template.Name };
            item.Click += (_, _) =>
            {
                _session.ApplyViewTemplate(templateId, viewIds);
                ApplyViewVisibility();
            };
            menu.Items.Add(item);
        }
    }

    /// <summary>Menu of the project browser's view list: applies a template to every view at once.</summary>
    private ContextMenu CreateAllViewsMenu()
    {
        var apply = new MenuItem { Header = GetText("ViewTemplate.ApplyAll") };
        AddTemplateItems(apply, _session.Document.Views.Select(view => view.Id).ToList());
        return new ContextMenu { Items = { apply } };
    }

    /// <summary>Rebuilds the scene and the views so they show the new visibility.</summary>
    private void ApplyViewVisibility()
    {
        _picker = null;
        MarkSceneDirty();
        RefreshAll();
    }
}
