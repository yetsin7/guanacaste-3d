# Workspace, views and icons

How the drawing area behaves in version 0.10. Everything here is covered by
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
