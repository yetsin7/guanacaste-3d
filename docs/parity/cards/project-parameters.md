# Capability card: project parameters and category schedules

## User-visible workflow

1. **Manage → Parámetros del proyecto…** opens the manager. It starts on the
   category of the current selection.
2. Create a definition: name, data type (spec), binding (instance or type),
   group, description, and one or more categories.
3. The category table lists every element of the chosen category, with one
   column per bound parameter. Edit a cell to set a value. Select several cells
   and press Ctrl+V to fill them, or Supr to clear them. Each action is one undo step.
4. Type-bound columns (marked "· tipo") share one value across every instance
   of the same wall type.
5. Export the table to CSV.

## Domain entities and relations

| Entity | Fields | Notes |
|--------|--------|-------|
| `ParameterDefinition` | Id, Name, Spec, Binding, Categories, Group, Description | Id is stable across renames |
| `ParameterValue` | DefinitionId, OwnerId, Number?, Text? | Owner is the instance, or the type when type-bound |
| `ElementCategory` | Walls, Doors, Windows, Rooms, Floors, Levels, Sheets | Doors/Windows split from `WallOpening.Kind` |
| `ElementCatalog` | Category-neutral `ElementEntry` view of the document | Feeds schedules and validation |
| `CategorySchedule` | Columns (built-in + parameters), rows with effective values | Derived on demand; never stored |

## Instance and type parameters

- Instance binding: the value is stored on each element.
- Type binding: the value is stored once on the type. Only categories in
  `ParameterCategoryRules.TypedCategories` (Walls → `WallType`) qualify today;
  others are rejected at validation.

## Validation rules

- Names are 1–80 characters, trimmed, and unique (case-insensitive). At least
  one category; no duplicate categories.
- Spec and binding are fixed after creation, so a stored value never changes meaning.
- Value storage matches the spec: Text uses `Text` only, at most 1,000
  characters. Every other spec uses a finite `Number` only. Integer values must
  be integral. Yes/No values are 0 or 1. Area and Volume must be ≥ 0.
- At most one value per (definition, owner). The owner must exist and match
  the binding and categories.
- Files containing orphaned values are **rejected on open**. Inside an edit
  session, orphans are **regenerated away** in the same transaction.

## Dependencies and regeneration

Deleting an element, deleting a definition, or removing a category from a
binding removes the affected values in the same transaction (see
[transactions](transactions.md)). Undo restores the element and its values together.

## Representations

| View | Behavior in increment 1 |
|------|-------------------------|
| Category schedule | **Done**: editable, live, CSV export |
| Properties panel | Pending: shared MainWindow wiring |
| Plan / 3D / elevation / section | No graphic effect yet; tags and view filters are later increments |
| Sheets | Values on sheets are stored (e.g. "Drawn by"); title block display pending |

## Persistence (.g3 schema 4)

`ParameterDefinitions` and `ParameterValues` arrays are added to the document
JSON. Migration 3 → 4 starts with empty catalogs. The round trip preserves
identifiers, specs, bindings, and values (see the automated checks).

## Undo/redo

Every operation is a named transaction: `Transaction.AddParameter`,
`UpdateParameter`, `RemoveParameter`, `SetParameterValue`, `SetParameterValues`.

## Evidence

| Statement | Class | Source |
|-----------|-------|--------|
| Revit separates `Definition`/`InternalDefinition`/`ExternalDefinition` from `Parameter` values | Observed | `RevitAPI.xml` type docs |
| Bindings are `InstanceBinding` or `TypeBinding` over a `CategorySet`, held in the document `BindingMap` | Observed | `RevitAPI.xml` type docs |
| Value storage is one of Integer, Double, String, ElementId (`StorageType`) | Observed | `RevitAPI.xml` field docs |
| Data types are identified by `SpecTypeId`/`ForgeTypeId`; groups by `GroupTypeId` | Observed | `RevitAPI.xml` type docs |
| `Parameter.SetValueString` parses unit-formatted input; `AsValueString` formats it | Observed | `RevitAPI.xml` member docs |
| `Parameter.SetMultiple` sets several parameters together | Observed | `RevitAPI.xml` member docs |
| Shared parameters carry a stable GUID (`Parameter.GUID`, `IsShared`) | Observed | `RevitAPI.xml` member docs |
| Revit drops values when a category leaves a binding | Inferred | Consistent with binding semantics; to verify by behavior test on an independent sample model |
| Internal value storage layout inside RVT | Unknown | Not required; RVT is treated as an unsupported format |

## Guanacaste 3D design (original) and improvements

- **One concept instead of three.** Revit splits user parameters into project,
  shared, and global parameters. Here, one definition with a stable GUID covers
  project and shared use. A future definitions file only needs to export them.
- **Schedules are the bulk editor.** Fill-down paste and delete over a
  multi-cell selection commit as one named transaction, so a whole column can
  be undone in one step.
- **Culture-tolerant input.** "2,5 m" and "2.5" both parse in a Spanish UI;
  thousands separators are rejected so "2.5" can never silently become 25.

## Acceptance tests

Automated (25 checks in `parameter-checks.cs`): binding rules, unique names,
type propagation, integer rejection, bulk edits as one step, regeneration on
delete, undo restores values, binding narrowing, definition removal, CSV
header/content, parsing in es-ES, schema-4 round trip, 3 → 4 migration, and
rejection of orphaned values on open.

Manual:

1. Open a real `.g3` file, open the manager, create "Acabado" (Text, Instance,
   Walls + Rooms), and fill three wall cells with Ctrl+V. Undo once and confirm
   all three clear.
2. Create "Resistencia al fuego" (Integer, Type, Walls). Set it on one wall and
   confirm every wall of the same type shows the value.
3. Save, close, reopen, and confirm the values persist.

## Defects found during installed-build validation (fixed)

| Defect | Cause | Fix | Regression coverage |
|--------|-------|-----|---------------------|
| Manager always opened on Sheets | The sheet shown in the workspace was treated as a user selection | `SelectedCategory` ignores display-only sheet state | UI Automation script: initial category is Walls with nothing selected |
| Unreadable status text | Status bar used the dark chrome brush | Panel brush | Screenshot review |
| Type marker glued to name ("fuego· tipo") | XAML trimmed the leading space of the resource | `xml:space="preserve"` | UI Automation header read-back |
| Install left old binaries yet reported success | `Stop-Process` returned before DLL handles closed; `Copy-Item` errors were non-terminating | `Wait-Process`, `-ErrorAction Stop`, bounded retry | Reinstall over a running instance; hashes compared |

## Interactive validation (installed 0.4.0, real `.g3`)

Driven through Windows UI Automation against
`artifacts/validation/parameters-smoke.g3`: the manager opens on Walls;
setting 60 on one "Interior 100 mm" wall shows on both walls of that type;
"7,5" is rejected for an Integer column; Ctrl+V into three cells commits one
"Cambiar valores de parámetro" step, which one Undo fully reverts; the Undo
tooltip shows the transaction name; the main window is marked dirty.

## Pending

Formulas and calculated values, global parameters, a shared definitions
file, the properties-panel editor, view filters by parameter, tags, title
block fields, and IFC property sets.
