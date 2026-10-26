# ADR-001 — Rendering backend

**Status:** accepted. **Date:** 2026-10-25.

## Context

The brief asked for the best quality per unit of GPU, CPU, memory and energy, and told us not to assume any technology.
The application is a WPF shell over a UI-independent .NET 10 core; the existing viewport is WPF `Viewport3D` and the
existing renderer is a CPU path tracer.

Development machine (the only one measured): Intel i7-12650H (10 cores / 16 threads, 256-bit SIMD), 31.7 GB RAM,
NVIDIA GeForce RTX 4050 Laptop (6 GB, ray-tracing hardware) plus Intel UHD Graphics.

## Options

| | Option | Evidence |
|---|---|---|
| A | D3D12 + DXR in a native module | Needs `dxc` and a C++ toolchain: **none installed** (`dxc`, `glslc`, `nvcc`, `cmake`, `cl` all absent). Not compilable or testable here. |
| B | Vulkan ray tracing | Same: no Vulkan SDK. |
| C | CUDA / OptiX | No CUDA toolkit; NVIDIA-only. |
| D | CPU path tracer in C# with SIMD (current) | Works today; portable to any Windows PC including laptops with only an integrated GPU. NuGet downloads run at ~21 KB/s on this machine, which also rules out pulling large native SDKs to evaluate them. |

## Decision

**D.** A GPU backend written blind would be unverifiable code in the most performance-critical, hardest-to-debug part
of the product. The CPU engine also serves the stated requirement that a laptop without a dedicated GPU must not be
excluded.

Seams kept so a GPU engine can be added later without touching the application:

* `PathTracer` is the façade used by `RenderSession`, `LivePreview` and the tour; a GPU engine would expose the same
  methods.
* `IDenoiser` and `IUpscaler` are interfaces; `HardwareProfile` already detects vendor, VRAM and (inferred) RT hardware.
* The scene description (`RenderScene`, `RenderMaterial`, `RenderLight`) contains no CPU-specific structure.

## Consequences

* Throughput ceiling is the CPU: ~15 Mray/s on 16 threads in the sample scenes ([benchmarks](benchmarks.md)).
* The user-visible text states the CPU backend and that the GPU is detected but unused.
* No GPU speed-up is claimed anywhere, because none was measured.

## Revisit when

`dxc` or a Vulkan SDK is available on the build machine, or a second developer with native GPU experience joins.
