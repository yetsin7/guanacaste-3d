# Materiales realistas: altura PBR y vistas esféricas

## Resultado de esta iteración

La galería de **Render → Materiales** muestra fotografías de material sobre una esfera virtual con
iluminación de estudio, sombreado difuso, un reflejo dependiente de la rugosidad y respuesta de
metalicidad. Es una **vista previa CPU de 132 × 84 píxeles**, no un render final ni un resultado
de path tracing. Las tarjetas de mapas PBR siguen mostrando **el mapa plano original** para
inspeccionar qué archivo está asociado.

El cargador de materiales `.g3dm` admite un **cuarto mapa opcional `heightMap`**, leído como
escala de grises **lineal** igual que el mapa independiente de rugosidad. El motor utiliza
el gradiente de altura para inclinar normales de sombreado y aumentar el volumen aparente
de poros, juntas y cortes de piedra. El efecto se reduce mediante mipmaps cuando la cámara
está lejos. **No modifica la silueta, las dimensiones ni la geometría BIM**; la opción
existente de microgeometría para `WallStone` sigue siendo independiente.

### Formato `.g3dm`

```json
{
  "version": 1,
  "name": "Piedra natural gris — PBR",
  "albedo": "piedra_albedo.png",
  "normal": "piedra_normal.png",
  "roughnessMap": "piedra_roughness.png",
  "metallicMap": "piedra_metallic.png",
  "heightMap": "piedra_height.png",
  "roughness": 0.94,
  "metallic": 0.0,
  "ior": 1.5,
  "textureSize": 1.2,
  "relief": 0.75
}
```

Coloca todas las imágenes al lado del manifiesto. Los archivos referenciados deben
permanecer **dentro de esa carpeta**, sin rutas absolutas ni `..`. Los mapas son
opcionales salvo el albedo. En el asistente que crea `.g3dm` desde una fotografía,
los nombres auxiliares aceptados son `_normal`, `_roughness`, `_metallic` y
`_height` (también `_displacement`, `_disp`, `_bump`).

La escala `textureSize` representa **metros por repetición**. Para una imagen de
piedras cuyo dibujo real representa 1,2 m, elige 1,2 m para no renderizar bloques
desproporcionadamente grandes. La propiedad `relief` regula la amplitud de la
perturbación normal; un mapa de altura no genera un muro con piezas físicas separadas.

## Presets para comparar con un muro de contención

Se incluyen acabados para **piedra gris cortada**, **arenisca labrada**, **concreto
de zapata**, **grava drenante** y **tierra de relleno compactado**. Son materiales
visuales asignables a superficies/elementos existentes, no un asistente de ingeniería
de cimentaciones. Activa la microgeometría opcional de piedra únicamente en render
final y compara el tiempo y memoria con la opción desactivada.

## Compatibilidad y presupuesto

- Los `.g3dm` antiguos sin `heightMap` siguen funcionando: el render conserva
  el relieve derivado del albedo donde ya existía.
- El nuevo canal está integrado solo en `.g3dm`, no hay todavía sustitución por
  elemento de un mapa de altura individual en el inspector. Los otros tres canales
  mantienen su editor.
- El cambio del sombreado no activa GPU ni crea un mapa de desplazamiento geométrico.
- El catálogo conserva su límite de 8 miniaturas por página, 2 decodificaciones en
  paralelo y 24 vistas previas en caché; la imagen de entrada se limita a 192 px para
  las esferas. El trabajo costoso queda fuera del hilo de interfaz.
- No se han agregado imágenes de terceros sin revisar licencia y atribución.

## Verificación

```powershell
dotnet run -c Release --project tests/Guanacaste3D.Checks
dotnet run -c Release --project tests/desktop-interaction-checks
```

Validar en Windows además con un conjunto completo que contenga `heightMap`, una
escena de pared de piedra vista de cerca y de lejos, y una comparación antes/después
de activar el relieve. El motor CPU debe seguir respetando su presupuesto de memoria.
