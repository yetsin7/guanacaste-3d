# Exteriores: mejora incremental de calidad fotográfica

Referencias de diseño: muros de piedra natural iluminados lateralmente, verjas de metal negro, hojas retroiluminadas, luminarias cálidas y jardines de escala real. Estas referencias **no** son imágenes generadas por GRE ni resultados verificados del producto.

## Implementado en esta revisión

- `SurfacePattern.StoneMasonry`: juntas escalonadas y desplazadas de forma determinista, variaciones cálidas de cada pieza, bordes despostillados y mortero hundido. Se usa por defecto en `SurfaceKind.WallStone` y funciona en superficies BIM sin UV.
- El relieve de piedra altera la normal de sombreado con diferencias centrales, se desvanece con la distancia y **no** añade triángulos ni cambia las dimensiones del muro.
- `NormalMap` crea una pirámide de normales filtradas en espacio vectorial, hace muestreo bilineal/trilineal y selecciona el nivel a partir del tamaño proyectado de un píxel. El path tracer comparte la misma huella que la textura difusa en mallas UV, evitando normal maps de hojas y cortezas excesivamente afilados a distancia.
- Pruebas deterministas de variación de mampostería, continuidad de caras, filtrado de normales, imágenes de tamaño impar, validación de dimensiones y ausencia de mutación del buffer original.

## Cómo evaluar

```powershell
dotnet run -c Release --project tests/Guanacaste3D.Checks
dotnet run -c Release --project tests/Guanacaste3D.RenderBench -- --diagnostics artifacts/render-exterior
```

Comparar el mismo proyecto, hora solar, cámara, exposición y número de muestras antes/después. Para piedra fotografiada de cerca, usar luz rasante y un encuadre que permita ver las juntas. Ejecutar los golden tests de render y revisar diferencias aprobadas, sin actualizar referencias automáticamente.

## Límites pendientes para igualar las imágenes

Este cambio **no** aporta desplazamiento real de piedras/siluetas, mapas PBR completos por cara BIM, vegetación escaneada con translucencia medida, HDRI, render GPU, ni calidad fotográfica garantizada. Las fotografías de referencia incluyen geometría compleja y muchos detalles modelados; un patrón de sombreado no puede inventar faroles, forja ornamental, flores o marcos que no existan en el modelo. El siguiente paso de mayor valor es añadir materiales PBR por cara (albedo, normal, roughness, displacement opcional), activos de vegetación/faroles y escenas de comparación bajo iluminación controlada.
