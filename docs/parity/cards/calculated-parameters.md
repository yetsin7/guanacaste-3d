# Capability card: calculated parameters (formulas)

## User-visible workflow

1. In **Parámetros del proyecto**, enter a formula when creating or editing a
   definition. The help line lists the names available for the checked
   category: model quantities (Ancho, Alto, Antepecho, Longitud, Espesor,
   Volumen, Área, Elevación) and the other parameters bound to it.
2. **Aplicar** validates syntax, names, units, the result type, and dependency
   cycles. An invalid formula is rejected with a Spanish reason that includes
   the character position for syntax errors.
3. Calculated columns are marked "· fórmula" and are read-only. They update on
   every model change (for example a door type resize) and undo/redo.

## Language

- Operators: `+ - * / ^`, comparisons `= <> < <= > >=`, `and`, `or`, `not`.
- Functions: `if(c, a, b)`, `and(...)`, `or(...)`, `not(x)`, `abs`,
  `round/roundup/rounddown(x[, step])`, `min`, `max`, `sqrt`, `sin`, `cos`,
  `tan`, `asin`, `acos`, `atan` (degrees), `ln`, `log`, `exp`, `pi()`.
- References: bare names (`Width`, `Ancho`) or bracketed names with spaces
  (`[Unit cost]`). Parameter names shadow quantity names.
- Literals: invariant numbers (`2.5`) with optional metric units (`50 cm`,
  `2 mm`, `1 m²`, `0.5 m3`), and quoted text (`"Grande"`).

## Domain and rules

| Rule | Behavior |
|------|----------|
| Dimensional analysis | Every number carries a power of length; `+ - < =` need equal powers, `*` adds, `/` subtracts, `sqrt` halves even powers, and `^` needs a whole-number literal for dimensioned bases |
| Result type | Must match the spec: Length = L¹, Area = L², Volume = L³, Number/Integer/Currency/Angle = L⁰, Yes/No = boolean, Text = text |
| Binding | Calculated parameters are instance-bound (values are per element) |
| Storage | Never stored; stored values for a calculated definition are invalid in files and pruned in sessions |
| Cycles | Rejected via a depth-first search on the reference graph |
| Renames | Renaming a parameter that a formula uses fails validation and leaves the document unchanged |
| Runtime | Division by zero, sqrt/ln of invalid input, or a missing input give an empty cell, never a wrong value; `if()` evaluates only the chosen branch |
| Integer results | Rounded half away from zero; results outside the spec range (negative area) are empty |

## Persistence (.g3 schema 6)

`ParameterDefinition.Formula` (string). Migration 5 → 6 changes no data; the
version bump makes older builds refuse files whose formulas they would
otherwise drop silently.

## Evidence

| Statement | Class | Source |
|-----------|-------|--------|
| Revit family parameters support formulas (`FamilyManager.SetFormula`, `FamilyParameter.Formula`) | Observed | `RevitAPI.xml` member docs for `FamilyManager` and `FamilyParameter` |
| Schedules support calculated fields (`ScheduleField`, `ScheduleFieldType`) | Observed | `RevitAPI.xml` type docs |
| Revit checks unit consistency in formulas | Inferred | Documented user-facing behavior; not observable via API metadata. To verify with an independent test family |
| Revit's formula engine internals | Unknown | Not needed; Guanacaste's language is original |

## Guanacaste 3D improvements over the studied workflow

- Formulas work on **project parameters** of any category, not only inside
  family definitions or a single schedule.
- **Unit literals** (`50 cm`, `1 m²`) keep units explicit, so
  `[Unit cost] * [Leaf area] / 1 m²` is a currency, not an area.
- `round(x, step)` rounds to any increment (`round(Longitud, 5 cm)`).
- Spanish names for quantities alongside English ones.

## Acceptance tests

Automated (`formula-checks.cs`, 19 checks): unit algebra, unit rejection,
comparison and `if()` typing, syntax positions, unknown names, unit literals,
rounding with step, precedence and trigonometry, tolerant equality, undefined
results, lazy `if()`, dependency chains with Spanish aliases, read-only
columns, edit rejection, result-type mismatch, cycle rejection, rename
protection, model-driven recalculation, stored-to-calculated regeneration, and
schema-6 round trip.

Interactive (installed 0.6.0, real `.g3`): `Ancho + Alto * Alto` is rejected
for inconsistent units, `Ancho * Alot` for an unknown name, `Ancho * (Alto`
reports "se esperaba «)» (carácter 14)", and `Ancho * Alto` yields 1.89 m² for
900 × 2100 doors and 1.68 m² for 800 × 2100 doors.

## Pending

Formulas in type parameters and family geometry, text concatenation,
conditional formatting in schedules, schedule totals and grouping, and
formula autocompletion.
