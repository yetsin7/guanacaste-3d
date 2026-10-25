# Placement and dimension validation

## Behavior delivered

Door and window commands arm a transient placement tool. Pointer movement resolves
nearby wall hosts, shows a translucent symbol and distinguishes invalid positions.
Only a valid plan click creates the opening. Escape and right click cancel the tool.
The tool remains armed after insertion for repeated placement. Missing built-in
opening types fall back to the remaining catalog or standard defaults.

Columns and rooms have pointer previews. Wall and structural line tools show a
provisional footprint before the first anchor. Straight stairs and straight guards
use shared component geometry for plan and 3D: rising steps, posts and rails.
These initial circulation tools do not implement landings, curved runs, automatic
story-height constraints or a building-code compliance check.

Clicking a temporary dimension number opens an editor directly in the plan.
Enter applies the distance; Escape cancels. Decimal commas and periods, optionally
followed by `m`, are accepted. Clear-gap edits translate the selected supported
object; wall-length and opening-width edits change that intrinsic dimension.
Opening end distances slide the instance along its host and reject collisions.
Opening movement perpendicular to its host is rejected.

The arrow promotes a temporary measurement into an associative annotation.
Annotations reference model identities, recompute after edits, survive save/reopen,
and support undo/redo. Their remove control deletes only the annotation. Deleting
an owner removes dependent annotations. Project schema 10 migrates schema 9;
templates strip annotations along with their model owners. Cardinal gaps use
axis-aligned bounding envelopes, not perpendicular distances between arbitrary
oblique faces. DXF export includes current measurement lines, architectural ticks and numeric
labels on a dedicated DIMENSION layer per level. This is a static drawing
snapshot, not native CAD constraint objects. IFC annotation export and sheet
placement are not included.

Wall corners now match faces independently of endpoint direction and type.
Miter extensions are bounded against short walls. The plan strokes the combined
wall footprint so internal seams at tees and intersections are hidden. The 3D
model still uses individual wall solids; this is not a general solid-union kernel.

## Reproducible checks

```powershell
dotnet build Guanacaste3D.slnx -c Release
dotnet run --project tests/Guanacaste3D.Checks -c Release --no-build
dotnet run --project tests/desktop-interaction-checks -c Release
./scripts/inspect-managed-assemblies.ps1
```

Core checks include the 3-to-5-meter door move, collision/out-of-bounds rejection,
undo/redo, annotation deduplication, schema migration, native serialization,
owner deletion, column clear gaps, reversed wall endpoints at multiple angles,
and real stair/guard components in the shared mesh.

Desktop checks instantiate real WPF controls with a throwaway document and route
button/key events. Placement coordinates are injected deterministically; these
checks do not constitute physical mouse automation. They verify in-place editing,
cancellation, promotion, deselection persistence and native save/reopen, and
capture the composed WPF window into `artifacts/interaction-evidence/`.

The final build and both check suites passed on 2026-10-25. Logs are in
`artifacts/final-interaction-build.log`, `artifacts/final-core-checks.log` and
`artifacts/final-desktop-checks.log`. The current-user Windows installation was
updated; its desktop assembly SHA-256 matched the published assembly. A live
installed-window check opened the editor and showed the wall preview, but another
application took foreground during pointer testing. Full physical pointer
acceptance remains unverified.

## REA integration

The official `rea-agents` 6.7.0 package is installed under the ignored
`artifacts/rea-tooling/` directory. It runs with Node 24.22.1 supplied by the local
Codex runtime. The source checkout at `C:/Dev/tools_and_scripts/rea` was inspected
but not modified. To reproduce installation with a supported Node on PATH:

```powershell
npm install --prefix artifacts/rea-tooling rea-agents@6.7.0 --no-audit --no-fund
```

`inspect-managed-assemblies.ps1` inspects assembly identity and managed members,
checks the JSON result envelopes, and records the target SHA-256 and runtime
version. It accepts explicit `-NodeExecutable`, `-AssemblyPath` and
`-OutputDirectory` parameters. Evidence lives in `artifacts/rea-evidence/`.
These are static metadata results, separate from runtime and visual checks.
No native reverse-engineering provider or MCP registration is configured.

The two master engineering briefs remain broader product direction. This change
does not implement their complete family editor, B-Rep kernel or in-place BIM
modeling system.
