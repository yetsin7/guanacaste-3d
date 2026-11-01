using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>
/// Non-destructive PBR inspector: scene-wide or element-local values are persisted in the project.
/// A slider drag only changes the label; Apply creates one undoable transaction and scene rebuild.
/// </summary>
public partial class RenderPanel
{
    private bool _settingGalleryAppearance;

    private RenderMaterialAssignment? GalleryAssignment() =>
        _source?.RenderMaterials.FirstOrDefault(item =>
            item.Category == GalleryTarget() && item.ElementId == _focusedElement);

    private (double Roughness, double Metallic, double Relief) GalleryOriginalAppearance(
        RenderMaterialAssignment? assignment, SurfaceKind category)
    {
        if (assignment is not null)
        {
            if (PbrMaterialPackages.IsPackage(assignment.TexturePath))
            {
                var package = PbrMaterialPackages.Read(
                    PbrMaterialPackages.ResolveManifest(assignment.TexturePath, _source?.ProjectFilePath));
                if (package is { } imported)
                    return (imported.Manifest.Roughness, imported.Manifest.Metallic, imported.Manifest.Relief);
            }
            if (MaterialPresets.FromPath(assignment.TexturePath) is { } preset)
                return (preset.Material.Roughness, preset.Material.Metallic, preset.Relief);
            var library = RenderTextureLibrary.BuiltIn.FirstOrDefault(item =>
                RenderTextureLibrary.BuiltInPrefix + item.Key == assignment.TexturePath);
            if (library is not null)
            {
                var stock = MaterialLibrary.ForKind(category);
                return (stock.Roughness, stock.Metallic, library.Relief);
            }
        }
        var material = MaterialLibrary.ForKind(category);
        return (material.Roughness, material.Metallic, assignment?.Relief ?? 0.3);
    }

    private void UpdateGalleryAppearanceEditor()
    {
        if (!_galleryReady || GalleryAppearanceEditor is null || _source is null ||
            GalleryExpander.IsExpanded != true) return;
        var target = GalleryTarget();
        var current = GalleryAssignment();
        var initial = GalleryOriginalAppearance(current, target);
        _settingGalleryAppearance = true;
        try
        {
            GalleryRoughnessSlider.Value = current?.RoughnessOverride ?? initial.Roughness;
            GalleryMetallicSlider.Value = current?.MetallicOverride ?? initial.Metallic;
            GalleryReliefSlider.Value = current?.Relief ?? initial.Relief;
            GalleryAppearanceName.Text = current is null
                ? Text("RenderGallery.StockMaterial")
                : MaterialDisplayName(current.TexturePath);
            GalleryApplyAppearanceButton.IsEnabled = true;
            GalleryResetAppearanceButton.IsEnabled = current is not null;
            UpdateGalleryAppearanceLabels();
            RefreshGalleryMapCards(current, target);
        }
        finally { _settingGalleryAppearance = false; }
    }

    private void GalleryAppearanceValue_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settingGalleryAppearance) return;
        UpdateGalleryAppearanceLabels();
    }

    private void UpdateGalleryAppearanceLabels()
    {
        if (GalleryRoughnessValue is null || GalleryMetallicValue is null || GalleryReliefValue is null) return;
        GalleryRoughnessValue.Text = GalleryRoughnessSlider.Value.ToString("0.00");
        GalleryMetallicValue.Text = GalleryMetallicSlider.Value.ToString("0.00");
        GalleryReliefValue.Text = GalleryReliefSlider.Value.ToString("0.00");
    }

    private void GalleryApplyAppearance_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null || !_galleryReady) return;
        var category = GalleryTarget();
        if (_focusedElement is Guid && category != _focusedMaterial) return;
        _source.SetRenderMaterialAppearance(category, _focusedElement,
            GalleryRoughnessSlider.Value, GalleryMetallicSlider.Value, GalleryReliefSlider.Value);
        RefreshMaterialRows();
    }

    private void GalleryResetAppearance_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null || !_galleryReady) return;
        var category = GalleryTarget();
        if (_focusedElement is Guid && category != _focusedMaterial) return;
        var defaults = GalleryOriginalAppearance(GalleryAssignment(), category);
        _source.SetRenderMaterialAppearance(category, _focusedElement,
            null, null, defaults.Relief);
        RefreshMaterialRows();
    }

    private void RefreshGalleryMapCards(RenderMaterialAssignment? selected, SurfaceKind category)
    {
        GalleryPbrMapCards.Children.Clear();
        // The package channels are inherited when there is no explicit per-element replacement.
        PbrMaterialManifest? package = null;
        string? packageFolder = null;
        if (selected is not null && PbrMaterialPackages.IsPackage(selected.TexturePath))
        {
            var manifestPath = PbrMaterialPackages.ResolveManifest(selected.TexturePath, _source?.ProjectFilePath);
            package = PbrMaterialPackages.Read(manifestPath)?.Manifest;
            packageFolder = manifestPath is null ? null : Path.GetDirectoryName(manifestPath);
        }
        for (var channel = 0; channel < 4; channel++)
        {
            var index = channel;
            var label = channel switch { 0 => "Normal", 1 => "Rugosidad", 2 => "Metálico", _ => "Altura" };
            var overridePath = channel switch
            {
                0 => selected?.NormalPath,
                1 => selected?.RoughnessPath,
                2 => selected?.MetallicPath,
                _ => null
            };
            var bundledName = channel switch
            {
                0 => package?.Normal,
                1 => package?.RoughnessMap,
                2 => package?.MetallicMap,
                _ => package?.HeightMap
            };
            var bundlePath = packageFolder is not null && bundledName is not null
                ? Path.Combine(packageFolder, bundledName) : null;
            var path = overridePath is not null
                ? RenderTextureLibrary.Resolve(overridePath, _source?.ProjectFilePath)
                : bundlePath;

            var card = new StackPanel
            {
                Width = 98, Margin = new Thickness(2), Background = new SolidColorBrush(Color.FromRgb(27, 41, 56))
            };
            var frame = new Grid { Width = 90, Height = 56, Background = new SolidColorBrush(Color.FromRgb(45, 62, 78)) };
            var placeholder = new TextBlock
            {
                Text = path is null ? "Sin mapa" : "Cargando…", FontSize = 10, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center
            };
            var preview = new Image
            {
                Width = 90, Height = 56, Stretch = Stretch.UniformToFill
            };
            frame.Children.Add(placeholder);
            frame.Children.Add(preview);
            card.Children.Add(frame);
            card.Children.Add(new TextBlock
            {
                Text = label + (overridePath is not null ? " ✓" : bundlePath is not null ? " · PBR" : ""),
                Foreground = Brushes.White, TextAlignment = TextAlignment.Center,
                FontSize = 11, Margin = new Thickness(0, 4, 0, 4)
            });
            var button = new Button
            {
                Content = overridePath is not null ? "Cambiar" : "Elegir",
                Padding = new Thickness(5, 3, 5, 3), Margin = new Thickness(2, 0, 2, 0),
                ToolTip = "Seleccionar imagen del mapa " + label
            };
            // Height is defined by the material package; per-element height overrides do
            // not exist yet and must not accidentally be passed to the 3-channel editor.
            if (channel < 3)
            {
                button.Click += (_, _) => ChoosePbrMap(category, _focusedElement, index);
                card.Children.Add(button);
            }
            else card.Children.Add(new TextBlock
            {
                Text = "Desde .g3dm", Foreground = Brushes.LightGray,
                FontSize = 10, TextAlignment = TextAlignment.Center
            });
            if (overridePath is not null)
            {
                var clear = new Button
                {
                    Content = "Quitar", Padding = new Thickness(5, 2, 5, 2),
                    Margin = new Thickness(2, 4, 2, 0),
                    ToolTip = "Quita el mapa individual; si existe un mapa del .g3dm, vuelve a utilizarlo"
                };
                clear.Click += (_, _) => ChangePbrMap(category, _focusedElement, index, null);
                card.Children.Add(clear);
            }
            GalleryPbrMapCards.Children.Add(card);
            if (path is not null)
            {
                // Existing bounded thumbnail cache and cancellation policy are shared with catalog cards.
                var key = Path.GetFullPath(path);
                if (_galleryPreviews.TryGetValue(key, out var bitmap))
                {
                    preview.Source = bitmap;
                    placeholder.Visibility = Visibility.Collapsed;
                }
                else _ = LoadGalleryThumbnailAsync(key, preview, placeholder,
                    _galleryRevision, _galleryLoadCancellation.Token);
            }
        }
    }
}
