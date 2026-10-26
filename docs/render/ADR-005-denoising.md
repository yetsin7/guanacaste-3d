# ADR-005 — Denoising and reconstruction

**Status:** accepted. **Date:** 2026-10-25.

## Context

Only the indirect light is filtered (sun shadows and visible sky stay sharp). The filter in place was an edge-aware à-trous
filter of fixed strength. Measured against a 1 024-sample reference it **stopped improving**: in the house, 128 samples
denoised were worse than 128 samples raw (41.7 vs 43.8 dB). A fixed filter biases forever.

## Decision

`VarianceGuidedDenoiser` (behind `IDenoiser`): an à-trous filter whose luminance tolerance comes from each pixel's own
sample variance (after Schied et al., single frame), with the variance pre-smoothed 3×3 and the tolerance scaled by the
image's measured relative noise: wide (σ = 6) while the image is noisy, narrow (σ = 2) once it is clean (noise 0.25 → 0.05).
Scaling by *noise* rather than by sample count was necessary: the room's noise is heavy-tailed (rare sun-lit bounces), its
sample variance underestimates the truth, and a sample-count schedule made 32–64 spp **worse** than the old filter.

## Evidence (paired: every variant sees the same samples)

House exterior, PSNR vs reference:

| samples | raw | fixed filter (old) | **guided** |
|---|---|---|---|
| 8 | 30.1 | 36.8 | 36.4 |
| 32 | 36.1 | 40.1 | 40.4 |
| 64 | 39.0 | 40.7 | **42.6** |
| 128 | 41.7 | 41.5 | **44.4** |
| 256 | 44.2 | 42.7 | **46.0** |

Room: equal to the old filter within ±0.3 dB from 4 to 128 samples, +0.3 dB (SSIM +0.009) at 256. The only losses are 0.4–0.5 dB
at 8–16 samples in the house, where the old filter's extra smoothing happens to help.

Synthetic checks: noisy image error 0.401 → 0.048; a 5σ edge stays sharp; a converged image is unchanged.

## Cost

Replacing `Pow`/`Exp` in the inner loop by a power-of-two product and a table, and precomputing luminance, made it **2.2×
faster** (1 080p: 1.03 s → 0.46 s per frame, 16 threads). Working buffers come from `ArrayPool`: allocation per frame at
1 080p fell from **134.5 MB to 23.8 MB** (the result array only), which matters because those arrays live on the large object heap.

## Alternatives not adopted

* **Intel Open Image Denoise** is Apache-2.0 and the strongest CPU option for final images, but needs native binaries and
  model weights that could not be downloaded or verified here (NuGet ran at ~21 KB/s). `IDenoiser` is the adapter point;
  a plugged-in denoiser is tested (`DenoiserSeam`).
* **NRD / ReBLUR / ReLAX, DLSS Ray Reconstruction, AMD Ray Regeneration** are GPU- and vendor-bound.
* **Temporal denoising** needs consecutive frames of the same view; the still renderer accumulates instead.

## Revisit when

OIDN binaries can be obtained and benchmarked against this filter at equal time.
