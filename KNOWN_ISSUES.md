# Known issues

| # | Severity | Issue | Notes |
|---|---|---|---|
| 1 | Blocker (CI) | GitHub Actions jobs never start: the account's Actions budget is exhausted. | Owner action: GitHub → Settings → Billing → Actions budget. |
| 2 | High | Remote branch `feat/dxf-orbit-quality-20261023` (draft PR #1) diverged from `main` with 67 commits and conflicts in 8 files. | Its DXF exporter, CI changes and verify script were ported to `main`; its `ModelAudit` snapshot and the old `Slab` model were not. Close the PR or port the rest by hand. |
| 3 | Medium | The self-contained publish downloads ~70 MB of runtime packs; very slow on this network. | Use `scripts/verify-windows.ps1 -FrameworkDependent` locally. |
| 4 | Medium | The title bar overlaps "Guanacaste 3D" and the tagline. | In MainWindow.xaml the right-docked tagline must come before the centered title. |
| 5 | Medium | The render has no roofs, materials per type or interior lights; doors render as empty openings. | Next render milestones in NEXT_TASKS.md. |
| 6 | Medium | The DXF export has not been validated in an independent CAD program. | Open it in LibreCAD/QCAD or with ezdxf's auditor. |
| 7 | Low | View and level names created by the core are English ("Level 1 Plan"). | Localize at creation or display time. |
| 8 | Info | Importing RVT/RFA is not possible without Autodesk software or a licensed SDK. | Practical path: Revit exports IFC, then Guanacaste imports IFC. SKP: the official SketchUp C SDK (free, needs a Trimble developer account). |
