# Plan de migración — Guanacaste 3D a Rust

Fecha inicial: 2026-11-11. Estado: **en ejecución por etapas, NO completado**. Rama de trabajo: `migration/rust-foundation-20261111`.

## Objetivo y restricciones
Migrar gradualmente la aplicación BIM Guanacaste 3D desde C#/.NET 10 y WPF hacia una arquitectura mayoritariamente Rust, sin perder los proyectos existentes, la geometría, la precisión, el editor de familias ni las funciones de render. La migración no constituye por sí misma una optimización: cada sustitución exige medidas comparables de memoria, tiempo y calidad. El código C# actual permanece utilizable hasta que sus sustitutos cumplan paridad funcional. No borrar código, datos, recursos ni historial de migración prematuramente.

## Inventario inicial verificado
- `src/Guanacaste3D.Core`: modelo BIM `BuildingDocument`, sesiones transaccionales `DocumentSession`, formatos `.g3/.g3d/.g3df/.g3dt`, geometría, importación, mallas, BVH y trazador.
- `src/Guanacaste3D.Desktop`: interfaz WPF Windows y render en pantalla.
- `src/Guanacaste3D.Agent`, `CrossPlatform`, `RevitBridge`, `SketchUpExporter`, `shared` y `windows-thumbnails`: requieren auditoría de dependencias individual.
- `tests/`: pruebas C#, UI, geometría, auditoría, miniaturas, rendimiento y más. Mantenerlas como oráculo de paridad hasta reemplazarlas.
- Integración con Guanacaste Render: enlaces de modelo, reexportación con fusión y preservación de trabajo del render según commits recientes. No romper el protocolo.
- Contratos de coordenadas: metros, Z arriba, +Y norte; adaptación WPF (x,z,-y).
- Archivo nativo: firma `G3DNATV1`, esquema y checksums SHA-256; formato `.g3` legado `G3-PROJECT/1\n` + JSON. No introducir escritores incompatibles antes de crear pruebas de ida y vuelta.

## Arquitectura objetivo propuesta
```
rust/
  Cargo.toml                    # workspace
  crates/
    guanacaste-math/            # vectores, transformaciones, cotas y tolerancias
    guanacaste-document/        # entidades BIM, invariantes y sesiones
    guanacaste-geometry/        # triangulación, mallas, perfiles, operaciones
    guanacaste-io/              # compatibilidad .g3/.g3d/.g3df/.g3dt
    guanacaste-render-core/     # BVH, materiales, integradores y procesado
    guanacaste-ffi/             # ABI C estable para transición .NET ↔ Rust
    guanacaste-desktop/         # futura aplicación Rust y su interfaz
```
La creación de cada crate se realizará cuando exista funcionalidad probada, sin crear módulos vacíos que aparenten progreso. Para UI investigar egui/eframe, Slint, Iced o winit + wgpu; elegir tras prototipos de accesibilidad, Wacom/entrada, multiventanas, escalado HiDPI, docking, internacionalización, edición de propiedades y viewport BIM. Para gráficos evaluar wgpu en GPU con fallback CPU; para integraciones Win32 o SDK propietarios mantener adaptadores específicos cuando sea necesario. No prometer eliminación absoluta de todas las dependencias no-Rust.

## Fases y criterios verificables
- [x] **F0.** Inspeccionar arquitectura inicial y abrir este documento de control en una rama separada.
- [x] **F1.** Crear workspace Rust mínimo que compile; primera biblioteca de matemática/transformaciones con pruebas de invariantes, NaN, tolerancias, precisión métrica y determinismo. CI Rust Windows/Linux verificada en commit `2b1d48a7`.
- [ ] **F2.** Establecer bancos de pruebas de equivalencia C# ↔ Rust con fixtures de edificios, transformaciones, mallas y persistencia; medir tiempos y memoria con misma entrada y máquina.
- [ ] **F3.** Portar geometría computacional, cortes, triangulación, snapping y topología con pruebas de casos degenerados.
- [ ] **F4.** Portar documento BIM, validaciones, transacciones atómicas, undo/redo, parámetros, familias y regeneración de operaciones.
- [ ] **F5.** Portar formatos `.g3/.g3d/.g3df/.g3dt` con lectura/escritura y pruebas golden; nunca modificar archivos originales en pruebas.
- [ ] **F6.** Portar importadores/exportadores y adaptadores interoperables; verificar IFC, OBJ, DXF, STL, glTF y enlaces externos.
- [ ] **F7.** Portar BVH/render, materiales físicamente basados, luces, texturas, denoise y manejo progresivo de GPU/CPU; benchmarking con calidad igualada.
- [ ] **F8.** Nueva UI Rust con editor BIM equivalente: ribbons, familias, niveles, muros, aberturas, vistas, planos, propiedades, edición directa, atajos, accesibilidad, configuración y español.
- [ ] **F9.** Integración robusta con Guanacaste Render y exportación, compatibilidad de protocolos, instalador, Windows e interfaces externas necesarias.
- [ ] **F10.** Paridad de pruebas automatizadas y manuales, modelos reales, cargas grandes, consumo de RAM, estabilidad, seguridad y empaquetado.
- [ ] **F11.** Cambio de ejecutable predeterminado sólo con todas las puertas de calidad verdes; retirar C# por paquetes cuando exista evidencia de paridad y copia recuperable.

## Reglas de ejecución
1. Ejecutar un cambio pequeño y reversible por commit, sobre rama aislada, con PR hacia `main`; nunca forzar push ni alterar archivos de usuario.
2. Antes de reemplazar módulo: documentar API, contratos y pruebas; después, medir paridad y rendimiento. `unsafe` sólo con invariantes y tests explícitos.
3. Preservar `Casa del Cobogó` y los recursos de usuario. No eliminar samples, modelos ni formatos en esta fase.
4. Cada PR debe registrar: archivos portados, comandos de compilación, pruebas ejecutadas, pruebas no ejecutadas y funcionalidad pendiente. Sin una ejecución real de tests, el estado es «no verificado».
5. No alterar compatibilidad de los formatos ni integración con Guanacaste Render sin fixtures y protocolo versionado.
6. La meta es software BIM útil y optimizado, no sólo un conteo alto de archivos `.rs`.

## Registro de progreso
- 2026-11-11: inspección de `ARCHITECTURE.md`, solución `.slnx`, carpetas `src/`, `tests/` y commits recientes; se establece la rama y el plan.
- Próxima entrega: workspace, primer crate con pruebas y CI Rust en Windows/Linux, manteniendo .NET/WPF.

- 2026-11-11, segunda entrega: añadido `guanacaste-geometry`, port inicial de triangulación de polígonos desde `PolygonTriangulator.cs` y pruebas de polígonos cóncavos, inversión de orientación, vértices colineales y desplazamientos de coordenadas grandes. **F3 sigue incompleta**: faltan triangulación validada en casos adversarios, mallas BIM, booleanas, snapping y paridad cruzada automatizada. Corregida la política de acciones de terceros mediante pin SHA del checkout y `rustup`.

- 2026-11-11, tercera entrega: implementado `rust/crates/guanacaste-geometry/src/mesh.rs` con mallas triangulares Z-up, límites geométricos, volumen firmado y extrusión de huellas de planta simples mediante tapas y paredes orientadas hacia fuera; pruebas de volumen, polígonos cóncavos, orientación inversa y elevaciones inválidas. La extrusión todavía no cubre huecos, booleanas ni regeneración BIM. CI Linux verde para `c2192084`; Windows pendiente al momento de documentar. La validación de Windows WPF existente fallaba en la tarea `Desktop interaction and persistence regressions` antes de esta entrega y se mantiene como bloqueo independiente a investigar.

- 2026-11-11, cuarta entrega: `guanacaste-geometry::topology::validate_extrusion_footprint` valida polígonos simples antes de extruir: descarta aristas cruzadas/no adyacentes que se tocan, duplicados de cierre y vértices consecutivos colineales que producirían uniones en T. `extrude_footprint` ejecuta el preflight. Pruebas nuevas cubren polígonos cóncavos e invertidos y errores geométricos. Esta validación usa predicados f64 exactos; todavía requiere predicados robustos/tolerancias en el núcleo BIM antes de producción. CI se comprueba separadamente; F3 aún incompleta.

- 2026-11-11, quinta entrega: `guanacaste-geometry::wall::mesh_wall_with_openings` genera malla triangular de muros rectos con huecos rectangulares pasantes mediante subdivisión conforme en cuadrícula local u/z, creando caras de muro, jambas, dinteles y alféizares. Valida espesores, elevaciones, límites y superposición de aberturas; incluye pruebas de volumen, orientación con muros girados y entradas inválidas. Todavía no implementa muros inclinados, curvos, uniones de varios muros, apertura parcial o mapeo de materiales/IDs BIM. **F3 sigue incompleta**. Validación Linux satisfactoria para `b06df8cd`, Windows pendiente de confirmación.

- 2026-11-11, seguimiento de quinta entrega: al publicar `wall` como módulo del crate, CI encontró un fallo real en el volumen firmado; se corrigió la orientación de caras extremas/jambas en `ca42a59b`. Los tests Rust Linux pasan en este commit (run `43075225168`); el resultado Windows debe confirmarse por separado. Nota: ejecuciones previas del mismo archivo antes de `pub mod wall` no ejercitaban sus tests. Esta entrega no modifica aún el generador BIM C# en producción.

- 2026-11-11, sexta entrega: añadido crate `guanacaste-document` con tipos iniciales `Level`, `Wall`, `HostedOpening`, identificadores de 128 bits, referencias, validación de cotas y pertenencia, y operación `Document::transact` por copia/validación/commit (cambio inválido no altera documento). `Document::wall_mesh` genera geometría a través de `guanacaste-geometry`, aplicando elevación de nivel y desplazamiento de base. Tests: geometría con hueco y offset, IDs duplicados, referencias inexistentes, invalidación transaccional y huecos superpuestos. CI Rust Windows/Linux **aprobada** en commit `529830f8`, run `43077398600`. **F4 está iniciada, NO completada**: faltan esquema 15, todas las entidades BIM, undo/redo, familias, serialización nativa, interoperabilidad C# y equivalencia completa.

- 2026-11-11, séptima entrega: nuevo `guanacaste-document/src/session.rs` implementa `DocumentSession` con snapshots limitados, edición mediante candidato validado, `undo`/`redo`, descarte de rama redo tras una nueva edición y detección de cambios nulos. Pruebas de reversibilidad, preservación de malla/volumen, capacidad máxima, transacciones fallidas y validación de documento inicial. Corregidos rustfmt y Clippy detectados en CI. Linux Rust aprobado en `dfd7b8f0` (run `43079436958`), Windows pendiente al registrar esta entrada. NO es todavía equivalente a todas las operaciones/undo del `.NET DocumentSession`, F4 continúa parcial.

- 2026-11-11, octava entrega: creado crate `guanacaste-io` con lector **solo lectura** de envoltura `.g3` (`G3-PROJECT/1\\n`) y contenedor `.g3d`/`.g3df`/`.g3dt` (`G3DNATV1`) según `ProjectFile.cs` y `native-documents.cs`. Verifica firma, versión, tipo, GUID en bytes de .NET, tamaños máximos, gzip, SHA-256 y concordancia de identidad de cabecera/JSON para el contenedor nativo. Conserva bytes JSON originales sin escribir archivos; pruebas sintéticas de integridad, identidad, campos obligatorios y truncamiento. **Esto NO es un deserializador BIM de esquema 15 ni migrador de esquemas**; aún faltan fixtures creados por C# real, validación de claves JSON duplicadas y límites/protocolos más completos. Linux CI aprobado en `a5c7dd4f` (run `43080112311`), Windows pendiente al registrar. F5 inicia parcialmente.

- 2026-11-11, novena entrega: añadido parser JSON estricto (`guanacaste-io/src/strict_json.rs`) con `serde::Visitor` recursivo: rechaza nombres de campo duplicados en objetos raíz y anidados, incluyendo claves equivalentes tras decodificar escapes Unicode; conserva bytes originales si el documento pasa la inspección. Las comprobaciones se aplican tanto a `.g3` como a `.g3d`, con pruebas en las entradas públicas y casos de JSON anidado. No se modifica ni sobreescribe ningún archivo. El nuevo CI Rust debe confirmar el commit `f6438d7b`; faltan aún fixtures reales de C#, soporte integral del esquema BIM y escritura segura. F5 continúa parcial.

- 2026-11-11, décima entrega: incorporado `guanacaste-io::inspect_legacy_json` para la tercera vía de importación de `ProjectFile.cs`, `.g3d.json` sin cabecera. Comprueba límite de 20 MiB, objeto JSON y campos obligatorios, identidad básica y claves duplicadas en todos los niveles; devuelve exactamente los bytes originales y no realiza migración/escritura. Pruebas sintéticas comprueban equivalencia de payload con `.g3`, rechazo de cabecera incorrecta, duplicados y exceso de tamaño. Commit `f74bd35c`; CI Windows/Linux pendiente de resultados para este commit. La equivalencia es de reglas de envoltura observadas en C#, NO de deserialización real ni fixtures producidos por C#. F2 y F5 siguen incompletas.

- 2026-11-11, nueva corrección de paridad C#/Rust: `HostedOpening` usa cotas de antepecho/dintel relativas al `Level`, como `WallOpening` C#, y NO a la base del muro; el generador recorta huecos cuando `Wall.BaseOffset` eleva la base y omite los completamente inferiores. Nuevo adaptador `HostedOpening::from_dotnet_dimensions` convierte `Offset` central y `Width` a intervalos sin cambiar alturas (`Sill`, `Height`). Pruebas agregadas para muros elevados y dimensiones inválidas. Esta entrega NO implica paridad de muros curvos, `TopProfile`, uniones ni compatibilidad de todos los documentos. CI Rust en revisión para commit `87e6524c`. La automatización horaria existente continúa activa.

- 2026-11-11, siguiente avance F5: `guanacaste-io::bim_slice::extract_wall_slice` extrae de JSON previamente inspeccionado de esquema 15 un subconjunto BIM tipado de `Levels`, `Walls` rectos y `Openings`. Convierte GUID canónicos al `ElementId` Rust de 128 bits y las aberturas C# (`Offset` central, `Width`, `Sill`, `Height`) al modelo geométrico. Valida referencias y malla; rechaza explícitamente perfiles superiores, muros curvos, top-level constraints y offsets de coronación no implementados; preserva los bytes originales y los campos desconocidos en `InspectedDocument`. Pruebas sintéticas cubren extracción y volumen, rechazo de geometrías no compatibles, esquemas anteriores, referencias ausentes e IDs inválidos/duplicados. No es aún una apertura completa de proyectos reales; faltan fixtures de integración creados por C# y paridad de `WallGeometry`. CI Linux confirmado para `75b17f3d`, Windows pendiente de confirmación.

- 2026-11-11, validación de reglas de aberturas C#→Rust: `bim_slice::extract_wall_slice` aplica en el límite de importación las restricciones de `WallGeometry.Validate` de `WallOpenings.cs` para el subconjunto de muros rectos: `OpeningKind` 0/1, ancho y alto ≥0.3 m, separación de extremos ≥0.05 m, distancia entre aberturas ≥0.05 m, antepecho cero para puertas y dintel ≤ altura de muro menos 0.05 m. Tests sintéticos de aceptación/rechazo agregados; **no equivalen a fixtures producidos/ejecutados por C#**, ni validan el resto de las entidades BIM. Primer CI `d0da5730` falló por una llave sin escapar en `format!` de test; corregido en `76202506`, nueva CI Windows/Linux pendiente. La capa Rust aún tiene una validación geométrica distinta a C# para casos fuera del subconjunto; F4/F5 incompletas.

- 2026-11-11, prueba incremental de paridad de envolturas F5: commit `1a2c48d8` añade tests que pasan el mismo payload sintético de esquema 15 por `inspect_native` (G3DNATV1 + gzip/SHA-256), `inspect_preview` (.g3) y `inspect_legacy_json` (.g3d.json), y comparan la salida tipada de `extract_wall_slice` sin reescribir bytes. Se prueba además que una alteración de SHA-256 es rechazada antes de acceder al adaptador. Este fixture sintético contiene listas de niveles y muros vacías; NO verifica aún equivalencia geométrica de documentos C# reales ni serialización. Antes del cambio: CI Rust y desktop cross-platform del commit `09eec3d2` aprobadas; Windows WPF/core falló en el paso «Desktop interaction and persistence regressions» (run `43091405881`, pendiente de diagnóstico de logs). Para `1a2c48d8`, run Rust `43098858616` inicialmente en cola; cargo fmt/clippy/test y Windows/Linux pendientes de resultado, no se declara aprobación. F4/F5 continúan incompletas.
