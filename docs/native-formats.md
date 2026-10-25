# Guanacaste 3D native document containers

## Official extensions

- `.G3D` — project document.
- `.G3Df` — parametric family definition.
- `.G3Dt` — project template.

Extension matching is case-insensitive on supported platforms. Loaders verify the internal signature and document kind; the extension is a user-facing convention and is not the sole identification mechanism.

## Container v1

Every new native file starts with the ASCII signature `G3DNATV1`, followed by:

1. container version;
2. document kind;
3. schema version;
4. persistent document GUID;
5. uncompressed payload length;
6. compressed payload length;
7. SHA-256 of the uncompressed payload;
8. GZip-compressed UTF-8 JSON payload.

The reader rejects unsupported container versions, invalid kinds, impossible lengths, truncated data, decompression mismatches, hash mismatches, header/payload identity mismatches and document-specific validation failures.

Writes are performed to a sibling temporary file, flushed to disk, and then moved over the destination. When overwriting an existing file, its previous bytes are copied to `<filename>.bak` first.

## Compatibility

The earlier `.g3` + `G3-PROJECT/1` text envelope remains readable and explicitly writable so existing preview projects are not stranded. `.g3d.json` remains import-only. New Save As operations default to `.g3d`.

## Family payload

`.G3Df` currently persists the semantic regeneration model: category, hosting behavior, type/instance/shared/calculated parameters, family types, work planes, geometry-operation descriptors, dependency edges, nested-family references and metadata. Cyclic operation graphs are rejected.

This is infrastructure, not a claim that the exact CAD/B-Rep evaluator or WPF family editor is finished. Geometry-operation descriptors are intentionally separated from tessellated display meshes so a future exact kernel can regenerate authoritative solids.

## Template payload

`.G3Dt` stores metric/project initialization settings, levels, material presets, preloaded-family references, view-template names, sheet bases and other named settings. Creating a project from a template produces an independent `BuildingDocument`; subsequent project edits do not mutate the template.
