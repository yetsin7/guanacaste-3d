# Guanacaste 3D — plan C#/.NET 10 primero (2026-11-17)

## Decisión arquitectónica
Mantener **C#/.NET 10 y WPF** como plataforma principal de escritorio y núcleo BIM. Detener la promoción de la rama experimental Rust a producción; conservar su historial como investigación, sin borrarlo ni fusionarlo automáticamente. No migrar archivos, formatos ni API a Rust salvo decisión futura respaldada por benchmarks reproducibles. Priorizar rendimiento medido en .NET (algoritmos, estructuras de datos, asignaciones, paralelismo seguro, cachés, GPU para render).

## Reglas para cambios
1. Una función por PR pequeña, con prueba de regresión y trazabilidad del requisito.
2. Ninguna PR se fusiona sin CI verde en Windows, checks Core, pruebas de persistencia y revisión de compatibilidad de proyectos .g3d/.g3df/.g3dt.
3. Proteger datos: guardado atómico y copias de seguridad; no sobrescribir exportaciones ajenas o sin vínculo verificado; no modificar documento activo durante exportación.
4. Benchmark antes/después con escenas pequeñas, medianas y grandes; registrar tiempo, memoria, GC y latencia UI; no aceptar regresiones sin justificación.
5. Conservar Casa del Cobogó como escenario de regresión principal y comprobar apertura, edición, undo/redo, guardado, reapertura, render y exportación.
6. No copiar recursos propietarios, código, iconos, marcas ni diseño visual exacto de Autodesk. Buscar equivalencia funcional mediante implementación original y UX propia.

## Fase 0 — seguridad y estabilidad (bloqueante)
- Resolver #132 y #131: no sobrescritura y exportación recuperable/portable, con pruebas de fallo entre escrituras.
- Diagnosticar #134 y validar PR #140 para recuperar Render integrado sin romper exportación externa.
- Revisar PR #118, #133, #135, #140 individualmente; no fusionar PR #136 de Rust. PR #138 apunta a la rama Rust y no es prioridad para main.
- Verificar CI Windows Release, Core y escritorio en HEAD de cada PR; si falla, corregir y repetir.

## Fase 1 — núcleo BIM .NET
- Topología robusta (uniones, recortes, tolerancias, sólidos válidos), transacciones y restricciones paramétricas.
- Familias anidadas, parámetros compartidos, tipos e instancias, puertas/ventanas hospedadas, vacíos, extrusiones, barridos.
- Dependencias y regeneración incremental del modelo, deshacer/rehacer transaccional y auditoría de referencias.

## Fase 2 — documentación y coordinación
- Vistas asociativas, secciones, elevaciones, detalles, etiquetas, cotas, tablas, láminas y plantillas.
- IFC/DXF con validación de unidades y coordenadas, importación/exportación sin pérdida silenciosa.
- Worksharing con detección de conflictos y versiones, permisos y recuperación de sesiones.

## Fase 3 — disciplinas y rendimiento
- Estructuras y MEP por módulos, detección de interferencias, cantidades y presupuestos.
- Índices espaciales, render incremental, instanciación de geometría, profiling de .NET, pruebas de escenas grandes.
- Publicar matriz de paridad funcional por característica; no afirmar equivalencia total con Revit sin pruebas.

## Criterios de aceptación por función
Especificación, pruebas unitarias, integración UI, serialización/reapertura, compatibilidad hacia atrás, accesibilidad, benchmark, CI Windows verde y revisión humana de riesgos. Mantener un registro de capacidades implementadas, parciales y pendientes.
