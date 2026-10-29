using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Desktop;

/// <summary>
/// The picture of a project on the Home screen: an isometric line drawing of its model (the same projection as a sheet's
/// 3D view), the building in dark lines and its planting in light green, on white.
/// </summary>
internal static class ProjectThumbnail
{
    public const int Width = 256;
    public const int Height = 256;

    /// <summary>One line of the drawing, already projected: planting draws lighter.</summary>
    public readonly record struct Line(Point A, Point B, bool Planting);

    /// <summary>Projects the model's feature edges. Pure computation, so it runs off the UI thread.</summary>
    public static IReadOnlyList<Line> Lines(BuildingDocument document)
    {
        var mesh = BuildingMesher.Build(document);
        var architecture = new List<int>();
        var planting = new List<int>();
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle++)
        {
            var kind = mesh.TriangleKinds[triangle];
            if (kind == SurfaceKind.Ground) continue;
            (kind is SurfaceKind.Foliage or SurfaceKind.Leaf or SurfaceKind.Bark ? planting : architecture)
                .AddRange([mesh.Indices[triangle * 3], mesh.Indices[triangle * 3 + 1], mesh.Indices[triangle * 3 + 2]]);
        }
        static Point Project(Vector3 point) => new((point.X - point.Y) * 0.70710678, -((point.X + point.Y) * 0.40824829 + point.Z * 0.81649658));
        return FeatureEdges.Extract(mesh.Positions, planting, maximumEdges: 6_000).Select(edge => new Line(Project(edge.A), Project(edge.B), true))
            .Concat(FeatureEdges.Extract(mesh.Positions, architecture, maximumEdges: 20_000)
                .Select(edge => new Line(Project(edge.A), Project(edge.B), false))).ToArray();
    }

    /// <summary>Draws the lines fitted to the thumbnail. WPF drawing: call it on the UI thread.</summary>
    public static BitmapSource Render(IReadOnlyList<Line> lines)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, Height));
            var points = lines.Where(line => !line.Planting).SelectMany(line => new[] { line.A, line.B }).ToArray();
            if (points.Length > 0)
            {
                var (minX, maxX) = (points.Min(point => point.X), points.Max(point => point.X));
                var (minY, maxY) = (points.Min(point => point.Y), points.Max(point => point.Y));
                var scale = Math.Min((Width - 24) / Math.Max(1e-6, maxX - minX), (Height - 24) / Math.Max(1e-6, maxY - minY));
                Point Map(Point point) => new(Width / 2 + (point.X - (minX + maxX) / 2) * scale, Height / 2 + (point.Y - (minY + maxY) / 2) * scale);
                var plantPen = new Pen(new SolidColorBrush(Color.FromRgb(150, 176, 130)), 0.5);
                var linePen = new Pen(new SolidColorBrush(Color.FromRgb(44, 48, 54)), 0.7);
                plantPen.Freeze();
                linePen.Freeze();
                foreach (var line in lines) context.DrawLine(line.Planting ? plantPen : linePen, Map(line.A), Map(line.B));
            }
        }
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Writes the thumbnail of a project as PNG, replacing an older one.</summary>
    public static void Save(BitmapSource image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>Loads a cached PNG without locking the file, or null when there is none.</summary>
    public static BitmapSource? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
