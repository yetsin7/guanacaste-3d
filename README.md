# Guanacaste 3D

Guanacaste 3D is an independent Windows desktop BIM and architectural CAD
application built in C# with .NET 10 and WPF.

## Status

Version 0.3.0 is a working technical preview, not a complete professional BIM
system. It must not be used for certified structural calculations or issued
construction documents without independent professional review.

Implemented capabilities include:

- Native WPF desktop workspace with a ribbon, project browser, properties
  palette, plan, coordinated 3D view, schedule, and drawing-sheet workspace.
- Persistent plan, 3D, north-elevation, section, and schedule views generated
  from the same model.
- One versioned document containing levels, reusable wall types, straight walls,
  hosted door and window openings, rooms, floor slabs, and numbered sheets.
- Parametric wall-type propagation, plan placement, selection, editing,
  transformation, deletion, and bounded undo and redo.
- Net wall volume with opening subtraction and overlap rejection.
- Live project schedule and CSV wall-quantity export.
- Official compressed `.g3d` project container with signature, schema metadata, SHA-256 integrity checking, atomic save and backup; `.g3` and `.g3d.json` remain compatible.
- Versioned `.g3df` family and `.g3dt` template persistence APIs, including a validated universal family operation graph and metric architecture template foundation.
- Automated checks for domain rules, migrations, persistence, and coordinated
  BIM elements.

Still required for broad professional parity: robust geometry and wall joins,
complete door and window objects, advanced families and constraints, elevations,
sections, associative annotation, placed sheet views, IFC and DXF exchange,
collaboration, structural and MEP authoring, physical rendering, and large-model
performance engineering.

## Requirements

- Windows 10 or 11, x64.
- Current .NET 10 SDK.
- Visual Studio with the .NET desktop workload is optional; the CLI is enough.

## Build and run

```powershell
git clone https://github.com/yetsin7/guanacaste-3d.git
cd guanacaste-3d
dotnet restore Guanacaste3D.slnx
dotnet build Guanacaste3D.slnx -c Release --no-restore
dotnet run --project src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj -c Release --no-build
```

## Run checks

```powershell
dotnet run --project tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj -c Release --no-build
```

## Publish for Windows

```powershell
dotnet publish src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj `
  -c Release -r win-x64 --self-contained false `
  -o artifacts/win-x64
```

The publish output is `artifacts/win-x64/Guanacaste3D.Desktop.exe`. Install it
for the current user and register `.g3` by running:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/install-windows.ps1
```

## Architecture

- `src/Guanacaste3D.Core/`: document model, validation, commands, history,
  schedules, and persistence.
- `src/Guanacaste3D.Desktop/`: WPF presentation and interaction.
- `tests/Guanacaste3D.Checks/`: UI-independent executable checks.
- `docs/revit-functional-parity-roadmap.md`: lawful functional-parity plan.

New projects use the official `.g3d` binary container. The application still opens and can explicitly save the earlier `.g3` preview envelope, and it can import `.g3d.json`. Family definitions use `.g3df`; project templates use `.g3dt`. The new containers are identified by their internal signature rather than extension alone and verify payload integrity before deserialization. Keep independent backups while the product remains a technical preview.
