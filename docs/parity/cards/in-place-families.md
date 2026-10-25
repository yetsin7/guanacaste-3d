# In-place families (Generic Models, core slice)

Status: **Partial**. An in-place family is a `FamilyDocument` hosted in place. The
family model, geometry, validation, transactions, persistence in the `.g3d`
container, `.g3df` export, and scene integration are shipped and tested. The user
interface (category picker, contextual ribbon, Edit In Place mode, Finish and
Cancel commands) is not started.

Schema: project schema 9, on top of `main` (schema 8). The door-repair change
on `feat/opening-live-move` also uses schema 9; that branch must be renumbered
to schema 10 when it is merged.

## One family model

An earlier increment kept a second, parallel family model (`InPlaceFamily`,
`InPlaceExtrusion`, `WorkPlane`). It was unified into `FamilyDocument`, the model
the family foundation defines:

| Concept | Where it lives now |
|---|---|
| Family, hosting `InPlace`, category `GenericModels` | `FamilyDocument` |
| Length parameter with its default and type value | `FamilyParameterDefinition` (kind `Length`) and `FamilyTypeDefinition` |
| Work plane (origin, unit normal, X axis) | `FamilyWorkPlane`, with `FamilyPoint3` |
| Extrusion: closed profile, depth and start offset | `FamilyOperation` of kind `SolidExtrusion`, with `Profile` and `Expressions` |
| Material of an extrusion | a `Material` parameter referenced by `MaterialParameterId` |

The depth is the expression `Profundidad`, and the start offset is `Desfase`.
Each expression is a parameter name, a number, or a formula over parameters.
The formula evaluator (`FormulaParser`, `FormulaEvaluator`) reads them.

## Evidence

| Claim | Class | Evidence |
|---|---|---|
| Release build has 0 errors and 0 warnings | Observed | `dotnet build Guanacaste3D.slnx -c Release` |
| All regression checks pass: 260 `OK:` lines, exit code 0 | Observed | `dotnet run --project tests/Guanacaste3D.Checks -c Release`. The 219 existing checks still pass, and 41 are in-place checks. |
| Extrusion volume equals profile area times depth, with outward faces | Observed | `SignedVolume` checks on rectangular, clockwise and 32-gon profiles |
| Solids follow the work-plane normal on vertical and inclined planes | Observed | Bounds and offset checks on planes with normals (0,1,0) and (0,1,1)/√2 |
| A depth formula over parameters evaluates; a missing reference is rejected | Observed | Checks with `Altura * 2` and `Altura * Falta` |
| A family saved as `.g3df` reopens and regenerates the same solid | Observed | Round-trip check through `NativeDocumentStore.SaveFamily` and `LoadFamily` |
| Families survive the official `.g3d` container | Observed | Save, reopen and volume check through `ProjectFile` |
| Schema 8 projects migrate to schema 9 without families | Observed | Migration check on a schema 8 file written from JSON |
| The built-in door keeps validating; its operations have no profiles | Observed | Check on `BuiltInFamilyLibrary.CreateArchitecturalDoor()` |
| Real Windows UI behaves correctly | Unknown | The editor UI does not exist yet, so it has not been exercised |

## Implemented

- **Category catalog** (`in-place/family-categories.cs`, `BimCategoryCatalog`):
  the 28 categories from the reference, each with a stable identifier, a
  discipline and a Spanish default name. Only Generic Models accepts in-place
  families, and it maps to `FamilyCategory.GenericModels`.
- **Work planes**: `FamilyWorkPlane` values. An extrusion reuses a plane with the
  same geometry, or adds one named "Plano de extrusión". Unused planes that
  in-place extrusions created are pruned.
- **Closed profiles** (`in-place/in-place-profile-rules.cs`): 3 to 500 vertices.
  Rejects coincident adjacent vertices, zero-area loops, non-finite coordinates
  and self-crossing edges, and names the crossing edges. Clockwise input is
  normalized to counter-clockwise.
- **Validation**: `FamilyDocument.Validate` calls `FamilyGeometry.ValidateSolids`,
  which checks each profile-bearing operation: its plane, its profile, and that
  its depth (1 mm to 1 km) and offset evaluate correctly. `InPlaceFamilyRules`
  adds the project rules: in-place hosting, the Generic Models category, unique
  names, and length parameters between 1 mm and 1 km.
- **Regeneration** (`families/family-geometry.cs`): `FamilyGeometry.Tessellate`
  and `AppendTo` build the solids. `BuildingMesher` uses them, so the 3D view,
  the path tracer and the OBJ and GLB exports include them.
- **Transactions** (`in-place/document-session-in-place.cs`): create, rename,
  set parameter, add, update and remove extrusion, and remove family. Every change
  goes through `Commit`, so undo and redo work and rejected changes leave the
  project unchanged.
- **Persistence**: `BuildingDocument.InPlaceFamilies` is a list of
  `FamilyDocument` values, cloned through `FamilyDocument.Clone()`, validated and
  migrated from schema 8 (`ProjectFile`). It is stored in the `.g3d` container.
- **Family files**: a family saves and reopens as `.g3df` through
  `NativeDocumentStore.SaveFamily` and `LoadFamily`.
- **Localization**: `localization.in-place.es.xaml` holds the seven transaction
  labels. `localization.errors.es.xaml` holds Spanish text for the literal core
  messages.

## Not implemented

- The UI: category picker, contextual Create ribbon, Edit In Place mode,
  Finish and Cancel commands, and the Properties panel for families.
- Void extrusions and boolean cuts. A `VoidExtrusion` with a profile is validated
  but does not yet subtract from any solid.
- Sweeps, blends, revolves, sweep blends and their void variants. Their operation
  kinds exist in `FamilyOperationKind`, but they have no geometry.
- Interior loops (holes) in a profile.
- Reference planes, reference lines, and the Define, Show and Viewer work-plane
  commands.
- Parameter kinds other than length, except material text. Formulas and
  constraints are supported through the depth expression only.
- Nested families in an in-place family.
- Built-in door and window operations have no geometry yet. Their operations
  need an exact-kernel evaluation first.
- IFC and DXF export of in-place families. Only mesh exports include them.
- Interpolated validation messages (for example "Family 'X' has an invalid
  parameter list") are not translated. The localization guard only checks
  literal messages.
- Schema 9 projects saved by the earlier in-place commit (`b445e43`, which used
  a separate `InPlaceFamily` shape) cannot be read after this unification. No
  such files exist outside the test suite, but they must be rejected explicitly
  if they turn up.
- Tolerance policy: `InPlaceProfileRules.LengthTolerance` returns a fixed
  1 µm. A design choice is pending (see the marker in the code).

## Next step

Add void extrusions as a boolean cut of the solids they intersect, then wire
the Create ribbon and the category picker to the public session methods.
