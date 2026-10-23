# Development status

Last updated: 2026-10-23. Version in development: 0.4.0 (csproj still says 0.3.0
until the parameters increment lands).

## Verified on Windows 11 (local)

| Area | State | Evidence |
|---|---|---|
| Build | `dotnet build Guanacaste3D.slnx -c Release`: 0 errors, 0 warnings | local build |
| Regression checks | `tests/Guanacaste3D.Checks`: all checks pass (domain, persistence, migration, DXF, geometry, BVH, path tracer, sample, parameters) | `dotnet run --project tests/Guanacaste3D.Checks -c Release` |
| App startup | Main window opens; sample project opens from *Archivo → Abrir proyecto de ejemplo* | UI Automation smoke test |
| Photorealistic render | Progressive CPU path tracer renders the sample house: 192 samples at 727×356 in ≈9 s | screenshot from the running app |
| 3D view | Orbit (drag) and zoom (wheel) on the shared mesher; concave slabs are correct | screenshot + mesh-volume check |
| DXF export | R12 ASCII, one layer group per level, deterministic | structural checks (no external CAD validator run yet) |

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
