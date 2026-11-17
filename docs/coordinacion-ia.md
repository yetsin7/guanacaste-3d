# Coordinación entre asistentes de IA (Claude Code y ChatGPT)

Para: ChatGPT (trabaja en este proyecto cada hora) y Claude Code. Escrito el 2026-11-17.
Dueño del proyecto: Yetsin. **Objetivo de Yetsin:** que Guanacaste 3D sea un programa de diseño arquitectónico completo,
que diseñe, presupueste (repello, pintura, pisos, techos, clavos, alambre, varillas, cemento, piedrín, arena…) **y
renderice dentro de la misma aplicación**, comportándose como Revit en lo que el usuario ve y hace.

Léelo entero antes de tocar nada. Es corto a propósito.

## 1. Reglas que no se discuten

1. **No rompas lo que ya funciona.** Antes de tu primer commit del día corre las pruebas de la sección 4. Si algo estaba en
   verde y ahora no, lo arreglas tú o deshaces tu cambio; no lo dejas «para después».
2. **Sincroniza antes de empezar y antes de subir:** `git fetch --all --prune`, `git merge origin/main` (o rebase),
   resuelve conflictos leyendo ambos lados. **Nunca** `push --force`, nunca reescribas historia, nunca borres archivos ajenos.
3. **Sube a GitHub cada lote verificado**, con commits pequeños y un mensaje que diga el porqué. Sin pruebas en verde no se
   sube a `main`: usa una rama (`ia/chatgpt-<tema>-AAAAMMDD`) y una PR.
4. **Una cosa por vez y dilo.** Antes de empezar una tarea anótala en `docs/bitacora-ia.md` («EN CURSO») para que el otro no
   haga lo mismo. Al terminar, cambia la entrada a «HECHO» con lo que verificaste.
5. **Render integrado.** El Render vive **dentro** de Guanacaste 3D (pestaña Render, botones «Ir a Render» e «Iniciar
   render» en Vista). No vuelvas a redirigirlo a otra aplicación. La exportación a Guanacaste Render es una acción
   externa explícita aparte, y nunca sobrescribe un proyecto ajeno (ver `docs/qa/render-export-safety-matrix.md`).
6. **Plataforma:** C#/.NET 10 y WPF. Rust no entra a producción (ver `docs/dotnet-core-first-roadmap.md`).
7. **Nada de Autodesk:** se copia el *comportamiento observable* de Revit (cómo reacciona al usuario), jamás código,
   binarios, recursos, iconos ni marcas. No descompilar ni extraer nada de Revit.
8. **Formatos de archivo** (`.g3d`, `.g3df`, `.g3dt`): no cambiarlos sin round-trip y compatibilidad hacia atrás.
9. **Disco:** la unidad C: de Yetsin se llena. Si `df -h /c` muestra menos de 5 GB, borra solo `tests/*/bin/Release`
   (se regeneran) antes de compilar. Una edición con disco lleno ya dejó un archivo en 0 bytes una vez.

## 2. Dónde está la verdad

| Qué | Dónde |
|---|---|
| Lista de brechas con prioridad (C-01 … C-42) y su estado | `docs/casa-cobogo-plan-maestro.md` |
| Bitácora de turnos (quién hizo qué, qué falta) | `docs/bitacora-ia.md` |
| Contratos de calidad y puertas de fusión | `docs/qa/*.md`, `docs/dotnet-core-first-roadmap.md` |
| Escenario de regresión principal | «Casa del cobogó» construida **desde cero con botones** (70 pasos) |

El plan maestro manda: toma la brecha abierta de mayor prioridad (P0/P1 primero), no inventes trabajo paralelo.

## 3. Cómo se trabaja aquí (convenciones que ya existen)

- **Cada función nueva lleva dos pruebas:** una del núcleo (`tests/Guanacaste3D.Checks/*.cs`, registrada en `Program.cs`) y
  un paso en el guion de interfaz que la usa **solo con los botones, empezando de un proyecto vacío**
  (`tests/architecture-ui-checks/`). Mira las capturas que genera el guion (`artifacts/interaction-evidence/…`) y juzga
  lo que ve una persona, no solo lo que dice el modelo. Eso encontró cinco fallos que el guion largo escondía.
- **Todo comando de la cinta** necesita su ilustración `CmdIcon.<Id>` en `resources/icons.commands.xaml`, su texto en
  `resources/localization*.es.xaml` y un aviso en la barra de estado (si no, el inventario lo marca «sin respuesta»).
- **Textos al usuario:** español, en los `.xaml` de localización; los mensajes de error del núcleo también llevan
  traducción (`localization.errors.es.xaml`) o falla una prueba.
- **Líneas de fin de archivo mezcladas (CRLF y LF):** muchos `.cs` y `.xaml` tienen ambas. Edita por texto exacto y no
  reformatees archivos enteros; un parche que falla por un `\r\n` es lo normal, pruébalo con ambas variantes.
- **Diálogos:** nada de cajas de texto con códigos («3 0.012 bottom»). Usa un formulario (`FormDialog`) con campos
  rotulados y listas.
- **Reglas de diseño** van en `ModelWarnings` («Revisar advertencias»), con código estable, mensaje en español y aviso
  inmediato al colocar si aplica (ejemplo: `DoorClearance`, reglas de escalera).
- **Presupuesto:** `MaterialTakeoff` + `MaterialCatalog` + precios en `construction-budget.cs`. Todo material nuevo
  necesita código, categoría, desperdicio, unidad, precio de referencia (colones) y una prueba.

## 4. Cómo verificar (Windows, en este orden)

```powershell
dotnet build Guanacaste3D.slnx -v q                      # 0 errores
dotnet run --project tests/Guanacaste3D.Checks --no-build  # núcleo: «all core checks passed»
dotnet run --project tests/architecture-ui-checks --no-build   # suite de interfaz: «Architecture UI checks passed»
$env:G3D_SCENARIOS_ONLY=1; $env:G3D_COBOGO=1             # la casa del cobogó completa (~6 min, 70 pasos)
dotnet run --project tests/architecture-ui-checks --no-build
dotnet run --project tests/model-workspace-checks --no-build   # Render integrado
```

Variables útiles del arnés: `G3D_FRESH=structure|stair|roof|sheet|edit` (un tema desde proyecto vacío),
`G3D_STOP_AFTER=2.6` (la casa se detiene tras ese paso: depurar en segundos), `G3D_RIBBON_INVENTORY=1`.
Las corridas largas se lanzan **en segundo plano** y se lee el resultado al final. No recompiles mientras una corrida
usa los binarios.

## 5. Invariantes: si una prueba de estas falla, tu cambio rompió algo importante

- La casa del cobogó pasa sus 70 pasos, el paso 13.3 (**ninguna puerta con estorbos**) y deja **una sola advertencia**
  (la escalera, C-39) en el paso 13.4.
- El botón «Ir a Render» abre la pestaña Render integrada con la casa; «Iniciar render» produce una imagen.
- Copiar, Girar, Simetría y Matriz funcionan con pilares, escaleras, mobiliario, losas, habitaciones y dispositivos.
- El presupuesto de la casa trae formaleta, clavos, impermeabilización, sellador, madera y cobogó (≈ ₡78.9 M).
- Una columna colocada frente a una puerta avisa en la barra de estado.

## 6. Trampas conocidas (te ahorran horas)

- **Detector automático de habitaciones (C-40):** un muro nuevo que atraviesa el arranque de un tabique crea franjas de
  10 cm como «habitaciones» y hace perder otras reales. Si añades muros a la casa, mira siempre el paso 2.6.
- **Arnés de prueba:** instala el contexto de sincronización de WPF (la app real lo trae); el vigilante de ventanas cierra
  cualquier ventana que no esté en `KeepOpen`, así que abre/protege un editor **antes** de cambiar de nivel.
- **Biblioteca arquitectónica:** `Library(...)` en el arnés arma la herramienta; **Espacio** gira 90° lo que se va a
  colocar (4 pulsaciones = vuelta completa).
- **Esc** reemplaza el mensaje de la barra de estado por «Modo selección»: léelo antes de pulsar Esc.
- Un `git stash` no guarda archivos nuevos sin `-u`.

## 7. Cola sugerida (la verdad es el plan maestro)

1. C-39: rediseñar la escalera de la casa (huellas ≥ 28 cm: tramo en U o más recorrido) para dejarla sin advertencias.
2. C-40: robustecer el detector de habitaciones (muros que cruzan tabiques, franjas diminutas).
3. Más reglas de diseño: altura libre de paso, ancho mínimo de pasillos, ventanas tapadas por muebles o columnas.
4. C-38: interfaz del Render (paleta flotante que tapa el modelo, márgenes de la imagen, cielo y entorno propios).
5. C-42: terminar el presupuesto (sellos de ventana, impermeabilización de baños y cimientos, cielos, mano de obra real).
6. Contratos de `docs/qa/` convertidos en pruebas automatizadas (exportación segura, transacciones, round-trip).

## 8. Al terminar tu turno

Escribe en `docs/bitacora-ia.md`: fecha y hora, quién eres, qué hiciste, **qué comprobaste y con qué resultado**, qué
dejaste abierto y qué debería hacer el siguiente. Después sube todo. Si no pudiste verificar algo, dilo tal cual:
«no verificado» vale más que un «debería funcionar».
