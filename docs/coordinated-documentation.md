# Coordinated plans, axes, sections, and elevations

Project schema 13 persists project-wide `GridAxis` references and optional finite
`SectionCut` endpoints on section views. Earlier projects migrate without
inventing axes or moving their existing default section. The `.g3d` container and
its integrity checks remain unchanged.

## Authoring workflow

- **Grid / Rejilla** in the ribbon starts a two-click building-axis tool. The
  background grid checkbox still controls the drafting grid independently.
  Axes appear on every level and in perpendicular elevations. Double-click or
  use the context menu to rename an axis; Delete removes only that reference
  and its dependent dimensions. Undo restores both.
- **Section / Sección** accepts two plan points and opens the resulting section.
  The cut can be transverse, longitudinal, or oblique. The project browser and
  plan reference open the same persistent view. The reference context menu
  opens or renames it.
- Selecting an axis or a cut reference exposes four metric endpoint coordinates
  in Properties. Applying them regenerates the existing reference and its views
  as one undoable operation. Invalid coordinates leave the document intact.
- Selecting a supported element offers temporary dimensions to nearby walls or
  orthogonal building axes. Click the number to edit its clearance; use the
  promotion arrow to persist it. The fixed datum is referenced by identity, so
  changing its name does not detach the measurement.
- **Annotate > Text** uses the existing note-placement tool. Notes persist with
  the project and are included in printed plans.
- **Beam** creates one line member. **Beam System** continues to distribute
  multiple members through a rectangle.

## Geometry and output

Sections intersect the shared model mesh instead of showing a whole-building
elevation. Contours preserve directed winding: touching and overlapping solids
stay filled, while actual voids remain empty. The desktop outlines the filled
region to avoid internal triangulation and overlap seams. Geometry beyond the
finite cutting endpoints is excluded.

Columns, structural members, foundations, and in-place family edges now appear in
elevations. Cut geometry includes the corresponding triangulated family solids.
Level elevations remain absolute project coordinates, including below-grade
foundations.

Without saved sheets, PDF export produces one page per level plan, section, and
elevation. With saved sheets, it prints their editable compositions and reusable
title-block families. Plans include permanent dimensions, notes, axes, cut
references, and family outlines. Generated pages use their saved view names and level datums.
`PdfPlanExporter.Export` remains available for plan-only callers;
`ExportDocumentation` produces the coordinated set.

## Current boundaries

- Sections currently show geometry intersecting the cutting plane; background
  elements behind the plane are not projected into the section.
- Elevation PDFs use projected feature linework without hidden-line removal.
- Axis-based dimensions measure orthogonal bounding-face clearances. They are
  not centerline constraints, oblique perpendicular dimensions, or a general
  angular/radial annotation editor.
- Text notes use the existing model-text representation. A full view-owned
  annotation/tag-family system remains separate work.
- Editable sheets support multiple placed views with independent scales. See
  [editable sheet families](editable-sheet-families.md) for composition and family-file workflows.

## Reproducible checks

```powershell
dotnet build Guanacaste3D.slnx -c Release
dotnet run --project tests/documentation-checks/documentation-checks.csproj -c Release --no-build
```

The executable checks actual mesh intersections and void filling, axis-based
movement, migration, official save/reopen, history, PDF primitives/page counts,
and the real WPF handlers with injected model coordinates. It writes PDF and
window captures under `artifacts/documentation-evidence/`. This is WPF runtime
evidence, not physical mouse automation or installed-application verification.
