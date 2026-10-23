# Revit-Inspired Functional Parity Roadmap

## Product objective

Guanacaste 3D should become an original, local-first BIM authoring application
that supports the professional workflows users expect from a modern parametric
building modeler. Functional parity is the target; proprietary Autodesk source
code, assets, icons, internal file formats, and undocumented implementation
details are not.

The product must use its own interaction design, domain model, file format, and
rendering implementation. Public Autodesk documentation, the supported Revit
API, open standards, and independently created test models are acceptable
sources for requirements and interoperability tests.

## Verified baseline

The current `0.3.0` preview provides:

- A native Windows application built with WPF and .NET 10.
- One document model shared by plan and 3D views.
- Levels, reusable wall types, straight walls, hosted openings, rooms, floor
  slabs, numbered sheets, selection, property editing, and bounded undo and redo.
- Versioned JSON persistence with validation and backup-before-overwrite.
- A live project schedule, sheet workspace, CSV quantity export, schema-one
  migration, and automated checks for coordinated document behavior.
- A native `.g3` project envelope, Windows file association, generated north
  elevation, and generated section view.

Version `0.4.0` adds named transactions, transaction groups with a
regeneration step, project parameters (instance and type bindings, nine data
types, seven categories) with editable per-category schedules and CSV export,
and `.g3` schema 4 with migration. The per-capability record lives in
[parity/capability-catalog.md](parity/capability-catalog.md).

It does not yet provide production geometry, complete doors or windows,
families, constraints, sections, elevations, view placement, collaboration,
structural design, MEP systems, or professional exchange formats.

## Public workflow baseline

The following public Revit 2027 concepts define workflow requirements without
dictating Guanacaste 3D's implementation:

- A project browser organizes views, schedules, sheets, families, groups, and
  links in a navigable hierarchy.
- A properties surface edits instance parameters and provides a separate path
  to type parameters.
- Families group elements with shared parameters and multiple types.
- Views are generated from one parametric model so changes propagate across
  plans, sections, elevations, schedules, and sheets.
- The supported Revit API exposes documented concepts for geometry, views,
  parameters, transactions, events, architecture, structure, and MEP.
- Revit 2027 uses .NET 10 and includes public API changes and IFC parameter
  mapping improvements.

Official references:

- <https://help.autodesk.com/cloudhelp/2027/ENU/Revit-WhatsNew/files/GUID-C81929D7-02CB-4BF7-A637-9B98EC9EB38B.htm>
- <https://help.autodesk.com/view/RVT/2027/ENU/?guid=GUID-C8D3E5A6-02A5-43A9-AFFC-D49DD27398B1>
- <https://help.autodesk.com/cloudhelp/2027/ENU/Revit-GetStarted/files/GUID-A764EA7A-FE26-469B-857C-F3A70812FC34.htm>
- <https://help.autodesk.com/cloudhelp/2027/ENU/Revit-Model/files/GUID-D927E0DC-F720-4F7D-AACD-8B06787793CB.htm>
- <https://help.autodesk.com/cloudhelp/2027/ENU/Revit-API/files/Revit_API_Revit_API_Developers_Guide_html.html>

## REA research boundary

REA can inspect ordinary .NET PE/CLI assemblies statically without loading or
executing them. Its managed-code tools expose assembly identity, metadata,
types, signatures, CIL, declared native boundaries, and evidence-backed
comparisons. That is useful for compatibility research against software the
analyst is authorized to inspect.

REA does not establish native runtime behavior from managed metadata, recover
the proprietary Revit model engine, or make RVT and RFA compatibility available.
Native analysis requires a separately installed provider such as Ghidra, Hopper,
or IDA and must remain read-only and evidence-scoped. Guanacaste 3D must not use
analysis results to copy protected source, proprietary assets, or undocumented
formats.

## Delivery sequence

### Phase 1: Reliable authoring core

- Replace preview geometry with a tested geometry kernel abstraction.
- Add wall joins, layers, materials, hosted object orientation, and complete
  door and window geometry.
- Introduce stable element references, multi-selection, snapping, alignment,
  copy, mirror, array, and constraint primitives.
- Persist all model mutations through versioned commands and transactions.

### Phase 2: Parametric types and families

- Separate instance parameters from type parameters.
- Add categories, materials, formulas, shared parameter definitions, and typed
  units.
- Build an original family editor with reference planes, constraints, and
  reusable component types.
- Validate dependency cycles and make regeneration deterministic.

### Phase 3: Coordinated views and documentation

- Generate plans, reflected ceiling plans, elevations, sections, callouts, and
  detail views from the shared model.
- Add associative dimensions, tags, schedules, rooms, areas, and legends.
- Implement ISO sheet sizes, title blocks, view placement, scale, crop regions,
  and print and PDF parity.
- Ensure one edit updates every affected view and schedule in the same
  transaction.

### Phase 4: Building systems

- Add floors, roofs, ceilings, stairs, railings, terrain, and curtain systems.
- Add structural framing, analytical elements, reinforcement, and load-bearing
  metadata without claiming engineering certification.
- Add MEP connectors, systems, routing, equipment, and discipline-specific
  calculations with explicit validation boundaries.

### Phase 5: Exchange and coordination

- Implement IFC import and export against published buildingSMART schemas and
  independent round-trip fixtures.
- Add documented DXF, PDF, image, and quantity exports.
- Treat RVT and RFA as unsupported unless Autodesk provides a licensed,
  documented interoperability route.
- Add linked-model review, clash workflows, issue tracking, and model comparison.

### Phase 6: Scale, extensibility, and collaboration

- Add background regeneration, spatial indexes, level-of-detail rendering, and
  performance budgets for large projects.
- Publish a stable plugin API around documented Guanacaste 3D contracts.
- Add local history, branchable changes, permissions, locking, conflict
  resolution, and recoverable collaboration transactions.

## Acceptance gates

Each phase is complete only when:

1. Domain behavior has deterministic automated tests.
2. Persistence round trips preserve identity, parameters, and geometry.
3. Plan, 3D, schedules, sheets, and exports agree on the same model state.
4. Invalid inputs fail without partially mutating the document.
5. Undo and redo restore the exact prior state.
6. Performance is measured with representative project sizes.
7. Windows packaging, installation, launch, and core interaction are verified.
8. Unsupported formats and professional-design limitations remain explicit.

