# Etiquetar todas las habitaciones y áreas — función real

La cinta de **Arquitectura → Etiquetar todas las habitaciones** y **Etiquetar todas las áreas** activa etiquetas visuales en el plano del nivel activo. Estas herramientas antes solo anunciaban un número de elementos en la barra de estado.

## Comportamiento

- Las etiquetas se generan a partir de los objetos reales `Room` y `StructuralElementKind.Area`, sin duplicarlos en el documento ni convertirlos en anotaciones independientes que puedan quedar desactualizadas.
- Las de habitación muestran su número; el tooltip incluye el nombre y el área neta. La posición usa `Room.Center`, respetando concavidades y vacíos interiores.
- Las de área muestran el nombre y su superficie geométrica calculada, y quedan centradas en el rectángulo de área.
- **Cada vista de planta** conserva sus opciones `ShowRoomTags` y `ShowAreaTags`. Los ajustes se guardan dentro de `.g3d`, sobreviven a cerrar y reabrir y aceptan deshacer/rehacer.
- Repetir el mismo comando no añade una operación inútil al historial.
- El comando de etiquetas de puertas, ventanas y columnas mantiene su funcionamiento existente.
- Si cambia el nombre, número, posición o contorno del elemento, la etiqueta se recalcula desde el modelo durante el dibujo.

## Pruebas

- Núcleo: cálculo de etiquetas de área, visibilidad independiente por vista, idempotencia, persistencia `.g3d`, undo/redo.
- Interfaz WPF: ejecutar los comandos desde la cinta de Arquitectura y verificar que los símbolos de habitación y área están en `PlanCanvas`.

## Alcance

Las etiquetas son marcadores con tooltip, **no todavía** familias de anotación complejas con directrices, escalas o estilos personalizados. Tampoco hay que confundir esta entrega con la paridad de todas las herramientas ARQ-01…ARQ-19.
