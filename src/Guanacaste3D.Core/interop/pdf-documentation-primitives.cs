using System.Globalization;

namespace Guanacaste3D.Core.Interop;

public static partial class PdfPlanExporter
{
    /// <summary>Includes associative measurements, notes, datums, section references, and families in printed plans.</summary>
    private static void AddDocumentationPrimitives(List<SheetPrimitive> items, BuildingDocument document, Guid levelId)
    {
        foreach (var axis in document.Grids)
        {
            items.Add(new SheetPolyline([axis.Start, axis.End], 0.13, Grey, Dashed: true));
            foreach (var point in new[] { axis.Start, axis.End })
            {
                items.Add(new SheetPolygon(Circle(point, 0.18, 32), 0.18, Black, (1, 1, 1)));
                items.Add(new SheetText(new(point.X, point.Y - 0.05), axis.Name, 2.5, Black));
            }
        }
        foreach (var dimension in document.Dimensions.Where(dimension => dimension.LevelId == levelId))
        {
            if (PlanDimensions.Resolve(document, dimension) is not { } measurement) continue;
            items.Add(new SheetPolyline([measurement.From, measurement.To], 0.13, Black));
            foreach (var point in new[] { measurement.From, measurement.To })
                items.Add(new SheetPolyline([new(point.X - 0.07, point.Y - 0.07),
                    new(point.X + 0.07, point.Y + 0.07)], 0.25, Black));
            items.Add(new SheetText(new((measurement.From.X + measurement.To.X) / 2,
                (measurement.From.Y + measurement.To.Y) / 2 + 0.08),
                measurement.Value.ToString("0.00", CultureInfo.CurrentCulture), 2.5, Black));
        }
        foreach (var element in document.StructuralElements.Where(element => element.LevelId == levelId))
        {
            if (element.Kind == StructuralElementKind.ModelText)
                items.Add(new SheetText(element.Start, element.Name, 2.5, Black));
            else if (element.IsDatumLine)
                items.Add(new SheetPolyline([element.Start, element.End], 0.13, Grey,
                    Dashed: element.Kind == StructuralElementKind.ReferencePlane));
        }
        foreach (var view in document.Views.Where(view => view.Cut is not null))
        {
            items.Add(new SheetPolyline([view.Cut!.Start, view.Cut.End], 0.18, Grey, Dashed: true));
            items.Add(new SheetText(view.Cut.Start, view.Name, 2.5, Black, Bold: true));
        }
        var elevation = document.Levels.Single(level => level.Id == levelId).Elevation;
        var upper = document.Levels.Where(level => level.Elevation > elevation).Select(level => level.Elevation)
            .DefaultIfEmpty(elevation + 3).Min();
        foreach (var family in document.InPlaceFamilies)
            foreach (var segment in FamilyPlanGeometry.ForLevel(family, elevation, upper))
                items.Add(new SheetPolyline([segment.Start, segment.End], 0.18, Black));
    }
}
