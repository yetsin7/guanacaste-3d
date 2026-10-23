# Revit functional parity program

This folder holds the working record of Guanacaste 3D's progress toward
functional parity with professional BIM workflows. It complements the strategic
[roadmap](../revit-functional-parity-roadmap.md).

| Document | Purpose |
|----------|---------|
| [capability-catalog.md](capability-catalog.md) | The 20 capability areas, API anchors, current status, and next step |
| [evidence-log.md](evidence-log.md) | Analyzed artifacts with hashes and versions; every evidence entry and its class |
| [rea-runs.md](rea-runs.md) | REA commands, versions, coverage, and limitations |
| [cards/](cards/) | One capability card per implemented capability |

## Working rules

1. Every increment is vertical. It covers the domain model, validation,
   regeneration, the `.g3` schema and migration, undo/redo, the UI, the
   localization dictionary, and automated checks.
2. Evidence is classified as Observed, Derived, Inferred, or Unknown, and an
   inference is never reported as an observation.
3. Designs are original. Autodesk material defines requirements, never the implementation.
4. After each increment: Release build, all checks, `.g3` round trip and
   migration, publish to `artifacts/win-x64`, install with
   `scripts/install-windows.ps1`, then verify version, hash, shortcut, file
   association, and window title against a real `.g3` file.
