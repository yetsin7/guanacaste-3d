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

Revit users can also export by hand from the **Guanacaste 3D → Exportar a Guanacaste 3D** ribbon button and open the
`.g3dx` file in Guanacaste 3D on a computer without Revit.

## What becomes native

| Revit | Guanacaste 3D |
| --- | --- |
| Levels | Levels (lowest is the base floor, the next one the base roof) |
| Straight basic walls on a level | Walls with a wall type per Revit type (construction guessed from the layer materials) |
| Doors and windows hosted by those walls | Openings with an opening type per Revit family, type and size |
| Flat floors, ceilings and flat roofs | Floor slabs; axis-aligned rectangular holes become slab openings |
| Rectangular rooms | Rooms |
| Everything else (furniture, fixtures, casework, stairs, railings, sloped roofs, curtain, curved, attached or profile-edited walls) | Mesh component families (`.g3df`), one per distinct geometry, placed as components |

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
