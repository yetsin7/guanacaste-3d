# Guanacaste 3D Roadmap

## Vision

Build an original desktop BIM product with one parametric model that coordinates
geometry, plans, elevations, sections, sheets, schedules, and cost information.

## Stage 0: Native foundation

- [x] C# and .NET 10 solution with a WPF desktop application.
- [x] Versioned project document, levels, straight walls, and hosted openings.
- [x] Coordinated plan and 3D geometry.
- [x] Generated north elevation and section views from the shared model.
- [x] Native `.g3` project envelope and Windows file association.
- [x] Validated persistence with backup-before-overwrite.
- [x] Bounded undo and redo history.
- [x] Local Release build and executable domain checks on Windows.
- [x] Local application installation and launch verification.
- [ ] Repeatable visual-interaction automation for native WPF controls.

## Stage 1: CAD and BIM authoring core

- [ ] Production geometry abstraction for intersections, joins, and solids.
- [x] Reusable wall types with coordinated instance thickness.
- [x] Parametric hosted openings with net volume and coordinated 3D voids.
- [x] Rooms and floor slabs with calculated areas.
- [ ] Complete door and window objects, families, materials, and orientation.
- [ ] Associative constraints, dimensions, references, and transformations.
- [ ] Multi-selection, copy, align, mirror, arrays, and richer modification tools.

## Stage 2: Coordinated documentation

- [x] Live multi-category project schedule.
- [x] Numbered ISO drawing sheets with a live plan preview.
- [x] Initial generated elevations and sections.
- [ ] Details, crop regions, multiple view directions, and scale-aware view placement.
- [ ] Tags, associative dimensions, legends, title-block families, and PDF output.
- [ ] IFC and DXF exchange validated with independent round-trip fixtures.

## Stage 3: Disciplines and collaboration

- [ ] Structural and MEP authoring tools with explicit engineering boundaries.
- [ ] Phases, design options, linked models, and coordination workflows.
- [ ] Transactional collaboration, locking, conflict handling, and recovery.
- [ ] Stable plugin API and extension contracts.

## Quality conditions

1. Keep the domain model independent from WPF and rendering.
2. Validate all external project documents before accepting state.
3. Do not claim compatibility with formats that are not implemented and tested.
4. Do not use third-party proprietary branding, source code, icons, or assets.
5. Measure performance with representative large models before choosing the
   final geometry and rendering engines.
