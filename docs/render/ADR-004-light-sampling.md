# ADR-004 — Many lights, IES and light units

**Status:** accepted. **Date:** 2026-10-25.

## Context

The integrator traced one shadow ray per light at every shading point: cost linear in the number of luminaires. A building
with hundreds of lights must not cost hundreds of rays per vertex.

## Decision

`LightSampler`:

* **Grid with range culling.** Lights are bucketed in a lazily built grid (cells of diagonal/40 clamped to 1.5–8 m). A cell
  keeps only the lights that can add at least `LightCullThreshold` (0.0005 irradiance) to it, with a proposal table that
  favors nearer, brighter lights. Cells are built on first use, so rooms no camera sees cost nothing.
* **Resampled importance sampling (RIS) with a one-slot reservoir.** With more than 4 lights in the cell, 16 candidates
  (`LightCandidates`) are drawn from the proposal, scored by their unshadowed contribution, one is kept, and a single
  shadow ray is traced. The estimator weight is Σw/(M·p̂). With 4 lights or fewer, all are evaluated exactly.
* **IES (LM-63, photometric type C).** `IesProfile` parses and interpolates the candela table, applies the file's declared
  symmetry, and integrates luminous flux. `RenderLight.Ies` points it along the luminaire axis. Other photometric types
  are rejected, not guessed.
* **Units.** One renderer unit of irradiance = 15 915 lux, so the default sun is a 100 000 lux clear noon;
  `RenderUnits.FromCandela` converts candelas. A 1 000 lm downlight is correctly dim next to daylight.

## Evidence (360 luminaires in 30 rooms, 240×135, reference = RIS 1 500 samples)

| Method | rays/pixel/sample | equal time 8 s, raw | equal time 8 s, denoised |
|---|---|---|---|
| every light, no cull | 743 | 19.6 dB | 31.8 dB |
| RIS 4 candidates | 6.6 | 26.5 | 34.1 |
| RIS 8 | 6.9 | 26.5 | 35.6 |
| **RIS 16** | 6.9 | 26.5 | **35.7** |

At 1 s the exhaustive method has the better SSIM (0.77 vs 0.63 for RIS 16): each of its samples sees every light. RIS
overtakes it from about 3–4 s and its cost does not grow with the number of lights; at 3 000 lights exhaustive would be
roughly ten times worse again.

Unbiased: irradiance at a point from 300 lights, all lights **2.43797**, RIS(16) **2.43767**, RIS(16)+cull **2.43767**
(0.01 % apart, 400 000 draws). IES: a 200 cd cosine downlight reads 200 cd straight down, 100 cd at 60°, dark upward;
integrated flux of an isotropic 100 cd source is 4π·100 lm within 0.5 %.

## Not done

Emissive-triangle lights are sampled by area × luminance without spatial culling (a scene with 200 000 emissive triangles
is capped). Area/line lights beyond emissive geometry are not separate types.
