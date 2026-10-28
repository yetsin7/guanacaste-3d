# Modeling workspace navigation and editing

Version: 0.23.0

## Interaction

The orientation cube follows the shared modeling/render camera. Click a face
center for an elevation or plan, an outer face region for an edge or corner
view, or drag the cube to orbit. The arrows rotate by 90 degrees around the
scene center. Home restores the initial three-quarter view; Fit preserves the
current orientation while framing the visible model. Framing accounts for the
actual viewport aspect ratio, including compact windows. Top and bottom targets
are vertical; free mouse look retains its existing 89-degree pitch limit.

Select an element in the modeling view, then either drag the selection or choose
Move from its context menu, ribbon, or application menu. The Move command uses a
base click followed by a destination click. Pointer movement creates a transient
ghost and metric feedback. Release or destination click commits one transaction.
Escape, interrupted capture, or window deactivation cancels a preview. Switching
views also cancels an active 3D preview.

Windows move along their host wall and vertically. Doors keep their required
zero sill. A destination on another visible wall can re-host an opening to that
wall on the same level. Invalid wall extents, negative sills, overlaps, and
cross-level hosting are rejected. Window offset, sill, and host are committed
together and restored together by undo.

Walls, columns, slabs, structural elements, and floor cavities use planar
translations. The 10 cm grid switch applies to these translations. Opening
movement remains continuous, as in the plan tool. Void cavities have selection
proxies despite having no solid material. Moving a floor opening or shaft outside
every eligible slab is rejected atomically. Point-placed openings use their
actual default footprint when resolving hosts.

## Appearance

Modeling always uses category colors and assigned textures. Wood, glass, metal,
walls, floors, and terrain retain distinct material appearances. Edges and the
service X-ray remain available in the modeling toolbar. Its light background
keeps controls legible over scene geometry; the optional navigation hint hides
at compact widths.

Gray study materials are available in Render under the scene settings. The
override applies to the rendered scene and walkthroughs, including linked-model
materials, without rewriting document assignments. Turning it off restores the
project materials. It does not turn the modeling workspace gray.

## Verification

The final Release build completed with zero warnings and zero errors. The core
suite, 92 documentation checks, 285 modeling workspace assertions, architecture
UI suite, and existing desktop interaction suite all passed.

The framework-dependent Windows x64 package was installed under the current
user's `Programs/Guanacaste 3D` directory. Installed executable, desktop assembly,
and core assembly SHA-256 hashes matched the generated package. The installed
0.23.0.0 process opened its main window and reported that it was responding.
Changes remain local to the working tree; no Git commit or remote publication
was performed.

Recorded evidence includes `artifacts/navigation-verification.log`,
`artifacts/architecture-navigation-checks.log`,
`artifacts/desktop-navigation-checks.log`,
`artifacts/navigation-installed-hashes.json`, and
`artifacts/navigation-installed-launch.json`.

Run the standard Windows gate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-windows.ps1 -SkipPackage
```

This builds the solution and runs core, documentation, and modeling workspace
checks. Existing architecture and desktop interaction suites can also be run:

```powershell
dotnet run --project tests/architecture-ui-checks/architecture-ui-checks.csproj -c Release
dotnet run --project tests/desktop-interaction-checks/desktop-interaction-checks.csproj -c Release
```

The modeling suite exercises 26 camera orientations and all framing-box corners,
compact layout at 960 by 640, selected-element movement, window height, re-hosting,
invalid edits, cancellation, undo/redo, native persistence, regenerated slab cuts,
and isolation of the render-only material override. It writes real composed WPF
captures and a sample native project under `artifacts/model-workspace-evidence/`.

Computer Use could not connect to its native pipe in this session. The evidence
therefore consists of production WPF controls, routed events, deterministic ray
picking, and composed-window captures. It does not establish physical mouse input
behavior or a manual GPU visual pass.

## Reference scope

The public [REA repository](https://github.com/morluto/rea) and its README were
reviewed online without cloning, downloading, or installing REA. No Revit 2027
binary analysis was performed. REA's documentation describes local target
inspection; reading the repository is not evidence of Revit internals.

No navigation reference images were attached to the request. The new navigation
control is an independent design and has not been compared against those images.
This increment does not establish feature parity with Revit or Lumion.
