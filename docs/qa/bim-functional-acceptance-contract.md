# Guanacaste 3D — contrato de compatibilidad BIM y puerta de entrega (.NET 10)

Fecha: 2026-11-17. Estado: criterios de aceptación, **no** funcionalidad implementada ni pruebas ejecutadas.

## Arquitectura objetivo

- C#/.NET 10 como implementación de Core, geometría, documentos y comandos; WPF como shell Windows. Mantener Rust fuera de la ruta de producción.
- Separar `Domain` (entidades y parámetros), `Application` (comandos y transacciones), `Infrastructure` (persistencia, IFC, render), `Presentation` (WPF). Evitar referencias WPF desde Core.
- Cada cambio al modelo pasa por transacción: validar -> aplicar -> actualizar dependencias -> registrar undo/redo -> publicar evento. No persistir modelos parcialmente mutados.
- Versionar el formato con migradores de esquema y conservar fixtures de versiones anteriores. No cambiar `.g3d`, `.g3df` ni `.g3dt` sin round-trip y compatibilidad.

## Matriz de equivalencia funcional (objetivo, no afirmación de paridad)

| Área | Escenario mínimo verificable | Prueba exigida |
|---|---|---|
| Muros y niveles | Mover nivel anfitrión y recalcular altura/posición del muro | Dependencias + undo/redo + round-trip |
| Puertas y ventanas | Cambiar espesor del muro anfitrión y conservar abertura/ubicación | Regeneración + colisiones |
| Familias | Tipo compartido con dos instancias y parámetros independientes | Propagación de tipo + override |
| Extrusión/vacío | Sustraer vacío de sólido y rechazar perfil autointersectado | Topología y volumen |
| Restricciones | Bloquear distancia y rechazar edición incompatible sin cambios parciales | Transacción fallida |
| Vistas | Actualizar sección, planta y elevación al modificar geometría | Consistencia asociativa |
| Cotas y etiquetas | Mantener referencias estables tras mover elemento | Integridad de identificadores |
| Tablas | Recalcular cantidades al agregar/quitar elementos | Comparación con modelo |
| Planos | Colocar vistas y exportar con escala, unidades y tipografías legibles | PDF y reapertura |
| IFC | Importar/exportar categorías, unidades y GUID sin duplicados | Round-trip de fixture |
| Render integrado | Abrir escena, iniciar/cancelar, exportar imagen sin alterar BIM | Windows WPF + cancelación |
| Guardado | Fallo de escritura no destruye proyecto anterior | Inyección de fallo + hash |
| Colaboración | Dos ediciones incompatibles no se sobrescriben silenciosamente | Conflicto reproducible |

## Orden de ejecución

1. P0: resolver exportación segura #131/#132 y restaurar Render integrado #134/#140. Convertir la matriz `docs/qa/render-export-safety-matrix.md` en pruebas automatizadas.
2. P1: crear pruebas de contratos de transacciones, IDs estables y round-trip de documentos. Corregir primero los fallos encontrados.
3. P2: añadir un módulo vertical completo por PR (modelo, UI, serialización, undo/redo y prueba) comenzando por muros/niveles y familias hospedadas.
4. P3: vistas asociativas, cotas, tablas, láminas e IFC; después disciplinas estructurales/MEP y colaboración.
5. P4: perfilar con BenchmarkDotNet o herramienta equivalente, registrar tiempo p50/p95, asignaciones, GC y uso de RAM con Casa del Cobogó y escenas escaladas; optimizar solo cuellos medidos.

## Regla de fusión

No aprobar PR sin revisión de diff, CI verde en el SHA exacto (Windows WPF Release y Core), pruebas de apertura/edición/undo/redo/guardado/reapertura, y regresión de Render/exportación. Ante ausencia de evidencias, dejar PR abierta. Mantener un diseño visual propio; implementar comportamientos BIM equivalentes sin reproducir código ni recursos propietarios de Autodesk.
