# Door and window families

New projects include a 900 x 2100 mm single door with a 40 mm leaf and a 1200 x 1200 mm fixed window with a 900 mm sill. The shared mesher creates jambs, a head, and either an inset door leaf or a 6 mm glass pane with a bottom frame. These surfaces appear in the interactive model, renderer, and mesh exports. Type dimensions define the rough wall opening; frame members occupy its inside edges.

Select a wall position in plan and use the door or window command. Select the resulting symbol to edit the hosted instance. The type catalog provides duplication, size changes, and assignment through the existing type workflow. Placement, type changes, undo, redo, schedules, and native project persistence share the same document.

Schema 9 repairs zero-depth built-in doors from earlier projects while preserving custom sizes, names, identifiers, and nonzero depths. Custom types with zero depth remain without a leaf.

## External dimension import

In the type editor, select the door or window category, then import dimensions. Review the dimension summary before accepting. This creates a native parametric opening; it does not preserve external mesh geometry, textures, formulas, connectors, or proprietary family behavior.

- Revit: place the family in a project and export IFC. The importer reads the first matching IfcDoor or IfcWindow with overall dimensions. Assigned SI length units in meters, decimeters, centimeters, and millimeters are supported. Unsupported units are rejected. Legacy fragments without a unit assignment are interpreted as meters.
- SketchUp: export one isolated component as OBJ. Window OBJ coordinates must use X for width, Y for height, Z for depth, and meters. Door OBJ retains the existing sorted-extents heuristic (largest dimension is height); dimensions above 50 are interpreted as millimeters. Confirm all dimensions in the preview.
- DXF: import an isolated plan outline. The editor height supplies the missing vertical dimension. Drawing extents are used; blocks and curves are not reconstructed.

Native RFA, RVT, and SKP files are not supported. Direct vendor-file ingestion and preservation of source geometry remain future work requiring a separate conversion integration. No real vendor fixture has been validated in this change; importer tests use synthetic format fixtures.

## Verification

Run `dotnet build Guanacaste3D.slnx -c Release` and `dotnet run --project tests/Guanacaste3D.Checks -c Release`. Family checks cover complete solids, built-in selection, native round trips, schema migration, undo/redo, IFC units, wide OBJ windows, category rejection, and non-finite dimensions.
