using System.IO;
using System.Windows;
using System.Windows.Input;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Opens the family editor and places what it loads: a family type made in the program goes into the project and the
/// plan arms a placement tool, so each click puts one where it is wanted, as the reference application's Component tool
/// does. Esc ends placement.
/// </summary>
public partial class MainWindow
{
    /// <summary>The loaded family the next plan click places; null when no family is being placed.</summary>
    private Guid? _placingFamilyId;

    private FamilyEditorWindow? _familyEditor;

    /// <summary>Opens the family editor with a new furniture family, or brings the open one to the front.</summary>
    private void OpenFamilyEditor()
    {
        if (_familyEditor is { IsLoaded: true })
        {
            _familyEditor.Activate();
            return;
        }
        _familyEditor = new FamilyEditorWindow(null, LoadFamilyAndPlace) { Owner = this };
        _familyEditor.Closed += (_, _) => _familyEditor = null;
        _familyEditor.Show();
    }

    /// <summary>Loads one type of an edited family into the project and arms its placement in the plan.</summary>
    private void LoadFamilyAndPlace(FamilyDocument family, Guid typeId)
    {
        var baked = FamilyAuthoring.Bake(family, typeId);
        var id = _session.LoadComponentFamily(baked);
        SetWallTool(false);
        _placingFamilyId = id;
        _componentRotation = 0;
        ViewTabs.SelectedIndex = 0;
        RefreshAll();
        StatusText.Text = FormatText("Status.PlaceFamily", baked.Name);
        Activate();
        Keyboard.Focus(PlanCanvas);
    }

    /// <summary>Places the armed family at the clicked point; the tool stays armed for the next one.</summary>
    private bool TryPlaceFamilyClick(MouseButtonEventArgs e)
    {
        if (_placingFamilyId is not Guid id) return false;
        try
        {
            _session.PlaceComponentFamily(_activeLevelId, id, CanvasToWorld(PlanPosition(e)), _componentRotation);
            RefreshAll();
            StatusText.Text = FormatText("Status.PlaceFamily", _session.Document.ComponentFamilies.FirstOrDefault(family => family.Id == id)?.Name ?? "");
        }
        catch (InvalidDataException exception) { StatusText.Text = LocalizedErrors.Translate(exception.Message); }
        return true;
    }
}
