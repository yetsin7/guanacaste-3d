# Biblioteca de cielos y nubes de Guanacaste 3D

## Uso inmediato, sin descargas

**Render → Escena → Cielo** ofrece más de treinta ambientes originales, reproducibles y incluidos en el software. Son **presets procedurales** del motor físico de sol y atmósfera, no archivos HDRI de terceros, imágenes estáticas ni familias BIM geométricas. Las nubes se generan mediante ruido multiescala al renderizar: no requieren red, cuentas, licencias ni una GPU dedicada.

Entre los ambientes: amanecer tropical, cielo azul caribeño, cúmulos de verano, bruma del Pacífico, lenticulares de montaña, altocúmulos, altoestratos, cirros, frente tormentoso, cumulonimbos, claridad tras la lluvia, atardecer cálido, crepúsculo violeta, hora azul, cielo nocturno y estudio arquitectónico difuso.

### Familias atmosféricas

| Familia | Característica |
| --- | --- |
| Cumulus | masas blancas abultadas |
| Cirrus | filamentos delgados a gran altura |
| Stratocumulus | bandas y rollos bajos |
| Storm | frente oscuro con claros |
| Altocumulus | pequeñas agrupaciones moteadas de altura media |
| Altostratus | velo extenso y translúcido |
| Cumulonimbus | torres profundas de tormenta |
| Lenticular | formaciones elípticas alargadas por el viento |

### Ajustes que se guardan en `.g3d`

- Cielo elegido por identificador estable, con fecha y hora solar.
- Cobertura nubosa, tipo, semilla (0 a 1023) y orientación horizontal (−180° a +180°).
- Turbidez atmosférica (1,7 a 10).
- Intensidad de sol y cielo (0 a 2, por separado) para recrear mañanas, clima cerrado y estudios.
- **Generar nueva distribución de nubes** cambia únicamente la semilla: la escena y los archivos se pueden guardar y abrir con el mismo resultado.

La fuente luminosa y los colores son relativos al motor existente. No afirmar que el modelo calcule meteorología profesional ni nubes volumétricas de vuelo: son capas procedurales bidimensionales elevadas con iluminación aproximada, no raymarching volumétrico de km de profundidad.

### HDRI

El HDRI externo Radiance (.hdr) sigue disponible: elegir un preset procedural sustituye el HDRI activo deliberadamente. El panorama HDRI se puede volver a cargar con los controles de la misma sección, con rotación e intensidad propias; no se incluyen recursos de Lumion ni imágenes de terceros sin permiso.

### Rendimiento

El muestreo de nubes usa cálculos CPU en los rayos que alcanzan el cielo, sin mallas extra ni texturas descargadas. El coste crece con el número de muestras del render. Reducir calidad y rebotes para previsualizaciones en equipos modestos.

### Validación

```powershell
dotnet run -c Release --project tests/Guanacaste3D.Checks
```

Comprueba al menos dos presets con la misma cámara, el mismo año/día, y compara las capturas antes y después de cambiar semilla y orientación. Al reabrir el proyecto el cielo debe permanecer igual.
