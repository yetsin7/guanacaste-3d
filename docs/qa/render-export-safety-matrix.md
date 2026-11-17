# Guanacaste 3D — matriz de regresión P0 (C#/.NET 10)

Estado: especificación de pruebas; **no** constituye evidencia de ejecución ni aprobación de PR.

## Invariantes de seguridad

- Un archivo de proyecto de render existente y ajeno **nunca** se sobrescribe, incluso si el sidecar `.model-link` falta, está corrupto o contiene un token distinto.
- El identificador de `Baseline` debe coincidir con el proyecto vinculado; ante discrepancia, rechazar antes de escribir.
- La exportación no modifica el documento BIM abierto, su estado de cambios ni su historial undo/redo.
- Proyecto y sidecar se publican de manera recuperable: una interrupción en cualquier paso no deja una pareja aparentemente válida pero incoherente.
- Un error de permisos, disco lleno o destino bloqueado deja intactos los datos preexistentes y muestra un error comprensible.

## Casos automatizados exigidos para #131 y #132 / PR #133

| Caso | Preparación | Resultado obligatorio |
|---|---|---|
| E01 | Destino existente sin sidecar | Rechazo sin modificación de bytes |
| E02 | Sidecar truncado o JSON inválido | Rechazo sin modificación |
| E03 | Sidecar con token diferente | Rechazo sin modificación |
| E04 | `Baseline.Id` ajeno | Rechazo sin modificación |
| E05 | Destino y vínculo legítimos | Reexportación correcta y reapertura |
| E06 | Destino nuevo | Proyecto y vínculo válidos y reabribles |
| E07 | Fallo inyectado tras escribir temporal | Destino anterior intacto; temporal limpiable |
| E08 | Fallo inyectado entre publicación de proyecto y vínculo | Recuperación explícita; nunca aceptar pareja inconsistente |
| E09 | Permiso denegado o disco lleno | Sin pérdida de datos; error visible |
| E10 | Dos exportaciones simultáneas al mismo destino | Serialización o rechazo seguro; sin corrupción |
| E11 | Exportación con cambios BIM no guardados | Documento, dirty flag y undo/redo intactos |
| E12 | Ruta Unicode, larga y con espacios | Exportación/reapertura correcta o error seguro |

Usar directorios temporales por prueba, snapshots de hashes antes/después y limpieza aislada. Inyectar fallos por una abstracción de filesystem o puntos de fallo explícitos; evitar pruebas basadas en temporización frágil. No simular una escritura atómica donde no existe: documentar las garantías reales del sistema de archivos y el mecanismo de recuperación.

## Render WPF — PR #140 / #135

En Windows Release, abrir `Casa del Cobogó`, seleccionar la pestaña Render y verificar que aparece el panel integrado sin lanzar automáticamente otra aplicación. Ejecutar iniciar/cancelar render, vista previa de materiales, guardar imagen, cerrar/reabrir el proyecto y comprobar que **Archivo > Exportar a Guanacaste Render** sigue siendo una acción externa explícita. Registrar los controles visibles, `IsLoaded`, pestaña seleccionada y excepciones del hilo UI. Repetir con DPI 100 % y 150 %.

## Puerta de integración

Para cada PR: revisión del diff y dependencias; pruebas unitarias Core; pruebas de persistencia y reapertura; Windows WPF Release; prueba de interacción con `Casa del Cobogó`; CI verde del **SHA exacto**; revisión de formatos `.g3d`, `.g3df`, `.g3dt`; comparación de benchmarks si cambia el camino crítico. Sin evidencia verificable, mantener la PR sin fusionar. Rust no forma parte de esta ruta de producción.
