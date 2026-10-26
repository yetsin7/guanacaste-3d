# Capability card: family (.g3df) and template (.g3dt) containers

## User-visible workflow

1. **Archivo → Nuevo proyecto con plantilla métrica** starts a project from the
   official metric architecture template. Its levels, wall types, opening
   types, views, and sheets are copied; no walls or openings are.
2. **Archivo → Nuevo proyecto desde plantilla…** does the same from a `.g3dt`
   file. The template is only read.
3. **Archivo → Guardar como plantilla…** writes the open project's setup
   (levels, types, parameter definitions, views, schedules, sheets) to a
   `.g3dt` file. Walls, openings, rooms, floors, columns, structural elements,
   and parameter values are left out. The open project keeps its path and edits.
4. **Archivo → Guardar familia de la selección…** saves the family of the
   selected door or window as a `.g3df` file, with every type of that family
   used in the project. Types keep their identifiers.
5. **Archivo → Importar familia de puertas o ventanas…** merges a `.g3df`
   family into the project catalog. One **Deshacer** removes the whole import.

## Container format

Every native document starts with one signature line, then its JSON payload:

```
G3-FAMILY/1 sha256=<64 lowercase hex>
{ …family JSON… }
```

`G3-TEMPLATE/10 …` marks a template. The checksum covers exactly the bytes after
the first newline. The project format is unchanged (`G3-PROJECT/1` header, no
checksum). Identity comes from the signature, so renaming a file to another
extension does not change its kind. Each file kind is checked before its payload
is parsed.

| Kind | Extension | Signature | Payload | Schema |
|------|-----------|-----------|---------|--------|
| Project | `.g3d` | `G3-PROJECT/1` | `BuildingDocument` | 10 |
| Family | `.g3df` | `G3-FAMILY/1` | `FamilyDocument` (Id, Name, Category, Types) | 1 |
| Template | `.g3dt` | `G3-TEMPLATE/10` | `BuildingDocument` without model content | 10 (shared with projects) |

## Validation rules

- A family has 1–500 types, all of its category and of its own family name.
  Types obey the same rules as project types (`OpeningTypeRules.Validate`).
- A template is rejected if it contains walls, openings, rooms, floors, columns,
  structural elements, or parameter values.
- A project created from a template has a new identifier and the trimmed name
  it was given. It shares no mutable state with the template.
- Import reuses a type with the same family, name, kind, and size. A new type
  keeps its identifier when that identifier is free; otherwise it receives a new
  one. A name clash with a different size receives a numeric suffix: "Name (2)".
- A file over 20 MB is rejected before it is parsed.

## Dependencies and regeneration

`DocumentSession.ImportOpeningFamily` commits through the standard transaction
path, so validation runs on the merged catalog and nothing changes on failure.
Templates reuse `ProjectFile.DeserializeDocument`, so they follow the same
migration chain as projects.

## Known limits

- Projects now save as `.g3d`. Previous `.g3` projects and `.g3d.json` previews remain readable and require Save As to migrate.
- Family files hold type tables only (size, sill, depth, material). They do not
  yet hold a parametric geometry tree, formulas, reference planes, or nested
  families, so the door and window geometry still comes from the shared mesher.
- The Windows installer registers `.g3d`, `.g3df`, and `.g3dt` with file associations.
- The official template is defined in code (`OfficialTemplates`); it is not
  shipped as a `.g3dt` file.

## Verification

- Checks in `tests/Guanacaste3D.Checks/family-template-checks.cs` (33 assertions):
  signature and checksum, tampering, kind by signature (renamed files), schema
  from a newer version, backups, import reuse, name suffixing, identifier
  collision, mixed-category rejection, undo and redo of an import, template
  stripping, template content rejection, and project independence.
- The localization guard (`localization-checks.cs`) confirms every new Core
  message has a Spanish translation.
- Runtime: the Release build was launched and the Archivo menu was read through
  UI Automation. All five entries show their Spanish labels. The save and
  import dialogs were not driven.
