# Pointer input quality and validation

## Improvements

Navigation ignores pointer jitter within four device-independent pixels of the
press position. A right click no longer rotates the camera before opening its
context menu. A middle click does not pan accidentally. Once movement crosses
the threshold, the complete displacement from the original press is applied.
Selected-element dragging uses the same threshold before starting a preview.

Camera locking now blocks an already-active pointer gesture. Samples received
while locked update the pointer origin so unlocking does not produce a jump.
Lost capture still cancels a model drag exactly once.

Wheel zoom uses the full delta, including fractional notches from precision
mice. Eight 15-unit samples compose to one 120-unit notch. Opposite wheel
directions restore the original focus distance unless the scene-scale or
surface-clearance bounds intervene. Zero-delta events do not redraw the model.

## Automated validation

`tests/model-workspace-checks/pointer-gesture-checks.cs` exercises production
pointer handlers with deterministic samples. It checks camera position, pivot
projection, redraw notifications, model edit claiming, capture cancellation,
lock/unlock continuity, and wheel precision. These are automatic WPF checks,
not physical mouse injection.

The local Release build passed without warnings or errors. The model workspace
suite passed 411 assertions in `artifacts/pointer-input-workspace-checks.log`.
Build output is recorded in `artifacts/pointer-input-build.log`.

## Native control retry

The initial Computer Use backend failed with a missing native pipe. A later
unified Computer Use backend successfully enumerated native windows, launched
Guanacaste 3D, and returned accessibility state and native screenshots. Input
attempts then encountered these backend errors:

- `unknown screenshotId screenshot-0`
- `coordinate input geometry is unavailable`
- A point-over-another-process rejection while a test window overlapped the app.
- `window bounds changed; call get_window_state before using this window`
- `call get_window_state before using this window` immediately after refreshing
  the target with accessibility state and a screenshot.

The backend was reset and the target was rebound. Raising the target succeeded,
but a subsequent click was still rejected. No completed physical mouse gesture
or Shift-plus-middle drag is claimed by this validation.
