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
