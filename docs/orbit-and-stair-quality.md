# Selection orbit and stair geometry

## Navigation behavior

In the 3D model and render preview, hold Shift and drag with the middle mouse
button to orbit. The pivot is the center of the selected element's complete
world-space bounding box. A multiple-element selection uses its combined box.
With no surviving selection, the pivot is the center of the complete project,
including hidden project geometry and visible linked models. An empty project
uses a finite default center.

The pivot is captured when the gesture starts and remains fixed until that
orbit ends. Shift may be pressed or released during a middle-button drag to
switch between orbit and pan. Shift plus a middle-button double-click keeps
orbiting; the unmodified middle-button double-click still fits the model.

Camera position and orientation rotate together. The pivot retains its screen
position and distance, avoiding reframing when orbit starts after panning.
The model geometry is never transformed by a camera gesture. Edits, undo,
deletion, visibility changes, and project replacement invalidate cached bounds.

## Stair behavior

- Straight solid and open stairs share the riser count used by flight and guard
  geometry. Low stairs receive positive tread and stringer solids.
- Open stairs use thin treads and stepped stringers with bounded depth, leaving
  the space below the upper steps open.
- L and U flights share both riser height and tread going. The first flight's
  placed run determines going; the second flight scales with its riser count.
- The upper flight's underside starts at the landing elevation.
- Landing guards subtract exact flight mouths from landing edges. They leave
  both access paths open and protect the gap between switchback flights.
- Straight-flight guards meet landing boundary guards without lateral gaps.
- Spiral guard segments rise continuously from the stair offset to its exact
  upper elevation, avoiding an extra riser above the stair.
- Stair properties show total rise, riser count, riser height, and width/diameter.
  Straight and turning stairs also show flight count and actual tread goings.

The application tooltip style and command demonstrations keep WPF's required
`StaysOpen` setting. Explicit owner-deactivation handlers still dismiss tooltips
and custom placement bounds them to their window. This prevents an automatic
hover from throwing after a tooltip has already been shown once.

The 175 mm maximum riser setting is a modeling default. It does not certify
building-code compliance, headroom, accessibility, or structural performance.

## Regression evidence

The core suite includes `orbit-pivot-checks.cs` and `stair-continuity-checks.cs`.
The WPF model workspace suite includes `orbit-interaction-checks.cs` and
`stair-workspace-checks.cs`. They exercise production camera/navigation code,
selection, project bounds, localization, generated solids, undo, and native save
and reopen. Stair geometry cases cover all five styles, four heights, and three
placement directions.

Logs are stored in `artifacts/orbit-stair-*-checks.log`. The editable stair study
and composed WPF captures are in `artifacts/model-workspace-evidence/`.

Computer Use initialization failed because its Windows native pipe could not be
connected. WPF control/event regressions and composed captures do not constitute
physical mouse injection or a complete manual GPU/performance acceptance pass.
