using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private bool _showElementTags;

    /// <summary>Shows or hides the automatic door, window and column tags of the plan.</summary>
    private void ToggleElementTags()
    {
        _showElementTags = !_showElementTags;
        RefreshAll();
        StatusText.Text = GetText(_showElementTags ? "Status.TagsShown" : "Status.TagsHidden");
    }

    /// <summary>Adds associative wall and opening dimensions to every element of the active level.</summary>
    private void AutoDimensionActiveLevel()
    {
        var added = _session.AutoDimensionLevel(_activeLevelId);
        RefreshAll();
        StatusText.Text = added == 0 ? GetText("Status.AutoDimensionedNone") : FormatText("Status.AutoDimensioned", added);
    }

    /// <summary>Draws a small round mark beside each tagged element, with its description as a tooltip.</summary>
    private void RenderElementTags()
    {
        if (!_showElementTags) return;
        foreach (var tag in ElementTags.Build(_session.Document, _activeLevelId))
        {
            var center = WorldToCanvas(tag.Position);
            var mark = new Border
            {
                Background = Brushes.White, BorderBrush = Brushes.DarkSlateGray, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), MinWidth = 18, Height = 18, Padding = new Thickness(3, 0, 3, 0),
                ToolTip = tag.Description, IsHitTestVisible = true,
                Child = new TextBlock
                {
                    Text = tag.Mark, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateGray,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(mark, center.X - 9);
            Canvas.SetTop(mark, center.Y - 9);
            PlanCanvas.Children.Add(mark);
        }
    }
}
