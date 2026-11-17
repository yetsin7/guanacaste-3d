# Casa del cobogó — plan maestro (empezar aquí)

> **Si es una conversación nueva sobre Guanacaste 3D, lea primero este archivo.** Resume el objetivo, cómo se
> mide el avance, qué falta y en qué orden. Se actualiza al final de cada sesión de trabajo.

## 1. El objetivo (palabras del usuario, 14/11/2026)

Diseñar **la casa del cobogó** (la casa de ejemplo de Guanacaste 3D) **desde cero**, como lo haría una persona, usando
solo los botones y comandos del programa, y obtener el **juego completo de planos**: arquitectónicos, estructurales,
eléctricos, de iluminación, hidrosanitarios, de ventilación, etc., más el presupuesto. Al hacerlo se descubre qué le
falta al programa; eso es lo que hay que mejorar. La ambición: que Guanacaste 3D sea **más capaz que Revit 2028** para
diseñar.

**Ampliación (14/11/2026):** el usuario usará Guanacaste 3D **de ahora en adelante para todos sus planos
arquitectónicos y para cualquier cosa que imagine, por ejemplo muebles (apartado de familias)**. Por tanto: todas las
vistas, todos los planos y todos los diseños completos, sin fallas ni errores, y un **editor de familias** capaz de
crear muebles y componentes propios (geometría, parámetros, tipos, materiales) que luego se colocan en la casa. La
casa del cobogó debe terminar amueblada con familias creadas en el propio programa (fase 11, por escribir).

## 2. Cómo se mide: la prueba «casa del cobogó desde cero»

La casa se construye con una prueba automática que actúa como una persona: pulsa botones de la cinta, hace clic en el
plano por los mismos manejadores del ratón (con *hit-test* del elemento bajo el puntero), escribe con el teclado,
elige tipos en los menús y la biblioteca, rellena diálogos (niveles, renombrar, etiquetar, láminas) y aplica
propiedades. Cada paso que falla dice qué le falta al programa.

| Archivo | Qué es |
|---|---|
| `tests/architecture-ui-checks/cobogo-house-checks.cs` | Fases 0–3: proyecto, niveles, ejes, planta baja, interiores, planta alta, cubiertas |
| `tests/architecture-ui-checks/cobogo-disciplines-checks.cs` | Fases 4–10: estructura, hidrosanitario, eléctrico, iluminación, ventilación, documentación, presupuesto |
| `tests/architecture-ui-checks/house-from-scratch-checks.cs` | El «conductor humano» (clics, teclas, menús, diálogos, evidencia) y la casa pequeña de 30 pasos |
| `tests/architecture-ui-checks/ribbon-inventory-checks.cs` | Pulsa **todos** los botones de todas las pestañas y anota qué hace cada uno |
| `src/Guanacaste3D.Core/architecture/breeze-block-house-*.cs` | La casa de referencia hecha por código: coordenadas y tipos que la prueba reproduce a mano |

Ejecutar (PowerShell o bash, desde la raíz):

```bash
dotnet build Guanacaste3D.slnx
```

```bash
G3D_SCENARIOS_ONLY=1 G3D_COBOGO=1 dotnet run --project tests/architecture-ui-checks --no-build
```

```bash
G3D_SCENARIOS_ONLY=1 G3D_RIBBON_INVENTORY=1 dotnet run --project tests/architecture-ui-checks --no-build
```

Resultados: `artifacts/interaction-evidence/casa-cobogo/report.md` (tabla de pasos) y una imagen del plano por paso;
`artifacts/interaction-evidence/ribbon-inventory.md` (qué hace cada botón). Sin variables, la suite de interfaz corre
todo lo demás y la casa pequeña; la casa del cobogó es opcional hasta que pase entera, luego será obligatoria.

**Regla:** ninguna brecha se da por cerrada sin una comprobación en `tests/Guanacaste3D.Checks` (núcleo) y un paso de
la casa del cobogó que la ejercite como una persona. Revise también las imágenes: varios fallos (marcas de alzado,
etiquetas encimadas, casa en el nivel equivocado) solo se vieron mirando las capturas.

## 3. Estado por fase (última ejecución: ver sección 7)

| Fase | Pasos | Qué cubre |
|---|---|---|
| 0. Proyecto, niveles, ejes | 0.1–0.3 | Proyecto nuevo, «Gestionar niveles» (Planta baja 0.00, Planta alta 3.90), ejes A–D y 1–3 |
| 1. Envolvente planta baja | 1.1–1.5 | Muros de concreto 300, pórtico de cochera 3.10 m, muro cortina, muro de listones y puerta pivotante, muro posterior |
| 2. Interiores planta baja | 2.1–2.7 | Tabiques de yeso, puertas, ventanas con tamaño y antepecho, vano ancho, pisos por boceto, habitaciones con nombre, muebles y sanitarios |
| 3. Planta alta y cubiertas | 3.1–3.12 | Entrepiso, muros blancos, celosía de cobogó, balcón, tabiques y puertas, escalera, hueco, barandilla de vidrio, cubierta a 3.30, marquesina a 3.85 con columnas, losa de cochera, habitaciones |
| 4. Estructura | 4.1–4.3 | Cimiento corrido, zapatas aisladas, vigas perimetrales |
| 5. Hidrosanitario | 5.1–5.5 | Aparatos del baño, calentador, medidor y salida a cloaca, conectar plomería, cuadro de plomería |
| 6. Eléctrico | 6.1–6.6 | Tablero, tomacorrientes, luminarias, apagadores, circuitos con el cableado, cuadro de cargas |
| 7. Iluminación | 7.1 | Lux por habitación en el plano |
| 8. Ventilación y A/C | 8.1–8.2 | Rejilla y ducto de extracción, minisplit |
| 9. Documentación | 9.1–9.6 | Cotas, etiquetas, secciones A-A y B-B, plantas por disciplina (duplicar vista + disciplina + F2), láminas por disciplina, PDF |
| 11. Familias propias | 11.1–11.6 | Mesa paramétrica (2 tipos) y silla hechas en el Editor de familias, guardar .g3df, colocar con clics y Espacio, verlas en 3D |
| 10. Presupuesto y archivo | 10.1–10.2 | Presupuesto de obra por rubros, guardar y reabrir sin pérdida |

## 4. Juego de planos objetivo

| Lámina | Contenido | Hoy |
|---|---|---|
| A-000 Portada | Vista 3D, localización, cuadro de áreas | Lo hace la muestra por código; falta por botones |
| A-101 / A-102 | Plantas arquitectónicas PB y PA con cotas, ejes, etiquetas | Sí (por botones) |
| A-201 | Alzados N, S, E, O | Sí; falta la cubierta en el alzado (U-12) |
| A-301 | Secciones A-A y B-B | Sí |
| A-401 | Tablas: puertas, ventanas, acabados, áreas | Parcial: tablas por código, no por botones |
| S-101 / S-102 | Cimentaciones; entrepiso, vigas y columnas; cuadro de columnas y vigas | Planta estructural por disciplina sí; faltan armados y cuadros |
| E-101 | Fuerza: tomacorrientes, circuitos, tablero, cuadro de cargas | Planta por disciplina sí; falta el cuadro en la lámina |
| E-102 / I-101 | Alumbrado: luminarias, apagadores, lux por recinto | Planta por disciplina sí; falta leyenda de símbolos |
| H-101 / H-102 | Agua potable; aguas negras y pluviales; isométrico | Planta sí; falta el isométrico y la lámina con el cuadro |
| M-101 | Ventilación y aire acondicionado | Planta sí; faltan cálculo y tabla de equipos |
| Presupuesto | Cómputo de materiales y presupuesto por rubros | Sí (ventana y CSV); falta como lámina |

## 5. Brechas encontradas y su prioridad

Estado: **corregido** (con prueba), **abierto**. P1 bloquea la casa; P2 la hace incómoda; P3 es pulido o ventaja.

| ID | P | Brecha | Estado | Dónde |
|---|---|---|---|---|
| C-01 | P1 | El botón Suelo creaba un rectángulo alrededor de todos los muros; no había boceto de piso | corregido: tipo y luego boceto (cierra en el primer punto); «Por contorno de muros» queda en el menú | `main-window-architecture-tools.cs` `ShowFloorSketchMenu` |
| C-02 | P1 | No existía «Duplicar vista» ni la disciplina de la vista: imposible hacer plantas eléctrica, estructural, hidrosanitaria, mecánica | corregido: Vista › Crear vistas › Duplicar vista y Disciplina; lo ajeno se atenúa en la planta | `views/view-discipline.cs`, `main-window-view-discipline.cs` |
| C-03 | P1 | Las láminas y el PDF no dibujaban **ningún** dispositivo eléctrico, circuito ni tubería, y no aplicaban la disciplina | corregido: cada disciplina imprime su trabajo sobre la arquitectura atenuada; Coordinación imprime todo. Falta: leyenda de símbolos y cableado de apagadores | `interop/pdf-discipline-primitives.cs` |
| C-04 | P1 | Selección «pegada»: al terminar un piso o elegir una puerta/ventana quedaban seleccionados el eje, la columna o el dispositivo anterior; Propiedades mostraba otro elemento y «Aplicar» fallaba con «cuatro dimensiones» | corregido | `FinishSlabSketch`, `TrySelectOpeningSymbol`, clic de plano |
| C-05 | P1 | No había referencia a cara de muro al dibujar (solo eje, extremo, punto medio) | corregido: el ajuste alcanza las dos caras y sus esquinas («Cara de muro»); cerca del eje sigue ganando el eje | `WallPlacementGeometry.FindNearestSnap` |
| C-06 | P2 | Las guías de alineación «pegan» puntos a 15 cm de distancia con zoom normal (tolerancia en píxeles sin tecla para soltar) | corregido: la tolerancia es de 8 px en pantalla y mantener Alt suelta todas las guías | `AlignmentTolerancePixels` |
| C-07 | P2 | Ejes: el siguiente no continuaba la serie (tras renombrar a «A» seguía «5») | corregido: A→B, 1→2, Z→AA | `grid-axis-naming.cs` |
| C-08 | P2 | Faltaban el desfase inferior y la restricción superior del muro en Propiedades (largo, alto, espesor y acabado sí existían) | corregido: Desfase inferior, Restricción superior (hasta un nivel) y Desfase superior, aplicados en un solo paso; quedan sin propiedades de ejemplar el muro cortina, la viga y la celosía | `main-window-instance-properties.cs` |
| C-09 | P2 | Los tipos de piso, cubierta, cielo y columna se veían en inglés | corregido: menús, biblioteca y elementos creados usan el nombre en español; el catálogo conserva su nombre interno como clave | `PresetLabel`, `localization.workflow.es.xaml` |
| C-10 | P2 | Un vano o ventana sobre un eje no se podía seleccionar: el clic seleccionaba la línea del eje, dibujada encima | corregido: puertas, ventanas y vanos tienen prioridad sobre los ejes al hacer clic | `OpeningUnderPointer`, `TrySelectGridAxis` |
| C-11 | P2 | «Cota alineada» acota todo el nivel de golpe; no pide dos referencias y la posición | parcial: «Lineal» ya acota clic en muro, clic en muro; «Alineada» sigue siendo acotado automático | `AutoDimensionActiveLevel` |
| C-12 | P1 | «La cubierta no aparece en los alzados» | no era fallo: la captura se tomó antes de dibujar la cubierta; la casa pequeña ahora comprueba que la cubierta se dibuja en el alzado Norte | `house-from-scratch-checks.cs` |
| C-13 | P2 | Sin respuesta visible: Eliminar sin selección, Informes, Análisis estructural, Análisis de sistemas, Parámetros compartidos, Coordenadas, Transmitir modelo, Configuración CES | abierto | ver inventario |
| C-14 | P2 | Tablas de puertas/ventanas/habitaciones: se crean por código en la muestra; falta el flujo por botones (nueva tabla por categoría) | corregido: Gestionar › Tablas crea una tabla por categoría con columnas por defecto; el paso 9.7 de la casa la ejerce (puertas 8, ventanas 4, habitaciones 11) | `SchedulesWindow` |
| C-15 | P1 | Las láminas no tenían cuadro de cargas, cuadro hidrosanitario, simbología ni presupuesto | corregido: cuatro tablas generadas (vistas del navegador) que se colocan en láminas y se mantienen al día: cuadro de cargas con conductor y caída de tensión, cuadro hidrosanitario, simbología con los mismos símbolos de los planos y presupuesto por rubros; los proyectos antiguos las reciben al abrirse | `sheets/engineering-tables.cs` |
| C-16 | P2 | Ventilación: solo hay rejilla, ducto y minisplit; faltan caudales, renovaciones por hora y dimensionado de ductos | corregido (17/11/2026): `VentilationAnalysis` calcula por habitación el caudal requerido (cambios/h por uso), las ventanas abribles (≥ 1/20 del piso) y el caudal de los ductos (4 m/s); el «Cuadro de ventilación» va en la lámina M-101; faltan pérdidas de carga y selección de ventilador | MEP |
| C-17 | P2 | Estructura: hay cimientos, zapatas, vigas y columnas, pero no armado (varillas, estribos) dibujado ni cuadros de armado en lámina | abierto | `reinforcement.cs` |
| C-18 | P3 | Presupuesto: no cuenta escalera, barandillas, celosía, muro cortina, aparatos sanitarios ni instalaciones eléctricas/hidráulicas | corregido (17/11/2026): escaleras, rampas, barandas, muro cortina, tragaluces, canoas, bajantes, ductos, dispositivos eléctricos con conductor estimado, sanitarios y puntos de red con tubería estimada; faltan celosía y tubería/cable medidos del trazado real | `budget/material-takeoff.cs` |
| C-20 | P1 | Las habitaciones no se detectaban si los muros terminan en la **cara** de otro muro (se dibujan así en la casa real) ni con muro cortina; la casa del cobogó tenía 0 habitaciones | corregido: los extremos sueltos se prolongan hasta el eje del muro de enfrente si están dentro de su medio espesor; el muro cortina delimita | `room-planar-boundary-detection.cs` `ExtendToFaces` |
| C-21 | P3 | Cochera y descansillo abiertos: hace falta un separador de habitación, como en Revit (no es fallo, queda en la prueba como paso humano) | documentado | — |
| C-22 | P1 | No existía editor de familias: imposible crear muebles propios (el núcleo ya tenía el modelo paramétrico, sin interfaz) | corregido (primera versión): Insertar › Editor de familias con parámetros, tipos, cajas con fórmulas, vista previa en planta e isométrico, guardar .g3df, cargar y colocar | `FamilyEditorWindow.cs`, `families/family-authoring.cs` |
| C-23 | P1 | Una familia cargada se colocaba en el origen (0, 0); no había herramienta de clic para colocarla | corregido: cada clic coloca una, Espacio gira 90°, Esc termina | `main-window-family-editor.cs` |
| C-24 | P2 | Familias: solo cajas (extrusión rectangular). Faltan en la geometría: extrusión por boceto libre, revolución (patas torneadas, lámparas), barrido (molduras), fusión, vacíos, y material por sólido | parcial: revolución, extrusión por contorno tecleado y material por sólido (9 materiales) hechos en núcleo y editor; faltan barrido, fusión y vacíos | `family-geometry.cs`, `family-authoring.cs`, `FamilyEditorWindow.cs` |
| C-25 | P2 | Los muebles propios no entran en el presupuesto ni tienen símbolo de planta propio (usan la silueta genérica) | corregido: entran al presupuesto (rubro «Mobiliario y equipo», una línea por familia, precio de referencia por categoría, editable y guardado, sin mano de obra) y se dibujan en planta y PDF con su propia geometría vista desde arriba (`FamilyPlanSymbol`, con líneas ocultas) | `PlanShape`, `material-takeoff.cs` |
| C-26 | P2 | La planta arquitectónica se saturaba con circuitos, rótulos de lux, números de circuito y tuberías | corregido: esas capas se dibujan en vistas Eléctrica/Fontanería/Coordinación o mientras se usa su herramienta; la arquitectónica muestra solo los aparatos | `ShowsEngineeringOverlay` |
| C-27 | P1 | No había forma de copiar un piso a otros niveles: un edificio se dibujaba piso por piso | corregido: Modificar › Pegar en niveles (Ctrl+A incluye habitaciones e instalaciones; un paso de deshacer) | `main-window-paste-to-levels.cs` |
| C-28 | P1 | Con un edificio grande cada cambio tardaba ~1 s: el navegador del proyecto (999 nodos) se reconstruía siempre | corregido: solo se reconstruye si cambia lo que muestra; niveles plegados. Cambiar de nivel 1.0 s → 0.24 s | `main-window-project-sidebar.cs` |
| C-29 | P2 | Ctrl+A y Supr no borraban aparatos eléctricos ni puntos de plomería | corregido: se borran con sus circuitos, enlaces de apagador, sistemas y bomba, en un solo paso de deshacer | `RemoveElements`, `RemoveInstallation` |
| C-30 | P1 | CI de `main` en rojo desde a84dda4: la prueba de documentación esperaba cabezas de sección de 28 px fijos | corregido (6a139e8) | `desktop-documentation-checks.cs` |
| C-31 | P2 | Pruebas frágiles en CI: el perfil de muro se probaba en un muro de 4 m a 500 m de la casa (menos de 4 px en pantallas pequeñas) | corregido: se usa el muro más largo de la casa visto de frente | `cobogo-interaction-checks.cs` |
| C-32 | P1 | Desde un proyecto vacío: la herramienta Pilar se apagaba tras un clic, la viga seleccionada no ofrecía campos en Propiedades y «Armadura» pedía un texto críptico («3 0.012 bottom») | corregido (17/11/2026): Pilar sigue activo hasta Esc; Propiedades edita sección y desfase de vigas, cimientos y ductos; Armadura abre un diálogo (cantidad, diámetro #3–#6, cara). Prueba: `G3D_FRESH=structure`, proyecto nuevo y capturas de ventana completa |
| C-33 | P2 | Desde cero con escalera: Propiedades mostraba el espesor y la elevación del piso dibujado antes, aunque la escalera era lo seleccionado; en un proyecto nuevo la «Vista Norte» salía resaltada estando en planta | corregido (17/11/2026): el editor de Propiedades sigue al elemento estructural seleccionado; solo se resalta el alzado que se está viendo. Prueba: `G3D_FRESH=stair` |
| C-34 | P2 | Desde cero con cubierta: tras crearla seguía seleccionado un muro (Propiedades con volcado de cantidades y cotas del muro en 3D) y Propiedades llamaba «suelo» a la cubierta; «Barras en un área» y «Barra por recorrido» pedían líneas de códigos | corregido (17/11/2026): la losa nueva queda como única selección y se llama cubierta o cielo; ambos comandos usan un formulario (`FormDialog`) con diámetros #3–#6 y las esquinas de la losa seleccionada. Pruebas: `G3D_FRESH=roof` y pasos F6–F7 de `G3D_FRESH=structure` |
| C-35 | P1 | Modificar solo servía con muros: Copiar, Girar, Simetría, Matriz respondían «seleccione un muro» ante un pilar, escalera, mueble, losa, habitación o dispositivo; en una lámina nueva se podía apilar la misma vista dos veces | corregido (17/11/2026): `PlanTransform` + `TransformPlanElements` (giro, simetría, traslación, copia); Matriz con formulario; Desfase/Escala/Cortar explican que son de muros; una vista gráfica va una vez por lámina. Pruebas: `G3D_FRESH=edit` y `G3D_FRESH=sheet` |
| C-36 | P1 | Render dentro del programa: no había ningún botón en la cinta (solo manejadores); al abrir Render la casa salía recortada y «↩ 3D» tapaba controles | corregido (17/11/2026): grupo «Render» en Vista con «Ir a Render» e «Iniciar render», la cámara se encuadra al abrir Render por primera vez, y la telemetría ya no queda bajo el botón 3D. La casa del cobogó (fase 12) llega al Render y produce la imagen (568×402, 24 muestras, ~21 s). Pendiente: paleta flotante grande que tapa el modelo, imagen con márgenes blancos por relación de aspecto, sin suelo/entorno propio de la casa |
| C-37 | P1 | Nada debe estorbar frente a una puerta (como en la vida real): el programa no avisaba de columnas, muebles o escaleras en el giro de una puerta; la casa tenía un pilar frente a la puerta de la planta alta | parcial (17/11/2026): `DoorClearance` define la zona libre a cada lado de la puerta (ancho + 5 cm por lado, fondo = ancho de la hoja, mínimo 0.90 m); aparece en «Revisar advertencias» y como aviso inmediato al colocar o seleccionar el elemento; el pilar de la casa se resolvió moviendo la puerta. **Abierto:** inodoro, lavamanos, lavadora y ducha dentro del giro de las puertas del baño, y la escalera pegada a la puerta del muro x = 10.8; exige rediseñar el núcleo húmedo (hoy abierto hacia la cochera). Próximas reglas: altura libre de paso, distancia entre puertas, circulación mínima en pasillos |
| C-38 | P2 | Render: la paleta flotante oscura tapa el modelo, la imagen sale con márgenes blancos por la relación de aspecto, y la casa renderiza solo con plano verde, sin cielo/entorno propio | abierto (la casa ya lleva césped, calle, acera y árboles desde la fase 13 del guion) |
| C-19 | P2 | Hay dos tipos de cuadro de diálogo de texto (PromptWindow y TextPrompt) con comportamientos distintos | abierto | `PromptWindow.cs`, `text-prompt.cs` |

Brechas anteriores ya corregidas en esta línea de trabajo (ver `MEJORAS.md` sección 6): U-03 eje de Arquitectura,
U-04 cerrar boceto en el primer punto, U-05 barandillas duplicadas, U-06 nombres de habitación, U-07 marcas de alzado,
U-08 etiqueta encima del nombre, U-11 nivel nuevo cambiaba la planta activa.

## 6. Orden de trabajo recomendado

0. **Familias (C-24/C-25):** ~~revolución, extrusión por boceto, material por sólido~~ hechos; faltan barrido y vacíos.

1. **C-24** familias más ricas (revolución, barrido, materiales) y **C-25** muebles propios en presupuesto.
2. **C-05/C-06** referencia a cara de muro y control de las guías: dibujar exacto sin trucos de zoom.
3. **C-12** cubierta en alzados y **C-10** seleccionar vanos con clic.
4. **C-08** propiedades de ejemplar de muro y elementos lineales.
5. **C-14** tablas por botones; **C-11** cota alineada clic-clic-clic.
6. **C-17/C-16** armados y ventilación con cálculo; **C-18** presupuesto completo.
7. Cuando la casa pase entera: volver obligatoria la prueba (quitar `G3D_COBOGO`) y añadir la portada A-000 por botones.

## 7. Registro de ejecuciones

| Fecha | Pasos que funcionan | Notas |
|---|---|---|
| 2026-11-14 (1.ª) | 31 de 56 | Primera vuelta: muchas brechas de la propia prueba (nombres localizados, diálogos) y reales (C-01…C-04) |
| 2026-11-14 (2.ª) | 43 de 56 | Corregidos C-01, C-02, C-04 (parte), C-07; quedan muros desviados (C-05/C-06) |
| 2026-11-14 (3.ª) | 52 de 56 | Muros con longitud tecleada y zoom, como en Revit; C-04 completo; 0 habitaciones detectadas (C-20) |
| 2026-11-14 (11.ª) | 63/63 | C-25 corregido: 6 muebles propios en el presupuesto (9 rubros) y con símbolo de planta propio. Prueba más estable: el puntero simulado queda fijo donde lo dejó la «persona» (un MouseMove sintético de WPF desviaba muros de Planta alta al azar) |
| 2026-11-14 (10.ª) | 63/63, núcleo y escritorio en verde | C-24 parcial: lámpara de pie por revolución en metal (paso 11.7), contornos tecleados con coma o punto decimal, material por sólido |
| 2026-11-14 (9.ª) | 62/62, edificio 13/13, CI verde | Tablas de ingeniería en láminas, ajuste a caras, borrar instalaciones, restricciones de muro, nombres en español; `main` verde en GitHub desde b808a63 |
| 2026-11-14 (8.ª) | 62/62 + edificio 12/12 | Edificio de 6 pisos por botones y torre de 10 pisos con límites de tiempo; todo subido a GitHub main (7beb6dd, 6a139e8) |
| 2026-11-14 (7.ª) | 62 de 62 | Fase 11: mesa y silla paramétricas hechas en el Editor de familias, colocadas en el comedor. Suite de interfaz completa en verde |
| 2026-11-14 (6.ª) | 56 de 56 | C-10 corregido: la casa del cobogó se construye entera por botones |
| 2026-11-14 (5.ª) | 55 de 56 | Regresión completa limpia (casa pequeña 30/30, presupuesto, interfaz). Solo falla C-10 |
| 2026-11-14 (4.ª) | 53 de 56 | C-20 y C-03 corregidos; faltan separadores de cochera y descansillo (paso humano añadido) y C-10 |

## 8. Más allá de Revit 2028 (ideas propias del programa)

- **Presupuesto en vivo** ligado al modelo, con precios locales en colones y CSV (ya existe; ampliar a todas las disciplinas).
- **Verificación normativa costarricense** en el propio plano: iluminación por uso (ya muestra lux), detectores en dormitorios,
  cuadro de cargas, presiones y diámetros de plomería (ya calcula), pendientes de desagüe, accesibilidad (Ley 7600).
- **Planos por disciplina automáticos**: un clic que cree las plantas E/I/H/M/S duplicadas, con disciplina, nombre y lámina.
- **Prueba de usabilidad incorporada**: el propio programa puede reproducir la casa del cobogó como tutorial guiado.
- **Comparación de opciones** con costo: dos variantes de diseño con su presupuesto lado a lado.
