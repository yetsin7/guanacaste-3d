# MEJORAS — casa desde cero: Revit 2027 frente a Guanacaste 3D

Objetivo: cada paso que se da en Revit 2027 para levantar una casa completa debe poder darse igual
en Guanacaste 3D, con botones equivalentes, y con una prueba que lo demuestre. Este documento es el
registro vivo; los planes largos ya existen y no se repiten aquí:
[docs/architecture-tool-parity-2026-11-01.md](docs/architecture-tool-parity-2026-11-01.md) y
[docs/revit-functional-parity-roadmap.md](docs/revit-functional-parity-roadmap.md).

> **Plan vigente:** la casa del cobogó desde cero y sus brechas priorizadas están en
> [docs/casa-cobogo-plan-maestro.md](docs/casa-cobogo-plan-maestro.md). Empezar por ahí.

## 0. Qué está verificado y qué no (leer primero)

- **Verificado contra fuente para Revit 2027** (resumen de terceros, Symetri, 25/03/2026): solo lo
  de la sección 3 (muros alojados y numeración de escaleras). Las páginas oficiales de Autodesk
  respondieron 403, así que **no hay cobertura leída de 2027.1 a 2027.4**.
- **Lo demás** (paneles y botones de la pestaña Arquitectura, Modificar, Vista, etc.) es el diseño
  estable de la cinta de Revit conocido de versiones anteriores. Está marcado «por confirmar» hasta
  que alguien lo compare con la cinta real de Revit 2027 abierta.
- **Ninguna fila se marca «hecho» sin una prueba que la ejecute** (regla ya vigente del proyecto).
- No se copian iconos, textos ni recursos de Autodesk; solo se igualan funciones.

## 1. Flujo «casa desde cero» en Revit y estado en Guanacaste 3D

Estado: `botón` = hay comando en la cinta (`ArchitectureCommand`), `parcial` = existe código pero
sin prueba de aceptación completa (según el plan de paridad), `falta` = sin comando encontrado,
`auditar` = hay que revisarlo a mano.

| # | Paso en Revit (pestaña / panel / botón) | Guanacaste 3D hoy | Qué probar a mano |
|---|---|---|---|
| 1 | Nuevo proyecto con plantilla, unidades métricas (Gestionar › Unidades) | auditar | Crear proyecto, ver nombre de niveles y unidades |
| 2 | Niveles: Arquitectura › Referencia › Nivel; renombrar y poner cotas | botón `Level`, `Levels` | Crear Nivel 1 y 2, cota 0 y 3.00 m, que cada uno genere su plano |
| 3 | Ejes: Arquitectura › Referencia › Rejilla; cotas entre ejes | botón `Grid` (relación muro-eje parcial) | Trazar 4 ejes y que el nombre no se repita |
| 4 | Plano de trabajo / plano de referencia | botones `SetWorkPlane`, `ShowWorkPlane`, `ReferencePlane` | Colocar un plano y dibujar sobre él |
| 5 | Muro: Arquitectura › Construir › Muro; tipo, altura, ubicación (línea de acabado, núcleo) | botón `Wall`, `WallArchitectural` (capas: parcial) | Dibujar contorno cerrado de 4 muros con tipo exterior |
| 6 | Cadena, rectángulo, polígono, arco al dibujar muros; dimensión temporal; teclear longitud | auditar | Teclear 6.00 y Enter, ver cota temporal editable |
| 7 | Uniones de muro, limpiar uniones | auditar | Esquinas sin solape ni hueco en plano y 3D |
| 8 | Puerta: Construir › Puerta, cargar familia, voltear con la barra espaciadora | botón `Door` (parcial) | Colocar en muro, voltear apertura, mover con cota |
| 9 | Ventana: Construir › Ventana, antepecho | botón `Window` (parcial) | Colocar con antepecho 0.90 m y editar tipo |
| 10 | Piso: Construir › Piso › Arquitectónico, por contorno, pendiente | botón `Floor`, `FloorArchitectural` (parcial) | Piso con «Seleccionar muros» y con boceto |
| 11 | Techo (cielo raso) automático | botón `Ceiling`, `CeilingAutomatic` (parcial) | Cielo raso automático en un recinto |
| 12 | Cubierta por perímetro / por extrusión, pendiente, voladizo | botón `RoofByFootprint`, `RoofByExtrusion` (parcial) | Cubierta a dos aguas con voladizo 0.50 m |
| 13 | Escalera por componente y por boceto; barandillas automáticas | botón `Stair`, `Railing` (parcial) | Escalera entre Nivel 1 y 2 con barandilla |
| 14 | Hueco: por cara, vertical, de muro, de cubierta (buhardilla) | botones `FaceOpening`, `VerticalOpening`, `WallOpening`, `DormerOpening`, `ShaftOpening` | Hueco de escalera en el piso del Nivel 2 |
| 15 | Habitaciones: Habitación, separador, etiquetar todo | botones `Room`, `RoomAutomatic`, `RoomSeparator`, `TagAllRooms` (detector solo recintos ortogonales) | Habitaciones con área y nombre; un L-shaped |
| 16 | Componentes (muebles, sanitarios): Componente › Colocar | botón `Component` (biblioteca parcial) | Colocar cama, mesa, inodoro |
| 17 | Muro cortina, rejilla, montante | botones `CurtainSystem`, `CurtainGrid`, `Mullion` | Fachada acristalada con 3×4 paneles |
| 18 | Masa y emplazamiento: Superficie topográfica, plataforma de edificio, línea de propiedad | auditar (topografía parcial; plataforma no encontrada) | Terreno con plataforma bajo la casa |
| 19 | Modificar: alinear, desfase, espejo, recortar/extender, dividir, matriz, rotar, escala, copiar, fijar | auditar (hay código de varias; revisar botones) | Duplicar una habitación, espejar muros |
| 20 | Vista: planta, alzado, sección, 3D, duplicar vista, visibilidad/gráficos | persistentes ya en el modelo | Crear alzado y sección, cambiar nivel de detalle |
| 21 | Anotar: cotas, etiquetas, texto, leyenda, cotas de nivel | cotas y etiquetas presentes | Acotar la casa, etiquetar puertas y habitaciones |
| 22 | Tablas de planificación y láminas | existen tablas y hojas | Tabla de puertas y lámina con planta |
| 23 | Guardar / abrir proyecto | `.g3d` | Guardar, cerrar, reabrir sin pérdida |

## 2. Qué suele hacer incómodo a un humano (a medir en la prueba a mano)

Para cada paso de la tabla anotar: pasos de clic de más respecto a Revit, falta de vista previa
al dibujar, ausencia de cota temporal, falta de snap (extremo, punto medio, intersección,
perpendicular), tecla Escape/Enter/Tab distinta a Revit, y mensajes sin explicación. Cada hallazgo
va a la tabla de la sección 6.

## 3. Novedades de Revit 2027 verificadas (Symetri) → trabajo nuevo

- [ ] **M-27-01 Muros alojados.** «Colocar por habitación» y «Colocar por segmento» junto a «Por
      cara» en las herramientas de muro; elegir lado Exterior o Interior.
- [ ] **M-27-02 Muro nuevo alojado en el existente.** Sustituye a «Auto Join & Lock»; el muro
      alojado sigue a su anfitrión y admite **desfase** (cámara de aire sin objeto aparte).
- [ ] **M-27-03 Opciones de muro en la cinta**, no en una barra de opciones aparte.
- [ ] **M-27-04 «Límite de habitación»** editable en Propiedades al crear el muro.
- [ ] **M-27-05 Numeración de escaleras**: parámetros de tipo para la etiqueta, por encima y por
      debajo de la línea de corte (fuente, color), y entrada en «Etiquetar todo lo no etiquetado».
- Ya hay trabajo local en esa dirección (`Protect hosted elements`, `hosted visibility`); revisar
  antes de duplicarlo.

No relacionado con modelar una casa, pero existe en 2027: carbono incorporado en materiales,
Forma Connected Client (vista previa). Se deja fuera hasta que haga falta.

## 4. Rediseño de las pruebas (propuesta, pendiente de aprobar)

Problema: `tests/desktop-interaction-checks` es un ejecutable único con selección por variable de
entorno (`G3D_ONLY`), campos privados leídos por reflexión (`Field`, `SetField`, `Invoke`) y un
solo archivo `interaction-checks.cs` que despacha todo. Es frágil y no refleja lo que hace una
persona.

Propuesta:
1. **Pasos declarativos que espejan el flujo de la sección 1**: cada paso = «pulsar botón X,
   clic en A, B, teclear 6.00, Enter» y la aserción sobre el documento. Misma tabla para Revit
   (guion manual) y para la app (automatizado).
2. **Entrada por la cinta real** (disparar `ArchitectureRibbon_CommandInvoked` / clics reales en
   el lienzo), no por métodos privados, para que un botón roto falle la prueba.
3. **Escenario «casa completa»** que encadena los 23 pasos y guarda captura por paso en
   `artifacts/` para comparar con Revit.
4. Mantener las comprobaciones de Core en `tests/Guanacaste3D.Checks` como están.

**Aprobado y empezado.** Proyecto nuevo `tests/house-flow-checks` (no toca
`tests/desktop-interaction-checks`, donde otra sesión tiene cambios sin confirmar):

- `house-driver.cs`: único sitio con reflexión; pulsa botones de la cinta por UI Automation
  (falla si el botón no existe, no es visible o está desactivado) y hace clic en el plano en
  coordenadas del modelo.
- `house-steps.cs`: lista de pasos con el mismo número que la tabla de la sección 1.
- `program.cs`: un fallo **no detiene** la ejecución; cada línea es un hallazgo para la sección 6.
- Ejecutar: `dotnet run --project tests/house-flow-checks/house-flow-checks.csproj`.
- Limitación honesta: el clic en el lienzo llama a `HandleWallPlacementClick`; no genera un
  evento de ratón real (WPF no permite posición sintética fiable). La cinta sí es real.
- Cubiertos hoy: pasos 1, 2, 5 y 8 (los cuatro pasan). Paso 8 solo comprueba que la herramienta
  queda armada; falta colocar la puerta en un muro y comprobar el anfitrión.

## 5. Coordinación entre sesiones

- Árbol de trabajo con muchos cambios sin confirmar de otras sesiones (`MainWindow*`,
  `main-window-*.cs`, `interaction-checks.cs`). **No se revierte nada ajeno.**
- Un solo escritor por archivo. Cada sesión anota aquí el archivo que toma.
- La otra sesión con control de Revit debe indicar qué herramienta usa; hasta entonces las
  columnas «Qué probar a mano» las ejecuta el usuario.

| Sesión | Archivos que toma | Estado |
|---|---|---|
| Cómputo y casa completa (2026-11-13) | `budget/*`, `MaterialBudgetWindow.cs`, `main-window-material-budget.cs`, `localization.budget/workflow.es.xaml`, `tests/architecture-ui-checks/{budget,house-from-scratch}-checks.cs` | hecho, sin commit |

**Prueba automática «casa desde cero»:** `tests/architecture-ui-checks/house-from-scratch-checks.cs` hace 26
pasos como una persona (botón de la cinta, clics en el plano por los mismos manejadores del ratón, teclado,
menús de tipo) y escribe `artifacts/interaction-evidence/house-from-scratch/report.md` con una imagen por paso.

## 6. Registro de fallos de usabilidad (rellenar durante la prueba a mano)

| ID | Paso (sección 1) | Qué falla o cuesta | Cómo lo hace Revit | Prioridad | Estado |
|---|---|---|---|---|---|
| U-01 | 2 Nivel | Pulsar «Nivel» crea un nivel al instante. Falta comprobar si pide cota y nombre (por confirmar a mano). | Revit pide dibujar el nivel en un alzado y deja renombrarlo | Media | por confirmar |
| U-02 | 8 Puerta | La prueba solo ve la herramienta armada; no hay prueba de colocarla en un muro ni de voltearla. | Colocar, voltear con espacio, cota temporal | Alta | colocar: probado (casa completa, pasos 6–8); voltear: pendiente |
| U-03 | 3 Ejes | Arquitectura › Rejilla solo mostraba u ocultaba la cuadrícula de fondo; no creaba ejes. | Dos clics crean un eje con nombre | Alta | corregido y probado |
| U-04 | 12 Cubierta | Al bocetar un contorno, volver a pulsar el primer punto no cerraba ni terminaba. | Cerrar el lazo en el primer punto termina el boceto | Media | corregido y probado; falta decirlo en `Status.SketchNext` (archivo de otra sesión) |
| U-05 | 13 Escalera | «Barandilla en anfitrión» sobre una escalera que ya trae barandillas las duplicaba (y el presupuesto las contaba dos veces). | No duplica barandillas de un anfitrión | Alta | corregido y probado |
| U-06 | 15 Habitaciones | Habitaciones nuevas se llamaban «Room 001». | «Habitación 1» en plantilla española | Baja | corregido («Habitación 001») |
| U-07 | 20 Vistas | En el plano, la marca «Vista Norte» queda al sur, «Este» al oeste y «Oeste» al este (captura del paso 17). | La marca Norte se coloca al norte del edificio | Alta | corregido y probado: cada alzado se coloca en el lado de su nombre y mira hacia dentro; el dibujo Norte/Sur ya no sale espejado (casa completa, pasos 13–14) |
| U-08 | 15/21 Etiquetas | «Etiquetar todas las habitaciones» dibuja la burbuja encima del rótulo de la habitación y tapa el nombre. | La etiqueta es el rótulo, no se superpone | Media | corregido y probado: con etiquetas activas, el rótulo es la etiqueta (número en burbuja, nombre y área debajo) (paso 12) |
| U-09 | 18 Cotas | «Cota alineada» acota todo el nivel de golpe en vez de pedir dos referencias y la posición. | Clic, clic y clic para situar la línea de cota | Media | pendiente |
| U-10 | 21 Láminas | `documentation-checks` falla en la cabeza de sección estructural (2 círculos) tras cambios de otra sesión en secciones (11:07–11:14). | — | Alta | de la otra sesión: la prueba espera cabezas de 28 px fijos, pero `PlanAnnotationFactor` ahora depende de la escala del plano; como la suite se detiene en el primer fallo, las comprobaciones posteriores (alzado, línea 105) no se ejecutan |
| U-11 | 2 Nivel | Pulsar «Nivel» convierte el nivel nuevo en el activo: si se crean los niveles primero, toda la casa se dibuja en el último (la prueba levantó la casa en el Nivel 3, a 6 m). | Crear un nivel no cambia la planta en la que se trabaja | Alta | corregido y probado: el nivel nuevo recibe su planta, se sigue en la planta en uso y la barra de estado dice cómo cambiar (pasos 1, 2 y 29) |
| U-12 | 12/20 Cubierta en alzado | La cubierta a dos aguas existe (se guarda y se cuantifica) pero no aparece en el alzado Norte. `DrawRoofProfile` existe, así que se filtra antes (visibilidad de la vista o nivel de la cubierta): sin diagnosticar. | La cubierta se ve en todos los alzados | Alta | pendiente |
