# Protected base levels and editable architectural sheets

## Base datums

New projects start with a base floor at 0 m and a base roof at 3 m. Their default
names are localized. Protection follows the persisted `LevelRole`, so renaming
either datum never makes it deletable. Both datums can change elevation, provided
the roof remains above the floor. Existing wall top-constraint regeneration still
validates any dependent geometry before committing the change.

Unused additional levels remain removable. The level editor disables deletion
for the base pair and explains the restriction. The domain session enforces the
same rule independently of the UI.

Project schema 15 adopts existing floor/roof levels during migration. If an older
project has only one level, migration adds the missing roof and its plan view
without moving walls or other existing geometry. Opening an old native container
checks its original header and payload integrity before migration, and does not
overwrite the source file. Projects created from one-level templates receive the
same protected pair.

## Paper families and sheet instances

A title block is a native `FamilyDocument` in the `TitleBlocks` category, with an
editable `SheetFamilyLayout`. It can be saved and loaded independently as `.g3df`.
The family stores physical paper dimensions, margins, reserved header/footer
areas, lines, rectangles, text, font sizes, and line weights.

A project sheet references that family and stores its number, title, description,
subtitle, date, and placed model views. Sharing a family between sheets allows a
single family edit to regenerate all those sheets. Saving a family does not embed
the project model or its placed views in the family file.

The initial composition follows the supplied architectural reference: a thin
outer border, heading and description, a drawing area, numbered view captions,
and a restrained footer with project, sheet number, and date. A0-A4 portrait and
landscape families are available. Every line and text field is editable.

Supported dynamic family fields are `{ProjectName}`, `{SheetName}`,
`{SheetNumber}`, `{Description}`, `{Subtitle}`, and `{Date}`. Other text is retained
literally. User-authored names and text are model content, while application
labels and errors use localized resources.

## Editing and printing

1. Open or create a sheet, then double-click the paper or choose its contextual
   editing command. Properties also exposes the composition editor.
2. Add plan, elevation, section, or isometric model views. Arrange them in rows
   or columns, then edit their positions and dimensions in paper millimeters.
3. Set each view's scale denominator independently. Zero selects a fitting
   standard scale; a positive number is retained exactly. Views are clipped to
   their rectangles instead of silently changing a requested scale.
4. In the family tab, edit graphics or create/load a native paper family. Apply
   validates the whole composition and shared family in one undoable transaction.
5. Export PDF. When project sheets exist, PDF export prints those saved sheets at
   their actual physical sizes. With no sheets, the generated documentation-set
   export remains available.

Preview and PDF consume the same paper-space composition. Drawing annotation
heads and line weights retain physical paper dimensions across view scales.
Wheel zoom and middle-button panning change only the preview magnification; they
never change printed dimensions. Double middle-click restores the fitted paper.

## Scope and verification

The reference PDF is a visual composition reference. Its structural notes and
dimensions are not interpreted as construction instructions or engineering
validation. Current placed elevations and isometric views use vector feature
linework; material-filled elevations, hidden-line removal, and a complete
view-owned annotation-family editor remain separate work.

`tests/documentation-checks` exercises historical native migration, deletion
protection, templates, shared family edits, `.g3df` and `.g3d` round trips,
independent scales, clipping, real WPF controls, and matching preview/PDF output.
Evidence is written under `artifacts/documentation-evidence/`.
