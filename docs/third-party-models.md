# Third-party 3D models

The detailed mesh families in `src/Guanacaste3D.Desktop/resources/families/` were converted from public-domain
(CC0 1.0) models. CC0 needs no attribution; the sources are listed so the conversion can be repeated.

| Family file | Family name | Source | License |
|---|---|---|---|
| `jacaranda-tree.g3df` | Jacaranda Tree (detailed) | [Jacaranda Tree](https://polyhaven.com/a/jacaranda_tree), Poly Haven, glTF 1k | [CC0 1.0](https://polyhaven.com/license) |
| `sedan-car.g3df` | Sedan Car (detailed) | [Sedan](https://grab3d.com/models/vehicles/sedan/), Grab3D, GLB | [CC0 1.0](https://grab3d.com/license/) |
| `coupe-car-silver.g3df` | Coupe Car Silver (detailed) | [Sports Car](https://grab3d.com/models/vehicles/sports-car/), Grab3D, GLB | [CC0 1.0](https://grab3d.com/license/) |

The originals are not stored in the repository. Each family keeps its source and license in its metadata.

## What the conversion changes

`tools/mesh-family-converter` reads the model with the application's importer and writes a `.g3df` mesh family:

- Materials map onto render surfaces (`--map`), so the families use the project's materials (leaf, bark, car paint,
  tinted glass) rather than the source textures. The cars are repainted: navy for the sedan, silver for the coupe.
- The jacaranda (3.9 million triangles) is reduced to about 540,000. Trunk and branches are simplified by vertex
  clustering, and the leaves are thinned (`--thin`) to one in five whole leaves, each enlarged to keep the crown's coverage.
- Every family also stores a light preview (about 25,000 triangles) that the interactive 3D view draws; the renderer
  always uses the full mesh.

```bash
dotnet run -c Release --project tools/mesh-family-converter -- convert jacaranda_tree_1k.gltf jacaranda-tree.g3df --name "Jacaranda Tree (detailed)" --category Planting --triangles 90000 --map leaves=Leaf --map branches=Bark --map trunk=Bark --default Bark --thin leaves=0.2 --preview 25000 --meta License=CC0-1.0
```

```bash
dotnet run -c Release --project tools/mesh-family-converter -- convert sedan.glb sedan-car.g3df --name "Sedan Car (detailed)" --category Entourage --plan-shape Car --map body=CarPaint --map tire=Rubber --map glass=TintedGlass --map trim=Metal --map hubcap=Metal --default Metal --meta License=CC0-1.0
```

```bash
dotnet run -c Release --project tools/mesh-family-converter -- convert sports-car.glb coupe-car-silver.g3df --name "Coupe Car Silver (detailed)" --category Entourage --plan-shape SilverCar --map body=CarPaintSilver --map tire=Rubber --map glass=TintedGlass --map rim=Rubber --map trim=Metal --map hubcap=Metal --default Metal --meta License=CC0-1.0
```

After converting, regenerate the sample project that embeds the families:

```bash
dotnet run --project tools/sample-projects
```
