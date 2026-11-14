namespace Guanacaste3D.Core;

/// <summary>
/// The discipline a view is drawn for, as in the reference application. In a discipline view the elements of other
/// disciplines are drawn halftone, so an electrical plan reads as electrical work over a faint architectural base.
/// Architectural and Coordination views draw everything at full strength.
/// </summary>
public enum ViewDiscipline
{
    Architectural,
    Structural,
    Mechanical,
    Electrical,
    Plumbing,
    Coordination
}

/// <summary>Which discipline every model element belongs to, and which ones a discipline view draws halftone.</summary>
public static class ViewDisciplines
{
    /// <summary>The discipline of an element, or null when the id is not a model element (views, grids, annotation).</summary>
    public static ViewDiscipline? Of(BuildingDocument document, Guid id)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Walls.Any(wall => wall.Id == id) || document.Openings.Any(opening => opening.Id == id) ||
            document.Floors.Any(floor => floor.Id == id) || document.Rooms.Any(room => room.Id == id))
            return ViewDiscipline.Architectural;
        if (document.Columns.Any(column => column.Id == id)) return ViewDiscipline.Structural;
        if (document.ElectricalDevices.FirstOrDefault(device => device.Id == id) is { } device)
            return device.Kind == ElectricalDeviceKind.MiniSplit ? ViewDiscipline.Mechanical : ViewDiscipline.Electrical;
        if (document.ElectricalCircuits.Any(circuit => circuit.Id == id)) return ViewDiscipline.Electrical;
        if (document.PlumbingPoints.Any(point => point.Id == id) || document.PlumbingSystems.Any(system => system.Id == id))
            return ViewDiscipline.Plumbing;
        return document.StructuralElements.FirstOrDefault(element => element.Id == id) is { } element ? Of(element) : null;
    }

    /// <summary>The discipline of a placed element by its kind; bathroom fixtures belong to plumbing.</summary>
    public static ViewDiscipline Of(StructuralElement element) => element.Kind switch
    {
        StructuralElementKind.Beam or StructuralElementKind.Brace or StructuralElementKind.Truss or StructuralElementKind.WallFoundation
            or StructuralElementKind.IsolatedFoundation or StructuralElementKind.Connection or StructuralElementKind.Plate
            or StructuralElementKind.Bolt or StructuralElementKind.Weld or StructuralElementKind.Rebar or StructuralElementKind.FabricSheet
            => ViewDiscipline.Structural,
        StructuralElementKind.Duct or StructuralElementKind.DuctPlaceholder or StructuralElementKind.FlexDuct or StructuralElementKind.AirTerminal
            or StructuralElementKind.MechanicalControlDevice or StructuralElementKind.DuctStiffener or StructuralElementKind.FabricationPart
            => ViewDiscipline.Mechanical,
        StructuralElementKind.CableTray or StructuralElementKind.Conduit => ViewDiscipline.Electrical,
        StructuralElementKind.FlexPipe or StructuralElementKind.Gutter or StructuralElementKind.Downspout => ViewDiscipline.Plumbing,
        StructuralElementKind.Component when element.ComponentCategory == ResidentialComponentCategory.Bathroom => ViewDiscipline.Plumbing,
        StructuralElementKind.Component when element.ComponentCategory == ResidentialComponentCategory.Lighting => ViewDiscipline.Electrical,
        _ => ViewDiscipline.Architectural
    };

    /// <summary>Whether a view of <paramref name="view"/> discipline draws an element of <paramref name="element"/> halftone.</summary>
    public static bool IsHalftone(ViewDiscipline view, ViewDiscipline element) =>
        view is not (ViewDiscipline.Architectural or ViewDiscipline.Coordination) && element != view;
}

public sealed partial class DocumentSession
{
    /// <summary>
    /// Duplicates a floor plan without its annotation-free settings changing: same level, range, scale, crop, graphics and
    /// hidden elements, under the next free name "… copia N". Returns the new view's id; one undoable step.
    /// </summary>
    public Guid DuplicateView(Guid viewId)
    {
        var view = Document.Views.FirstOrDefault(candidate => candidate.Id == viewId)
            ?? throw new InvalidDataException("The view does not exist.");
        if (view.Kind != ProjectViewKind.Plan) throw new InvalidDataException("Only floor plans can be duplicated.");
        var number = 1;
        string Name() => $"{view.Name} copia {number}";
        while (Document.Views.Any(other => string.Equals(other.Name, Name(), StringComparison.CurrentCultureIgnoreCase))) number++;
        var id = Guid.NewGuid();
        var copy = view with { Id = id, Name = Name(), HiddenElementIds = [.. view.HiddenElementIds] };
        Commit("Transaction.DuplicateView", document => document.Views.Insert(document.Views.FindIndex(candidate => candidate.Id == viewId) + 1, copy));
        return id;
    }

    /// <summary>Sets the discipline a view is drawn for; one undoable step, nothing when it already has it.</summary>
    public void SetViewDiscipline(Guid viewId, ViewDiscipline discipline)
    {
        if (!Enum.IsDefined(discipline)) throw new InvalidDataException("The view discipline is not valid.");
        var index = Document.Views.FindIndex(candidate => candidate.Id == viewId);
        if (index < 0) throw new InvalidDataException("The view does not exist.");
        if (Document.Views[index].Discipline == discipline) return;
        Commit("Transaction.SetViewDiscipline", document =>
        {
            var at = document.Views.FindIndex(candidate => candidate.Id == viewId);
            document.Views[at] = document.Views[at] with { Discipline = discipline };
        });
    }
}
