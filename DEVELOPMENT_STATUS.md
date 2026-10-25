# Development status

Last updated: 2026-10-25. Version in development: 0.4.0 (csproj still says 0.3.0
until the parameters increment lands).


## Native documents and family-system foundation (2026-10-25)

- Official `.g3d` project container implemented with a typed header, schema version, document identity, GZip payload, SHA-256 verification, atomic replacement and `.bak` recovery copy.
- Previous `.g3` project files and `.g3d.json` imports remain supported. New Save As defaults to `.g3d`.
- `.g3df` family persistence and `.g3dt` project-template persistence implemented in Core.
- Universal family domain model added: categories, hosting behavior, type/instance/shared/calculated parameters, work planes, operation dependency graph, nested-family references and cycle rejection.
- Built-in parametric foundations added for the architectural door, mortise lock and sliding window, including the requested default door dimensions, grille/lock/hinge operation descriptors and nested hardware references.
- Metric architecture template foundation can instantiate an independent `BuildingDocument`.
- Regression checks cover round trips, content-signature detection, corruption rejection, wrong-document-kind rejection, family defaults, template independence and circular-dependency rejection.

**Not yet claimed complete:** exact B-Rep evaluation of family operations, the WPF family editor, wall-host placement of `.g3df` instances, and direct desktop editing of `.g3dt` remain subsequent implementation stages.

## Verified on Windows 11 (local)

| Area | State | Evidence |
|---|---|---|
| Build | `dotnet build Guanacaste3D.slnx -c Release`: 0 errors, 0 warnings | local build |
| Regression checks | `tests/Guanacaste3D.Checks`: all checks pass (domain, persistence, migration, DXF, geometry, BVH, path tracer, sample, parameters) | `dotnet run --project tests/Guanacaste3D.Checks -c Release` |
| App startup | Main window opens; sample project opens from *Archivo → Abrir proyecto de ejemplo* | UI Automation smoke test |
| Photorealistic render | Progressive CPU path tracer renders the sample house: 192 samples at 727×356 in ≈9 s | screenshot from the running app |
| 3D view | Orbit (drag) and zoom (wheel) on the shared mesher; concave slabs are correct | screenshot + mesh-volume check |
| DXF export | R12 ASCII, one layer group per level, deterministic | structural checks (no external CAD validator run yet) |
| IFC4 export | Storeys, walls, voids/fills (doors, windows), slabs, spaces, materials; deterministic GlobalIds | STEP structure, reference resolution and IFC4 attribute-count checks (IfcOpenShell not run yet) |
| OBJ/GLB export | Shared mesh in Y-up meters with materials | OBJ index/material checks, GLB chunk/accessor checks |
| Denoiser | Indirect light filtered with an edge-aware à-trous filter; at 4 spp: indirect error −47 %, total −31 % vs a 256-spp reference | `denoiser-checks.cs`, render screenshot after 2 s |

## Not verified / blocked

- **GitHub Actions does not run**: the account's Actions budget is exhausted
  ("The job was not started because an Actions budget is preventing further
  use"). The workflow is correct as far as local runs show; it cannot be
  confirmed on CI until the owner raises or resets the budget.
- **Self-contained portable package**: NuGet downloads of the win-x64 runtime
  packs run at ~21 KB/s on this machine; publishing with
  `scripts/verify-windows.ps1 -FrameworkDependent` avoids the download.
- DXF output has not been opened in an independent CAD program yet.

## Parallel work

A second session is building the typed parameter system (schema 4,
`src/Guanacaste3D.Core/parameters/`, `transactions/`, `docs/parity/`).
See `docs/parity/README.md`.
