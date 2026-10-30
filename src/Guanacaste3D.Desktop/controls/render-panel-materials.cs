using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>
/// The Materials card of the Render tab: for each surface category, its stock material, a preset of the material
/// library, one of the shipped textures, or any image from disk, plus the real size one copy of the image covers. Choices are stored in the project and
/// undoable; the scene is rebuilt when they change.
/// </summary>
public partial class RenderPanel
{
    private const string StockTag = "stock";
    private const string CustomTag = "custom";
    private bool _refreshingMaterials;
    private SurfaceKind? _focusedMaterial;

    internal void FocusMaterial(SurfaceKind category)
    {
        _focusedMaterial = category;
        RefreshMaterialRows();
        if (SectionTabs.Items.Count > 3) SectionTabs.SelectedIndex = 3;
    }


    /// <summary>Rebuilds the material rows from the project, for example after undo or opening another project.</summary>
    private void RefreshMaterialRows()
    {
        if (_source is null || MaterialRows is null) return;
        _refreshingMaterials = true;
        MaterialRows.Children.Clear();
        foreach (var (category, labelKey) in RenderTextureLibrary.Categories)
        {
            var assignment = _source.RenderMaterials.FirstOrDefault(material => material.Category == category);
            var label = new TextBlock { Style = (Style)FindResource("PanelLabel"), Text = Text(labelKey) };
            if (_focusedMaterial == category) { label.FontWeight = FontWeights.Bold; label.Foreground = System.Windows.Media.Brushes.DarkOrange; }
            MaterialRows.Children.Add(label);
            var row = new DockPanel { LastChildFill = true };
            var size = new TextBox
            {
                Width = 52, Margin = new Thickness(6, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center,
                Text = (assignment?.TextureSize ?? 1.0).ToString("0.##", CultureInfo.CurrentCulture),
                ToolTip = Text("RenderPanel.TextureSizeHint"), IsEnabled = assignment is not null
            };
            DockPanel.SetDock(size, Dock.Right);
            var picker = new ComboBox();
            if (_focusedMaterial == category) { picker.BorderBrush = System.Windows.Media.Brushes.DarkOrange; picker.BorderThickness = new Thickness(2); }
            picker.Items.Add(new ComboBoxItem { Content = Text("RenderPanel.MaterialStock"), Tag = StockTag });
            // The material library, grouped under headings that cannot be chosen themselves.
            foreach (var group in MaterialPresets.All.GroupBy(preset => preset.Group))
            {
                picker.Items.Add(new ComboBoxItem
                {
                    Content = Text($"MaterialFamily.{group.Key}"), IsEnabled = false, FontWeight = FontWeights.SemiBold
                });
                foreach (var preset in group)
                    picker.Items.Add(new ComboBoxItem
                    {
                        Content = "   " + preset.Name, Tag = MaterialPresets.Prefix + preset.Id
                    });
            }
            picker.Items.Add(new ComboBoxItem
            {
                Content = Text("MaterialFamily.Textures"), IsEnabled = false, FontWeight = FontWeights.SemiBold
            });
            foreach (var texture in RenderTextureLibrary.BuiltIn)
                picker.Items.Add(new ComboBoxItem
                {
                    Content = Text(texture.LabelKey), Tag = RenderTextureLibrary.BuiltInPrefix + texture.Key
                });
            if (assignment is not null && !assignment.TexturePath.StartsWith(RenderTextureLibrary.BuiltInPrefix, StringComparison.Ordinal))
                picker.Items.Add(new ComboBoxItem { Content = Path.GetFileName(assignment.TexturePath), Tag = assignment.TexturePath });
            picker.Items.Add(new ComboBoxItem { Content = Text("RenderPanel.MaterialFromFile"), Tag = CustomTag });
            picker.SelectedItem = picker.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag as string == (assignment?.TexturePath ?? StockTag)) ?? picker.Items[0];
            picker.SelectionChanged += (_, _) => MaterialChosen(category, picker, size);
            size.LostFocus += (_, _) => MaterialSizeChanged(category, size);
            size.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) MaterialSizeChanged(category, size);
            };
            row.Children.Add(size);
            row.Children.Add(picker);
            MaterialRows.Children.Add(row);
        }
        _refreshingMaterials = false;
        if (_focusedMaterial is { } focused)
        {
            var index = 0;
            foreach (var (category, _) in RenderTextureLibrary.Categories)
            {
                if (category == focused && MaterialRows.Children[index * 2] is FrameworkElement label)
                {
                    Dispatcher.BeginInvoke(new Action(label.BringIntoView),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                    break;
                }
                index++;
            }
        }
    }

    private void MaterialChosen(SurfaceKind category, ComboBox picker, TextBox size)
    {
        if (_refreshingMaterials || _source is null || picker.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        switch (tag)
        {
            case StockTag:
                _source.AssignRenderMaterial(category, null, 1, 0);
                break;
            case CustomTag:
                var dialog = new OpenFileDialog
                {
                    Title = Text("RenderPanel.MaterialFromFile"),
                    Filter = Text("RenderPanel.TextureFilter")
                };
                if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                    _source.AssignRenderMaterial(category, dialog.FileName, ReadSize(size, 1), 0.3);
                break;
            case var preset when MaterialPresets.FromPath(preset) is { } chosen:
                _source.AssignRenderMaterial(category, preset, chosen.TextureSize, chosen.Relief);
                break;
            default:
                var builtIn = RenderTextureLibrary.BuiltIn.FirstOrDefault(texture =>
                    RenderTextureLibrary.BuiltInPrefix + texture.Key == tag);
                _source.AssignRenderMaterial(category, tag, builtIn?.Size ?? ReadSize(size, 1), builtIn?.Relief ?? 0.3);
                break;
        }
        RefreshMaterialRows();
    }

    private void MaterialSizeChanged(SurfaceKind category, TextBox size)
    {
        if (_refreshingMaterials || _source is null) return;
        var assignment = _source.RenderMaterials.FirstOrDefault(material => material.Category == category);
        if (assignment is null) return;
        var value = ReadSize(size, assignment.TextureSize);
        if (Math.Abs(value - assignment.TextureSize) < 1e-9) return;
        _source.AssignRenderMaterial(category, assignment.TexturePath, value, assignment.Relief);
    }

    private static double ReadSize(TextBox box, double fallback) =>
        double.TryParse(box.Text.Replace("m", string.Empty).Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
        value is >= 0.01 and <= 1000 ? value : fallback;

    private static string Text(string key) => Application.Current?.TryFindResource(key) as string ?? key;
}
