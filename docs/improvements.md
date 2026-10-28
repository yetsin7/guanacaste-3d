# Improvement log

## Workspace navigation and drawing legibility

- Middle-button double-click frames the active plan or the 3D bounds. The 3D camera keeps its orientation and fits
  the bounds to the viewport aspect ratio; middle drag pans and Shift plus middle drag orbits.
- Restrict middle-click tab closing to the tab header; model content no longer triggers that close action.
- Correct the view-cube face direction. Plan controls can temporarily hide electrical and plumbing overlays without
  deleting the underlying elements; these switches are session controls, not persisted view templates.
- Visibility templates now capture the active view. Tests cover per-view wall visibility, batch application, undo and
  native project persistence; visibility for all categories and all views still needs end-to-end verification.
- Follow up: inspect the taskbar icon on the installed app and pinned shortcut, improve elevation projection and
  overlapping labels, review the sidebar, and exercise all ribbon commands with their actual model effects.

## Interactive 3D responsiveness

- Moved building mesh generation, feature-edge extraction and frozen WPF model creation off the UI thread.
- Discard stale build results and serialize pending builds so rapid edits do not publish an older scene.
- Reuse the completed mesh bounds instead of building the same model again when publishing the 3D view.
- Apply the saved 3D visibility filter to the interactive model.
- Limit feature-edge tubes to 10,000 for scenes above 100,000 triangles to reduce interactive graphics load; some fine
  outlines are omitted in those large scenes. The full model geometry remains available.
- Follow up: measure first-open and navigation latency on large projects with actual desktop interaction; linked-model
  presentation and the initial camera framing can still do work on the UI thread.
- The ten-storey test produced about 604,000 triangles. A WPF dispatcher benchmark reported 10.2 seconds until the
  scene was ready and a 7.8-second maximum timer interval, so the freeze is not yet resolved for this model.
- Desktop interaction checks passed with the asynchronous rebuild, including the textured 3D view. The benchmark does
  not establish smooth camera movement or interactive performance on other hardware.

## Residential and multistorey studies

- A two-storey house includes a roof, stair opening, bathroom and a catalog toilet associated with cold water and drainage.
- The ten-storey study is an editable test model, not a completed construction design. Its preliminary audit reports
  electrical, lighting, water-pressure and fire-protection findings that need investigation.
- Follow up: improve the residence facade, space planning, dimensions, installations and construction documentation.
- Follow up: support placement and real plumbing connectors for standalone `.g3df` plumbing families; the current toilet
  family file carries extrusion geometry and descriptive connection metadata only.
- Follow up: check stair dimensions, headroom, landing and guard requirements against the applicable local rules and a
  qualified designer's review; the existing geometry does not certify compliance.

## Validation and integration

- Preserve the other active worktrees and uncommitted changes while integrating branches.
- Resolve all current localization and test failures, then repeat the complete validation before publishing or installing.
- Verify actual installed runtime behavior, rather than treating a successful build or process launch as visual proof.
