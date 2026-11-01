# Cielo HDRI Radiance en el render CPU

En **Render → Escena → Iluminación ambiental HDRI**, usa **Cargar HDRI…** y elige un panorama equirectangular con formato Radiance RGBE (`.hdr`). El panorama sustituye el modelo procedural de cielo y aporta radiancia **lineal de alto rango dinámico** tanto a la imagen de fondo como a los rebotes que llegan al cielo (y al muestreo de portales cuando corresponda).

Se sigue utilizando el mismo path tracer de CPU, sin exigir GPU dedicada. Los mapas de materiales PBR, árboles y elementos BIM mantienen sus geometrías originales.

## Formato y presupuesto

- `#?RADIANCE` o `#?RGBE` con `FORMAT=32-bit_rle_rgbe`.
- Equirectangular, orientación `-Y +X`, con scanlines RGBE antiguos sin RLE o scanlines modernos RLE por canal.
- Archivo máximo **32 MiB**, resolución máxima **2048×1024** y como máximo 2 097 152 píxeles. Estas restricciones evitan grandes reservas en equipos sin GPU.
- Decodifica exponentes RGBE a `Vector3` lineal (sin sRGB ni compresión de tonos); filtra bilinealmente, envuelve el horizonte y limita los polos.
- Los archivos malformados o truncados se rechazan con un mensaje claro. Las imágenes Radiance con otras orientaciones o variantes no admitidas se rechazan, sin suponer una interpretación.

## Iluminación y controles

- Al activar HDRI, el sol analítico se apaga para evitar añadir un segundo disco solar encima de un panorama que ya puede llevar el sol; nubes procedurales también se omiten.
- `SkyIntensity` sigue controlando la intensidad del panorama. Exposición y revelado de imagen continúan siendo independientes.
- **Quitar HDRI** regresa al cielo solar procedural.
- La ruta del archivo HDRI y sus ajustes se guardan en el `.g3d` como referencias externas. Para mover el proyecto entre equipos, mueve también las imágenes de la carpeta `assets/` o vuelve a enlazarlas.
- La calidad de la iluminación de ventanas y exteriores depende de la distribución del mapa y del número de muestras. Este primer backend no hace muestreo de importancia por luminancia HDRI.

## Validación

```powershell
dotnet run -c Release --project tests/Guanacaste3D.Checks
```

Las regresiones construyen un RGBE sintético con dos hemisferios de distinto color, verifican la decodificación RLE por canal, rechazo de dimensiones enormes y detección de bytes sobrantes. También conviene comparar visualmente un exterior con y sin HDRI manteniendo los demás valores estables.


## Persistencia y orientación del entorno

Las versiones recientes guardan junto al `.g3d` el archivo HDRI seleccionado (referencia a archivo, sin incorporar megabytes de imagen), su orientación angular (0–360°) y su intensidad (−4 a +4 pasos fotográficos).

Los panoramas dentro de la carpeta del proyecto se guardan mediante una ruta relativa: esto facilita copiar un proyecto junto con su carpeta `assets/`. Un archivo externo conserva su ruta absoluta local. Las rutas de red UNC no se recargan automáticamente y las rutas relativas no pueden escapar de la carpeta del proyecto. Si la imagen falta o está malformada, la apertura del proyecto continúa usando el cielo físico procedural y muestra el recurso ausente en el panel.

En **Render → Escena** usa **Rotación HDRI** para girar la iluminación alrededor de Z, e **Intensidad HDRI** para cambiar su potencia sin alterar la exposición de la cámara. Una edición del cielo reinicia las muestras de la vista previa, no modifica el modelo BIM.
