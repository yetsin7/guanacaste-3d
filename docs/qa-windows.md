# Guanacaste 3D Windows Verification

The desktop application requires Windows 10 or 11 and .NET 10. Preview features
run locally without external services.

## Build and executable checks

Run from the repository root in PowerShell:

```powershell
dotnet --info
git status --short
dotnet restore Guanacaste3D.slnx
dotnet build Guanacaste3D.slnx -c Release --no-restore
dotnet run --project tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj -c Release --no-build
```

Do not interpret missing CI logs as passing evidence. Use completed local output
or completed CI jobs with executed steps.

## Manual interaction checklist

- [ ] Launch the installed Guanacaste 3D application.
- [ ] Place walls with two clicks and assign different reusable wall types.
- [ ] Add a door and window to a selected wall and verify the openings in plan
  and 3D.
- [ ] Attempt overlapping openings and confirm that the model remains unchanged.
- [ ] Create a room from two corners and verify its area in properties and the
  project schedule.
- [ ] Create a floor from an enclosed wall extent and verify plan and 3D parity.
- [ ] Add a drawing sheet and verify its number, title block, and live plan view.
- [ ] Save and reopen a `.g3d` file without losing coordinated entities.
- [ ] Double-click a `.g3d` file and confirm that its project opens in the installed application.
- [ ] Confirm that plan, 3D, north elevation, section, and schedule update from the same model.
- [ ] Overwrite a project and confirm that the `.bak` copy exists.
- [ ] Undo and redo representative wall, opening, room, floor, and sheet edits.
- [ ] Reject malformed or unsupported JSON without replacing the open document.

## Diagnostics

When a build fails, retain the complete `dotnet build` output, including error
code, file, and line. For editor failures, record the action, reproduction steps,
non-sensitive screenshot, error message, and `dotnet --info` output.

## Current technical boundaries

Hosted openings do not yet contain complete leaves, frames, or family geometry.
The application has no production boolean-solid kernel, complex joins,
engineering certification, code-compliance validation, or complete cost model.
Do not issue the current preview as a professional construction deliverable.
