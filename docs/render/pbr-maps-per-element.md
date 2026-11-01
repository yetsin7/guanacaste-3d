# Mapas PBR por elemento y categoría

## Qué admite Guanacaste 3D

Además del color base, cada material de render ahora puede llevar tres imágenes independientes:

| Canal | Interpretación | Formato / convención |
| --- | --- | --- |
| Normal | Dirección de la micro-superficie para recibir iluminación rasante | RGB, normal map en espacio tangente OpenGL (+Y, canal verde hacia arriba) |
| Rugosidad | Qué tan difuso es el reflejo | 0=espejo, 255=mate, canal rojo leído en espacio **lineal** |
| Metalicidad | Mezcla físicamente basada dieléctrico/metal | 0=no metal, 255=metal, canal rojo leído en espacio **lineal** |

Los mapas de normales RGB se decodifican como vectores, nunca como sRGB. Los mapas escalares se muestrean con filtrado trilineal y niveles de detalle. Esto reduce centelleo en superficies distantes. Las texturas de color existentes siguen funcionando y los mapas son opcionales.

### Flujo de edición

1. Abre el proyecto y ve a **Render → Materiales**.
2. Para una superficie general usa **Volver a materiales generales**. Para modificar un objeto concreto, haz clic sobre el elemento en la vista Render.
3. Elige un material de la biblioteca o un archivo de textura. Pulsa **Normal +**, **Rugosidad +** o **Metálico +** para cargar las imágenes de cada canal.
4. Para eliminar un mapa sin alterar los otros, usa **×** junto al canal. Los cambios de material son reversibles mediante Deshacer.
5. La vista Render muestra el resultado. La vista 3D WPF conserva su previsualización ligera y no calcula los mapas PBR detallados.

Si únicamente necesitas cambiar las propiedades PBR de un material base sin textura de color, el proyecto guarda el identificador interno `stock:`; no es un archivo.

### Compatibilidad

- Los proyectos anteriores con materiales por categoría siguen funcionando. Las nuevas asignaciones se distinguen por `ElementId`, sin duplicar la geometría ni alterar la construcción BIM.
- El render atribuye triángulos a muros, aberturas, suelos, columnas, componentes, vegetación y familias in-situ. El material elegido se aplica al elemento **solo en la categoría de cara correspondiente**, conservando otras categorías (como el revoque interior).
- Los recursos desde archivos locales se resuelven con la misma ruta de proyecto ya existente. Si falta una imagen, el motor conserva el material base y no interrumpe la sesión.
- Los mapas de rugosidad y metalicidad separados leen el **canal rojo**, no los mapas ORM combinados de glTF. No hacen falta GPU ni bibliotecas nativas de terceros.

### Limitaciones importantes

- La selección es por elemento y categoría de cara, no identifica todavía caras BIM individuales dentro de la misma categoría.
- No hay *displacement* geométrico ni subdivisión adaptativa: los mapas de normales solo cambian la normal de sombreado, no la silueta ni las dimensiones.
- Tampoco hay BRDF de capa transparente, dispersión de subsuperficie completa, HDRI ni backend GPU.
- El rendimiento de los mapas PBR depende de resolución, píxeles y número de rebotes: compara tiempos de render y memoria antes/después, sobre todo en equipos modestos.

### Validación

```powershell
dotnet run -c Release --project tests/Guanacaste3D.Checks
dotnet run -c Release --project tests/Guanacaste3D.RenderBench -- --diagnostics artifacts/pbr-comparison
```

Prueba con dos muros del mismo tipo y materiales distintos, controla la dirección solar, exporta la misma cámara en alta calidad, examina el nivel de detalle a larga distancia y confirma que Deshacer/Guardar/Reabrir conservan las asignaciones.
