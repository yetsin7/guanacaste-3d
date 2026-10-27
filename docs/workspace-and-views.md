# Workspace, views and icons

How the drawing area behaves in version 0.14. Everything here is covered by
`tests/desktop-interaction-checks` and `tests/Guanacaste3D.Checks`.

## Views

- **Tabs show only open views.** Opening a view from the browser, a ribbon
  command or the elevation symbols adds its tab. The cross on a tab, a
  middle-click, or *Cerrar todas las demás vistas* in the tab's menu closes it;
  the current view can never be closed.
- **3D and Render** are the only view buttons outside the ribbons, and they sit
  together in the quick access bar beside Save. Layouts saved before this
  version gain the Render button once.
- **3D view**: grey study model (switch to project colors with *Maqueta gris*),
  outlined feature edges (*Aristas*), a click selects the element under the
  cursor and measures it with temporary dimensions, and with a tool armed the
  click places the element at that point, exactly as in the plan. The Render
  preview accepts the same clicks.
- **Elevations**: *Vista Norte, Sur, Este, Oeste* exist in every project
  (older projects gain the missing ones). Each has a circle-and-arrow symbol in
  the plan; click selects, double-click opens, F2 / the symbol menu / the
  browser rename it.
- **Zoom**: the plan zooms toward the cursor up to 800 px per meter; the
  elevation and section views zoom with the wheel and pan with the middle button
  (double-click resets).

## Editing

- **Walls**: the tool uses a crosshair, shows no wall-shaped ghost before the
  first click, and a chain ends by itself when it lands on or crosses another
  wall (the tool stays armed).
- **Closing a room**: coming back within 30 px of the chain's first point pulls
  the pointer onto it (green ring, *Cerrar contorno*); the click joins the last
  wall to the first, also with a face location line, and ends the chain.
- **Snaps, in Revit's order**: chain start, endpoint (square), midpoint
  (triangle), nearest point on a wall (hourglass, for tees), work plane, then the
  drawing direction (axes within 5°, 45° within 3°, 15° steps within 1.5°, with
  the length rounded to 10 cm or stopped on an alignment guide), alignment with
  corners on screen (one guide per axis, the nearest corner), and the grid.
- **Keys while drawing walls**: SHIFT constrains to the axes like *Ortogonal*,
  SPACE flips the wall to the other side of a face location line, F8 toggles
  *Ortogonal*, typing a number sets the length.
- **Vegetation**: *Componente de emplazamiento* arms planting with the last
  species used; SPACE steps through broadleaf, guanacaste, palm, cypress and
  shrub. Plans show a translucent canopy with a scalloped edge (spiked for palms
  and conifers) and the trunk; width is the canopy spread, height the overall
  height, both editable like any element.
- **Terrain**: *Sólido topográfico* sketches a site boundary like a floor
  (double click or right click closes it); *Región graduada* adds elevation
  points (click asks the height, Ctrl+click removes, Esc ends). The surface is a
  thin-plate spline through the points, the corners rest at the toposolid's
  elevation. Plans show contours every 0.5 m (labelled every 2.5 m, *Etiquetar
  curvas de nivel* toggles the labels) and spot elevations; elevations show the
  terrain profile; plantings stand on the terrain.
- **Stairs**: the stair command starts with a straight run; SPACE steps through
  L-shaped (quarter landing), U-shaped (switchback) and spiral stairs. Risers
  never exceed 17.5 cm, the stair rises to the level above when there is one,
  and railings are added on every flight and open landing side in the same undo
  step. Plans show the walking line with its arrow and *SUBE*.
- **Roofs**: pitched roofs have an overhang (0.6 m for the catalog roofs, edited
  with the roof) that keeps the ridge where it was and drops the eave, close
  their gables with wall up to the roof underside (switchable), and are surfaced
  with clay tiles. Plans draw the overhang dashed; elevations draw the roof's
  silhouette.
- **Realistic 3D view**: with *Maqueta gris* off, the 3D view shows the render
  textures assigned in *Render → Materiales*, projected exactly as the renderer
  projects them.
- **PDF sheets**: *Archivo → Exportar planos a PDF…* writes one vector sheet per
  level on the paper of the project's first sheet (A3 otherwise): walls cut and
  filled, doors with leaves and swings, windows, rooms with name and area,
  stairs with their walking line, roofs with overhang and ridges, plantings,
  contours, title block (project, view, scale, date, sheet number), scale bar
  and north arrow, at the largest standard scale that fits.
- **Wall, door and window catalog**: about thirty wall types grouped by what
  they are built of (gypsum partitions 75–160 mm, moisture-resistant, acoustic
  and shaft-wall gypsum, glass partitions and storefronts, concrete block,
  brick, concrete, stone, timber cladding, metal panel, bahareque and adobe).
  The construction sets the faces in 3D and in the render: exposed brick,
  block, concrete, stone, timber or metal outside and plaster inside; gypsum
  painted on both faces; interior brick, block or concrete partitions exposed
  on both faces; glass partitions are a 12 mm pane in aluminium channels with
  a mullion at least every 1.2 m. Doors come panelled, flush, six-panel,
  louvered, frameless glass and security (grille over glass); windows can carry
  exterior security bars, always drawn on the side away from the rooms.
- **Furniture and fixtures**: about 65 families. They cover living,
  dining, bedroom, kitchen, bath, utility and outdoor use. Recent additions:
  - Seating and tables: round and bistro tables, an L-shaped sectional, an
    office chair, a bar stool, an ottoman and a lounge chair.
  - Bedroom: a bunk bed and a crib.
  - Kitchen: a sink cabinet with an undermounted steel bowl and gooseneck
    mixer, a range hood, a tall pantry, 60 cm cabinets and a microwave.
  - Bath and utility: a pedestal sink, a double vanity with two vessel basins,
    a bidet, a wall-hung urinal, mirrors and a water heater.
  - Outdoor: a hammock on its stand, a sun lounger and a patio umbrella.

  SPACE turns the next component a quarter turn while placing, and turns the
  selected one afterwards (*Rotar* does the same). One undoable step; the turn
  is saved with the project.
- **Electrical**: *Luminaria*, *Dispositivo eléctrico* (receptacles) and
  *Interruptor* place devices (SPACE cycles the kind; wall devices snap to the
  nearest wall within 0.8 m and take their standard height: receptacles 0.30 m,
  switches 1.20 m, luminaires at the ceiling). *Equipo eléctrico* places a
  panel. *Cable*: click a switch, then the lights it controls (click again to
  unlink); click a panel to open a new 20 A circuit, then click devices to add
  or remove them. Double-clicking a switch flips it; single-pole switches act
  in series, three-way switches by parity, dimmers scale the light. The plan
  draws each circuit's run coloured by state (ok, over 80 %, open, tripped)
  and the switch legs; lit fixtures show in yellow, and the 3D view and render
  light or darken them at once. *Tabla de paneles* lists every circuit with
  load, current, utilization, conductor gauge (NEC table) and voltage drop, and
  turns breakers on/off, resizes them or opens the main. A circuit above 100 %
  trips; while a project has no panel, unwired devices count as powered.
- **Default names** follow a Spanish Revit template: *Nivel 1* (its plan view
  carries the same name), *{3D}*, *Sección 1*, *Tabla de cantidades*. Older
  projects keep any name the user gave; untouched English defaults are translated.
- **Doors**: SPACE steps through the four hinge/swing combinations while a
  door is being placed, and flips the selected door afterwards (one undoable
  step). Delete removes only the door; deleting a wall takes its openings.
- **Double-click**: a wall opens its type editor, a door or window opens its
  family (parameters, plan and orbitable 3D view, live), other elements open
  their property editor.

## Tooltips and icons

- Hovering any command button shows its title, a sentence on how it works and a
  looping animated demonstration. Scenes are built only when the tooltip is about
  to appear.
- Every ribbon command has its own icon, `CmdIcon.<CommandId>`. They are
  generated, not drawn by hand: edit `tools/icons/icon_kit.py` (pictograms and
  badges) or the `MAP` in `tools/icons/generate_command_icons.py` and run
  `python tools/icons/generate_command_icons.py`. The interaction checks fail
  when a ribbon command has no icon and write a contact sheet of all of them to
  `artifacts/interaction-evidence/command-icons.png`.
