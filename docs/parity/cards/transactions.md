# Capability card: transactional model and regeneration

## User-visible workflow

- Every model edit is one named step. The Undo and Redo controls report which
  step they affect (for example "Deshacer: Crear muro").
- Compound operations, such as drawing a closed room outline or pasting one
  value into many schedule cells, appear as a single undo step.
- An edit that would leave the model invalid is rejected as a whole; the user
  sees why, and nothing in the model changes.

## Domain entities and relations

| Entity | Role |
|--------|------|
| `DocumentSession` | Owns the active `BuildingDocument`, its undo/redo stacks, and the open group |
| `HistoryEntry(Name, Snapshot)` | One undoable step: the localization key and the state before the step |
| `TransactionGroup` | Scope that aggregates inner transactions; `Assimilate` merges, `RollBack`/`Dispose` reverts |

## Rules

- A transaction runs on a cloned document: mutate → regenerate → validate →
  swap. Validation failure leaves the active document untouched.
- Groups cannot nest; undo and redo are unavailable while a group is open.
- History is bounded to 40 entries; trimming is deferred while a group is open
  so the group's start depth stays valid.
- Transaction names are localization keys (`Transaction.*`). The core never
  contains display text.

## Dependencies and regeneration

`Regenerate` runs inside every transaction, before validation. Increment 1
registers one rule: drop parameter values whose definition, owner element, or
category binding no longer exists. Future rules (wall joins, hosted geometry,
room boundaries) plug into the same step, which keeps one invariant: a committed
document is always regenerated and valid.

## Representations

Not a geometric capability. Effects show in every view because every view
re-reads the committed document after `Changed`.

## Persistence

History is session state and is not written to `.g3`, matching the local-first
scope of this increment. A persisted change journal is a later increment (area 16).

## Undo/redo

Snapshot-based: each entry holds an immutable prior document. Records make
cloning cheap; nested lists are copied defensively in `BuildingDocument.Clone`.

## Evidence

| Statement | Class | Source |
|-----------|-------|--------|
| Revit exposes `Transaction`, `SubTransaction`, and `TransactionGroup` with `Start(name)`, `Commit`, `RollBack`, `GetStatus` | Observed | `RevitAPI.xml` member docs |
| `TransactionGroup.Assimilate` "merges inner transactions into a single undo item" | Observed | `RevitAPI.xml`, `M:Autodesk.Revit.DB.TransactionGroup.Assimilate` |
| `TransactionStatus` has Uninitialized, Started, RolledBack, Committed, Pending, Error, Proceed | Observed | `RevitAPI.xml` field docs |
| Failure handling is configurable per transaction (`FailureHandlingOptions`, `IFailuresPreprocessor`) | Observed | `RevitAPI.xml` type docs |
| Revit regenerates dependent elements when a transaction commits | Derived | Public API docs describe `Document.Regenerate` and updaters (`IUpdater`) triggered by commits |
| Revit's internal undo storage is snapshot-based or delta-based | Unknown | Not observable through the public API; Guanacaste's choice does not depend on it |

## Guanacaste 3D design (original)

Guanacaste 3D replaces the API's explicit Start/Commit protocol with a single
`Commit(name, action)` primitive, so a transaction can never be left open or
half-applied by UI code. Groups are the only explicit scope, and `using` makes
rollback the default when an exception escapes.

**Why this is clearer:** callers cannot forget to commit or roll back, and
validation is centralized instead of each command checking its own invariants.

## Acceptance tests

Automated ([parameter-checks.cs](../../../tests/Guanacaste3D.Checks/parameter-checks.cs)):

- Names recorded in undo order; undo moves the name to redo.
- Assimilated group is one undo step; undo/redo apply the whole group.
- Disposed group rolls back; failing inner step plus `RollBack` restores start state.
- Nested groups rejected; undo disabled while a group is open.

Manual: in the parameter manager, edit a cell, hover over Undo to read the
transaction name, undo, and confirm that the plan view and schedule update.

## Known, inferred, pending

- Pending: failure severity (warning vs. error) with user resolution, like Revit's failure dialog.
- Pending: sub-transactions for tool previews.
- Pending: the main-window ↶/↷ tooltips show names after the shared MainWindow wiring lands.
