using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private bool _synchronizingPlanScale;

    /// <summary>Returns the current plan's persistent paper-to-model scale denominator.</summary>
    private int CurrentPlanDrawingScale() => _session.Document.Views.FirstOrDefault(view =>
        view.Kind == ProjectViewKind.Plan && view.LevelId == _activeLevelId)?.DrawingScale ?? 100;

    /// <summary>Converts a paper measurement in millimeters into the current plan's screen units.</summary>
    private double PlanPaperPixels(double millimeters) => Math.Clamp(millimeters * CurrentPlanDrawingScale() * _scale / 1000, 0.1, 5000);

    /// <summary>Scales legacy annotation proportions from an 11-pixel, 2.5 mm paper text reference.</summary>
    private double PlanAnnotationFactor() => Math.Clamp(PlanPaperPixels(2.5) / 11, 0.02, 50);

    /// <summary>Keeps the scale control aligned with the active plan without creating undo entries.</summary>
    private void SynchronizePlanScalePicker()
    {
        if (PlanScalePicker is null || _session is null) return;
        _synchronizingPlanScale = true;
        try
        {
            var denominator = CurrentPlanDrawingScale().ToString(System.Globalization.CultureInfo.InvariantCulture);
            PlanScalePicker.SelectedItem = PlanScalePicker.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => Equals(item.Tag, denominator));
        }
        finally { _synchronizingPlanScale = false; }
    }

    /// <summary>Persists a selected scale on the active plan and refreshes its annotations.</summary>
    private void PlanScalePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingPlanScale || _session is null ||
            PlanScalePicker.SelectedItem is not ComboBoxItem { Tag: string tag } || !int.TryParse(tag, out var denominator)) return;
        var view = _session.Document.Views.FirstOrDefault(item => item.Kind == ProjectViewKind.Plan && item.LevelId == _activeLevelId);
        if (view is not null) _session.UpdatePlanDrawingScale(view.Id, denominator);
    }
}
