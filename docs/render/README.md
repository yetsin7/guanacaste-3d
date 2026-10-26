# Guanacaste Rendering Engine (GRE)

The renderer behind the Render tab, the live preview and the walkthrough tour. It is a **CPU path tracer**, written in
C# with SIMD (`System.Numerics`), that renders the real BIM model: it uses the same triangles the 3D view shows
(`BuildingMesher`), plus any linked models. It runs on any Windows machine, with or without a dedicated GPU. It does not
use the GPU today; `HardwareProfile.BackendSummary` says so to the user instead of implying otherwise
([ADR-001](ADR-001-backend.md)).

The goal that drove every decision: **the most physically coherent image for the least CPU time and memory**, measured,
never assumed. Each technique below entered only after an A/B measurement; the ones that lost are documented too.

## Pipeline

```
BuildingDocument ──► BuildingMesher ──► RenderSceneBuilder ──► RenderScene (triangles, materials, lights)
                                                                   │
        PathTracer ◄───────────────────────────────────────────────┘
          │  Bvh (opaque) + Bvh (glass)            acceleration, built once
          │  SkyPortals       windows as light portals          ADR-003
          │  LightSampler     many lights: grid + RIS           ADR-004
          │  RadianceCache    world-space reuse of bounced light ADR-006
          │  Trace()          MIS: sun · portals · lights · emitters · BSDF
          ▼
   accumulation (direct / indirect, per-pixel sample counts, variance)
          │
          ▼  IDenoiser (variance-guided à-trous)               ADR-005
   ImageProcessor: exposure · tone map · bloom · vignette · dither
          ▼
        BGRA32 → WPF
```

`RenderSession` (progressive loop, pause, ETA, memory watchdog) and `LivePreview` call the `PathTracer` façade:
`RenderPass`, `LinearImage`, `ToBgra32`, `SetCamera`, `UpdateSettings`, `Resize`, `Telemetry`, `Diagnostic`.
That façade is the seam where a future GPU engine would plug in.

## What each quality level really does

| Level | Samples (target) | Bounces | Radiance cache | Notes |
|---|---|---|---|---|
| Draft | 24 | 2 | **on** | Half resolution. Fast look at composition and light. Interiors are brighter than a 2-bounce render would be because the cache supplies deeper bounces (blurred by one cache cell). |
| Standard | 96 | 4 | **on** | Clean at screen size. Same cache note. |
| High | 320 | 5 | off | Unbiased apart from the firefly clamp and the denoiser. |
| Final | 1024 | 6 | off | Unbiased; adaptive sampling stops converged pixels early. |

`QualityPlanner.Plan(null, HardwareProfile.Detect(), …)` is **AUTO**: High on 12+ threads with 8 GB free, Standard on
6+ threads with 4 GB, Draft below that; never Final. On battery it drops to Standard with half the cores. If the image
would not fit in half the free memory, the output is shrunk (and the user is told).

Nothing here is "4K" unless it was actually traced at that size: the engine does **not** upscale (the reconstruction
experiment lost, [ADR-007](ADR-007-reconstruction.md)), and the cache is the only biased component, off in High/Final.

## Measuring

| Tool | Use |
|---|---|
| `dotnet run -c Release --project tests/Guanacaste3D.RenderBench -- --ab portals` | equal-time A/B against a converged reference |
| `… -- --ab cache` / `--ab cache-move` | equal-**rays** A/B; camera-move scenario |
| `… -- --ab denoiser-sweep` | paired, equal-**samples** comparison of denoiser settings |
| `… -- --ab lights` | RIS versus evaluating every light |
| `… -- --recon` | internal-resolution reconstruction experiment |
| `… -- --cost` | cost breakdown (patterns, bounces, portals, threads) |
| `… -- --bvh` | binary versus 4-wide BVH |
| `… -- --diagnostics <folder>` | writes every diagnostic view and a telemetry line per scene |
| `… -- --denoise-speed` | denoiser time and allocation at 320p/540p/1080p |

Method and pitfalls: [ADR-009](ADR-009-measurement.md). Results: [benchmarks.md](benchmarks.md).

Regression tests (`tests/Guanacaste3D.Checks/render-regression-checks.cs`) render four deterministic scenes and compare
them with `tests/Guanacaste3D.Checks/golden/*.png` using PSNR, SSIM, ΔE and the share of pixels out of tolerance on 4×4
block averages. After an intentional change of the picture: `G3_UPDATE_GOLDEN=1 dotnet run --project tests/Guanacaste3D.Checks -c Release`.

## Developer instruments

* `PathTracer.Telemetry()` → `RenderTelemetry`: size, triangles, lights, portals, samples/pixel, rays, Mrays/s, ms per pass,
  ms of denoising, acceleration build time, image/scene/cache bytes, cache hit rate and entries, managed memory, noise.
* `PathTracer.Diagnostic(view)` / `DiagnosticBgra32(view)`: normals, albedo, depth, world position, material id,
  roughness, metallic, sample count, noise, denoiser confidence, light complexity, cache residency, portal reach, direct and
  indirect light. Views that do not exist in a progressive CPU path tracer (overdraw, motion vectors, LOD, texture
  residency) are intentionally absent instead of faked.

## Honest status: what is not built

The brief asked for a very broad stack. Built and measured: path tracing with MIS, BIM-aware window portals, many-light
sampling, IES profiles, a world-space radiance cache with region invalidation, variance-guided denoising, adaptive
sampling, a physical camera (exposure, DoF, vertical correction), hardware detection and AUTO planning, memory budgeting,
telemetry, diagnostics, A/B and regression infrastructure.

**Not built, and why** (nothing in the interface pretends otherwise):

| Item | Reason |
|---|---|
| D3D12/DXR, Vulkan RT, CUDA/OptiX backends, mesh shaders, GPU-driven rendering, HZB, BLAS/TLAS updates, VRAM manager | No `dxc`, Vulkan SDK, CUDA or C++ toolchain on the development machine, so GPU code could not be compiled or verified ([ADR-001](ADR-001-backend.md)). |
| DLSS / FSR / XeSS / Ray Reconstruction / NRD / OIDN | GPU- or vendor-bound, or native libraries not obtainable here. The seams exist (`IDenoiser`, `IUpscaler`); nothing is wired to them. |
| ReSTIR DI/GI/PT, path guiding, neural radiance cache, DDGI, probes, SDF tracing | Prototyped selectively: RIS (the core of ReSTIR DI) and a hashed world-space cache were built and measured. The rest need GPU-scale sampling budgets or training infrastructure. |
| Virtual texturing, sparse textures, mip streaming, neural texture compression | The renderer has no image textures yet (materials are PBR parameters plus procedural patterns). |
| Subsurface scattering, clearcoat, sheen, anisotropy, displacement, thick refractive glass, dispersion, motion blur, wide gamut, HDRI environments | Not implemented. Glass is thin-walled (straight transmission with Fresnel). |
| Temporal accumulation across frames, frame generation | A still-image renderer; the live preview restarts accumulation on camera moves (the radiance cache is what carries over). |
| Cloud rendering | Out of scope for this pass. |
| Competitive comparison against Lumion / V-Ray | Not measured: they are not installed on the development machine, and claims without measurements are exactly what this project avoids. |
