namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>Commits persistent model-view graphics as one undoable operation, preserving all element assignments.</summary>
    public void SetViewGraphics(Guid viewId, ViewGraphicsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var view = Document.Views.FirstOrDefault(candidate => candidate.Id == viewId);
        if (view is null || Equals(view.Graphics ?? new ViewGraphicsSettings(), settings)) return;
        Commit("Transaction.SetViewGraphics", document =>
        {
            var index = document.Views.FindIndex(candidate => candidate.Id == viewId);
            document.Views[index] = document.Views[index] with { Graphics = settings };
        });
    }
}
