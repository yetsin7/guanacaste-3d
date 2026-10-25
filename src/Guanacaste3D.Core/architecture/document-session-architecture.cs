namespace Guanacaste3D.Core;

/// <summary>Library loading, roofs, column shapes and element styles; each public method is one transaction.</summary>
public sealed partial class DocumentSession
{
    /// <summary>
    /// Adds every wall assembly and door or window type of the standard library that the project lacks.
    /// Types already present, by identifier or by name, are left untouched. Returns how many types were added.
    /// </summary>
    public int LoadArchitecturalLibrary()
    {
        var wallIds = Document.WallTypes.Select(type => type.Id).ToHashSet();
        var wallNames = Document.WallTypes.Select(type => type.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingWalls = ArchitecturalLibrary.WallTypes()
            .Where(type => !wallIds.Contains(type.Id) && wallNames.Add(type.Name)).ToArray();
        var openingIds = Document.OpeningTypes.Select(type => type.Id).ToHashSet();
        var openingNames = Document.OpeningTypes
            .Select(type => (type.FamilyName.ToUpperInvariant(), type.Name.ToUpperInvariant())).ToHashSet();
        var missingOpenings = ArchitecturalLibrary.OpeningTypes()
            .Where(type => !openingIds.Contains(type.Id) &&
                openingNames.Add((type.FamilyName.ToUpperInvariant(), type.Name.ToUpperInvariant()))).ToArray();
        if (missingWalls.Length + missingOpenings.Length == 0) return 0;

        Commit("Transaction.LoadArchitecturalLibrary", document =>
        {
            document.WallTypes.AddRange(missingWalls);
            document.OpeningTypes.AddRange(missingOpenings);
        });
        return missingWalls.Length + missingOpenings.Length;
    }

    /// <summary>Adds a slab from a catalog recipe over a closed plan boundary.</summary>
    public Guid AddSlabFromPreset(Guid levelId, IReadOnlyCollection<Point2> boundary, SlabPreset preset,
        double elevationOffset = 0)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddFloor", document => document.Floors.Add(new FloorSlab(
            id, levelId, preset.Name, [.. boundary], preset.Thickness)
        {
            Kind = preset.Kind,
            ElevationOffset = elevationOffset,
            Finish = preset.Finish,
            Shape = preset.Shape,
            Pitch = preset.Pitch
        }));
        return id;
    }

    /// <summary>Changes a roof to a flat, shed, gable or hip shape with the given pitch in degrees.</summary>
    public void SetRoofShape(Guid roofId, RoofShape shape, double pitch)
    {
        var roof = Document.Floors.FirstOrDefault(floor => floor.Id == roofId)
            ?? throw new InvalidDataException("The floor slab does not exist.");
        if (roof.Kind != FloorKind.Roof) throw new InvalidDataException("Invalid roof shape or pitch.");
        Commit("Transaction.SetRoofShape", document =>
        {
            var index = document.Floors.FindIndex(floor => floor.Id == roofId);
            document.Floors[index] = document.Floors[index] with { Shape = shape, Pitch = shape == RoofShape.Flat ? 0 : pitch };
            RaiseStructuresOnRoof(document, document.Floors[index]);
        });
    }

    /// <summary>Changes the plan outline of a column.</summary>
    public void SetColumnShape(Guid columnId, ColumnShape shape)
    {
        if (!Document.Columns.Any(column => column.Id == columnId))
            throw new InvalidDataException("The column does not exist.");
        Commit("Transaction.SetColumnShape", document =>
        {
            var index = document.Columns.FindIndex(column => column.Id == columnId);
            document.Columns[index] = document.Columns[index] with { Shape = shape };
        });
    }

    /// <summary>Changes the geometry variant (section, guard type, stair type) of a structural element.</summary>
    public void SetElementStyle(Guid elementId, ElementStyle style)
    {
        if (!Document.StructuralElements.Any(element => element.Id == elementId))
            throw new InvalidDataException("The element does not exist.");
        Commit("Transaction.SetElementStyle", document =>
        {
            var index = document.StructuralElements.FindIndex(element => element.Id == elementId);
            document.StructuralElements[index] = document.StructuralElements[index] with { Style = style };
        });
    }

    /// <summary>Adds an element from a catalog section; line elements run between the two points.</summary>
    public Guid AddElementFromPreset(Guid levelId, ElementPreset preset, Point2 start, Point2 end)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddStructuralElement", document =>
            document.StructuralElements.Add(CreateStructuralElement(id, document, levelId, preset.Kind, preset.Name,
                start, end) with
            {
                Style = preset.Style, Width = preset.Width, Height = preset.Height, Offset = preset.Offset
            }));
        return id;
    }

    /// <summary>Adds a column from a catalog section.</summary>
    public Guid AddColumnFromPreset(Guid levelId, Point2 position, ColumnPreset preset, double height = 3.00)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddColumn", document =>
            document.Columns.Add(new Column(id, levelId, position, preset.Width, preset.Depth, height)
            {
                Shape = preset.Shape
            }));
        return id;
    }

    /// <summary>
    /// Adds a skylight whose footprint lies fully inside a roof of the same level. It stands on the lowest roof
    /// height under its footprint, and a flat roof is cut by the same rectangle. Returns null when no roof contains it.
    /// </summary>
    public Guid? AddSkylight(Guid levelId, string name, Point2 first, Point2 opposite)
    {
        var extent = Geometry.PlanRectangle.Of([first, opposite]);
        var roof = Document.Floors.FirstOrDefault(floor => floor.LevelId == levelId && floor.Kind == FloorKind.Roof &&
            Geometry.SlabOpenings.ContainsRectangle(floor.Boundary, extent));
        if (roof is null) return null;

        var id = Guid.NewGuid();
        Commit("Transaction.AddSkylight", document => document.StructuralElements.Add(
            CreateStructuralElement(id, document, levelId, StructuralElementKind.Skylight, name, first, opposite)
                with { Offset = roof.ElevationOffset + RoofGeometry.LowestRiseOver(roof, Geometry.SlabOpenings.Corners(extent)) }));
        return id;
    }

    /// <summary>Lifts dormers and skylights standing on a roof to the roof's new surface after its shape changed.</summary>
    private static void RaiseStructuresOnRoof(BuildingDocument document, FloorSlab roof)
    {
        for (var index = 0; index < document.StructuralElements.Count; index++)
        {
            var element = document.StructuralElements[index];
            if (element.LevelId != roof.LevelId ||
                element.Kind is not (StructuralElementKind.Dormer or StructuralElementKind.Skylight)) continue;
            var extent = Geometry.PlanRectangle.Of([element.Start, element.End]);
            if (!Geometry.SlabOpenings.ContainsRectangle(roof.Boundary, extent)) continue;
            document.StructuralElements[index] = element with
            {
                Offset = roof.ElevationOffset + RoofGeometry.LowestRiseOver(roof, Geometry.SlabOpenings.Corners(extent))
            };
        }
    }
}
