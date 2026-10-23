# Capability card: door and window types (family types)

## User-visible workflow

1. **Arquitectura → Tipos de puertas y ventanas…** lists every door and window
   type as "Family: Type", with its size, default sill, and number of placed
   instances. The list can be filtered by kind.
2. Select a type to edit its family, name, width, height, default sill, and
   material. **Aplicar** commits one transaction, and every instance of that
   type resizes.
3. **Duplicar** copies a type under a unique name, the usual way to start a
   new size. **Eliminar** deletes a type only when nothing uses it.
   **Purgar tipos no usados…** removes all unused types at once.
4. Placing a door or window without choosing a type uses the kind's default
   type. Changing one instance's size in the properties panel moves only that
   instance to a type of that size, creating one if needed.
5. Schedules for Doors and Windows show the type name, width, height, sill, and
   host wall, plus any project parameters. Type-bound parameters such as a fire
   rating are shared by every instance of the type.

## Domain entities and relations

| Entity | Fields | Notes |
|--------|--------|-------|
| `OpeningType` | Id, Kind, FamilyName, Name, Width, Height, DefaultSill, MaterialName | Type parameters; the family groups sizes |
| `WallOpening.OpeningTypeId` | Reference to its type | Width/Height mirror the type; Offset/Sill are instance parameters |
| `BuiltInOpeningTypes` | 3 doors, 3 windows with stable GUIDs | Identical ids in every project, so definitions can be exchanged |

## Validation rules

- Width 0.3–10 m, height 0.3–20 m, default sill 0–50 m; door types must have sill 0.
- Family plus type name is unique (case-insensitive); ids are globally unique.
- Every opening references an existing type of the same kind, and its width
  and height equal the type's (tolerance 5×10⁻⁷ m).
- Existing host rules still apply: inside the wall, no overlap, door sill = 0.

## Dependencies and regeneration

`Regenerate` first copies type sizes onto instances, then prunes orphaned
parameter values, then the document is validated. As a result, editing a type
that would make any instance overflow or overlap rejects **the whole edit**,
and deleting a type also drops its type-bound parameter values.

## Representations

| View | Effect |
|------|--------|
| Plan, 3D, elevation, section, render | Instance sizes follow the type, so every view updates (no view code changed) |
| Door/Window schedules | Type name, Width, Height, Sill, Host columns; type-bound parameters |
| CSV, DXF, IFC, OBJ/glTF exports | Use the synchronized instance sizes |

## Persistence (.g3 schema 5)

`OpeningTypes` array plus `OpeningTypeId` on each opening. Migration 4 → 5
creates the built-in catalog and matches each opening to a type of identical
size, or generates one ("Single Door: 1000 × 2200 mm"), so geometry is
preserved bit for bit. This is verified against the demonstration house.

## Undo/redo

Named transactions: `AddOpening`, `UpdateOpening`, `SetOpeningType`,
`AddOpeningType`, `DuplicateOpeningType`, `UpdateOpeningType`,
`RemoveOpeningType`, and `PurgeOpeningTypes`.

## Evidence

| Statement | Class | Source |
|-----------|-------|--------|
| A loadable family groups types; `FamilySymbol` is "an element that represents a single type with a Family" | Observed | `RevitAPI.xml`, `T:Autodesk.Revit.DB.FamilySymbol` |
| Placed doors/windows are `FamilyInstance` elements hosted by a wall | Observed | `RevitAPI.xml` type docs for `FamilyInstance` |
| `ElementType` is the base for all types; types carry type parameters | Observed | `RevitAPI.xml`, `T:Autodesk.Revit.DB.ElementType` |
| `FamilyManager` manages family types and parameters in a family document | Observed | `RevitAPI.xml` |
| Width/Height are type parameters and sill height is an instance parameter in typical door/window families | Inferred | Common content convention; families can define them otherwise. To verify against an independent test family |
| Revit requires duplicating a type before changing a size that other instances share | Derived | Type parameters are shared by definition (E3) |

## Guanacaste 3D design (original) and improvements

- **Size edits never corrupt shared types.** Resizing one instance rebinds
  that instance to a type of the new size instead of silently resizing every
  sibling, and no manual "Duplicate type" step is needed.
- **Atomic propagation.** A type edit is validated against every host wall
  before it commits; the user never ends up with a half-applied resize.
- **Protected deletion plus purge**, so types in use can't vanish by accident.

## Acceptance tests

Automated (21 checks in `opening-type-checks.cs`): built-in catalog, default
placement and sill, size-based reuse/creation, explicit type versus size
conflict, propagation and undo, atomic overflow rejection, instance rebind,
type assignment and kind mismatch, duplicate, name uniqueness, protected
delete, purge and fallback placement, type-bound door parameters in
schedules, value pruning on type delete, migration 4 → 5 with identical
sizes, and schema-5 round trip.

Manual: open the demonstration house, open the type manager, raise "Single
Door: 900 × 2100 mm" to 1000 mm, and confirm every 900 door widens in plan and
3D. Undo. Set 3.5 m and confirm the change is rejected with a message.

## Defects found during installed-build validation (fixed)

| Defect | Fix | Regression coverage |
|--------|-----|---------------------|
| "3,5" was rejected when the OS culture uses a decimal point | A lone comma is treated as a decimal comma | `parameter-checks.cs`: es-MX "3,5" → 3.5 |
| List items were announced as "LabeledValue { Label = … }" to UI Automation and screen readers | `LabeledValue.ToString()` returns the label | UI Automation read-back of list item names |
| Rejection reasons appeared in English | `LocalizedErrors` maps every Core message to a Spanish resource | UI Automation: the 6 m type edit reports "Una abertura no cabe dentro de su muro anfitrión." |

## Interactive validation (installed 0.5.0)

On `artifacts/validation/parameters-smoke.g3` (a schema-4 file migrated on
open), the type list shows generated types for the sample's sizes. Widening
"Single Door: 900 × 2100 mm" to 1.0 m updates its 3 instances in one
"Modificar tipo de abertura" step. 3.5 m is accepted because every instance
still fits, while 6.0 m is rejected atomically with a localized reason. Undo
restores the previous width.

## Pending

Swing direction and handing, frame and panel geometry (today an opening is a
rectangular void), nested families, family editor with reference planes and
formulas, type catalogs import, and localized validation messages.
