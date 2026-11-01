# Biblioteca PBR .g3dm y editor visual de apariencia

## Qué cambia

La biblioteca del estudio **Render → Materiales** puede mostrar tanto las texturas incluidas como los archivos **.g3dm** que se hayan importado de forma explícita o que ya estén asignados al proyecto. No se recorren automáticamente carpetas del equipo ni se copian las imágenes.

El botón **Añadir material .g3dm a la biblioteca…** valida el manifiesto y guarda su ruta en un pequeño índice de usuario. Hasta 64 rutas conocidas, sin copiar el contenido, en `%LOCALAPPDATA%/Guanacaste3D/imported-pbr-library-v1.json`. Las imágenes y los mapas permanecen junto al manifiesto original; si se retira un archivo, desaparece de la galería pero no se inventa un recurso sustituto.

El filtro **Materiales PBR .g3dm** permite mostrar solamente esos paquetes. La búsqueda reconoce nombres con o sin tildes, las miniaturas conservan el límite de ocho por página y las imágenes se decodifican a 132 px, con un máximo de dos trabajos concurrentes y una caché de 24 vistas previas. Para proyectos que ya usan paquetes `.g3dm`, la galería detecta sus referencias sin añadirlas al índice global.

## Panel de apariencia

Al seleccionar una superficie o categoría BIM, la galería muestra:

- **Rugosidad**: 0.02 (pulido) a 1 (mate).
- **Metalicidad**: 0 (dieléctrico) a 1 (metal).
- **Relieve**: 0 (plano) a 1 (fuerte).
- **Canales normal, rugosidad y metálico**: miniatura del mapa si existe, información de si proviene del `.g3dm` o de un reemplazo individual, y comandos para elegir o quitar un reemplazo.

Mover un control visual cambia el valor mostrado, **sin iniciar un trazado de rayos completo a cada movimiento del ratón**. Pulsar **Aplicar cambios** hace una transacción con deshacer/rehacer y actualiza el render. **Restaurar valores** elimina los overrides de rugosidad/metalicidad y vuelve al relieve original de la biblioteca o del paquete.

**Importante:** los valores específicos del proyecto se guardan como `RoughnessOverride` y `MetallicOverride` (opcionales) en el `RenderMaterialAssignment` existente. Proyectos antiguos siguen usando los valores del material original. Un `.g3dm` compartido nunca se modifica: dos objetos pueden tener apariencias distintas sin estropear el material utilizado por otros.

Los mapas PBR individuales siguen usando los caminos existentes `NormalPath`, `RoughnessPath` y `MetallicPath`. Sustituir un mapa en el proyecto no borra el mapa original incluido en el paquete; quitar el reemplazo recupera ese mapa automáticamente.

## Seguridad y límites

Los paquetes se validan con `PbrMaterialPackages.Read`, que restringe versión, nombre, escala, rangos físicos, tamaño de manifiesto, rutas relativas y existencia de las imágenes. Un archivo ausente o inválido no rompe la galería. No hay descargas ni importación masiva.

Las miniaturas muestran el albedo y el canal del material, no una esfera 3D precalculada ni un render físico final. El resultado definitivo depende de la iluminación, escala de las texturas y geometría de la escena.

## Comprobación

- **Core checks Linux**: validación de rangos y valores no finitos; transacción con deshacer y rehacer; persistencia nativa de nuevas propiedades.
- **Windows WPF tests**: registro de un `.g3dm` del proyecto, filtro y búsqueda, controles físicos, tarjetas de mapas, aplicación sin alterar el archivo original, guardar/abrir proyecto y restauración de los valores.
- CI de Windows: compilar el escritorio y generar el ejecutable portátil.

No fusionar hasta que todos los trabajos concluyan satisfactoriamente.
