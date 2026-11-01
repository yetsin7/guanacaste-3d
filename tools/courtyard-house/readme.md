# Reference house export

This tool exports the shared `BreezeBlockHouseShowcase` recipe with the detailed
tree and vehicle families embedded, reloads the native project, validates it,
and renders a portrait street view with the application's CPU renderer.

Run from the repository root:

```powershell
dotnet run --project tools/courtyard-house -c Release -- C:\Output\reference-house src/Guanacaste3D.Desktop/resources
```

The output directory receives `courtyard-house.g3d` and `courtyard-house.png`.
Use a new output directory to preserve an earlier revision. Existing files with
these names are replaced. The source resource directory is read only.

The reference photograph establishes the visible facade composition. Dimensions,
rear elevations, interior layout and building services are inferred. This is not
an as-built survey or a construction-ready design. Rendering the model does not
establish visual identity with the photograph.

The portrait render uses the project's saved camera, sun and tone mapper, with
48 sampling passes at 735 by 910 pixels. It is a review image, not evidence of
full-resolution render quality or desktop interaction. Geometry and room/family
persistence are checked after reopening the exported project.
