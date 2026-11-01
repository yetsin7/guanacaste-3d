# Render free navigation

The Render camera card provides a metric travel speed from 0.2 to 10 metres per
second. The default is 2 m/s, independent of the size of the project or linked
site geometry. Camera position remains shared with the modeling workspace.

Click the viewport before navigating:

| Input | Effect |
| --- | --- |
| W/A/S/D or arrow keys | Move forward, left, backward, or right |
| Q / E | Move down / up |
| Right-button drag | Look around from the current position |
| Middle-button drag | Pan |
| Shift + middle-button drag | Orbit |
| Mouse wheel | Travel along the cursor ray |
| Shift while traveling | Multiply travel speed by four |
| Alt while traveling | Reduce travel speed to one fifth |

Enable horizontal travel to maintain elevation while looking up or down.
Q/E still changes elevation explicitly. This is level navigation, without
gravity, floor snapping, or automatic stair climbing.

Collisions are off by default in Render. Keyboard and wheel travel can pass
through walls, floors, and roofs. Enable collisions to stop and slide against
the scene. This preference does not modify building geometry.

During camera movement, the live preview uses at most 144 pixels of vertical
resolution. After 250 ms without navigation, it restores the hardware-selected
resolution and progressive refinement. The preview remains a CPU ray tracer;
these changes do not imply a guaranteed frame rate or GPU rendering.

The modeling workspace retains cursor-centered surface zoom. Both workspaces
benefit from precision keyboard travel, a single camera-history entry per
continuous keyboard movement, and input suspension when the window is inactive.
The model camera uses a 2 cm near plane so large project bounds do not clip
interior objects metres ahead of the viewer.

## Validation

`flight-navigation-checks.cs` exercises metric travel, horizontal movement,
diagonal speed, invalid numeric input, wall traversal, collision opt-in, camera
history, the actual Render controls, and live-preview resolution recovery on a
WPF dispatcher. It saves `artifacts/model-workspace-evidence/render-interior-navigation.png`.

These checks exercise production navigation methods and real rendering without
injecting physical keyboard or mouse input. They do not establish full Revit
modeling parity or Lumion rendering parity.
