# Model audit

`ModelAudit.Analyze` performs a read-only review of a validated `.g3` document.
It reports stable element and level identifiers, a finding code, and a metric
measurement. The model remains editable even when findings exist; these are
designer review items rather than file-validation failures.

Current checks:

- **NearWallGap:** a wall endpoint ends within 100 mm of another wall segment
  on the same level without entering its body. The measured centerline gap is
  returned in meters. A 2 mm tolerance suppresses coincident ends.
- **OverlappingRooms:** two rectangular room annotations on one level overlap
  by more than 0.01 m². The returned value is intersection area in m².
- **LowOpeningHeadClearance:** less than 100 mm remains between the top of a
  door or window and the top of its host wall. The returned value is meters.

The wall check indexes segments in 5 m spatial cells before measuring nearby
endpoints. Very long segments use an overflow list so indexing them cannot
allocate an excessive number of cells. Results are sorted for repeatable
reviews and recomputed from current geometry after an edit or save/reopen.

The API intentionally returns codes instead of visible strings. A future
interface can localize messages and navigate to the referenced element. It
does not yet check structural capacity, egress, room closure, clash volumes,
or building-code compliance.

Validation: `dotnet run --project tests/model-audit-checks -c Release`. Checks
cover all three findings, cross-level isolation, joined walls, immutability,
native round-trip stability, and clearing a finding by fixing the geometry.

To inspect a saved project from a terminal, run:

```powershell
dotnet run --project tools/project-audit -c Release -- path/to/project.g3
```

The command prints a JSON report with the project identity, finding count,
codes, element references and measurements. A nonzero exit code indicates an
invalid argument or project file; advisory findings themselves exit normally.
The CLI was run against a native saved family project and returned a valid
empty JSON report. An older interaction fixture was rejected because its type
catalog failed current project validation; the CLI returned a structured error.
