# Reference house photographs and orbit

Windows / .NET 10. This tool prepares the **Casa terraza** reference model, loads the existing detailed plant families, saves textured materials and three camera bookmarks, then renders with Guanacaste 3D's own CPU path tracer. It does not generate or retouch images with AI.

From the repository root:

```powershell
dotnet run --project tools/reference-house -c Release -- artifacts/reference-house
dotnet run --project tools/house-photo-render -c Release -- artifacts/reference-house/Casa-terraza-30x40.g3d src/Guanacaste3D.Desktop/resources artifacts/house-photos 1280 192
```

Arguments: input project, desktop resources directory, output directory, optional width (default 1600), samples (default 320), and shot (`all`, `01`, `02`, `03`, `video`). Start with `640 16` to inspect composition. Detailed families must already be installed in the desktop resources directory; missing assets fail explicitly. On laptops render one process at a time (the detailed scene has roughly 3.7 million triangles).

The output includes PNG photographs, a separate editable `.g3d`, copied texture assets and an explicit light manifest. Keep the textures folder beside the project. Original input is preserved. The extra explicit photographic lights are recreated by this tool and recorded in the JSON manifest; opening the `.g3d` in the desktop restores fixtures/materials/cameras but does not import that external light rig automatically.

## Orbit video

```powershell
dotnet run --project tools/house-photo-render -c Release -- artifacts/reference-house/Casa-terraza-30x40.g3d src/Guanacaste3D.Desktop/resources artifacts/house-video 800 12 video
 # The native Windows encoder writes Casa-terraza.mp4 alongside the rendered frames.
```

The 60 actual 3D camera positions form a 10-second rising orbit. Windows Media Foundation encodes the 6 source frames/second directly to H.264 MP4. It does not add music. Review moving foliage and railing edges for interpolation artifacts. Increase source resolution/samples for less noise; the video keeps exposure fixed between frames. The render uses deterministic plant placement and a smooth camera path.

## Scope and validation

This is a house-specific authoring/photography recipe, not a universal renderer. Dimensions are adapted to the 30 × 40 ft lot because the reference's printed room sizes conflict with its overall dimensions. Terrace furniture, soffit and surrounding landscape are interpretive additions. This is not construction documentation.

The accompanying regression check exercises the three native thin ceiling presets, persistence, meshing, valid thickness boundaries and rejection of invalid floor/roof/ceiling thicknesses:

```powershell
dotnet run --project tests/ceiling-thickness-checks -c Release
```

