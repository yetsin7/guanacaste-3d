# Development status

Last updated: 2026-10-27. Version: 0.21.0.

## Water storage, solar power, icon ribbon and area table (0.21, 2026-10-27)

- Cistern with an electric pump: the house's pressure depends on the pump's circuit in real time. Elevated tank with
  gravity pressure.
- Photovoltaic arrays on roof slopes and grid-tie inverters:
  - yield and DC/AC ratio;
  - anti-islanding;
  - 120 % backfeed rule.
- Icon-only ribbon with named, animated tooltips; floor hatching by finish; area table on sheets.

## X-ray view, septic tank and storm drainage (0.20, 2026-10-27)

- X-ray 3D view: architecture as faint glass, building services solid; the mesher records service triangle ranges.
- Septic tank (NBR 7229) with drain field sized by infiltration, as a sanitary outlet; modelled below grade.
- Storm drainage:
  - roof runoff by the rational method;
  - downspouts placed automatically and checked against their leader capacity;
  - buried drains sized by Manning at 1 % to a storm outlet.

## Traps and vents, minisplit air conditioning (0.19, 2026-10-27)

- P-traps and standpipes modelled per fixture.
- Vent stacks: sized by DFU, through the roof, added automatically where a trap has none in reach, with the IPC
  909.1 trap-arm check.
- Minisplit air conditioners:
  - sized for their room;
  - outdoor condenser and line set placed outside the wall;
  - capacity steps;
  - 240 V circuits with a wrong-voltage fault.
- Fix: buried pipes no longer lower the render's ground plane and lift the house out of it.

## Plumbing systems and ceiling fans (0.18, 2026-10-27)

- Plumbing: water meter and sewer outlet; cold, hot and sanitary systems connected by hand or automatically;
  tree routing along the axes.
  - Supply: sized by Hunter's curve and velocity, with flowing pressure per fixture (Hazen–Williams) and the wait
    for hot water.
  - Drains: sized by DFU at 2 % slope from the outlet invert, with a cover check.
  - Views: plan drawing by system, plumbing schedule, 3D pipes.
  - Project schema 14.
- Ceiling fan with light kit as a switchable, dimmable electrical device.
- Dryer and microwave families have shapes of their own (a dryer needs no water).

## More families, component rotation and dimmed lights (0.17, 2026-10-26)

- 27 new furniture and fixture families (sectional, round tables, office chair, bar stool, bunk bed, crib, sink cabinet,
  range hood, pedestal sink, double vanity, bidet, urinal, mirrors, water heater, hammock, sun lounger, umbrella...).
- Components turn about their insertion point (SPACE while placing or on the selection, *Rotar*), saved and undoable.
- Dimmers dim their lights in the render, not only on/off; mirrors render as silvered glass.

## Electrical design and an expanded construction catalog (0.16, 2026-10-26)

- Electrical system: receptacles (standard, GFCI, USB, 240 V), switches (single-pole, three-way, dimmer), luminaires
  (ceiling, downlight, sconce, pendant) and panels; switch links, circuits and breakers, live load/current/utilization,
  breaker and main trips, NEC conductor sizing and voltage drop; panel schedule window; lights on or off in plan, 3D and
  render. Project schema 12.
- Catalog: about thirty wall types by construction (gypsum, glass, block, brick, concrete, stone, timber, metal), door
  leaf styles (flush, six-panel, louvered, frameless glass, security) and windows with exterior security bars, all
  meshed with their own materials.

## Revit-style wall drawing, site planting and a photographic render (0.10–0.11, 2026-10-26)

- Wall chains close on their first point (30 px magnet, joined corner also with face location lines); Revit snap
  order with nearest-on-wall, angular and alignment snapping; SHIFT ortho, SPACE flip; Spanish default names.
- Site planting: five procedural species (broadleaf, guanacaste, palm, cypress, shrub), plan symbols, library
  category, SPACE to change species, exact height and spread; smooth vertex normals in `SceneMesh`.
- Render: procedural relief, new lawn, aerial perspective, whitened horizon, procedural clouds, AgX tone mapping,
  white balance, and denoising of rough reflections and window panes.


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
| Model import | OBJ, DXF, DWG (via AutoCAD/ODA), STL, IFC, glTF, COLLADA, SKP (via the SketchUp SDK); Revit files show version and preview and point to IFC. Drag and drop, linked models saved beside the project | `import-checks.cs` incl. real files: 450 MB Revit OBJ in ≈4 s; IFC volume equals IfcOpenShell's; SketchUp sample through the real SDK |
| Live render | Navigable ray-traced preview (Lumion-style controls, shared camera), final render with pause/stop, walk-through to PNG frames, memory watchdog keeps the image if memory runs short | `navigation-checks.cs`, `desktop-interaction-checks` (live preview, camera, import, links) |
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
