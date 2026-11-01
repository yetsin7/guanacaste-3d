# Galería visual de texturas del estudio de Render

## Qué incluye

El panel **Render → Material** ahora dispone de una sección desplegable **Biblioteca visual de texturas**. Presenta fotografías y materiales procedurales existentes en la biblioteca como miniaturas con sus nombres reales, sin crear copias del material BIM ni cambiar la forma de guardar proyectos.

El editor original de materiales PBR `.g3dm`, imágenes importadas, canales normal/rugosidad/metálico, escala y relieve sigue disponible debajo de la galería. No es necesario usar la galería para continuar con el método de selección anterior.

## Uso

1. Abrir **Render → Material** y desplegar **Biblioteca visual de texturas**.
2. Elegir la superficie objetivo (p. ej., **Troncos y corteza** o **Copas y follaje**). Si se está editando el material de un elemento seleccionado, la categoría se fija automáticamente para evitar asignaciones a otro tipo de cara.
3. Filtrar por **Árboles y plantas**, **Cortezas y troncos**, **Follaje y hojas**, **Construcción y otros** o **Todas las texturas**.
4. Buscar por nombre o clave sin preocuparse por tildes. Elegir una tarjeta: la textura se asigna mediante la misma transacción del proyecto que usa el selector de materiales, por lo que sigue estando disponible el historial de deshacer.
5. Navegar por páginas de ocho materiales y ajustar la escala o el relieve en los controles que ya existen.

La textura seleccionada se resalta en la página donde aparezca. Las asignaciones por categoría se distinguen de las asignaciones propias de un elemento.

## Rendimiento

- La galería comienza **cerrada** y no decodifica miniaturas mientras no se use.
- Muestra un máximo de **8 tarjetas por página**; no crea decenas de controles ocultos.
- Carga las imágenes en segundo plano, con **dos tareas de lectura como máximo** y una resolución de miniatura de **132 px**.
- Descarta solicitudes de visualización obsoletas al buscar, cambiar página o cerrar la galería.
- Conserva hasta **24 miniaturas pequeñas** en memoria y expulsa las antiguas; nunca conserva una fotografía en resolución original para el navegador.
- Los 24 materiales procedurales de árboles siguen generándose a demanda en el almacenamiento local del usuario. El propio motor de render controla sus texturas completas de forma independiente.

Es posible que la primera miniatura de una textura de árbol se demore en crearse en equipos de bajo rendimiento; la interfaz permanece operativa mientras sucede.

## Validación

En `tests/desktop-interaction-checks/view-interaction-checks.cs` se prueban: apertura desde la nueva barra de Render, ocho tarjetas por página, navegación, búsqueda, filtro de corteza, aplicación al material del proyecto y regreso a la vista 3D.

La integración requiere que GitHub Actions complete **Core checks (Linux)** y **Windows WPF build and portable package**, incluidas las regresiones de interacción.
