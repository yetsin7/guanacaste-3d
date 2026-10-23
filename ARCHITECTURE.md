# Architecture

Guanacaste 3D is a C# / .NET 10 solution with a strict split between a
UI-independent core and a WPF shell.

```
Guanacaste3D.slnx
├─ src/Guanacaste3D.Core        net10.0, no UI dependencies
│  ├─ BuildingDocument.cs       source of truth: levels, walls, openings, types,
│  │                            rooms, floors, sheets, views (+ parameters, schema 4)
│  ├─ DocumentSession.cs        validated, atomic edits + bounded undo/redo
│  ├─ ProjectFile.cs            .g3 envelope ("G3-PROJECT/1\n" + JSON), migration,
│  │                            atomic save with .bak
│  ├─ WallOpenings.cs           hosted-opening rules and wall solid segmentation
│  ├─ QuantitySchedule.cs, project-schedules.cs   takeoffs and schedules
│  ├─ dxf-exporter.cs           DXF R12 plan export
│  ├─ sample-projects.cs        demonstration house built through DocumentSession
│  ├─ geometry/                 polygon triangulation, BIM → triangle mesh
│  └─ rendering/                BVH (binned SAH) + progressive path tracer
├─ src/Guanacaste3D.Desktop     net10.0-windows, WPF
│  ├─ MainWindow.xaml(.cs)      shell; split into main-window-*.cs partials
│  ├─ scene-presentation.cs     maps the Z-up core mesh into WPF Y-up 3D
│  ├─ main-window-raytrace.cs   orbit camera and the Render tab
│  └─ resources/                theme and Spanish localization dictionaries
└─ tests/Guanacaste3D.Checks    executable regression suite (top-level statements)
```

## Rules

1. **One model, many views.** Plan, 3D, elevation, section, schedule, sheet,
   DXF and the render all derive from `BuildingDocument`. No view stores its
   own copy of geometry.
2. **Every mutation goes through `DocumentSession`.** It clones, applies,
   validates and only then commits, so invalid geometry never enters the
   document and undo is snapshot-based.
3. **Geometry is computed once in Core.** `BuildingMesher` produces the
   triangles used by both the WPF viewport and the path tracer, so what the
   user orbits is what gets rendered.
4. **Coordinates:** model space is metric, Z-up, +Y = project north. WPF uses
   (x, z, −y), which is a proper rotation and preserves winding.
5. **Files are validated before they are accepted.** Unknown schema versions
   are rejected; older ones are migrated in `ProjectFile.Load`.

## Rendering pipeline

`BuildingDocument` → `BuildingMesher.Build` (walls split around openings,
glazing panes, ear-clipped slabs, ground) → `Bvh` (binned SAH, flat nodes,
two-sided Möller–Trumbore) → `PathTracer` (cosine-weighted diffuse bounces,
next-event estimation to a finite sun disk for soft shadows, thin-glass
Fresnel, analytic sky, Russian roulette, ACES tone mapping) → BGRA32 bytes →
WPF `WriteableBitmap`. Each pass adds one sample per pixel on all cores but
one, so the UI stays responsive and the image refines progressively.
