# Rendering and model import

This document describes how Guanacaste 3D imports models from other programs,
what the renderer does with them, and what is measured. Numbers come from this
repository's checks and from runs on the development machine (16 threads,
32 GB), with real files from AutoCAD, Revit and SketchUp.

## 1. Importing Revit, SketchUp and AutoCAD files

Drop one or more files on the window, or use *Archivo → Importar modelo, CAD o
IFC…* (also the *Insertar → Importar* buttons). A dialog shows what the file is
before anything is read, then imports with progress and a working Cancel.
Imported geometry is **linked, not embedded**: the project stores the path,
placement and options in `<project>.g3d.links.json` and reads the file again
when the project is opened (a moved project still finds its models through a
relative path).

| Format | How it is read | Notes |
|---|---|---|
| **IFC** 2x3 / 4 | Native reader (`import/ifc-*.cs`) | The route for **Revit**: extrusions, faceted and tessellated shells, instanced families, profiles with holes, plane clipping, and **openings cut out of their host elements**. Volume matched IfcOpenShell exactly on an independent test file (13.1800 m³). |
| **SKP** (SketchUp) | The SketchUp C API (`SketchUpAPI.dll`), loaded at run time | Found in `Common Files\Autodesk Shared\Components` (installed with Revit/AutoCAD) or a SketchUp install. Groups, components, colors, transparency, hidden objects and hidden tags are handled; inches become meters. Not bundled; a clear message is shown if it is missing. |
| **DWG** | Converted to ASCII DXF by **AutoCAD's Core Console** or the **ODA File Converter** | Needs one of them installed. Runs in a private temporary folder that is removed afterwards. ≈2 s for a typical drawing. |
| **DXF** (ASCII) | Native reader | 3DFACE, SOLID, MESH, polyface and polygon-mesh POLYLINEs, LWPOLYLINE with arcs, ARC, CIRCLE, LINE, INSERT with nested blocks, layer visibility, ACI colors, `$INSUNITS`. ACIS solids (3DSOLID) are counted and reported, not silently dropped. |
| **OBJ** (+MTL) | Native streaming reader | 450 MB / 3.6 M triangles in ≈4 s. Colors, glass (`d`), shininess, merged smooth normals, n-gon triangulation. |
| STL, glTF/GLB, COLLADA (DAE) | Native readers | DAE is what SketchUp Pro exports; units and up axis are honored. |
| **RVT / RFA** | Metadata and preview only | A Revit file keeps its geometry in a proprietary, compressed database that only Autodesk software reads. The importer shows the Revit version, build, last path, worksharing state and the embedded preview image, and explains the supported route: **File → Export → IFC in Revit**, then import that file. |

Everything is recognized **by content**, not by extension, and every reader is
defensive: truncated or malformed files, NaN coordinates, absurd sizes, empty
files and cancellation are covered by checks.

### Heavy models

`MeshProxy` reduces a heavy model (vertex clustering sized from its surface
area) to about 120 000 triangles for the interactive 3D view; the **renderer
always uses the full geometry**. A 3.6 M-triangle Revit house shows in the 3D
view immediately, imports in ≈4 s, and keeps the application at about
**1.1 GB** while navigating the live preview.

## 2. The renderer

A physically based CPU path tracer, tuned for architecture:

- **Light**: Perez/Preetham clear sky with a sun colored by air mass (golden
  hour is orange, dusk is blue), CIE overcast sky, sun as a disk with soft
  shadows, point/spot lights, emissive geometry, and window *portals* for
  interiors. Hour presets solve for a target sun altitude, so "golden hour"
  means 6° anywhere on Earth.
- **Surfaces**: Lambert diffuse under a GGX specular coat, metals, mirrors, thin
  glass with Fresnel. Sunlight **passes through glass** (shadow rays cross
  transmissive surfaces with their tint), which was blocked before.
- **Detail without textures**: procedural tiles, boards, masonry, concrete and
  lawn, evaluated from world position (imported meshes need no UVs). Each
  pattern also has **relief**: the shading normal follows a height field (grout
  lines, mortar joints, board gaps, trowelled plaster, grass blades), so grazing
  sun picks out the joints. Fine relief fades with distance so it never sparkles.
- **Construction materials**: walls render in the material of their type's
  construction (brick and stone masonry, concrete block, fair-faced concrete,
  timber cladding, metal panel), with plaster on faces that look into rooms.
- **Switched lights**: luminaires placed with the electrical tools are emissive
  only while their circuit is live and their switches are closed; downlights
  have a small, bright lens, and an open switch leaves the diffuser dark.
- **Render-quality vegetation**: the render (not the 3D view, which keeps the
  light modelling canopies) grows every planting: a flared trunk splits into
  scaffold limbs that branch recursively (Leonardo's rule for thickness), and
  leaf clusters are scattered through the crown's outer shell and joined to the
  nearest branch tip (simplified space colonisation). Every leaf is real
  geometry with a folded midrib, so sunlight falls through the crown in dapples:
  about 45 000 leaves for a broadleaf tree, 90 000 compound leaves for the
  guanacaste, needle sprays for the cypress, single pinnae on palm fronds.
  Trees are generated off the interface thread and cached.
- **Leaf translucency**: materials can transmit part of their diffuse light to
  the back face (thin diffuse transmission, sampled and weighted with MIS for the
  sun, sky portals, lamps and emissive surfaces), so backlit foliage glows.
- **Family materials**: walls are classified face by face (a face enclosed by
  walls in all directions is interior plaster, the others take the facade
  material); doors are panelled (stiles, three rails, two raised-and-fielded
  panels) with steel lever handles, roses and hinges; sofas, beds and rugs use
  upholstery and linen with a woven pattern and rounded corners; sanitary ware is
  glazed porcelain.
- **Light fixtures**: ceiling lights and floor lamps glow and light the render
  as area lights (*Luces artificiales* switches them off).
- **Vegetation**: site plantings (*Masa y emplazamiento → Componente de
  emplazamiento*, or the library's *Vegetación*) are generated procedurally:
  broadleaf tree, **guanacaste tree** (short massive trunk, very wide umbrella
  crown), coconut palm, cypress and shrub. Each tree is seeded by its id, so no
  two look alike and one always regenerates the same; it is fitted exactly to
  its height and canopy spread. Canopies are smooth-shaded lumpy clusters; the
  foliage and bark materials add leaf clumps and furrows as relief.
- **Image textures**: *Render → Materiales* assigns, per surface category
  (walls, floors, pitched roofs, terrain, structure, doors and furniture, earth),
  one of six shipped seamless textures (brick, wood floor, stone paving,
  concrete, white render, gravel — generated by
  `tools/textures/generate_textures.py`) or any PNG/JPEG/BMP/TIFF from disk, with
  the size in meters one copy covers. Images are projected in world space (no
  UVs needed), kept in a mip chain and sampled trilinearly so they never
  sparkle, and their brightness becomes relief. Assignments are saved in the
  project (schema 11); a missing image falls back to the stock material.
- **Roofing**: pitched roofs use a clay barrel-tile material whose courses run
  down each facet's slope, with tile relief.
- **Clouds**: a procedural cumulus layer about 1.5 km up (*Nubes*, 0–100 %),
  lit by the sun with a silver lining and greyer thick bodies, fading toward the
  horizon. Clouds part around the sun's disk, so sunlight and shadows do not
  change with the slider.
- **Atmosphere**: aerial perspective veils distant surfaces with the color of
  the horizon (clear air ≈ 12 km visibility, shorter with turbidity), and the
  daytime horizon band is whitened, removing the pink cast of the Perez fit.
- **Camera**: field of view, shift-lens vertical correction, depth of field,
  tent-filtered antialiasing.
- **Image**: edge-aware denoising of indirect light, rough glossy reflections
  (roughness ≥ 0.3, so polished floors are not grainy) and the reflections in
  window panes; sun shadows and sharp reflections stay sharp. Measured-exposure
  auto exposure, bloom, **AgX** tone mapping by default (*Fotográfico*: bright
  light bleaches to white like film instead of skewing to cyan or yellow), ACES
  (*Contrastado*) or a soft curve, white balance (±1500 K), vignette, dithering.
- **Efficiency**: BVH built in parallel with leaf-ordered triangle data, adaptive
  sampling that stops converged pixels (the sample house kept 34 % of pixels
  active at 96 spp), samples spent in batches that keep the preview updating
  about twice a second, and a hardware-aware planner.

### Using it

*Render* tab → the picture is a **live, navigable preview** at low detail. The
camera is shared with the 3D view.

| Input | Action |
|---|---|
| Right mouse + drag | Look around |
| Wheel | Zoom toward the point under the cursor |
| Middle button + drag | Pan |
| Left drag | Orbit around what is under the cursor |
| W A S D / Q E | Fly / down and up (Shift fast, Ctrl slow) |

*Renderizar* freezes the picture while the final image is made. Quality
(Borrador, Estándar, Alta, Final), size (view, HD, Full HD, 2K, 4K), and how much
of the computer to use (Eco, Equilibrado, Máximo) are one choice each; exposure,
glow, vignette, vertical correction and background blur can be adjusted after the
render without rendering again. *Guardar imagen…* writes PNG or JPEG; *Copiar*
puts the picture on the clipboard.

A **walk-through** is recorded with *Añadir marca*: the camera glides through the
marks on a spline and the video is written as numbered PNG frames, reusing one
acceleration structure for all of them.

### Keeping the computer safe

- Before rendering, the image size is **fitted to the memory that is really free**
  (and the render is refused if even the smallest size cannot fit).
- While rendering, a watchdog stops the render, **keeping the image so far**, if
  system memory passes 93 % or the process grows past its limit. Running out of
  memory is handled the same way.
- Eco mode uses half of the cores; the preview itself defaults to half.
- After each heavy build the heap is compacted, and per-frame buffers stay out of
  the large-object heap, so memory does not creep up during long previews.

## 3. Measured

| Check | Result |
|---|---|
| Revit OBJ import (450 MB, 3 623 690 triangles, 197 materials) | 4.1–4.4 s |
| BVH for 3.4 M triangles | ≈2 s (parallel build) |
| Live preview in the app on that model | first picture ≈5–6 s after opening the tab; UI stays responsive while flying |
| IFC volume vs IfcOpenShell (rotated wall, opening, slab) | 13.1800 = 13.1800 m³, identical bounds |
| SketchUp sample written and read through the real SDK | 48 triangles, hidden group skipped, 100 in = 2.54 m |
| Real AutoCAD DWG | converted and read in ≈2 s |
| Round trip of the application's own OBJ/GLB export | same triangle count and bounds |

## 4. Where things are

| Area | Files |
|---|---|
| Import | `src/Guanacaste3D.Core/import/` (`model-importer.cs`, one reader per format, `mesh-clipper.cs`, `polygon-mesher.cs`, `model-links.cs`, `mesh-proxy.cs`) |
| Rendering | `src/Guanacaste3D.Core/rendering/` (`path-tracer.cs`, `bvh.cs`, `sky-model.cs`, `render-session.cs`, `live-preview.cs`, `tour-renderer.cs`, `render-safety.cs`, `image-processor.cs`, …) |
| Navigation | `src/Guanacaste3D.Core/navigation/` (camera, collisions, scene queries, camera paths) |
| Interface | `src/Guanacaste3D.Desktop/` (`controls/render-panel.xaml`, `ImportWindow.xaml`, `LinkedModelsWindow.xaml`, `scene-navigator.cs`, `main-window-imports.cs`, `main-window-raytrace.cs`) |
| Checks | `tests/Guanacaste3D.Checks/` (`render-engine-checks.cs`, `import-checks.cs`, `navigation-checks.cs`); real-file checks skip themselves when the files are absent |
| UI checks | `tests/desktop-interaction-checks/` (live preview, drag-and-drop import, links saved and restored) |

## 5. Limits worth knowing

- Revit files cannot be read directly (see above); IFC is the supported route.
  IFC import does not yet read B-spline surfaces, swept disks or revolved solids
  (reported as a warning), and bounded half-spaces clip as unbounded planes.
- DWG needs AutoCAD or the ODA File Converter on the same computer; SketchUp
  needs `SketchUpAPI.dll` and reads files up to its own version.
- Image textures, HDRI skies, displacement and a material browser are not
  implemented yet (the renderer uses procedural patterns with procedural relief);
  this is the next step for materials.
- Linework (CAD lines) is imported and kept with the model but is not yet drawn
  in the plan view as an underlay.
