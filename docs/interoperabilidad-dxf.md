# Interoperabilidad DXF R12 — Guanacaste 3D

## Alcance actual

La opción **Archivo → Exportar dibujo DXF R12** genera un archivo DXF ASCII,
formato AC1009, sin paquetes CAD externos ni dependencia de Autodesk.

- Todas las coordenadas y elevaciones se expresan **en metros**.
- Cada nivel tiene sus capas independientes: `G3D-L001-WALL`,
  `G3D-L001-DOOR`, `G3D-L001-WINDOW`, `G3D-L001-SLAB`, y así sucesivamente.
- Los muros rectos se representan por segmentos de contorno 2D que
  respetan el **espesor** y omiten el espacio de las aberturas.
- Las puertas y ventanas se señalan con una línea central en su abertura.
- Las losas rectangulares se exportan con cuatro líneas perimetrales en su capa y nivel.
- La elevación de cada nivel se conserva en la coordenada Z de las entidades.
- El orden de exportación es estable para un mismo documento.

El archivo contiene entidades `LINE` y capas R12 compatibles con lectores DXF
básicos. Se valida todo el documento antes de comenzar la exportación.

## Limitaciones importantes

Este DXF es un **dibujo geométrico esquemático**, no una exportación BIM completa:

- No conserva GUID, propiedades, familias, materiales, ni relaciones paramétricas.
- No incluye el espesor de la losa, el alto del muro, el antepecho o la altura de las aberturas,
  aunque estos parámetros se conservan en el archivo original `.g3d.json`.
- No representa hojas, arcos de giro, marcos, acotaciones ni planos de impresión.
- No permite todavía importar DXF a Guanacaste 3D.
- Los rectángulos son contornos independientes; no incluyen uniones ni limpieza
  de esquinas entre muros intersectados.
- DXF R12 no identifica universalmente «metro» como unidad de inserción.
  **Configura metros al importar** en el programa receptor y verifica escala y Z.
- No debe utilizarse como documentación constructiva validada sin revisión.

## Comprobación

Desde el repositorio, en Windows:

```powershell
dotnet run --project tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj -c Release
dotnet run --project src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj -c Release
```

Crea un muro con una puerta y una ventana, exporta DXF e importa el archivo
en un visor CAD externo. Confirma escala en metros, separación de capas,
huecos, elevación de nivel y ausencia de entidades inesperadas.

No se ha certificado todavía compatibilidad con un visor CAD determinado.
