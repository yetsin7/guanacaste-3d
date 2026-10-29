# Opening Revit projects

Revit stores geometry in a proprietary, compressed database that only Autodesk software can read. Guanacaste 3D
therefore reads `.rvt` projects through the user's own Revit (2025 or later) with a small add-in, the **Revit bridge**
(`src/Guanacaste3D.RevitBridge`).

## Flow

1. The user opens or drops a `.rvt` file. `RevitProjectWindow` shows the Revit version and preview.
2. `RevitBridgeRunner` writes the add-in manifest to `%APPDATA%\Autodesk\Revit\Addins\<year>\Guanacaste3D.RevitBridge.addin`,
   writes a job file and starts `Revit.exe /nosplash` with `G3D_REVIT_JOB` pointing at it.
3. The add-in opens the project when Revit is ready (detached from central when workshared), answers dialogs, writes
   the exchange file (`.g3dx`) and asks Revit to exit. The first time, Revit asks the user to trust the unsigned add-in:
   choose **Always load**.
4. `ExchangeConverter` builds a new project. The exchange file is cached in
   `%LOCALAPPDATA%\Guanacaste3D\revit-cache`, keyed by path, size and save time, so reopening is immediate.
5. Optionally every family is saved as a `.g3df` file in `<project> - familias G3D` next to the model.

## The add-in for Revit users

`scripts/package-revit-addin.ps1` builds the add-in for every Revit version installed on the build machine and writes
`src/Guanacaste3D.RevitBridge/bin/Release/package/Guanacaste3D-Revit.zip`. Users extract it, close Revit and run
`Instalar.cmd` (no administrator rights): the add-in is copied to `%APPDATA%\Autodesk\Revit\Addins\<year>\Guanacaste3D`
with its manifest beside it. `Desinstalar.cmd` removes it. Guanacaste 3D installs the same way when it opens a `.rvt`.

The **Guanacaste 3D** ribbon tab has two buttons, each asking whether to export the whole model or only what the
active 3D view shows:

- **Abrir en Guanacaste 3D** exports to a temporary file and starts Guanacaste 3D with it. Guanacaste records its
  location in `%LOCALAPPDATA%\Guanacaste3D\application-path.txt` every time it starts.
- **Exportar .g3dx** saves a file to open in Guanacaste 3D on any computer, with or without Revit.

## SketchUp

`scripts/package-sketchup-extension.ps1` writes `src/Guanacaste3D.SketchUpExporter/bin/Guanacaste3D-SketchUp.rbz`,
installed from **Extensions → Extension Manager → Install Extension**. It adds **Extensions → Guanacaste 3D** and a
toolbar with the same two commands and writes the same `.g3dx` format. SketchUp has no BIM categories, so each
top-level group or component is classified by its name, definition and tag: thin upright boxes named or tagged
*muro/wall/pared* become native walls, *puerta/ventana* components standing in them become hosted openings,
*losa/piso/cielo* groups become slabs from their largest upward face, and everything else becomes mesh families shared
per component definition. Holes cut by hand in a wall (not by a door or window component) are not kept on a native
wall.

Lumion projects (`.ls`) are not readable outside Lumion; export from Lumion to COLLADA (`.dae`) or glTF and import
that file.

## What becomes native

| Revit | Guanacaste 3D |
| --- | --- |
| Levels | Levels (lowest is the base floor, the next one the base roof) |
| Straight basic walls on a level (or sunk up to 15 cm below it) | Walls with a wall type per Revit type (construction guessed from the layer materials) |
| Walls attached to a roof or with an edited profile | A native wall up to its lowest top, plus a "Remate de muro" family with Revit's gable above it |
| Doors and windows hosted by those walls | Openings with an opening type per Revit family, type and size |
| Flat floors, ceilings and flat roofs | Floor slabs, one per separate piece; axis-aligned rectangular holes become slab openings, holes under 0.05 m² (light cut-outs) are ignored |
| Rectangular rooms | Rooms |
| Everything else (furniture, fixtures, casework, stairs, railings, sloped roofs, beams, columns, curtain and curved walls) | Mesh component families (`.g3df`), one per distinct geometry, placed as components |

A wall whose doors or windows cannot be hosted natively (they overlap or run past the wall) keeps Revit's cut geometry
together with its openings. The conversion report lists each case.

## Exchange file (.g3dx), version 1

JSON, meters, Z up, Revit internal coordinates (the converter moves the plan center near the origin).

- `levels[]`: `id`, `name`, `elevation`.
- `materials[]`: `name`, `class`, `category`, `color` (`#RRGGBB`), `transparency` and `shininess` (0–1), `ownerCategory`.
- `meshes[]`: `positions` (float32 xyz), `indices` (int32), `materials` (int32 per triangle), base64 little-endian, in the
  element's own frame.
- `elements[]`: `id`, `uniqueId`, `category` (BuiltInCategory name), `categoryName`, `family`, `type`, `name`, `level`,
  `kind` (`wall`, `door`, `window`, `floor`, `ceiling`, `roof`, `instance`, `mesh`, `room`), `mesh`, `origin` and
  `rotation` (the frame: model point = origin + Rz(rotation) · mesh point), plus `wall`, `opening`, `slab` or room `loops`.
  Wall `start`/`end` are already moved from Revit's location line onto the centerline.
