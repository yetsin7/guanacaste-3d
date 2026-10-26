# Workspace, views and icons

How the drawing area behaves in version 0.9. Everything here is covered by
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
