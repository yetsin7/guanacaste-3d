# Parity evidence log

Every conclusion in the capability cards cites an entry here, or an artifact
listed here. Classification: **Observed**, **Derived**, **Inferred**, **Unknown**.
An inference is never reported as an observation.

## Scope and authorization

- Target: the local installation of Autodesk Revit 2027 at
  `C:\Program Files\Autodesk\Revit 2027`, which the user authorized for local,
  static, documentary, and observable-behavior analysis.
- The user approved metadata inspection of public and internal .NET assemblies.
  Analysis is read-only. No Autodesk code, resources, or icons are copied into
  Guanacaste 3D. Native core binaries are not disassembled.
- RVT/RFA remain unsupported interoperability targets until a documented,
  licensed route is validated with real tests.

## Inventory of analyzed artifacts

Collected 2026-10-23 with `sha256sum` and Windows file version info.

| Artifact | File version | Bytes | SHA-256 |
|----------|--------------|-------|---------|
| `RevitAPI.dll` | 27.0.10.13 | 33989464 | `bb4a5b3dec4e140527c311fbafc2e676e34594330748be79c0654a46d521cb89` |
| `RevitAPIUI.dll` | 27.0.10.13 | 3373912 | `ff73f6fec407e2bca84ef3208258810e36d70b3a88f012b480333a706f2eb3c1` |
| `RevitAPI.xml` | n/a | 13091543 | `89afc5c61a927b71ad57e0a7f56b50a5096ac676123e352cf28ea45d2f4c1beb` |
| `RevitAPIUI.xml` | n/a | 1172962 | `6ade0e085c09f903435db067d41d1da1af67a327235bdecfb0e7594d7a5df5c2` |
| `RevitDB.dll` | 27.0.10.13 | 48301912 | `1629853f1c5d52dab3ba1ea573d0ea8340d8d5497a0f44004c122a39ae4ca86a` |
| `RevitDBAPI.dll` | 27.0.10.13 | 17118552 | `ab1fac23eff7a441999d970b0ae4de355514bcf1a78b72c77001f56087b8f753` |

The installation directory contains 468 DLLs and more than 50 add-in folders
(for example `IFCExporterUI`, `BatchPrint`, `ProjectBrowser`, `MEPCalculation`,
`CoordinationModel`, `IssuesManagement`).

## Entries

### E1. Public API documentation census (Observed)

- Method: an XML parse of `RevitAPI.xml` (script kept in the session
  scratchpad; it only reads `<member name>` and `<summary>`).
- Result: 2,866 types, 9,624 methods, 13,444 properties, 7,239 fields, and 163
  events. Largest namespaces: `Autodesk.Revit.DB` (1,440 types), `DB.Structure`
  (257), `DB.BuiltInFailures` (169), `DB.Electrical` (112), `DB.Analysis`
  (106), `DB.Visual` (103), `DB.Architecture` (86), `DB.Events` (76),
  `DB.Mechanical` (71).
- Use: anchor types for the 20 capability areas were checked for presence. All
  anchors were found in `RevitAPI.xml` or `RevitAPIUI.xml`, except
  `TitleBlockPlacement`, `DirectContext3DDocument`, and
  `BackgroundProcessTaskManager`, which are not documented types under those names.

### E2. Transactions (Observed)

`Transaction.Start(string)`, `Commit`, `RollBack`, `GetStatus`, and
`SetFailureHandlingOptions`. `TransactionGroup.Assimilate` is documented as
merging inner transactions into one undo item. `TransactionStatus` values are
Uninitialized, Started, RolledBack, Committed, Pending, Error, and Proceed.

### E3. Parameters (Observed)

Definitions (`Definition`, `InternalDefinition`, `ExternalDefinition`), bindings
(`InstanceBinding`, `TypeBinding`, `BindingMap`, `CategorySet`), storage
(`StorageType`: Integer, Double, String, ElementId), specs and groups
(`SpecTypeId`, `GroupTypeId`, `ForgeTypeId`), unit text
(`Parameter.SetValueString`, `AsValueString`), bulk set (`Parameter.SetMultiple`),
and shared identity (`Parameter.GUID`, `IsShared`).

### E4. Revit internals behind parameters and transactions (Unknown)

Undo storage strategy, value layout, and regeneration scheduling inside the
native core are not exposed by the public API. Route: not pursued; the
Guanacaste design does not depend on them.

### E5. REA managed-assembly inspection

See [rea-runs.md](rea-runs.md) for commands, tool versions, coverage, and limits.
