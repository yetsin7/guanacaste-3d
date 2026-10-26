# ADR-002 — Acceleration structure

**Status:** accepted (keep the binary BVH). **Date:** 2026-10-25.

## Context

Every ray goes through a bounding-volume hierarchy (`Bvh`: binned SAH, leaf-ordered triangles, parallel build for large
inputs). Two hierarchies exist per scene: opaque triangles and transmissive ones, so shadow rays can cross glass.

## Experiment

A 4-wide BVH (`tests/Guanacaste3D.RenderBench/bvh4-experiment-tree.cs`): the SAH binary tree collapsed so each node tests
four child boxes with one SIMD slab test, ordered traversal, triangles in leaf order. Compared with the existing binary
BVH on incoherent rays, 16 threads (`-- --bvh`):

| Scene | binary closest-hit | 4-wide closest-hit | binary any-hit | 4-wide any-hit | build binary / 4-wide |
|---|---|---|---|---|---|
| sample house (1 918 tris) | 52.0 Mray/s | 61.7–64.8 Mray/s | 57.6 | 61.4 | 23 / 17 ms |
| 600 000 random triangles | 2.0 | 2.7–3.1 | 2.4–2.6 | 3.5–3.6 | 349 / 986 ms |

Unresolved: 3 disagreements in 20 000 rays on the house scene (0 on the large one); cause not found (candidates:
slab-test rounding on zero-thickness boxes).

## Where the time goes

`-- --cost` (480×270, house): bouncing paths run at ~15 Mray/s overall while pure traversal runs at ~55–65 Mray/s, so
**traversal is about a fifth of a frame**; shading, pattern evaluation, light sampling and tone code are the rest. A
traversal speed-up of 1.2–1.4× is therefore worth at most ~4–6 % end to end.

## Decision

Do **not** adopt the 4-wide BVH: the end-to-end gain is small, its build is 3× slower on large models (the user's
real 3.6 M-triangle model matters more than ray speed), and it carries an unexplained mismatch. The code stays in the
benchmark project as the reference for a future attempt.

What did pay off was in shading: procedural patterns were 40 % of the exterior frame (53.9 → 28.3 ms/pass at 480×270)
and are now evaluated only where they are seen sharply (first hit and mirror chains; the pattern's mean elsewhere).

## Revisit when

Shading is no longer dominant (e.g. after batching shading across rays), or a 4-wide build can be made parallel.
