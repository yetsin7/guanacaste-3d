# ADR-003 — Windows as light portals (BIM-aware sampling)

**Status:** accepted. **Date:** 2026-10-25.

## Context

In a closed room almost every cosine-distributed ray hits a wall; the few that leave through a window carry all the
daylight. Plain path tracing therefore converges slowly indoors (baseline: raw SSIM 0.56 at 128 samples in the benchmark
room). The renderer already knows where the windows are: they are the glass triangles of the model.

## Decision

`SkyPortals` merges coplanar glass panes (within 30 cm) into windows, classifies each by probing sky openness from points
1 and 2 m on both sides, and samples a point on a window as an extra light-sampling strategy with multiple importance
sampling against BSDF sampling. Selection weights and `Pdf` share one function, so the two strategies always sum to one.

Rules that came from measurement:

1. **Only when the space behind the glass is closed** (sky visible overhead < 25 % from 1 and 2 m inside). In the
   sample house, which has no roof yet, portals were a net loss: −2 dB at equal time (44.5 → 42.4 dB at 4 s), because
   every shading point paid a shadow ray for nothing. With the rule the house has 0 portals and no overhead.
2. **Edge faces are not windows.** The mesher models glass as a thin solid; its edge faces (a few cm wide) were classified
   as portals. A window must be ≥ 15 cm in both directions.
3. **Bounded cost.** At most 3 000 glass triangles are merged (largest first), 512 groups probed, 128 portals kept. The
   first version compared panes pairwise: 46 s on a real 3.6 M-triangle Revit export with 245 000 glass triangles; now
   78 ms for 500 000 glass triangles (test `PortalsOnHugeGlassModels`).

## Evidence

* Estimator, sky irradiance on the floor through the window: cosine sampling 0.0442 (variance 0.0422) versus portal
  sampling 0.0442 (variance 0.00037): same mean, **117× lower variance**. Density integrates to 1 over the sphere.
* Whole tracer, portals on versus off, 16 bounces, firefly clamp off: mean luminance ratio **1.001** (sky only) and
  **0.999** (sun + sky). Brightness differences at 3 bounces (1.053) are path-length truncation, not bias: the portal
  strategy adds one more segment of light than plain sampling reaches at equal depth.
* Equal-time A/B in the closed room (320×180, 4.1 s, raw): 29.3 dB versus 26.4 dB (≈ 2× the samples for the same error).
  After denoising the two are equal within 0.1 SSIM, because the denoiser, not noise, becomes the limit there.
* Cost: +28 ms/pass at 480×270 in the room (71 vs 43 ms).

## Also fixed on the way

The previous integrator used one hierarchy for shadows, so **glass blocked the sun** (no direct sun through windows).
Shadow rays now cross glass with Fresnel and tint. A path through glass keeps its light-sampling state, which also removes
a double count of emitters seen through glass.

## Not done

Portals are chosen by solid-angle proxy, not by BIM room membership; using the room polygons from the document would prune
portals of other rooms. Planned, not measured.
