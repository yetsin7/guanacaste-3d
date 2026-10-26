# ADR-006 — World-space radiance cache

**Status:** accepted for Draft/Standard and the live preview; **off** for High/Final. **Date:** 2026-10-25.

## Context

"If light on a wall was already computed, do not compute it again." An image-space accumulation throws everything away
when the camera moves; light on surfaces does not change when the camera does.

## Decision

`RadianceCache`: a hashed table keyed by a 3D cell (auto size: scene diagonal / 10, clamped 0.4–1.5 m) and the dominant
axis of the surface normal, packed losslessly in 64 bits. Values are running averages of the radiance a diffuse surface
sends back (window of 128 samples, ≥ 8 samples before use); concurrent updates can drop a sample but never store an invalid value.

* **Written** at every diffuse vertex of every path: `L_i = D_i + w_i·L_{i+1}` evaluated backwards, so the MIS weights,
  Russian roulette and clamps are already inside each estimate.
* **Read** only at vertices reached after a non-mirror bounce, on diffuse, non-emissive, non-metal surfaces. The first
  visible surface is always computed exactly. A hit ends the path, which is what saves rays.
* **Region invalidation.** The key stores the cell coordinates, so `InvalidateRegion(min, max)` drops only the cells of a
  moved wall or changed luminaire. `UpdateSettings` (light changed) clears everything; `SetCamera` keeps it.
* Capped at 2^20 entries (22 MB); a huge ground plane no longer sizes it (it once took 88 MB for the sample house).

## Evidence

Reference: **16 bounces**, no cache. (A 3-bounce reference made the cache look 10 % too bright and its PSNR fall as it
filled. The cache was right and the reference truncated; see ADR-009.) Equal traced rays per pixel, with training included:

| 240 rays/px | no cache, 6 bounces | cache (3 bounces) |
|---|---|---|
| closed room | 28.9 dB, SSIM 0.756 | **32.3 – 33.2 dB, SSIM 0.817** (3 runs) |
| sample house | 42.7 dB | 40.9 dB (−1.9) |

Camera move (room): quality at the **new** view after N rays/px since the move, PSNR:

| rays/px | 8 | 15 | 30 | 60 |
|---|---|---|---|---|
| fresh, no cache | 23.2 | 25.5 | 24.2 | 25.8 |
| cache, cold | 20.9 | 22.0 | 23.0 | 24.5 |
| cache warmed 60 rays/px at the old view | 26.5 | 28.1 | 29.6 | 31.2 |
| cache warmed 240 rays/px at the old view | **28.0** | **29.6** | **31.0** | **32.6** |

A cold cache is slightly worse than none for the first frames (learning from nothing); the benefit is the reuse.

Energy: closed room mean luminance, deep reference 0.1410; 3 bounces 0.1215; 3 bounces + cache **0.1392**.

## Costs and cautions

* The cache blurs light by up to one cell: it costs ~2 dB outdoors at high budgets, hence off in High/Final, which must be
  unbiased (apart from the firefly clamp).
* Results are not bit-reproducible (threads race on the table): ±0.5 dB between runs at 240 rays/px. The regression test
  for the cached scene has its own, wider tolerance.
* Cell sizes 0.15–0.25 m were worse than 0.75 m in the room (they fill too slowly); 1.5 m overshoots the other way.

## Revisit when

Interpolating between neighboring cells, or per-room cell sizes from the BIM room polygons, could recover the outdoor loss.
