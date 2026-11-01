# Fotografía arquitectónica: exteriores fotorrealistas

El motor GRE de Guanacaste 3D es un **path tracer por CPU** con iluminación global, sombras suaves, reflejos, materiales PBR básicos, cámara física y reducción de ruido. Esta actualización mejora la forma en que las texturas BIM capturan el relieve y añade acabados fotográficos; **no** incorpora un trazador por GPU, desplazamiento geométrico ni generación de imágenes por IA.

## Materiales escaneados con mapa normal

1. En **Render > Materiales**, asigna una imagen de color (albedo) a una categoría BIM, como muro de piedra, fachada, piso o concreto.
2. Coloca en la misma carpeta una imagen normal-map con el **mismo nombre base**. Ejemplo: `piedra.jpg` + `piedra_NormalGL.png`. También se reconocen `_normal.png`, `_Normal.png` y `_nrm.png`. Son mapas normales con convención **OpenGL**, no DirectX.
3. Ajusta la **escala real en metros** desde el editor. El mapa normal utiliza la misma escala y proyección del mapa de color. Si no existe un archivo normal válido, se conserva el relieve derivado de la textura original.
4. Usa **Calidad Alta** o **Final** en la resolución de salida requerida. El detalle de la normal se atenúa cuando es menor que un píxel para reducir parpadeo y aliasing.

Las superficies BIM no necesitan UVs: el motor usa proyección por ejes dominantes en coordenadas del mundo. Las mallas importadas con UVs mantienen su espacio tangente original. El relieve cambia la orientación de sombreado, no el contorno de las piedras ni sus sombras geométricas. Para juntas profundas, salientes y pilares se necesita modelar esa geometría.

## Escenarios fotográficos

Se añaden dos estilos y dos cielos relacionados:

- **Exterior de piedra y jardines · día** / **Fotografía arquitectónica · natural**: contraste moderado, texturas fieles, verdes menos saturados y nitidez contenida.
- **Exterior con lámparas cálidas · anochecer** / **Fotografía arquitectónica · anochecer**: cielo frío, sombras abiertas y luces cálidas auténticas. Los apliques deben ser objetos emisores o fuentes de luz reales dentro de la escena.

La biblioteca contiene ahora acabados fotográficos de mampostería de piedra natural y piedra beige para fachadas, basados en la textura ya incluida `stone-paving`.

## Qué hace falta para obtener resultados como las fotografías de referencia

Modela la piedra, remates de pilares, barrotes, portones, lámparas y vegetación con proporciones reales; utiliza fotografías albedo y normales de alta calidad, mapas de vegetación con transparencia y luces cálidas físicamente presentes. Compón con cámara a altura humana y corrección vertical. Evita aplicar excesivo grano, saturación, desenfoque o aberración cromática.

## Comprobación y límites

Las pruebas agregadas comprueban orientación, fuerza y desvanecimiento de los mapas normales proyectados. Ejecutar:

```powershell
dotnet run -c Release --project tests/Guanacaste3D.Checks
dotnet run -c Release --project tests/Guanacaste3D.RenderBench -- --diagnostics artifacts/render-photoreal
```

No se ha verificado un compilado local en esta sesión. Antes de integrar, deben pasar CI, regresiones de imágenes y una comparación visual controlada. Futuras etapas: mapas roughness/metalness independientes, desplazamiento real de piedra, HDRI, transmisión de vidrio físicamente completa, control fotométrico de luminarias y aceleración GPU. No se declaran implementadas.
