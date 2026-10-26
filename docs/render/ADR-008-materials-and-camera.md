# ADR-008 — Materials, units and the physical camera

**Status:** accepted. **Date:** 2026-10-25.

## Materials

`RenderMaterial` is a small, physically based subset (in the spirit of OpenPBR, not a conforming implementation):
base color, roughness, metallic, index of refraction (sets dielectric F0), emission, thin-walled transmission with tint,
and a procedural surface pattern. Shading is Lambert reduced by Fresnel plus a GGX microfacet lobe with Smith visibility,
sampled by visible normals; mirrors (roughness ≤ 0.05) are deltas; glass is thin-walled with Fresnel reflection and straight
transmission. BIM surface categories map to stock materials (`MaterialLibrary.ForKind`); imported models bring their own.

Patterns (tiles, planks, blocks, concrete, grass) are cosmetic surface detail computed from world position, so they need
no texture coordinates. They are evaluated sharply only where they are seen sharply (ADR-002).

**Not implemented:** subsurface scattering, clearcoat, sheen, anisotropy, displacement, thick refractive glass with
absorption, dispersion, image textures, normal maps. These are listed so that nothing in the interface suggests them.

## Units

Radiance is in renderer units where one unit of irradiance is 15 915 lux (the default sun, 2π units, is 100 000 lux).
`SkyModel` is the Preetham clear sky with a Kasten–Young air-mass sun color, plus the CIE overcast sky. Candelas convert
with `RenderUnits.FromCandela`.

## Physical camera (`PhotographicCamera`)

* **Exposure.** `Exposure(f-number, shutter, ISO, compensation)`: the renderer's default multiplier (0.4) is exactly the
  *Sunny 16* setting (f/16, 1/100 s, ISO 100); every other setting is expressed in stops from it. Equivalent exposures
  (f/8 at 1/400 s) give the same value; each stop doubles it.
* **Calibration check.** An 18 % grey card lit by the default noon sun, photographed with Sunny 16, reads **0.156** scene-referred
  (mid-grey is 0.18): within a fifth of a stop.
* **Field of view** from focal length and sensor height (a 24 mm lens on a 24 mm-high sensor sees 53.1° vertically).
* **Depth of field** from the lens opening, `focal/(2·N)`; a 135 mm f/1 lens focused at 2 m softens an edge 12 m away by
  the thin-lens amount (the steepest step falls to ~0.6 of the pinhole's).
* **Vertical correction** (shift lens) keeps building edges vertical when the camera tilts.

**Not implemented:** motion blur, wide-gamut output, color management beyond sRGB, physically based glare (the bloom in
`ImageFinish` is a modest, energy-limited glow).

## Tone mapping

Display transform is ACES-style with optional auto exposure, saturation, bloom, vignette and dither
(`ImageProcessor`). A neutral, hue-preserving curve would suit color-critical review better; not evaluated here.
