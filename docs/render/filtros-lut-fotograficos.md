# Filtros fotográficos de Guanacaste 3D

## Biblioteca por defecto

En **Render → Imagen → Filtro de color fotográfico (LUT)** dispones de catorce gradaciones de color originales, incorporadas dentro del programa y usables sin conexión: natural, luz cálida, azul moderno, vegetación tropical, atardecer dorado, cine azul/naranja, película suave, sepia, estudio luminoso, noche cobalto, bosque mate, pastel, monocromo cálido y neutro.

Son filtros LUT tridimensionales calculados en la CPU para R/G/B. El control **Intensidad del filtro** permite mezclar el resultado entre 0 % y 100 %; con cero no altera ni un píxel. Los filtros se aplican después de la transformación de tonos y antes del efecto de lente, sobre el render lineal ya calculado: **no es necesario lanzar un nuevo path tracing** al ajustar la intensidad. Se pueden combinar con los estilos de imagen, contraste, corrección de sombras y altas luces, bloom, enfoque, grano y viñeta.

## Importar LUT `.cube`

El botón **Importar .cube…** admite LUT 3D `LUT_3D_SIZE` de resolución 2 a 64, con directivas opcionales `TITLE`, `DOMAIN_MIN`, `DOMAIN_MAX`. El motor interpreta los valores por trilinealidad y lee el orden estándar de muestras con rojo como índice más rápido. Por seguridad, limita el archivo a **8 MiB** y rechaza datos no finitos, LUTs incompletos y resoluciones enormes. Las LUT 1D y los formatos propietarios de Lumion no son compatibles.

El filtro importado se puede guardar en el `.g3d` como **referencia externa**, no como imagen incrustada. Coloca las LUT personalizadas en `assets/` junto al proyecto para facilitar copiarlo con sus recursos. Si falta el archivo, el render se mantiene sin esa LUT. El software no incorpora filtros ni recursos propietarios de Lumion: el flujo de trabajo es semejante, pero la implementación y los presets son originales.

## Validación

```powershell
dotnet run --project tests/Guanacaste3D.Checks -c Release
```

Compara una imagen en el mismo render con LUT desactivada, intensidad 0 y 100 %, y confirma que la selección y ruta sobreviven a Guardar, Cerrar y Reabrir. Usa preferentemente LUTs destinadas a imágenes en el dominio 0–1.
