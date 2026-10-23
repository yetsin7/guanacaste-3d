# Capability card: saved schedules (fields, filters, sorting, grouping, totals)

## User-visible workflow

1. **Tablas de planificación…** lists the saved schedules. Choose a category
   and press **Nuevo** to create "Tabla de Puertas" with identity columns and
   the category's quantities.
2. Choose the **fields** and their order (available → in the table, ↑/↓).
   Fields include identity columns, model quantities, and every project
   parameter bound to the category, including calculated ones.
3. Add up to three **filters** (all must match): equal, not equal, greater,
   greater or equal, less, less or equal, contains, does not contain, begins
   with, ends with, has value, has no value. Numbers accept "0,9" or "0.9".
4. **Sort and group** by up to two fields, ascending or descending, each with
   an optional group header and a footer with totals.
5. Clear **Detallar cada ejemplar** to merge identical rows into one row with a
   count (for example one row per door type). **Totales generales** adds a
   grand total row.
6. **Aplicar** commits one transaction and the live preview updates.
   **Exportar CSV…** writes the evaluated table, including row kinds.

## Domain

| Entity | Fields |
|--------|--------|
| `ScheduleDefinition` | Id, Name, Category, Fields, Filters, SortGroups, Itemize, GrandTotals |
| `ScheduleFilter` | FieldKey, Operator, Value (invariant text) |
| `ScheduleSortGroup` | FieldKey, Descending, GroupHeader, GroupFooter |
| `ScheduleTable` | Columns, Rows (Element, GroupHeader, GroupFooter, GrandTotal), ElementCount |

## Rules

- 1–50 unique fields, up to 8 filters, and up to 4 sort levels; sort fields
  must be in the table. Names are unique (case-insensitive).
- Field keys must exist for the category. **Invalid user input is rejected
  before the transaction.** Regeneration only prunes fields whose parameter was
  deleted or unbound by another edit, in the same transaction.
- Totals sum lengths, areas, volumes, currency, and plain numbers, never
  heights, widths, or elevations. Non-itemized rows sum the summable columns
  and show a value in other columns only when every merged row agrees.

## Persistence (.g3 schema 8)

`Schedules` array. Migration 7 → 8 adds an empty list. The round trip keeps
fields, filters, sorting, grouping, and options.

## Evidence

| Statement | Class | Source |
|-----------|-------|--------|
| Schedules define fields, filters, sort/group fields, headers/footers, footer counts, itemization, and grand totals | Observed | `RevitAPI.xml`: `ScheduleDefinition.AddField/AddFilter/AddSortGroupField`, `IsItemized`, `ShowGrandTotal`, `ShowGrandTotalCount`; `ScheduleSortGroupField.ShowHeader/ShowFooter/ShowFooterCount/ShowBlankLine/SortOrder` |
| Filter types include Equal, NotEqual, Greater(OrEqual), Less(OrEqual), Contains, NotContains, BeginsWith, NotBeginsWith, EndsWith, NotEndsWith, HasValue, HasNoValue, HasParameter | Observed | `RevitAPI.xml` `ScheduleFilterType` fields |
| Revit decides per field whether to total | Observed (partly) | `ScheduleField` exposes display options in the API; Guanacaste uses a unit-based rule, pending per-field choice |

## Guanacaste 3D improvements

- Filters and footers work on **calculated parameters** (formulas) as well.
- The unit-aware totals rule avoids meaningless sums by default.
- Field and filter problems are reported in Spanish before anything changes.

## Acceptance tests

Automated (`schedule-checks.cs`, 11 checks): non-itemized merging with counts,
grand-total count and currency sum, numeric filter in meters, group headers
and footers with length sums and no height sums, secondary sort inside
groups, case-insensitive text filter, regeneration when a parameter is
deleted, rejection of unknown fields, unique names, CSV, undo of duplication,
and `.g3` round trip. `localization-checks.cs` guards dictionary keys and the
Spanish coverage of transactions and Core messages.

## Pending

Placing schedules on sheets, per-field totals and formatting, conditional
formatting, blank lines between groups, editing values from the saved
schedule (today in the category table), and multi-category schedules.
