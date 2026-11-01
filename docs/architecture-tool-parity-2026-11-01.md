# Plan maestro — herramientas de Arquitectura (referencias visuales 2026-11-01)

**Repositorio:** `yetsin7/guanacaste-3d`. **Base de revisión:** rama `main` al crear `feature/architecture-tool-parity-20261101`.  
**Estado del plan:** iniciado. **Regla:** ninguna herramienta se declara *terminada* por tener botón, icono o un único ejemplo.  
**Fuente:** 20 capturas proporcionadas por el usuario (19 capacidades distintas; la captura final repite «Separador de habitación»).

## 0. Principios de ejecución obligatorios

1. **Inventario y trazabilidad.** Para cada capacidad conservar ID estable, estado, código, pruebas, limitaciones y criterios de aceptación. Estados permitidos: `pendiente`, `parcial`, `verificado`. «Existe código» no equivale a «verificado».
2. **Una fuente de verdad.** Cada elemento editable vive en `BuildingDocument`; planos, secciones, alzados, 3D, metrados, render y exportaciones se derivan de él.
3. **Transacciones atómicas.** `DocumentSession` valida antes de confirmar. Crear, editar, arrastrar, duplicar y borrar ofrecen undo/redo coherentes; los elementos hospedados mantienen vínculos estables por GUID.
4. **Independencia en eliminación.** Borrar una referencia, como un eje, no borra automáticamente muros; borrar el muro no borra el eje. Las relaciones se desligan sin dejar identificadores colgantes. Otros hosts aplican la política de conservación o invalidación explícita del tipo.
5. **Geometría real.** El modelo geométrico se genera en Core, no mediante siluetas exclusivas de WPF. Los perfiles, vacíos, capas, cortes, encuentros y pendiente deben producir sólidos coherentes y cuantificables.
6. **Familias y tipos.** Distinguir tipos/instancias, restricciones, materiales, parámetros, unidades métricas y regeneración determinista; utilizar `.g3df` y `.g3d`.
7. **Vistas 2D y 3D.** Separar geometría del modelo de anotaciones específicas de una vista. Exportar con visibilidad, categorías, pesos de línea y patrones correctos.
8. **Calidad.** Tests de Core, persistencia/migración, geometría, transacciones, DXF/IFC/PDF según aplique, WPF automatizado y verificación manual registrada en Windows. Prohibido dar por terminada una capacidad con CI rojo.
9. **Interfaz original.** Usar las capturas para comprender el comportamiento; no copiar iconos, archivos, texto ilustrativo ni materiales propietarios de Autodesk.
10. **Integración segura.** Desarrollar por ramas/PR con CI; no forzar `main`, no sobrescribir commits locales no publicados y no incluir archivos binarios enormes en cambios de funcionalidades.

## 1. Inventario completo de las capturas

| ID | Captura | Capacidad solicitada | Línea base observable en `main` | Estado de paridad |
|---|---:|---|---|---|
| ARQ-01 | 01 | **Muro arquitectónico**: trazado paramétrico, tipo, núcleo/acabados/capas, grosor, altura, uniones y habitaciones | `Wall`, `WallType`, sesiones y mesher existentes; faltan comprobar capas constructivas editables completas | parcial |
| ARQ-02 | 02 | **Puerta**: escoger/cargar familia, hospedaje, hueco, giro, orientación, herrajes y simbología | `WallOpening`, `OpeningType`, bases de familia y previsualización | parcial |
| ARQ-03 | 03 | **Ventana**: familias, marcos, hojas, antepecho, dintel y huecos | Tipos, geometría y validaciones de inserción disponibles | parcial |
| ARQ-04 | 04 | **Componente**: biblioteca, cargar familias, colocar con host/plano de trabajo, rotar y editar | Biblioteca arquitectónica y `StructuralElementKind.Component` | parcial |
| ARQ-05 | 05 | **Pilar estructural**: sección, material, niveles, altura, rejillas, vinculación con cimentación/vigas | `Column` y herramienta de colocación existentes | parcial |
| ARQ-06 | 06 | **Cubierta por perímetro**: boceto, borde con/sin pendiente, voladizo, varias aguas y huecos | `FloorKind.Roof`, boceto y `RoofGeometry` | parcial |
| ARQ-07 | 07 | **Techo/cielo raso**: nivel, desfase y contorno, automático por recinto y boceto | `FloorKind.Ceiling`, menú de recetas | parcial |
| ARQ-08 | 08 | **Suelo/forjado**: contorno, alinear a muros, estructura multicapa, espesor y desfase | `FloorSlab`, presets y sketches | parcial |
| ARQ-09 | 09 | **Muro cortina por cara**: superficies anfitrionas, paneles, seguimiento de caras y huecos | Orden de cinta y elementos de muro cortina presentes | parcial |
| ARQ-10 | 10 | **Rejilla de muro cortina**: ejes U/V, reglas de espaciado, distribución y cambios locales | `UpdateCurtainGrid`, propiedades `GridWidth`/`GridHeight` | parcial |
| ARQ-11 | 11 | **Montante**: perfiles horizontales y verticales ajustados a cuadrícula; material y uniones | Render/representación de cortina inicial; verificar modelado y edición separada de montantes | parcial |
| ARQ-12 | 12 | **Barandilla**: dibujar recorrido, colocar sobre escalera/rampa y configurar postes, pasamanos y balaustres | `Railing`, `AddRailingsOnHost` y geometría | parcial |
| ARQ-13 | 13 | **Rampa**: niveles, longitud, ancho, pendiente, descansos y trazado curvo | `StructuralElementKind.Ramp`; geometría general inicial | parcial |
| ARQ-14 | 14 | **Escalera**: tramos, huellas, contrahuellas, descansillos, soporte y cota de nivel | `Stair`, geometría y edición de escalones | parcial |
| ARQ-15 | 15 | **Texto modelado**: texto tridimensional con fuente, relieve, tamaño, material y plano de trabajo | `StructuralElementKind.ModelText`; validar malla real de los glifos | parcial |
| ARQ-16 | 16 | **Línea de modelo**: geometría espacial visible en varias vistas, distinta de línea de detalle local a una vista | `StructuralElementKind.ModelLine` | parcial |
| ARQ-17 | 17 | **Grupo de modelo**: agrupar elementos independientes, instanciar repetidamente, editar tipo y propagar cambios | `GroupId` y `DuplicateStructuralGroup`; grupo de solo elementos estructurales por verificar | parcial |
| ARQ-18 | 18 | **Habitación**: detectar límite desde muros/suelos/techo y separadores, número/nombre, área y volumen | `Room`, `RoomDetection` actualmente limitado a recintos rectangulares ortogonales | parcial |
| ARQ-19 | 19 y 20 | **Separador de habitación**: línea invisible en 3D que delimita recintos 2D y actualiza áreas al mover | `StructuralElementKind.RoomSeparator`; el detector existente no la incorpora en todos los contornos | parcial |

**No se declara ninguna de las 19 capacidades «verificada» sin ejecutar sus pruebas de aceptación completas.** La asignación de «parcial» indica código inicial rastreable, no comportamiento de producción confirmado.

## 2. Fases y dependencias (ejecutar en este orden)

### Fase A — cimiento técnico y matriz de cobertura
- [x] Identificar exhaustivamente las 20 capturas y conservar 19 IDs de capacidad.
- [x] Localizar punto de entrada de comandos: `main-window-architecture-ribbon.cs`, `main-window-architecture-commands.cs`.
- [x] Identificar las entidades actuales: `BuildingDocument`, `DocumentSession`, `structural-elements.cs`, `architecture/`, `families/`.
- [ ] Completar auditoría por comando: gesto inicial, previsualización, estado persistido, representación en 2D/3D, errores y undo/redo.
- [ ] Generar fixtures deterministas y golden images que demuestren que cada herramienta hace trabajo real.
- [ ] Anotar en el PR las pruebas que sí se ejecutaron y las que requieren Windows.

### Fase B — referencias, relaciones y creación básica
- [ ] ARQ-01: núcleo multicapa real, encuentros, materiales, variantes y cargas útiles de referencia.
- [ ] ARQ-05: pilares con ejes/otros elementos, cota de base/top y edición controlada.
- [ ] ARQ-16: líneas de modelo 3D no confundidas con anotaciones de detalle de vista.
- [ ] ARQ-19: separadores de habitación persistentes, visibles solo cuando la vista lo requiere, que intervengan en el cálculo de límites.
- [ ] Rejillas/ejes: nombres únicos con mensaje y búsqueda del eje existente; relaciones opcionales de muros a ejes, traslación asociativa, borrado independiente y pruebas.

### Fase C — anfitriones, aperturas y familias
- [ ] ARQ-02/03: hospedar puertas/ventanas en muro, insertar huecos reales, controlar giro, desfases, tipo y familia; proteger contra colisiones.
- [ ] ARQ-04: colocar familias de distintos hospedajes; parámetros de tipo/instancia; rotación; editar/reemplazar sin perder identidad.
- [ ] ARQ-17: grupo con definición reutilizable, transformaciones, edición y propagación transaccional; migrar más allá de `StructuralElement`.

### Fase D — elementos horizontales y circulación
- [ ] ARQ-08/07/06: croquis editables de suelo, cielorraso y cubierta; contornos con huecos; capas, material y altura correctos.
- [ ] ARQ-13/14/12: rampas, escaleras y barandas con descansillos/curvas, anfitriones, límites de proyecto, edición y geometría compartida.
- [ ] Mostrar advertencias cuando la geometría incumpla reglas configurables; no afirmar certificación normativa automática.

### Fase E — fachadas paramétricas
- [ ] ARQ-09: muro cortina basado en cara con referencias estables y regeneración por cambio de superficie.
- [ ] ARQ-10: rejillas U/V insertables, removibles, editables, con espaciado y distribución.
- [ ] ARQ-11: montantes por tramo, perfil, material, alineación y encuentros; cantidades independientes.

### Fase F — espacios, modelo 3D y documentación
- [ ] ARQ-18/19: detección por grafo geométrico de espacios arbitrarios cerrados (incluidos separadores), área neta y volumen; aviso para recintos abiertos/no cerrados.
- [ ] ARQ-15: modelado 3D de texto con glifos vectoriales, extrusión, materiales, parámetros y cambios visibles en 3D.
- [ ] Asegurar actualización de propiedades, vistas, anotaciones, schedules y exportaciones sin duplicar modelo.
- [ ] Tooltips didácticos en español con ejemplos propios, accesibles por teclado y F1; iconografía original.

### Fase G — integración, desempeño y liberación
- [ ] Compilación `dotnet build Guanacaste3D.slnx -c Release`.
- [ ] Core: `dotnet run --project tests/Guanacaste3D.Checks -c Release`.
- [ ] WPF: ejecutar `tests/desktop-interaction-checks` y evidencia de interacción real en Windows.
- [ ] Round trip `.g3d`, plantillas `.g3dt`, familias `.g3df`, migraciones, backup/recovery, IFC/DXF/PDF cuando apliquen.
- [ ] Verificar tiempos de regeneración y memoria en modelo pequeño/mediano/grande; degradación elegante en hardware limitado.
- [ ] CI verde en el commit exacto; revisión del PR y merge seguro en `main`.

## 3. Guiones mínimos de aceptación (por herramienta)

- **ARQ-01:** crear un muro de bloques con repello y fino en ambos lados; inspeccionar capas y espesores en planta, sección y 3D; editar tipo y comprobar cantidades y undo/redo.
- **ARQ-02/03:** insertar dos tipos en un muro; confirmar huecos sólidos, simbología, cambio de host/orientación, colisión rechazada y eliminación sin destruir el muro.
- **ARQ-04:** crear y colocar una familia, cambiar un parámetro de tipo y otro de instancia, guardar/cargar `.g3df`, rotar, copiar y recuperar.
- **ARQ-05:** colocar en cruce de ejes, cambiar ancho/base/top y confirmar representación y cantidades sin borrar ejes al borrar pilar.
- **ARQ-06:** cubierta con huella cóncava y pendiente en lados seleccionados; comprobar alero, cumbreras, altura y corte.
- **ARQ-07:** techo por recinto y por croquis, en nivel y desfase diferentes; no atravesar espacios abiertos.
- **ARQ-08:** losa de contorno irregular con capas distintas y hueco; planta/corte/3D coinciden.
- **ARQ-09:** panelizar una cara plana y otra inclinada; mover host y regenerar o avisar si se pierde asociación.
- **ARQ-10:** añadir, mover y borrar una línea U/V; dividir paneles y conservar identidades no afectadas.
- **ARQ-11:** colocar montantes en rejillas seleccionadas y editar un perfil sin convertir panel en sólido único.
- **ARQ-12:** baranda siguiendo camino poligonal y escalera, con alturas, postes, pasamanos y undo/redo.
- **ARQ-13:** rampa con pendiente y descanso, inicio/fin con cotas verificadas, longitud computada y superficie continua.
- **ARQ-14:** dos tramos con descanso, huellas/contrahuellas consistentes, número calculado, barandas vinculadas.
- **ARQ-15:** «GUANACASTE 3D» 3D, relieve y material; inspección en ortográfica, perspectiva y render.
- **ARQ-16:** línea de modelo en XY/Z, visible desde vistas apropiadas; no confundir con una anotación específica de vista.
- **ARQ-17:** agrupar puerta+muros+componentes; colocar 3 veces, editar definición y observar propagación; desagrupar/undo.
- **ARQ-18:** habitación ortogonal y otra irregular, con separador; número único, área/volumen correctos y aviso de espacio abierto.
- **ARQ-19:** mover un separador y recalcular habitaciones sin convertirlo en muro ni mostrarlo como sólido en render.
- **Ejes asociados:** A y 8 no se pueden duplicar (comparación insensible a mayúsculas/espacios); localizar el eje existente; arrastrar eje mueve muros expresamente ligados; borrar uno mantiene el otro; todas las transacciones reversibles.

## 4. Entregables y seguimiento
- Esta matriz es el **índice maestro**. Cada PR debe mencionar IDs ARQ, archivos modificados, validaciones realizadas, limitaciones y evidencia.
- Una fase solo se marca `verificada` cuando todas sus pruebas de aceptación tienen evidencia (automática y, para WPF, manual o UI Automation).
- Si hay implementaciones simultáneas o ramas locales pendientes, **integrarlas sin sobrescribir trabajo**. Unificar carpetas locales es una tarea independiente, pues GitHub no puede borrar ni consolidar copias de la máquina del usuario.
- **Primera implementación incremental:** fortalecer la integridad y los flujos de los ejes de referencia y sus relaciones con muros, antes de extender las herramientas dependientes de referencias.

## 5. Incrementos realizados en la rama de trabajo

### Incremento 1 — referencias paramétricas de ejes y muros
- Vínculo opcional por GUID del muro con su eje central, actualización transaccional al mover/girar eje y eliminación independiente.
- Control de nombres de ejes duplicados, localización del existente y edición desde plano.
- Pruebas automáticas de undo/redo, eliminación independiente y persistencia.

### Incremento 2 — recintos y separadores
- Separadores de habitación ortogonales participan en el cálculo del conjunto de recintos, sin sólidos 3D.
- **Incremento 2B:** las habitaciones nuevas guardan `IsAutomatic`, para distinguirlas de las creadas manualmente y no interferir con esas.
- Al mover, añadir o quitar un separador se concilian las habitaciones automáticas *dentro de la misma transacción*; el mayor solape preserva GUID, número y nombre; los espacios nuevos reciben números únicos.
- Al usar «Habitaciones automáticas» también se sincronizan las existentes, sin generar pasos inútiles de deshacer cuando no hubo cambios.
- Se añadieron pruebas de dividir, fusionar, recalcular área, guardado nativo, nombres personalizados y undo/redo.

**Limitaciones abiertas:** en este incremento los contornos detectables siguen siendo rectángulos ortogonales. Las habitaciones heredadas de archivos anteriores sin `IsAutomatic` permanecen manuales para no modificarlas por accidente. Todavía no se actualizan automáticamente por cambios de muros, ni se garantiza conservación de metadatos externos al fusionar habitaciones. ARQ-18 y ARQ-19 **siguen parciales**.
