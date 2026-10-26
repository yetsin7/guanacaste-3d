using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Desktop.Controls;

/// <summary>Verifies generated artwork resolves across every ribbon and records a visual command atlas.</summary>
internal static class IconResourceChecks
{
    /// <summary>Checks real WPF resources, localized labels, fixed icon bounds, and the missing-command fallback.</summary>
    public static void Run(Action<bool, string> check)
    {
        var commands = Enum.GetValues<RibbonWorkspace>().SelectMany(RibbonCatalog.GetGroups)
            .SelectMany(group => group.Commands).DistinctBy(command => command.CommandId).ToArray();
        foreach (var command in commands)
        {
            if (Application.Current.TryFindResource("CmdIcon." + command.CommandId) is not DrawingImage icon ||
                icon.Width < 20 || icon.Height < 20)
                throw new InvalidOperationException("Missing or invalid command artwork: " + command.CommandId);
            if (Application.Current.TryFindResource(command.LabelResourceKey) is not string)
                throw new InvalidOperationException("Missing localized command label: " + command.LabelResourceKey);
        }
        check(commands.Length > 200, "Every ribbon command resolves to generated vector artwork and localized text");
        check(CommandIcons.For("UnknownCommand", "Icon.Wall") is DrawingImage,
            "A command without artwork safely resolves its shared fallback icon");
        var artworkDictionary = Application.Current.Resources.MergedDictionaries.Single(dictionary =>
            dictionary.Source?.OriginalString.EndsWith("icons.commands.xaml", StringComparison.Ordinal) == true);
        var artworkKeys = artworkDictionary.Keys.Cast<string>()
            .Where(key => key.StartsWith("CmdIcon.", StringComparison.Ordinal) ||
                key.StartsWith("Icon.", StringComparison.Ordinal)).ToArray();
        foreach (var key in artworkKeys)
            if (Application.Current.TryFindResource(key) is not DrawingImage artwork ||
                artwork.Width < 20 || artwork.Height < 20)
                throw new InvalidOperationException("Missing or invalid shared artwork: " + key);
        check(artworkKeys.Length > commands.Length, "Shared browser and property icons resolve alongside every command icon");
        SaveAtlas(artworkKeys.Where(key => key.StartsWith("CmdIcon.", StringComparison.Ordinal)).ToArray());
    }

    /// <summary>Exports the command artwork actually loaded by WPF for visual inspection at compact and large sizes.</summary>
    private static void SaveAtlas(IReadOnlyList<string> artworkKeys)
    {
        const int columns = 12;
        const int cellWidth = 112;
        const int cellHeight = 80;
        var height = (int)Math.Ceiling(artworkKeys.Count / (double)columns) * cellHeight;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, columns * cellWidth, height));
            var ordered = artworkKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var x = index % columns * cellWidth;
                var y = index / columns * cellHeight;
                var icon = (DrawingImage)Application.Current.FindResource(ordered[index]);
                context.DrawImage(icon, new Rect(x + 25, y + 6, 32, 32));
                context.DrawImage(icon, new Rect(x + 68, y + 14, 16, 16));
                var label = new FormattedText(ordered[index]["CmdIcon.".Length..], CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, Brushes.DimGray, 1)
                    { MaxTextWidth = cellWidth - 8, MaxTextHeight = 32 };
                context.DrawText(label, new Point(x + 4, y + 43));
            }
        }
        var bitmap = new RenderTargetBitmap(columns * cellWidth, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory("artifacts/interaction-evidence");
        using var stream = File.Create("artifacts/interaction-evidence/command-icon-atlas.png");
        encoder.Save(stream);
    }
}
