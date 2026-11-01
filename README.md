<div align="center">

# Guanacaste 3D

### Architecture is for everyone. Create what comes next.

**Free · Open source · Windows 10/11 · C# / .NET 10 · MIT**

[Explore downloads](https://github.com/yetsin7/guanacaste-3d/releases) · [Preview the website](website/index.html) · [Roadmap](ROADMAP.md) · [Report a bug](https://github.com/yetsin7/guanacaste-3d/issues)

</div>

> **Technical preview:** Guanacaste 3D is under active development. Download links lead to the public releases page; if no published release is visible, the app is not currently available for public download. It is not yet a certified professional BIM replacement.

**Why follow this project?** Guanacaste 3D brings architectural modeling, coordinated views, native project formats and an optional AI-assisted workflow into an independent, community-readable Windows application. Explore it, help improve it, and watch it grow.

**See the new landing-page prototype:** [website/index.html](website/index.html). This is a standalone concept, not confirmation that the existing production website has been updated.

---

Guanacaste 3D is an independent Windows desktop BIM and architectural CAD
application built in C# with .NET 10 and WPF.

Guanacaste 3D is free and open source under the [MIT License](LICENSE).
Optional third-party AI providers may charge for their own services.

## Download for Windows

Download the [Windows x64 installer ZIP](https://github.com/yetsin7/guanacaste-3d/releases/download/v0.23.2/guanacaste-3d-0.23.2-win-x64-installer.zip) from the
[releases page](https://github.com/yetsin7/guanacaste-3d/releases).
Extract the entire ZIP, close Guanacaste 3D after saving, and run `install.cmd`. The application includes
the .NET runtime and does not require an application subscription or an SDK.
Optional Explorer file thumbnails support `.g3d`, `.g3dt`, `.g3df`, and Revit/SketchUp `.g3dx` exchanges.
They are **disabled by default** because the current provider executes inside the Windows Shell process.
To opt in after extracting the ZIP, run `./install.ps1 -EnableThumbnails` in PowerShell. The installer bundles
the official .NET 10 Desktop runtime needed only by this optional component and installs it when missing.
Windows 10 or 11 on a 64-bit PC is required. The package is an unsigned
technical preview; Windows may display a reputation warning.

Current navigation and editing changes: [Modeling workspace](docs/model-workspace-navigation.md).

### Revit and SketchUp plugins

Bring Revit and SketchUp models into Guanacaste 3D as editable projects (native walls, doors, windows and slabs;
everything else as `.g3df` families) — see [Opening Revit projects](docs/revit-import.md):

- [Guanacaste3D-Revit.zip](https://github.com/yetsin7/guanacaste-3d/releases/download/plugins-v1.0.1/Guanacaste3D-Revit.zip)
  — Revit 2027 add-in 1.0.1 with embedded previews and optional Windows thumbnails (disabled by default). Unzip and run `install.cmd`, then restart Revit.
- [Guanacaste3D-SketchUp.rbz](https://github.com/yetsin7/guanacaste-3d/releases/download/plugins-v1.0.0/Guanacaste3D-SketchUp.rbz)
  — SketchUp extension (preview). Install from Extensions → Extension Manager.

With Revit 2025 or later installed, Guanacaste 3D also opens `.rvt` files directly.

## Status

Version 0.23.2 is a working technical preview, not a complete professional BIM
system. It must not be used for certified structural calculations or issued
construction documents without independent professional review.

Implemented capabilities include:

- Native WPF desktop workspace with a ribbon, project browser, properties
  palette, plan, coordinated 3D view, schedule, and drawing-sheet workspace.
- Persistent plan, 3D, north-elevation, section, and schedule views generated
  from the same model.
- One versioned document containing levels, reusable wall types, straight walls,
  hosted door and window openings, rooms, floor slabs, and numbered sheets.
- Parametric wall-type propagation, plan placement, selection, editing,
  transformation, deletion, and bounded undo and redo.
- Net wall volume with opening subtraction and overlap rejection.
- Live project schedule and CSV wall-quantity export.
- Official compressed `.g3d` project container with signature, schema metadata, SHA-256 integrity checking, atomic save and backup; `.g3` and `.g3d.json` remain compatible.
- Versioned `.g3df` family and `.g3dt` template persistence APIs, including a validated universal family operation graph and metric architecture template foundation.
- Automated checks for domain rules, migrations, persistence, and coordinated
  BIM elements.

Still required for broad professional parity: robust geometry and wall joins,
complete door and window objects, advanced families and constraints, elevations,
sections, associative annotation, placed sheet views, IFC and DXF exchange,
collaboration, structural and MEP authoring, physical rendering, and large-model
performance engineering.

## 2D architectural drafting (proposed in PR #36)

The **Annotate** ribbon now has real drawing workflows for **Detail Line** and **Filled Region** in the plan viewport. These annotations are attached to a specific plan view ID; they do not generate 3D surfaces or alter any wall, slab, or structural element.

- **Detail Line:** draw connected segments between two mouse clicks; choose solid, dashed, dotted, or centerline style.
- **Filled Region:** choose solid, diagonal, crosshatch, dotted, or herringbone hatch, then click opposite corners.
- Annotation geometry is saved as part of the native project, supports undo/redo and can explicitly be copied to another plan with a new view ID.
- Right click exits the current drawing tool. Construction hatch graphics are clipped to their region outline.

**Development status:** These changes are under review and must be tested on Windows before release. Additional work is planned for editing arbitrary polygon vertices, hatch density controls, line-weight palettes, view-specific clipboard commands and printing/export of the new annotation objects.

## Requirements

- Windows 10 or 11, x64.
- Current .NET 10 SDK.
- Visual Studio with the .NET desktop workload is optional; the CLI is enough.

## Build and run

```powershell
git clone https://github.com/yetsin7/guanacaste-3d.git
cd guanacaste-3d
dotnet restore Guanacaste3D.slnx
dotnet build Guanacaste3D.slnx -c Release --no-restore
dotnet run --project src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj -c Release --no-build
```

## Run checks

```powershell
dotnet run --project tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj -c Release --no-build
```

## AI assistant

The assistant (quick access button, command `AiAssistant`) is a chat that designs and calculates by calling the program's
own tools on the open project: levels, walls, doors, windows, rooms, slabs, columns, quantities, a priced budget, the model
audit, and file exports. Every model edit goes through `DocumentSession`, and one assistant step is a single undo step.
Exports and saves write files, so the user confirms each one.

- **Providers:** Claude (Anthropic API key), OpenAI (API key) and local servers with an OpenAI-compatible API such as
  Ollama. API keys are stored encrypted for the current Windows user in `%LOCALAPPDATA%/Guanacaste3D/ai-settings.json`.
- **No subscription login:** Anthropic does not allow third-party apps to sign users in with Claude subscriptions, so
  Claude needs an API key. OpenAI's "Sign in with ChatGPT" is limited to approved clients and is not implemented.
- **Code:** `src/Guanacaste3D.Agent` has no UI dependency (providers, tool registry, agent loop) and is covered by
  `tests/Guanacaste3D.Checks/agent-checks.cs`. The budget lives in `src/Guanacaste3D.Core/budget`; its default unit
  prices are placeholders to be replaced with local prices.

## Modern villa sample

`File > Abrir ejemplo: villa moderna` opens a two-storey villa built only through `DocumentSession`: a glazed ground-floor
pavilion, a white concrete upper volume that cantilevers past it, entrance steps, a recessed reflecting pool
(`FloorKind.Water`, rendered with the glossy `SurfaceKind.Water` material) and a planted garden. Render it with the
golden-hour lighting preset; `tests/Guanacaste3D.RenderBench` renders it with `G3_SHOT_ONLY=villa ... -- --shot`. The
assistant can build the same pieces with `add_curtain_wall`, `add_pool` and `add_planting`.

## Breeze-block house sample

`Inicio > Ejemplos > Casa del cobogó` opens a street-front house: a white painted volume floating over a recessed
glazed living room and a fair-faced concrete carport, a thin concrete canopy above it and a breeze-block screen
(`ElementStyle.BreezeBlock` curtain wall) at its east end. The carport holds a timber `Pivot Door`, slatted cladding and
two `Sedan Car` families; the site has lawn, stepping stones, driveway, sidewalk, curb and an asphalt street. Slabs now
render by finish: `ExposedConcrete` as fair-faced concrete, `Paint` as white plaster, and the new `Asphalt` and `Grass`
finishes as road and lawn. `G3_SHOT_ONLY=cobogo ... -- --shot` renders the street view in portrait.

The sample is saved as `resources/samples/casa-del-cobogo.g3d` and opens as an untitled copy. It carries three
detailed **mesh families**: a CC0 jacaranda from Poly Haven and two CC0 cars from Grab3D, converted with
`tools/mesh-family-converter` (see [docs/third-party-models.md](docs/third-party-models.md)). A mesh family is a `.g3df`
holding a triangle mesh with a render surface per triangle and a light preview for the interactive view.
`File > Import family` loads one into any project and places an instance that can be moved, copied, turned and resized;
`tools/sample-projects` regenerates the saved sample.

The sample is a complete project: interiors with doors, windows, fixtures and plumbing; a structural grid; associative
dimensions; sections A-A and B-B; door, window and room schedules; and six A1 sheets (cover, ground floor, upper floor,
elevations, sections, schedules). Printed plans carry door, window and column tags whose type marks (P1, V1) match the
schedules. Views keep a **crop region** (`ProjectView.ModelCrop`) that sheets honour, and saved schedules can be placed on
sheets as tables. The project also stores its **render setup** (camera, sun, size, quality and filters), restored on open.

## Home screen

Started without a document, the application opens on the Home screen: open or create a project or a family, reopen a
recent project (tiles or list, sorted by date or name, with search) or start from a sample. Recent projects are kept in
`%LOCALAPPDATA%\Guanacaste3Decent-projects.json` with cached isometric thumbnails. `File > Inicio` returns to it.

## Annotation

- **Tags:** the ribbon commands `TagAll` and `TagByCategory` toggle automatic plan tags for doors (P1...), windows (V1...)
  and columns (C1...). They are computed from the model by `ElementTags`, numbered by type across the project, and never
  stored, so they cannot go stale.
- **Automatic dimensions:** `AlignedDimension` adds associative wall-length and opening-width dimensions to the active
  level in one undo step (`DocumentSession.AutoDimensionLevel`); existing dimensions are kept.
- **Assistant:** `get_tags`, `auto_dimension` and `get_area_schedule`. The area table itself is `AreaSchedule`.

## Views and callouts

- **Callouts:** the `Callout` ribbon command marks a named detail region around the selected element. It is stored in the
  project (`BuildingDocument.Callouts`), drawn as a dashed frame with a name tab in the plan, listed in the project
  browser, and zooms the plan to its region. The assistant has `add_callout`.
- **View templates:** `ViewTemplates` opens a menu to save the active view as a template, apply one to it, or delete one.
  A template carries hidden and isolated elements plus display settings (style, detail, sun, edges); each view keeps its
  own camera and crop.
- **Tiled views:** the View ribbon's `TileViews` shows the plan and the 3D view side by side with a draggable splitter;
  `TabViews` goes back. The two views keep their own tabs (only the tab strip is hidden), so every command that selects
  a tab by index still works; opening a schedule, sheet, elevation, section or render ends the tiling. Work that needs a
  visible 3D view (scene build, realistic preview) is gated on `Model3DVisible`, not on the selected tab.

## Publish for Windows

```powershell
dotnet publish src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj `
  -c Release -r win-x64 --self-contained false `
  -o artifacts/win-x64
```

The publish output is `artifacts/win-x64/Guanacaste3D.Desktop.exe`. Install it
for the current user and register `.g3d`, `.g3df`, and `.g3dt` by running:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/install-windows.ps1
```

## Architecture

- `src/Guanacaste3D.Core/`: document model, validation, commands, history,
  schedules, and persistence.
- `src/Guanacaste3D.Desktop/`: WPF presentation and interaction.
- `tests/Guanacaste3D.Checks/`: UI-independent executable checks.
- `docs/revit-functional-parity-roadmap.md`: lawful functional-parity plan.

New projects use the official `.g3d` binary container. The application still opens and can explicitly save the earlier `.g3` preview envelope, and it can import `.g3d.json`. Family definitions use `.g3df`; project templates use `.g3dt`. The new containers are identified by their internal signature rather than extension alone and verify payload integrity before deserialization. Keep independent backups while the product remains a technical preview.

Native projects use `.g3d` and a versioned `G3-PROJECT/1` envelope. Families
use `.g3df`; templates use `.g3dt`. Earlier `.g3` projects and `.g3d.json`
preview files remain readable and are saved as new `.g3d` projects. Keep
independent project backups before using preview software on important work.
