# Bitácora de turnos (Claude Code y ChatGPT)

Lee primero `docs/coordinacion-ia.md`. Entradas nuevas **arriba**. Formato: fecha y hora · quién · estado · qué se hizo ·
qué se verificó · qué queda. Estados: EN CURSO (nadie más lo toca), HECHO, ABIERTO.

---

## 2026-11-17 · Claude Code · HECHO
**Render integrado y casa del cobogó desde cero.**
- Integré `fix/restore-integrated-dotnet-render-20261117` y la rama `plan/dotnet-core-first-20261117` (roadmap, contratos de
  QA y la protección contra sobrescribir exportaciones de render).
- Botones «Ir a Render» e «Iniciar render» en Vista; la cámara se encuadra al abrir Render; la casa renderiza (≈ 25 s a
  calidad borrador). Fase 12 y 13 del guion: Render y entorno (césped, acceso, calle, árboles).
- Reglas de diseño en «Revisar advertencias»: nada estorba frente a una puerta (`DoorClearance`), reglas de escalera
  (contrahuella, huella, paso, hueco en la losa), habitaciones superpuestas medidas sobre su contorno real.
- Modificar (Copiar, Girar, Simetría, Matriz) funciona con pilares, escaleras, mobiliario, losas, habitaciones, dispositivos.
- Presupuesto: formaleta, clavos, impermeabilización, sellador; muro de madera y cobogó dejaron de costearse como bloque y
  muro cortina. Casa: ≈ ₡78.9 M en 14 rubros.
- **Verificado:** compilación sin errores, checks del núcleo, suite de interfaz, inventario de la cinta, pruebas del
  Render integrado (446) y la casa completa (70 pasos), todo en verde en el último commit.
- **Abierto:** C-39 (escalera con huellas de 21 cm), C-40 (detector de habitaciones frágil), C-38 (interfaz del Render),
  C-42 (presupuesto: sellos de ventana, baños y cimientos, cielos, mano de obra). Ver el plan maestro.
- **Para ChatGPT:** toma de la cola de la sección 7 de `coordinacion-ia.md`; marca «EN CURSO» aquí antes de empezar.
