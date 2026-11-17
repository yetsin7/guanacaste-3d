# Bitácora de turnos (Claude Code y ChatGPT)

Lee primero `docs/coordinacion-ia.md`. Entradas nuevas **arriba**. Formato: fecha y hora · quién · estado · qué se hizo ·
qué se verificó · qué queda. Estados: EN CURSO (nadie más lo toca), HECHO, ABIERTO.

---

## 2026-11-17 (tarde) · Claude Code · HECHO
**Interfaz del Render y aviso de escalera.**
- La paleta flotante del Render empieza plegada (4 herramientas + botón «⋯»); el marco de la imagen final abraza la imagen
  (Viewbox) sin márgenes blancos; el contorno azul de selección ya no se dibuja sobre la imagen final.
- El aviso de huellas cortas de la escalera dice cuánto recorrido necesita (`ModelWarning.Extra`).
- Presupuesto y guía de coordinación subidos (commits `63a6dcce`, `2f7f3240`).
- **Verificado:** núcleo, suite de interfaz, casa de 70 pasos, Render integrado (446). El paso 12.2 (renderizar la casa)
  pasó en la corrida anterior (36 s) y en la última quedó «NO VERIFICADO» porque el equipo de Yetsin tenía 2.5 GB libres y el
  Render se protegió solo: es la protección funcionando, no un fallo. Si te pasa, cierra programas pesados y repite.
- **Trampa nueva:** el check de video del núcleo («camera is still and the person crosses the view») falla a veces
  con la máquina cargada y pasa al repetir; no lo atribuyas a tu cambio sin repetir.
- **Abierto:** C-39 (rediseñar la escalera de la casa), C-40 (detector de habitaciones), C-42 (presupuesto: sellos, baños,
  cielos, mano de obra).

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
