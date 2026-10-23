# Capability card: parametric levels and wall top constraints

## User-visible workflow

1. **Niveles…** lists the levels from top to bottom, each with its elevation,
   the walls it hosts, and the walls whose top is constrained to it.
2. Select a level to rename it or change its elevation. **Aplicar** commits one
   transaction: every wall constrained to the level regenerates its height,
   plan, 3D, elevation, section and schedules update, and a default plan view
   name ("Level 2 Plan") follows the rename.
3. **Unir los muros de este nivel al nivel superior** ties the top of every
   wall based on the level to the next level up, with an optional top offset
   (negative = below). **Liberar** releases them while keeping the current height.
4. **Nuevo nivel** adds a level 3 m above the highest one with a free name.
   **Eliminar** deletes an unused level and its plan views. Levels that host or
   constrain elements, and the last level, are protected.

## Domain entities and relations

| Entity | Fields | Notes |
|--------|--------|-------|
| `Level` | Id, Name, Elevation | Datum; elevation in meters from the project origin |
| `Wall.TopLevelId` | Guid? | Null = unconnected height (the previous behavior) |
| `Wall.TopOffset` | double, m | Must be 0 when unconnected; ±100 m |
| `LevelConstraints` | Apply / Validate / LevelAbove / CountDependents | Regeneration and rules |

## Rules

- Constrained height = top elevation + top offset − base elevation. It must be
  ≥ 0.1 m, and the stored height must match it (tolerance 5×10⁻⁷ m).
- A wall cannot be constrained to its own base level.
- Editing the height of a constrained wall keeps the constraint and adjusts
  the offset (an improvement over silently dropping the constraint).
- Level names are unique (case-insensitive) when renaming; new levels never
  reuse an existing name.

## Dependencies and regeneration

`Regenerate` runs `LevelConstraints.Apply` first, then opening-type sync, then
parameter pruning, then full validation. Lowering a level under a hosted
door's head therefore fails as a whole, with a localized reason.

## Representations

Every view, export, and schedule reads the synchronized `Wall.Height`. The
walls schedule adds "Restricción superior" and "Desfase superior" columns, and
formulas can use `TopOffset` (for example a yes/no "Parapet" = `TopOffset > 0`).

## Persistence (.g3 schema 7)

`TopLevelId` and `TopOffset` on walls. Migration 6 → 7 leaves existing walls
unconnected, so their geometry is unchanged.

## Undo/redo

Named transactions: `UpdateLevel`, `RemoveLevel`, `SetWallTopConstraint`,
`AttachWallsToLevelAbove`, `DetachWalls`, plus the existing `AddLevel`/`UpdateWall`.

## Evidence

| Statement | Class | Source |
|-----------|-------|--------|
| Walls expose "Top Constraint" (`WALL_HEIGHT_TYPE`), "Top Offset", "Base Constraint", "Base Offset", "Unconnected Height" | Observed | `RevitAPI.xml` `BuiltInParameter` field docs |
| `Level.Elevation` is readable and settable; `Level.Create(document, elevation)`; `FindAssociatedPlanViewId` links levels to plan views | Observed | `RevitAPI.xml` member docs |
| Changing a level elevation moves constrained elements | Derived | Top Constraint semantics plus settable elevation |
| How Revit schedules regeneration internally | Unknown | Native side of the C++/CLI boundary (REA R1, R3–R6) |

## Guanacaste 3D design (original) and improvements

- Constraint edits, level moves, and their consequences are one validated
  transaction. Impossible moves are refused before anything changes.
- Height edits keep the constraint by moving the offset.
- Bulk attach/detach per level in one undo step.

## Acceptance tests

Automated (`level-checks.cs`, 18 checks): attach derives height, negative
offset, height edit keeps constraint, self-constraint rejected, release keeps
height, level raise regenerates, undo, atomic rejection under a door,
unique names, plan view rename, protected deletion, unused deletion with
views, last-level protection, free names for new levels, schedule column,
formula over `TopOffset`, round trip, and 6 → 7 migration.

## Pending

Base offset and base constraint changes (needs coordinated geometry in the
mesher and exporters), attach-to-roof, level datums drawn in elevations with
drag editing, story heights above/below, and grids.
