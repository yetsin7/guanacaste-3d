# ADR-009 — How renderer decisions are measured

**Status:** accepted. **Date:** 2026-10-25.

Every claim in these ADRs comes from `tests/Guanacaste3D.RenderBench` and the checks in `tests/Guanacaste3D.Checks`.
Rules, each learned from a result that would otherwise have been wrong:

1. **Score against a converged reference of the same algorithm** (PSNR, SSIM, CIELAB ΔE; `ImageMetrics`). "Looks nicer" is
   not a measurement. Images are compared after exposure and tone mapping, because that is what people see.
2. **The reference must be deep enough.** A 3-bounce reference made the radiance cache look 10 % too bright and its error
   *grow* with more samples. The cache was supplying real bounces the reference had cut off (3 → 16 bounces adds ~20 % to
   the closed room). Compare against 16 bounces; compare brightness only at depth ≥ 16.
3. **Pick the right axis for the technique.** Post-processing (denoising) is compared at **equal samples**, paired:
   sampling is deterministic per pixel, so every variant sees the same samples. Techniques that change the cost of a
   sample (cache, light sampling, resolution) are compared at **equal traced rays** or **equal render time**.
4. **Wall-clock numbers need protection from other programs.** The machine was shared with other tasks and some timed runs
   varied 3×. Timings use the minimum of several short batches; decisions rest on rays or samples, not seconds.
5. **A test that cannot fail protects nothing.** Each regression comparison has controls that must fail (half the sun; one
   bounce without portals) and one that must pass (another noise seed). Measured margin: another seed 40.9 dB, half the
   sun 24.0 dB, no portals 12.1 dB.
6. **Compare in the frequency band that matters.** Full-resolution SSIM of two independent noise realizations of the same
   room was 0.897; on 4×4 block averages it is 0.987, while real changes of light stay far apart. Regression compares
   block averages.
7. **Look at the images.** Two defects were invisible to the metrics and obvious to the eye: a many-lights scene exposed
   so brightly that it clipped to white (which also inflated its scores), and a diagnostic view that flickered on walls
   lying on whole-meter planes.
8. **Fix the scene before trusting a verdict.** The first portal result was bad because the sample house has no roof; the
   fix was a rule about *closed* spaces, not a tweak to the technique.

## Baseline

The same two scenes were measured before any change (house exterior and a closed room with one window, 480×270, 16
threads): **3.9 Mpaths/s** and 33 ms/pass (house), and the original denoiser stopped improving at ~41.7 dB. See
[benchmarks.md](benchmarks.md) for before and after.
