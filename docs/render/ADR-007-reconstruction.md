# ADR-007 — Internal resolution and reconstruction

**Status:** rejected as a default; the seam is kept. **Date:** 2026-10-25.

## Context

The brief asks the engine not to assume the traced resolution equals the displayed one. For a CPU path tracer the
honest way is to decouple what is cheap and sharp (direct light: sun, sky, lights, shadows) from what is expensive and
smooth (indirect light): trace direct light at full resolution with no bounces, trace the indirect light at a fraction
of the resolution, denoise it there, and reconstruct it with a guided filter that uses the *full-resolution* normals,
depths and albedo. Geometry and materials stay exact; only illumination is interpolated, so nothing can be invented.

## Experiment

`GuidedUpsampler` (joint bilateral upsampling, radiance divided by albedo before blending and multiplied back after) behind
`IUpscaler`. Compared with full-resolution path tracing plus the guided denoiser at equal traced rays per pixel (`-- --recon`).

Closed room, PSNR vs reference (rays per pixel in parentheses):

| full-res + denoise | decoupled ×0.5 |
|---|---|
| 26.8 (31) · 29.1 (64) · 31.1 (127) · 32.6 (252) · 33.8 (504) | 28.4 (34) · 30.0 (59) · 30.9 (99) · 31.8 (179) · 32.2 (322) |

Better below ~150 rays/px (+1 to +1.6 dB), equal near 200, worse above (ceiling ≈ 32 dB).

Sample house: **worse at every budget** (35.4 dB ceiling versus 42.5): at 59.8 rays/px 35.1 dB against 38.4 dB for full resolution.

**Oracle.** Taking the *converged* indirect light, shrinking it and enlarging it again (no noise at all) gives 35.6 dB
(room) and 36.9 dB (house) at ×0.5: the reconstructor's own error bounds the quality, however many samples are spent.

## Decision

Do not use internal-resolution reconstruction by default. The gain exists only at the lowest budgets in interiors and
reverses in exteriors; the radiance cache (ADR-006) gives larger gains where this one gives any.

`IUpscaler` and `GuidedUpsampler` stay (tested), as the seam for a vendor or learned upscaler. **Nothing in the
interface or documentation may call a reconstructed image "4K"**; the planner never reduces the traced size, it
reduces the output size and says so.

## Revisit when

A better reconstruction (edge-aware with variance, or a learned model) lowers the oracle error below ~40 dB.
