namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>Sets a plan's drawing scale as an undoable change without modifying model geometry.</summary>
    public void UpdatePlanDrawingScale(Guid viewId, int denominator)
    {
        if (denominator is < 1 or > 10000) throw new InvalidDataException("The plan drawing scale is invalid.");
        var view = Document.Views.FirstOrDefault(item => item.Id == viewId);
        if (view is not { Kind: ProjectViewKind.Plan }) throw new InvalidDataException("The view does not exist.");
        if (view.DrawingScale == denominator) return;
        Commit("Transaction.UpdatePlanDrawingScale", document =>
        {
            var index = document.Views.FindIndex(item => item.Id == viewId);
            document.Views[index] = document.Views[index] with { DrawingScale = denominator };
        });
    }
}
