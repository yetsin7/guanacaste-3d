# Revit capability catalog and Guanacaste 3D parity status

This catalog lists the professional capability areas of Autodesk Revit 2027
and tracks Guanacaste 3D's equivalent. It describes requirements and observable
behavior only. Guanacaste 3D implements original designs; it does not reuse
Autodesk code, assets, icons, or undocumented formats.

Evidence classes used throughout (see [evidence-log.md](evidence-log.md)):

- **Observed**: read directly from an installed artifact, such as the shipped
  `RevitAPI.xml` documentation, assembly metadata, or a behavior test.
- **Derived**: follows from observed evidence through a stated reasoning step.
- **Inferred**: a plausible design reading that has not been verified.
- **Unknown**: not established; the stated route is how it would be verified.

Status values: **Done** (vertical slice shipped and tested), **Partial**,
**Planned** (has a card and a next step), **Not started**.

| # | Area | Revit concepts (API anchor types, observed in RevitAPI.xml / RevitAPIUI.xml) | Guanacaste 3D status | Next step |
|---|------|---------------------------------------------------------------|----------------------|-----------|
| 1 | Interface, ribbon, browser, properties | `UI.RibbonPanel`, `UI.DockablePane`, `UI.Selection.Selection`, `UI.UIApplication` | Partial: ribbon tabs, project browser, properties panel, Spanish localization | Typed property grid driven by parameter definitions |
| 2 | Transactional model and regeneration | `Transaction`, `SubTransaction`, `TransactionGroup`, `FailuresAccessor`, `IUpdater`, `UpdaterRegistry` | **Done (increment 1)**: named transactions, groups with assimilate/rollback, regeneration hook. [Card](cards/transactions.md) | Failure messages with severity; dynamic updaters |
| 3 | Selection, filters, references, constraints, dimensions | `FilteredElementCollector`, `ElementCategoryFilter`, `ElementParameterFilter`, `BoundingBoxIntersectsFilter`, `Reference`, `Dimension` | Partial: single selection, numeric move/rotate | Stable references to wall faces/ends; locked dimension constraints |
| 4 | Levels, grids, reference planes, coordinates | `Level`, `Grid`, `ReferencePlane`, `BasePoint`, `ProjectLocation`, `SiteLocation` | Partial: **parametric levels with wall top constraints and regeneration (increment 4)** [card](cards/levels-and-constraints.md) | Grids and reference planes as datums that hosts can attach to |
| 5 | Walls, floors, roofs, ceilings, rooms, areas | `Wall`, `Floor`, `RoofBase`, `Ceiling`, `Architecture.Room`, `Area`, `CompoundStructure` | Partial: straight walls, wall types, polygon floors, rectangular rooms | Layered compound structures and wall joins |
| 6 | Doors, windows, components, families and types | `Family`, `FamilySymbol`, `FamilyInstance`, `FamilyManager`, `FamilyParameter`, `ElementType` | Partial: wall types; **door/window types with families, type parameters driving every instance, duplicate/purge (increment 2)** [card](cards/opening-types.md); type-bound parameters | Door swing/handing and frame geometry; family editor |
| 7 | Stairs, railings, curtain walls, terrain | `Architecture.Stairs`, `Architecture.Railing`, `CurtainGrid`, `Mullion`, `Toposolid` | Not started | After families and constraints |
| 8 | Plans, RCP, elevations, sections, details, 3D | `ViewPlan`, `ViewSection`, `View3D`, `ViewDrafting`, `ViewFamilyType` | Partial: plan, generated north elevation and section, 3D | View range, crop regions, user-placed sections |
| 9 | View templates, filters, visibility, phases, options | `ParameterFilterElement`, `OverrideGraphicSettings`, `Phase`, `PhaseFilter`, `DesignOption` | Not started; **parameters now exist to filter on** | Rule-based view filters over parameter values |
| 10 | Annotation, tags, text, associative dimensions | `IndependentTag`, `TextNote`, `SpotDimension`, `AnnotationSymbol`, `Architecture.RoomTag` | Partial: room labels in plan/DXF | Tags that read parameter values |
| 11 | Schedules, quantities, materials, costs, formulas | `ViewSchedule`, `ScheduleDefinition`, `ScheduleField`, `ScheduleFilter`, `Material`, `GlobalParameter` | Partial: project schedule, CSV quantities; **editable per-category schedules with parameter columns (increment 1) and model quantity columns: door/window width, height, sill, host; wall length, height, thickness, net volume; areas (increment 2)** | Sorting/grouping/filters, totals; **formulas done (increment 3)** |
| 12 | Sheets, title blocks, viewports, print, PDF | `ViewSheet`, `Viewport`, `ScheduleSheetInstance`, `PrintManager`, `PDFExportOptions` | Partial: numbered sheets; **sheet parameters (e.g. Drawn by) storable** | Viewports, title block fields bound to sheet parameters, PDF |
| 13 | Structure, analytical model, loads, rebar | `Structure.AnalyticalMember`, `Structure.Rebar`, `Structure.LoadCase`, `Structure.PointLoad` | Not started | Columns and beams as typed families |
| 14 | MEP connectors, systems, routing, calculations | `Connector`, `MEPSystem`, `Mechanical.Duct`, `Plumbing.Pipe`, `Electrical.ElectricalSystem`, `Mechanical.Space` | Not started | Connector graph domain model |
| 15 | Linked models, coordination, clash detection | `RevitLinkInstance`, `RevitLinkType`, `ImportInstance`, `ElementIntersectsElementFilter` | Not started | Read-only linked `.g3` with transform |
| 16 | Collaboration, history, permissions, conflicts | `WorksharingUtils`, `Workset`, `WorksetTable`, `SynchronizeWithCentralOptions` | Partial: bounded local undo history with names | Persisted change journal based on named transactions |
| 17 | Import, export, interoperability | `IFCExportOptions`, `DWGExportOptions`, `DXFExportOptions`, `ImageExportOptions` | Partial: DXF R12 plan export, CSV | IFC4 export of walls/slabs/spaces with parameter property sets |
| 18 | API, add-ins, events, automation | `UI.IExternalCommand`, `UI.IExternalApplication`, `Events.DocumentChangedEventArgs`, `ExtensibleStorage.Schema` | Partial: UI-independent core with events | Public command API over named transactions |
| 19 | Rendering, materials, lighting, presentation | `Visual.Asset`, `AppearanceAssetElement`, `Lighting.LightType`, `RenderingSettings` | Partial (other session): path tracer, BVH | Material assets linked to types |
| 20 | Performance, spatial indexes, caches, large models | `Outline`, `ElementQuickFilter`, `ElementSlowFilter`, `BoundingBoxIntersectsFilter` | Partial (other session): BVH for rendering | Element spatial index for selection and filters |

## Reading the anchor column

An anchor type listed above is **Observed** to exist in the documentation that
ships with Revit 2027 (`RevitAPI.xml`: 2,866 documented types, 9,624 methods,
13,444 properties; `RevitAPIUI.xml` for `UI.*`). Its existence shows that Revit
exposes the concept publicly. It says nothing about how Revit implements the
concept internally; any statement about internals is marked **Inferred** or
**Unknown** in the cards.

## Increment history

| Increment | Date | Capabilities | Card |
|-----------|------|--------------|------|
| 4 | 2026-10-23 | Parametric levels (rename, elevation with regeneration, protected delete), wall top constraints with offsets, bulk attach/detach, constraint schedule columns, unitless-zero formulas, REA evidence runs, schema 7 | [levels and constraints](cards/levels-and-constraints.md), [REA runs](rea-runs.md) |
| 3 | 2026-10-23 | Calculated parameters: formula language with dimensional analysis, unit literals, conditionals, dependency ordering, cycle rejection, read-only calculated schedule columns, Spanish formula errors, schema 6 | [calculated parameters](cards/calculated-parameters.md) |
| 2 | 2026-10-23 | Door and window types (families), type-driven instance sizes with atomic propagation, duplicate/protected delete/purge, type-bound opening parameters, schedule quantity columns, schema 5 | [opening types](cards/opening-types.md) |
| 1 | 2026-10-23 | Named transactions, transaction groups, regeneration hook, project parameters (instance/type, 9 specs, 7 categories), editable category schedules, CSV, schema 4 | [transactions](cards/transactions.md), [project parameters](cards/project-parameters.md) |
