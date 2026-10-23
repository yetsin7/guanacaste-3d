# Investigación de compatibilidad con Revit — enfoque REA

**Fecha:** 2026-10-23
**Producto propio:** Guanacaste 3D (C# / .NET 10 / WPF)
**Referente:** Autodesk Revit; no se usa su código o activos propietarios.
**Estado de investigación:** documentación pública y guías técnicas de REA revisadas; **sin sesión de análisis de binarios
ni una ejecución real de Revit en este entorno**.

## Herramientas investigadas

- REA, proyecto original: https://github.com/morluto/rea
- REA guía de instalación y MCP: https://github.com/morluto/rea/blob/main/docs/installation.md
- REA investigación de aplicaciones de escritorio:
  https://github.com/morluto/rea/blob/main/docs/desktop-applications.md
- REA disclaimer de uso responsable: https://github.com/morluto/rea/blob/main/DISCLAIMER.md
- Guía Autodesk Revit API 2027:
  https://help.autodesk.com/cloudhelp/2027/ENU/Revit-API/files/Revit_API_Revit_API_Developers_Guide_html.html
- Autodesk Revit API — información de SDK:
  https://aps.autodesk.com/developer/overview/revit-api
- RevitLookup (visualizador abierto de elementos y propiedades usando la API):
  https://github.com/lookup-foundation/RevitLookup
- Documentación sobre tipo/familia/instancia y estructura compuesta:
  https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/a1acaed0-6a62-4c1d-94f5-4e27ce0923d3.htm
  https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Revit_Geometric_Elements/Walls_Floors_Ceilings_Roofs_and_Openings/Revit_API_Revit_API_Developers_Guide_Revit_Geometric_Elements_Walls_Floors_Ceilings_Roofs_and_Openings_CompoundStructure_html.html

## Qué hace REA y cómo aplicarlo correctamente

REA expone herramientas al agente a través de MCP para explorar aplicaciones de escritorio,
código administrado .NET, ejecutables nativos y comportamientos observables. Puede
utilizar Ghidra, IDA o Hopper **cuando el propietario tenga una instalación legítima y
los derechos para realizar la inspección concreta**.

La vía preferida para el trabajo de Guanacaste 3D es la comparación de **contratos
funcionales observables**, priorizando la API/documentación autorizadas:

1. Identificar una acción concreta, por ejemplo: crear tipo de muro, asignarlo a dos
   instancias, cambiar su espesor y observar las cantidades calculadas.
2. Registrar versión de Revit, condiciones iniciales, configuración de unidades,
   secuencia de acciones, parámetros y resultado esperado. Marcar «no observado»
   hasta poder ejecutar la prueba de referencia.
3. Recopilar evidencias mediante el manual oficial, la API pública y, donde la
   licencia y las autorizaciones lo permitan, una sesión local instrumentada.
4. Escribir un **contrato de comportamiento** sin importar pseudocódigo,
   ensambladores, recursos UI, secretos, formatos privados ni funciones internas.
5. Implementar en Guanacaste 3D la misma capacidad mediante diseño y código
   independientes. Añadir comprobaciones automatizadas de invariantes,
   persistencia, deshacer y documentación.
6. Comparar los resultados de ambos productos con tolerancias métricas explícitas,
   registrar diferencias y corregir el propio producto.

**Prohibido para esta investigación:** deshabilitar licenciamiento o DRM, evadir
controles de acceso, distribuir binarios de Autodesk, importar código descompilado,
copiar iconos o reproducir una interfaz protegida elemento a elemento.

## Instalación opcional de REA en una máquina autorizada

En un equipo con Node.js/npm compatible con la versión publicada de REA:

```powershell
npx rea-agents setup
```

El asistente de REA propone una configuración de herramientas MCP para el agente
instalado. Revisar y aprobar la configuración antes de aplicarla. Para analizar
Revit, primero revisar expresamente los términos de Autodesk y verificar que
la inspección solicitada está autorizada; si no, limitar el estudio a API pública,
ayuda oficial y pruebas manuales de caja negra que sean lícitas.

**Este comando no se ha ejecutado sobre Revit en esta sesión.** El conector de
GitHub permite modificar código del repositorio, pero no controlar una instalación
local de Revit en tu laptop.

## Primera capacidad de comparación implementada: tipo de muro

| Contrato | En Guanacaste 3D | Fuente de referencia |
|---|---|---|
| Tipo reutilizable vs instancia | `WallType` y `Wall.TypeId` | Revit API, ElementType / FamilySymbol |
| Cambiar espesor de un tipo actualiza todas las instancias vinculadas | `DocumentSession.UpdateWallType` | Modelo de parámetros de tipo; requiere contraste real |
| Altura de cada muro como parámetro de instancia | `DocumentSession.UpdateWall` | Modelo de parámetros de instancia; requiere contraste real |
| Detach mantiene geometría actual | `DocumentSession.DetachWallType` | **Comportamiento propio, no se afirma equivalencia con Revit** |
| Operaciones reversibles | `DocumentSession.Undo` y `Redo` | Requiere contraste real |
| Proyectos previos abren sin catálogo | Campo opcional `WallTypes` | Compatibilidad propia, no equivalente a RVT |

Las comprobaciones automatizadas asociadas están en
`tests/Guanacaste3D.Checks/Program.cs`. El esquema de Guanacaste **no lee ni
escribe RVT**, ni incluye aún familias generalizadas, muros multicapa, restricciones
completas o tipos de cubierta, losa, puerta o ventana.

## Registro de evidencias

En cada futura comparación guardar:

- versión/build, licencia de ejecución permitida y fuente;
- escenario y unidades; tolerancia geométrica;
- observación del software de referencia (si realmente se ejecutó);
- resultado de Guanacaste;
- discrepancia comprobable y enlace al test;
- nivel de confianza y fecha.

Hasta contar con un ensayo real en Revit, anotar «documentación pública» o
«hipótesis por verificar». No sustituir estas etiquetas por «idéntico».

## Próximos pasos

1. Verificar compilación del núcleo y WPF en Windows; actualmente los runners
   de GitHub Actions terminan sin ejecutar pasos (issue #2).
2. Extender tipos hacia capas/materiales y restricciones paramétricas.
3. Incorporar uniones de muros y cotas asociativas desde el mismo documento BIM.
4. Comparar salidas con Revit usando datasets construidos expresamente
   para ensayos y estándares interoperables (p.ej., IFC cuando exista).
5. Mantener una matriz de características implementadas, parciales y pendientes.
