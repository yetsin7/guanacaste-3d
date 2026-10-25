# In-place families (Generic Models, core slice)

Status: **Partial**. The domain model, geometry, validation, transactions,
persistence and scene integration are shipped and tested. The user interface
(category picker, contextual ribbon, Edit In Place mode, Finish and Cancel
commands) is not started.

Schema: `.g3` schema 10. Branch `feat/in-place-family-core`, based on
`feat/opening-live-move`, which holds schema 9.

## Evidence

| Claim | Class | Evidence |
|---|---|---|
| Release build has 0 errors and 0 warnings | Observed | `dotnet build Guanacaste3D.slnx -c Release` |
| All regression checks pass, including 34 in-place checks | Observed | `dotnet run --project tests/Guanacaste3D.Checks -c Release`: 325 `OK:` lines, exit code 0 |
| Extrusion volume equals profile area times depth, with outward faces | Observed | `SignedVolume` checks on rectangular, clockwise and 32-gon profiles |
| Solids follow the work-plane normal on vertical and inclined planes | Observed | Bounds and offset checks on planes with normals (0,1,0) and (0,1,1)/√2 |
| Schema 9 projects migrate to schema 10 without families | Observed | Migration check on a schema 9 file written from JSON |
| Real Windows UI behaves correctly | Unknown | The application was not launched; no screenshots exist |

## Implemented

- **Category catalog** (`in-place/family-categories.cs`): the 28 categories
  from the reference, each with a stable identifier, a discipline and a
  Spanish default name. Only Generic Models accepts in-place families.
- **Work planes** (`in-place/in-place-family.cs`): origin, unit normal and unit
  X axis, with Y = normal × X. Axes are validated for unit length and
  orthogonality. Coordinates use `Point3` in double precision.
- **Closed profiles** (`in-place/in-place-profile-rules.cs`): 3 to 500
  vertices. Rejects coincident adjacent vertices, zero-area loops,
  non-finite coordinates and self-crossing edges, and names the crossing
  edges. Clockwise input is normalized to counter-clockwise.
- **Extrusions**: a prism along the normal from `StartOffset` for a depth of
  1 mm to 1 km. Caps come from ear clipping of the same loop. The depth can
  be bound to a family parameter.
- **Parameters**: length parameters per family, matched case-insensitively.
  Regeneration (`InPlaceFamilyRules.Synchronize`) keeps each bound extrusion's
  identifier, material and plane, and copies the new value into its depth.
- **Transactions** (`in-place/document-session-in-place.cs`): create, rename,
  set parameter, add, update, remove extrusion, remove family. Every change
  goes through `Commit`, so undo and redo work and rejected changes leave the
  project unchanged.
- **Persistence**: `InPlaceFamilies` in `BuildingDocument`, cloned, validated
  and migrated (`ProjectFile`, schema 9 to 10).
- **Scene**: `BuildingMesher.Build` appends in-place solids, so the 3D view,
  the path tracer and the OBJ, GLB and mesh exports include them.
- **Localization**: `localization.in-place.es.xaml` holds the seven
  transaction labels. `localization.errors.es.xaml` holds Spanish text for
  the literal core messages.

## Not implemented

- The UI: category picker, contextual Create ribbon, Edit In Place mode,
  Finish and Cancel commands, and the Properties panel for families.
- Void extrusions and boolean cuts. Nothing subtracts a void from a solid yet.
- Sweeps, blends, revolves, sweep blends and their void variants.
- Interior loops (holes) in a profile.
- Reference planes, reference lines, and the Define, Show and Viewer
  work-plane commands.
- Parameter types other than length (area, volume, angle, integer, text,
  material, reference, family, type), formulas and constraints.
- Subcategories, visibility rules, material assignment in the UI, and
  representation rules.
- IFC and DXF export of in-place families. Only mesh exports include them.
- Interpolated validation messages (for example "Family 'X' has an invalid
  parameter list") are not translated. The localization guard only checks
  literal messages.
- Category `NameKey` strings are not yet loaded from a dictionary. Display
  uses `DefaultName`.
- Tolerance policy: `InPlaceProfileRules.LengthTolerance` returns a fixed
  1 µm. A design choice is pending (see the marker in the code).

## Next step

Add void extrusions as a boolean cut of the solids they intersect, then wire
the Create ribbon and the category picker to these public session methods.
