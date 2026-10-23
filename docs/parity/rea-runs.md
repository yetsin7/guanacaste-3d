# REA runs

## Tool setup

| Item | Value |
|------|-------|
| REA | `rea-agents` 6.7.0 from the official npm package (`npm install --global`), CLI only; no agent MCP registration |
| Why not the source checkout | The official CONTRIBUTING guide states that Windows source checkouts lack the native addon and its build requires Linux; the npm package ships it |
| Node | 22.23.4 (REA requires `^22.23.4`). The system winget upgrade stalled on an unanswered UAC prompt, so the signed OpenJS MSI (`SHA-256 CA9303D3…427E`, Authenticode valid, OpenJS Foundation) was extracted without elevation (`msiexec /a`) into the session scratchpad |
| Network | npm registry around 21 KB/s with timeouts; installation needed throttled retries (`--maxsockets=2`) |
| `rea doctor` | Node and host checks healthy; no native provider (Ghidra/Hopper/IDA) configured, so native analysis is out of scope for these runs |

## Runs (2026-10-23)

All runs are static: REA reads PE/CLI metadata and does not load or execute
target code. Evidence JSON files stay in the session scratchpad
(`rea-evidence/`); their evidence ids are quoted so results can be
re-verified.

| # | Command | Target (SHA-256 prefix) | Result | Coverage and limits |
|---|---------|-------------------------|--------|---------------------|
| R1 | `inspect-managed-artifact` | `RevitAPI.dll` (`bb4a5b3d`) | `ev_d1e60c06…`: PE32+, x86_64, CLI runtime 2.5, metadata v4.0.30319, classified **`cpp-cli-mixed`** (ILONLY clear, native entry point). Tables: 20,378 TypeDef, 63,866 MethodDef, 13,443 Property, 2,123 ImplMap, 385 AssemblyRef | Complete identity |
| R2 | `inspect-managed-artifact` | `RevitAPIUI.dll` (`ff73f6fe`), `RevitDBAPI.dll` (`ab1fac23`), `RevitAPIIFC.dll` | Identity evidence recorded | Complete identity |
| R3 | `inspect-managed-members` (types Level, Wall, TransactionGroup, FamilySymbol) | `RevitAPI.dll` | **Unavailable**: "Managed metadata exceeds the 67108864 byte representation budget" | Hard REA resource limit; recorded, not worked around |
| R4 | `inspect-managed-native-boundaries` | `RevitAPI.dll` | **Unavailable** (same budget limit) | As R3 |
| R5 | `inspect-managed-members` | `RevitAPIUI.dll` | Unavailable (same budget limit) | As R3 |
| R6 | `inspect-managed-native-boundaries` | `RevitAPIUI.dll` | Complete: 2 module refs, 609 P/Invoke imports, 663 native implementations | Static declarations only; native export resolution not performed |
| R7 | `inspect-managed-members` + `inspect-managed-native-boundaries` | `RevitAPIIFC.dll` | Members complete: 172 types (46 in `Autodesk.Revit.DB.IFC`), 1,182 methods, 8,244 call edges; 59 P/Invoke, 70 native implementations | Members marked partial by REA for C++/CLI details |
| R8 | `inspect-managed-members` + native boundaries | `RevitAPIFoundation.dll` | 3,678 types (mostly C++ implementation details), 2,227 methods, 159 P/Invoke, 210 native implementations | Partial |
| R9 | `project-managed-application-graph` | R7 evidence for `RevitAPIIFC.dll` | Graph: 2,137 nodes, 2,136 edges, 1 assembly, 172 types, 1,182 methods, 651 fields | Partial |
| R10 | `compare-managed-members` | Guanacaste3D.Core 0.3 (`982e85e`) vs 0.6 (HEAD) | 175 unchanged, 425 changed, 1,065 added, 10 removed, 39 unknown. The only public removal is `DocumentSession.AddOpening(Guid, OpeningKind, double?, double, double, double)`, replaced by a nullable-size overload | Exact type+name+signature matching |

## Conclusions

| Conclusion | Class | Basis |
|------------|-------|-------|
| The Revit .NET API is a C++/CLI mixed-mode layer: managed signatures over native implementations | Observed | R1 classification `cpp-cli-mixed`, ILONLY clear; R6–R8 native implementation counts |
| Behavior such as regeneration, joins, and geometry lives on the native side of these boundaries and can't be read from managed metadata | Derived | R1 and R6–R8 together with the REA boundary note in the roadmap |
| Most of RevitAPI.dll's 20,378 metadata types are C++ implementation details rather than the 2,866 documented types | Inferred | Ratio between R1 and E1; R8 shows the same pattern in a smaller assembly. Verifiable with a member census once REA's budget allows it |
| Guanacaste 0.6 changed one public signature compared to 0.3 (binary break for compiled add-ins, source compatible) | Observed | R10 |

## Consequences for Guanacaste 3D

1. Requirements come from the documented API (`RevitAPI.xml`), public
   documentation, and behavior tests, not from Revit's native implementation,
   which managed static analysis cannot reach (R3–R6).
2. **Plugin API rule** (from R10): when the public plugin API is published,
   signatures are frozen, and changes add overloads or new members. R10 is
   repeated on every release as a compatibility gate.
3. Native analysis would need a configured provider (Ghidra, Hopper, or IDA)
   and stays limited to authorized, read-only, evidence-scoped questions. None
   are needed for the increments delivered so far.
