# Known issues

| # | Severity | Issue | Notes |
|---|---|---|---|
| 1 | Blocker (CI) | GitHub Actions jobs never start: the account's Actions budget is exhausted. | Owner action: GitHub → Settings → Billing → Actions budget. |
| 2 | High | Remote branch `feat/dxf-orbit-quality-20261023` (draft PR #1) diverged from `main` with 67 commits and conflicts in 8 files. | Its DXF exporter, CI changes and verify script were ported to `main`; its `ModelAudit` snapshot and the old `Slab` model were not. Close the PR or port the rest by hand. |
| 3 | Medium | The self-contained publish downloads ~70 MB of runtime packs; very slow on this network. | Use `scripts/verify-windows.ps1 -FrameworkDependent` locally. |
| 4 | Medium | The title bar overlaps "Guanacaste 3D" and the tagline. | In MainWindow.xaml the right-docked tagline must come before the centered title. |
| 5 | Low | Image textures are assigned per surface category, not per wall or floor type (the 3D view previews them when the grey study model is off). HDRI skies are not supported. | Image textures since 0.12; see `docs/rendering-and-import.md`. |
| 6 | Medium | The DXF export has not been validated in an independent CAD program. | Open it in LibreCAD/QCAD or with ezdxf's auditor. |
| 7 | Resolved (0.10) | View and level names created by the core were English ("Level 1 Plan"). | New projects use *Nivel 1*, *{3D}*, *Sección 1*, *Tabla de cantidades*; untouched English view names are translated on open. Levels of older projects keep their names. |
| 8 | Info | RVT/RFA geometry cannot be read without Autodesk software (proprietary compressed database). | Implemented: the importer reads the Revit version, path and preview, and explains File → Export → IFC, which **is** imported (openings, colors, families). SKP and DWG are imported through the SketchUp SDK library and AutoCAD/ODA already installed on the computer. |
| 9 | Low | Linked CAD linework is kept but not drawn as an underlay in the plan view; 3D views of linked models are a reduced proxy (the render uses full detail). | Plan underlay is the next import task. |
