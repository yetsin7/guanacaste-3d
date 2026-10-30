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

    private void MaterialSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (_ready && _source is not null) RefreshMaterialRows();
    }

    internal void FocusMaterial(SurfaceKind category)
    {
        _focusedMaterial = category;
        if (SectionTabs.Items.Count > 3) SectionTabs.SelectedIndex = 3;
        RefreshMaterialRows();
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
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var size = new TextBox
            {
                Width = 52, Margin = new Thickness(6, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center,
                Text = (assignment?.TextureSize ?? 1.0).ToString("0.##", CultureInfo.CurrentCulture),
                ToolTip = Text("RenderPanel.TextureSizeHint"), IsEnabled = assignment is not null
            };
            var picker = new ComboBox();
            if (_focusedMaterial == category) { picker.BorderBrush = System.Windows.Media.Brushes.DarkOrange; picker.BorderThickness = new Thickness(2); }
            picker.Items.Add(new ComboBoxItem { Content = Text("RenderPanel.MaterialStock"), Tag = StockTag });
            // The material library, grouped under headings that cannot be chosen themselves.
            var search = MaterialSearch.Text.Trim();
            foreach (var group in MaterialPresets.All
                .Where(preset => search.Length == 0 ||
                    preset.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                    preset.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    Text($"MaterialFamily.{preset.Group}").Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .GroupBy(preset => preset.Group))
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
            // Filtering must never hide the currently assigned preset or make it appear stock.
            if (assignment is not null && MaterialPresets.FromPath(assignment.TexturePath) is { } selectedPreset &&
                !picker.Items.OfType<ComboBoxItem>().Any(item => item.Tag as string == assignment.TexturePath))
                picker.Items.Add(new ComboBoxItem
                {
                    Content = "✓ " + selectedPreset.Name, Tag = assignment.TexturePath
                });
            // Preserve a missing preset in the UI instead of silently displaying Stock.
            // The renderer uses a safe stock fallback until the user replaces it.
            if (assignment is not null &&
                assignment.TexturePath.StartsWith(MaterialPresets.Prefix, StringComparison.Ordinal) &&
                MaterialPresets.FromPath(assignment.TexturePath) is null)
                picker.Items.Add(new ComboBoxItem
                {
                    Content = "⚠ Material no disponible: " + assignment.TexturePath[MaterialPresets.Prefix.Length..],
                    Tag = assignment.TexturePath
                });
            picker.Items.Add(new ComboBoxItem
            {
                Content = Text("MaterialFamily.Textures"), IsEnabled = false, FontWeight = FontWeights.SemiBold
            });
            foreach (var texture in RenderTextureLibrary.BuiltIn.Where(texture =>
                search.Length == 0 ||
                texture.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                Text(texture.LabelKey).Contains(search, StringComparison.CurrentCultureIgnoreCase)))
                picker.Items.Add(new ComboBoxItem
                {
                    Content = Text(texture.LabelKey), Tag = RenderTextureLibrary.BuiltInPrefix + texture.Key
                });
            if (assignment is not null &&
                assignment.TexturePath.StartsWith(RenderTextureLibrary.BuiltInPrefix, StringComparison.Ordinal) &&
                !picker.Items.OfType<ComboBoxItem>().Any(item => item.Tag as string == assignment.TexturePath))
            {
                var selectedTexture = RenderTextureLibrary.BuiltIn.FirstOrDefault(texture =>
                    RenderTextureLibrary.BuiltInPrefix + texture.Key == assignment.TexturePath);
                picker.Items.Add(new ComboBoxItem
                {
                    Content = selectedTexture is null
                        ? "⚠ Textura no disponible: " + assignment.TexturePath[RenderTextureLibrary.BuiltInPrefix.Length..]
                        : "✓ " + Text(selectedTexture.LabelKey),
                    Tag = assignment.TexturePath
                });
            }
            if (assignment is not null && !assignment.TexturePath.StartsWith(RenderTextureLibrary.BuiltInPrefix, StringComparison.Ordinal) &&
                !assignment.TexturePath.StartsWith(MaterialPresets.Prefix, StringComparison.Ordinal))
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
            var restore = new Button
            {
                Content = "Restaurar", ToolTip = "Volver al material original de esta superficie",
                Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(7, 2, 7, 2),
                IsEnabled = assignment is not null
            };
            restore.Click += (_, _) =>
            {
                _source?.AssignRenderMaterial(category, null, 1, 0);
                FocusMaterial(category);
            };
            var duplicate = new Button
            {
                Content = "Duplicar a…", ToolTip = "Copiar este material a otra categoría",
                Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(7, 2, 7, 2),
                IsEnabled = assignment is not null
            };
            var menu = new ContextMenu();
            foreach (var (targetCategory, targetLabel) in RenderTextureLibrary.Categories)
            {
                if (targetCategory == category) continue;
                var target = targetCategory;
                var item = new MenuItem { Header = Text(targetLabel) };
                item.Click += (_, _) =>
                {
                    if (_source is null) return;
                    var current = _source.RenderMaterials.FirstOrDefault(material => material.Category == category);
                    if (current is null) return;
                    _source.AssignRenderMaterial(target, current.TexturePath, current.TextureSize, current.Relief);
                    FocusMaterial(target);
                };
                menu.Items.Add(item);
            }
            duplicate.Click += (_, _) => { duplicate.ContextMenu = menu; menu.PlacementTarget = duplicate; menu.IsOpen = true; };
            // Give the material name its full width; controls no longer compete for
            // the same narrow horizontal row in the 420 px render sidebar.
            picker.HorizontalAlignment = HorizontalAlignment.Stretch;
            row.Children.Add(picker);
            var details = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            details.Children.Add(new TextBlock
            {
                Text = "Escala (m)", VerticalAlignment = VerticalAlignment.Center
            });
            details.Children.Add(size);
            var relief = new TextBox
            {
                Width = 52, Margin = new Thickness(8, 0, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = (assignment?.Relief ?? 0.3).ToString("0.##", CultureInfo.CurrentCulture),
                ToolTip = "Relieve de la textura: 0 (plano) a 1 (máximo)",
                IsEnabled = assignment is not null
            };
            relief.LostFocus += (_, _) => MaterialReliefChanged(category, relief);
            relief.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) MaterialReliefChanged(category, relief);
            };
            details.Children.Add(new TextBlock
            {
                Text = "Relieve", Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            details.Children.Add(relief);
            details.Children.Add(duplicate);
            details.Children.Add(restore);
            row.Children.Add(details);
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

    /// <summary>Updates the image relief independently of texture scale; the change is undoable.</summary>
    private void MaterialReliefChanged(SurfaceKind category, TextBox relief)
    {
        if (_refreshingMaterials || _source is null) return;
        var assignment = _source.RenderMaterials.FirstOrDefault(material => material.Category == category);
        if (assignment is null) return;
        var value = double.TryParse(relief.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)
            && double.IsFinite(parsed) && parsed is >= 0 and <= 1 ? parsed : assignment.Relief;
        if (Math.Abs(value - assignment.Relief) < 1e-9) return;
        _source.AssignRenderMaterial(category, assignment.TexturePath, assignment.TextureSize, value);
    }

    private static double ReadSize(TextBox box, double fallback) =>
        double.TryParse(box.Text.Replace("m", string.Empty).Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
        value is >= 0.01 and <= 1000 ? value : fallback;

    private static string Text(string key) => Application.Current?.TryFindResource(key) as string ?? key;
}
