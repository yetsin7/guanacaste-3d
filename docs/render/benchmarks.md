# Render benchmarks

Machine: Intel Core i7-12650H (10 cores / 16 threads, AVX2), 31.7 GB RAM, Windows 11, .NET 10, Release build. The GPU
(RTX 4050 Laptop) is **not used**. The machine was shared with other work while measuring, so timings are the minimum of
several batches and decisions rest on traced rays or sample counts ([ADR-009](ADR-009-measurement.md)).
Scenes: `BenchmarkScenes` (sample house, 1 918 triangles; closed daylit room with one window; 360-luminaire building).

## Speed of one pass (480×270)

| | house exterior | daylit room |
|---|---|---|
| Original engine (start of this work) | 33.1 ms/pass · 3.9 Mpaths/s | 51.2 ms/pass |
| Rewritten integrator, before the changes below | 53.9 ms/pass | 47.4 ms/pass |
| Now | **29–30 ms/pass** · 15 Mray/s · 3.4 rays/px | 71 ms/pass (43 ms without portals) · 7.85 rays/px |

The rewritten integrator does much more per ray (GGX materials, MIS with sun, sky portals and lights, glass crossing).
Evaluating patterns only where they are seen sharply recovered that cost (−47 % in the exterior).

## Denoising a displayable frame

| | before | after |
|---|---|---|
| 1 920×1 080 time | 1.03 s | 0.46 s (2.2× faster) |
| 1 920×1 080 allocation | 134.5 MB | 23.8 MB (−82 %) |
| 960×540 allocation | 33.6 MB | 6.0 MB |

## Quality

Each technique's table is in its ADR; the headline numbers, all against converged references:

| Question | Result | ADR |
|---|---|---|
| Closed room, sky through the window | portal sampling: 117× lower variance, ≈ 2× samples per error, same mean (ratio 1.001) | [003](ADR-003-sky-portals.md) |
| 360 luminaires | 743 → 6.9 rays per pixel; at 8 s, 35.7 vs 31.8 dB denoised | [004](ADR-004-light-sampling.md) |
| Denoiser stopped improving | house at 256 samples: 46.0 dB guided vs 42.7 dB fixed (raw 44.2) | [005](ADR-005-denoising.md) |
| Reuse of light | room +3 to +5 dB at equal rays; first frames after a camera move 3–5 dB better; outdoors −2 dB at high budget | [006](ADR-006-radiance-cache.md) |
| Render at lower resolution and reconstruct | lost: ceiling 32–35 dB versus 42 dB | [007](ADR-007-reconstruction.md) |
| 4-wide BVH | +20–40 % traversal, ≤ 6 % end to end, 3× slower build: not adopted | [002](ADR-002-acceleration-structure.md) |

## Time to first image and latency

Acceleration build (house, 1 918 triangles): 5–78 ms including portals and emitters. First pass at 480×270: ~30 ms. With the
live preview, `UpdateSettings` takes 0.2 ms (no geometry rebuild) and `SetCamera` clears the image buffers only.
A real 3.6 M-triangle Revit export builds its hierarchies in ≈ 2.3 s (the portal search used to add 46 s; now ≈ 0.1 s).

## Not measured

Frames per second of the GPU viewport, VRAM, comparisons with Lumion or V-Ray, energy use. None is claimed.
