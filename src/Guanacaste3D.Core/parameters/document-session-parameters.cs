namespace Guanacaste3D.Core;

/// <summary>A requested value change for one element, used by single and bulk edits.</summary>
public sealed record ParameterEdit(Guid ElementId, Guid DefinitionId, double? Number, string? Text);

/// <summary>Project parameter operations; each public method is one undoable transaction.</summary>
public sealed partial class DocumentSession
{
    /// <summary>Creates a project parameter bound to one or more categories.</summary>
    public Guid AddParameter(string name, ParameterSpec spec, ParameterBinding binding,
        IEnumerable<ElementCategory> categories, string group = "", string description = "")
    {
        var id = Guid.NewGuid();
        var definition = new ParameterDefinition(id, name.Trim(), spec, binding,
            categories.Distinct().ToList(), group.Trim()) { Description = description.Trim() };
        Commit("Transaction.AddParameter", document => document.ParameterDefinitions.Add(definition));
        return id;
    }

    /// <summary>
    /// Renames, regroups, or rebinds a parameter's categories. Spec and binding
    /// stay fixed so stored values never change meaning; values on categories
    /// removed from the binding are dropped by regeneration in the same step.
    /// </summary>
    public void UpdateParameter(Guid id, string name, IEnumerable<ElementCategory> categories,
        string group = "", string description = "")
    {
        if (!Document.ParameterDefinitions.Any(definition => definition.Id == id))
            throw new InvalidDataException("The project parameter does not exist.");
        var categoryList = categories.Distinct().ToList();
        Commit("Transaction.UpdateParameter", document =>
        {
            var index = document.ParameterDefinitions.FindIndex(definition => definition.Id == id);
            document.ParameterDefinitions[index] = document.ParameterDefinitions[index] with
            {
                Name = name.Trim(),
                Categories = categoryList,
                Group = group.Trim(),
                Description = description.Trim()
            };
        });
    }

    /// <summary>Deletes a parameter definition together with all of its stored values.</summary>
    public void RemoveParameter(Guid id)
    {
        if (!Document.ParameterDefinitions.Any(definition => definition.Id == id)) return;
        Commit("Transaction.RemoveParameter", document =>
            document.ParameterDefinitions.RemoveAll(definition => definition.Id == id));
    }

    /// <summary>Sets or clears one element's value; type-bound values are written to the element's type.</summary>
    public void SetParameterValue(Guid elementId, Guid definitionId, double? number, string? text) =>
        SetParameterValues([new ParameterEdit(elementId, definitionId, number, text)]);

    /// <summary>
    /// Applies several value edits as one transaction, as when pasting into a
    /// schedule column. Null number and text clear the value. Any invalid edit
    /// rejects the whole batch without touching the document.
    /// </summary>
    public void SetParameterValues(IReadOnlyCollection<ParameterEdit> edits)
    {
        if (edits.Count == 0) return;
        var elements = ElementCatalog.Index(Document);
        var resolved = edits.Select(edit =>
        {
            var definition = Document.ParameterDefinitions.FirstOrDefault(candidate => candidate.Id == edit.DefinitionId)
                ?? throw new InvalidDataException("The project parameter does not exist.");
            var owner = elements.TryGetValue(edit.ElementId, out var element)
                ? ParameterRules.ResolveOwner(definition, element)
                : null;
            if (owner is not Guid ownerId)
                throw new InvalidDataException("The parameter is not bound to the selected element.");
            return (Edit: edit, OwnerId: ownerId);
        }).ToArray();
        Commit(edits.Count == 1 ? "Transaction.SetParameterValue" : "Transaction.SetParameterValues", document =>
        {
            foreach (var (edit, ownerId) in resolved)
            {
                document.ParameterValues.RemoveAll(value =>
                    value.DefinitionId == edit.DefinitionId && value.OwnerId == ownerId);
                if (edit.Number is not null || edit.Text is not null)
                    document.ParameterValues.Add(new ParameterValue(edit.DefinitionId, ownerId, edit.Number, edit.Text));
            }
        });
    }
}
