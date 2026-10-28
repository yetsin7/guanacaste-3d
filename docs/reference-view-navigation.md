# Reference viewport navigation and display controls

The implementation uses the eight user-supplied Revit viewport screenshots as
visual and interaction references. The cube, compass, navigation strip, and
view-control icons are drawn locally with WPF vectors.

## Navigation

- The neutral gray cube has transformed face labels, inset edge highlights,
  a white compass platform, cardinal targets, home, and quarter-turn arrows.
  Face centers, outer edge regions, and corners provide 26 orientations.
  Dragging the cube or compass orbits the shared camera. Exact top and bottom
  views keep an orthogonal camera-up vector in WPF.
- The vertical strip provides the full wheel, compact full wheel, compact object
  wheel, compact building wheel, basic object wheel, and basic building wheel.
  The 2D wheel is disabled in the 3D workspace, as in the supplied menu.
- Wheel sectors perform zoom, orbit, pan, walk, look, vertical movement, forward
  movement, center picking, and camera-history rewind according to the layout.
  Cube and wheel drags are grouped into camera-history operations.
- Region zoom uses a transient rectangle and a surface-picked focus depth.
  Reduce applies a factor of 0.5. Fit frames visible architecture and visible
  links; Fit All includes the complete native project. Fit Sheet Size uses the
  defined viewport crop and is available only when that frame exists.
- Previous and Next restore camera snapshots independently of document undo.
  A new move after Previous abandons the forward branch. History is bounded
  and reset when another document is opened.

Two-letter shortcuts in the 3D workspace are `ZR` (region), `ZO` (reduce),
`ZF` (fit), `ZA` (fit all), `ZS` (fit crop frame), `ZP` (previous), and `ZN`
(next). Text inputs retain normal typing. Escape, view changes, capture loss,
and window deactivation dismiss incomplete navigation overlays.

## View controls

The bottom toolbar exposes:

- Wireframe, hidden line, shaded, consistent colors, textures, and realistic.
  Hidden line uses opaque white masks with depth-tested edges; wireframe removes
  face masks; consistent colors use unlit category colors.
- A progressive realistic image inside the normal editable model viewport.
  It uses the existing path tracer, project materials, linked geometry, and
  shared navigation camera. Preview resolution and worker count are bounded.
  A preview failure retains the interactive WPF model and shows a localized
  failure message.
- Coarse, medium, and fine detail. Coarse removes opening trim while preserving
  cuts and glazing; medium uses normal modeling geometry; fine uses detailed
  vegetation. Detail changes never alter the authored model.
- Graphics options for ambient illumination, background, visible edges, and
  perspective field of view; solar presets; and a shadow switch. Interactive
  native shadows are projected onto the ground plane. Realistic direct-light
  visibility uses the renderer's shadow setting. Solar presets are artistic
  viewing presets, not a site-specific sun study.
- Editable screen crop, crop boundary visibility, and camera locking. Crop
  coordinates remain normalized through window resizes. A locked camera stores
  its pose with the view so reopening does not frame it from another location.
- Temporary hide/isolate by element or category, reset, explicit application
  to saved visibility, and reveal/unhide. Hosted openings retain their required
  host during isolation. Reveal outlines hidden elements in magenta and makes
  them available to the full-document picker. Temporary filtering affects the
  native pickable architectural categories; linked models retain their own
  visibility controls.

The modeling surface uses the configured background rather than an automatically
generated infinite green ground. The dedicated Render workspace continues to
use its own ground and rendering controls. The viewport crop applies to the
modeling surface; it does not redefine a document sheet or the Render tab output.

Persistent graphics settings are stored on the native project view and support
document undo. Temporary hide/isolate and reveal mode are session-only. Perspective
views show a lens selector; they do not claim an orthographic printing scale.
Toolbar overflow is horizontally scrollable in compact windows.

## Verification

The focused WPF checks exercise the production controls, camera history, all
wheel layouts, actual style materials, realistic frame publication, temporary
visibility, promotion and undo, native graphics persistence, normalized crops,
crop framing, camera locks, polar views, and direct-shadow visibility.

Evidence is written under `artifacts/model-workspace-evidence/`, including
`reference-view-controls.png`, `reference-full-wheel.png`,
`reference-hidden-line.png`, `reference-realistic.png`, and
`reference-controls-compact.png`. Build and suite results are recorded in
`artifacts/revit-*.log`.

The initial Computer Use connection failed, but the unified native bridge later
became available. Native checks against a uniquely named copy of the validated
executable covered the built-in house, wheel selection and orbit dragging,
Escape dismissal, cube face snapping, zoom menus, rectangle zoom, visual-style
selection, and temporary wall hiding followed by reset. The corrected region
gesture returns the status bar to selection mode after completion.

Visual inspection found that emissive-only edge materials could render white.
Outlines and color studies now establish a black diffuse base before emission,
following the WPF material composition described in the
[Microsoft example](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-apply-emissive-material-to-a-3-d-object).
The focused suite also samples composed viewport pixels to reject invisible
model presentations. The final focused run passed 416 assertions. Native
screenshots confirmed the corrected hidden-line model and temporary visibility
restore. Pixel-for-pixel identity with Revit has not been established.
