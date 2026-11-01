# Microgeometría de piedra natural (opcional)

Los muros de piedra ahora pueden recibir **geometría tridimensional adicional solo durante el render**. Los bordes no son un filtro de iluminación: son triángulos que bloquean y reciben rayos, creando sombras reales entre juntas y piezas. El modelo BIM y su cómputo de espesores, longitudes y volúmenes no se altera.

## Activación

En **Render → Materiales**, activar **Relieve geométrico en piedra natural**. El cambio reconstruye la escena CPU y se puede desactivar sin efectos sobre el proyecto. Por defecto **apagado**, para que máquinas con poca RAM o CPU no paguen ningún coste adicional.

## Diseño del presupuesto

- Solo se teselan triángulos clasificados como `SurfaceKind.WallStone`.
- `RenderStoneDisplacement.Append(mesh, triangleBudget: 80_000, targetEdgeMeters: 0.075f)` genera como máximo 80 000 triángulos adicionales por escena. El tope duro de seguridad de la función es 150 000.
- Los triángulos originales siguen intactos como respaldo estructural. El relieve se calcula con el mismo patrón estable de mampostería y se proyecta a lo largo de la normal de la cara.
- La profundidad del relieve es de aproximadamente 2 a 28 mm fuera de la superficie fuente. No modifica el espesor BIM ni la posición persistida de elementos.
- Los intervalos de atribución `ElementRanges` se conservan para que la elección del material individual afecte también a los detalles nuevos. La malla de previsualización WPF no añade estas caras.
- El mosaico usa un tamaño de triángulo objetivo en metros y reduce la densidad si el presupuesto global no alcanza. Nunca se usa aceleración GPU.

## Qué limita la fidelidad

Esta es una mejora del relieve **geométrico**, pero no un motor de desplazamiento vectorial de alta subdivisión. La malla añadida no sustituye la geometría BIM y puede presentar uniones evidentes donde se encuentran caras con normales diferentes. Las piedras siguen generándose mediante un patrón procedural, no son escaneos fotogramétricos de piedras únicas.

Para un primer ensayo, usar luz solar de ángulo bajo, muros de piedra y cámara a pocos metros; hacer render con casilla apagada y encendida para comparar sombras y coste. Confirmar que la cantidad total de triángulos aumenta de forma acotada y que desactivar deja el resultado original.
