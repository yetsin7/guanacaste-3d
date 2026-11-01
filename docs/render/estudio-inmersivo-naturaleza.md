# Estudio Render inmersivo y biblioteca de árboles

## Estudio independiente del modelador BIM

Al elegir la vista **Render**, `MainWindow.UpdateRenderWorkspace` oculta:

- el encabezado personalizado de la ventana;
- la cinta superior del modelado BIM;
- la barra de estado global;
- las paletas normales de **Propiedades** y **Navegador de proyectos**.

Permanece un botón flotante **↩ 3D** que invoca `SelectProjectView(BuiltInProjectViews.Model3DId)`. El documento, la cámara compartida y la sesión de edición siguen abiertos; al volver, reaparecen las barras y paletas BIM. Render utiliza su propia barra lateral: **Entorno, Imagen, Cámara, Material y Calidad**. Es una primera etapa: conserva las funciones existentes de renderizado, controles de materiales, fotografía y salida.

### Investigación de interfaz Lumion

Fuentes oficiales revisadas en noviembre de 2026:

- [Lumion 2026.0 — notas de versión](https://support.lumion.com/knowledge-base/api/v2/help-center/en-us/articles/lumion-2026.0-release-notes): entornos Build, Photo, Movie, Panorama; biblioteca vegetal de fotogrametría; render y ampliación de imagen.
- [Lumion — Photo/Movie Mode](https://support.lumion.com/knowledge-base/movie-photo-mode-questions): catálogo de efectos y estilos, ray tracing, cielos y cámaras.
- [Atajos de Lumion 2024+](https://support.lumion.com/knowledge-base/api/v2/help_center/en-us/articles/12614115846428.json): navegación, barra lateral y modo de presentación.
- [Consejos de flujo de trabajo](https://lumion.com/tips-guides/build-scenes-efficiently): pila de efectos y miniaturas de fotografías.

Se toma como inspiración la **separación de modos**, el protagonismo de la imagen y la **navegación por herramientas**, no sus iconos, recursos, marcas, pantallas ni código. Guanacaste 3D conserva identidad y arquitectura propias.

## Biblioteca original de vegetación

El catálogo incorpora **24 variantes** en dos grupos:

- **12 cortezas y troncos:** Guanacaste, ceiba, mango, jacaranda, pino, cedro, roble, caoba, eucalipto, abedul, palmera y corteza antigua con musgo.
- **12 hojas y copas:** Guanacaste, tropical luminoso, mango, jacaranda, conífera, palmera, brotes tiernos, verde profundo, estación seca, verde plateado, bosque denso y hojas cálidas.

Cada variante genera dos PNG de **512 × 512 px**: textura de color y un normal map **OpenGL** con sufijo `_NormalGL.png`, para un máximo de 48 archivos. Se producen bajo demanda y se almacenan en el directorio local escribible del usuario:

`%LOCALAPPDATA%\\Guanacaste3D\\nature-textures-v1\\`

Los identificadores de proyecto permanecen estables, por ejemplo `builtin:tree-bark-guanacaste` y `builtin:tree-leaves-tropical`. El motor aplica su escala física y relieve ya existente. Las categorías Bark, Foliage y Leaf aparecen explícitamente en el panel de materiales y pueden recibir acabados independientes.

No se descargan imágenes de terceros. Las texturas usan ruido periódico multiescala con fisuras longitudinales, placas, poros, manchas de musgo y agrupaciones verdes. **Son texturas procedurales originales, no escaneos, fotos ni vegetación de fotogrametría.** Las siluetas de árboles, hojas, ramas y raíces continúan dependiendo de la geometría del modelo. Los mapas normales pueden almacenarse para el canal PBR cuando el pipeline de normal mapping de la superficie los soporte; no se promete que las 24 normales sean aplicadas por todas las rutas del trazador actual.

### Cómo usar

1. Crear o seleccionar un árbol en el modelo.
2. Entrar en **Render → Material**, escoger la categoría **Troncos y corteza** o **Copas y follaje**.
3. Buscar la variante por su nombre, asignarla y ajustar escala y relieve.
4. Regresar a **3D** con el botón flotante. No se pierde ni se cierra el proyecto.

Las asignaciones por categoría afectan los elementos de esa categoría que no tengan material particular; el editor conserva las opciones existentes de material por elemento cuando estén disponibles.

## Validación

- `tests/desktop-interaction-checks/view-interaction-checks.cs`: oculta cinta y paletas, verifica barra propia, navegación entre secciones y regreso seguro a 3D.
- `tests/desktop-interaction-checks/material-catalog-checks.cs`: comprueba los 24 nombres, resolución a PNG, decodificación y mapas normales generados.
- CI existente `windows-build.yml`: compila WPF y ejecuta pruebas de escritorio. Los checks Linux cubren el núcleo.

No confundir este trabajo con una implementación completa del editor de efectos de Lumion: faltan un explorador de activos visual, un panel de efectos reordenables, miniaturas y gestión avanzada de cámaras, y materiales PBR con mapas de rugosidad/desplazamiento. Esas etapas se diseñarán siguiendo los referentes concretos de pantalla que se elijan.
