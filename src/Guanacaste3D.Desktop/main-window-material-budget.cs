namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private MaterialBudgetWindow? _materialBudgetWindow;

    /// <summary>
    /// Opens the takeoff and budget beside the model, without blocking it, so a user can draw a wall and watch the
    /// quantities change. A second click brings the open window to the front instead of opening another.
    /// </summary>
    private void ShowMaterialBudget()
    {
        if (_materialBudgetWindow is { IsLoaded: true })
        {
            _materialBudgetWindow.Activate();
            return;
        }
        _materialBudgetWindow = new MaterialBudgetWindow(() => _session, RefreshAll) { Owner = this };
        _materialBudgetWindow.Closed += (_, _) => _materialBudgetWindow = null;
        _materialBudgetWindow.Show();
    }
}
