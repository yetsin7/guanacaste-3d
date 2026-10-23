# Roadmap

The detailed staged plan is in [docs/roadmap.md](docs/roadmap.md), and the
capability-by-capability parity catalog is in [docs/parity/](docs/parity/).
Short-term work is in [NEXT_TASKS.md](NEXT_TASKS.md).

## Milestones

| Milestone | Scope | State |
|---|---|---|
| 0.3 | Walls, openings, wall types, rooms, floors, sheets, views, schedules, `.g3` | done |
| 0.4 | DXF export, shared mesher, path-traced render, orbit 3D, sample project, typed parameters | in progress |
| 0.5 | IFC4 export/import, roofs, ceilings, materials in render, denoiser | next |
| 0.6 | Columns, beams, wall joins, associative dimensions, PDF sheets | planned |
| 0.7 | Families (parametric components), OBJ/glTF export, SKP import via SDK | planned |
| 0.8 | GPU rendering (DirectX Raytracing via Vortice.Windows), large-model performance | planned |
| 1.0 | Installer, stability on representative projects, user manual | planned |

## Principles

- Functional parity with professional BIM tools comes from original code,
  public documentation and open standards, never from proprietary code,
  binaries or assets.
- Nothing is marked done without an automated check or a recorded manual
  verification.
