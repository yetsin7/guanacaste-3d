using System.Windows;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>
/// Marks a ribbon button that opens a menu of related commands. Ribbon buttons show only their icon, so the
/// template draws a small arrow instead of the caption's trailing triangle.
/// </summary>
public static class RibbonButton
{
    public static readonly DependencyProperty HasMenuProperty = DependencyProperty.RegisterAttached(
        "HasMenu", typeof(bool), typeof(RibbonButton), new PropertyMetadata(false));

    public static bool GetHasMenu(DependencyObject target) => (bool)target.GetValue(HasMenuProperty);

    public static void SetHasMenu(DependencyObject target, bool value) => target.SetValue(HasMenuProperty, value);
}
