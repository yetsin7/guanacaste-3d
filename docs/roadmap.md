# Hoja de ruta — Guanacaste 3D

## Visión
Producto BIM de escritorio con un modelo paramétrico único que sincronice geometría, plantas, alzados, secciones, láminas, tablas y presupuestos.

## Etapa 0 — Base nativa (prototipo)
- [x] Solución C# / .NET 10 con aplicación WPF.
- [x] Modelo de documentos y niveles.
- [x] Muros rectos con parámetros métricos.
- [x] Planos 2D y geometría de muros en 3D.
- [x] Archivos propios versionados, validación y copia de seguridad.
- [x] Edición básica y deshacer/rehacer con historial acotado.
- [x] Flujo de CI configurado (todavía sin ejecución validada).
- [ ] CI compilado y pruebas ejecutadas exitosamente en Windows.
- [ ] Validación de interfaz en una laptop Windows real.

## Etapa 1 — Núcleo CAD/BIM
- [ ] Motor geométrico robusto (intersecciones, uniones y sólidos).
- [ ] Muros con capas, materiales y prioridades de unión.
- [x] Aberturas paramétricas básicas anfitrionadas en muros rectos, con volumen neto y huecos 3D.
- [ ] Objetos completos de puerta y ventana, familias, materiales, cotas y opciones de anfitrión.
- [ ] Restricciones, cotas asociativas, referencias y transformaciones.
- [ ] Tipos y familias con parámetros compartidos.
- [ ] Selección múltiple, copiado, alineación, matrices y herramientas de modificación.

## Etapa 2 — Documentación coherente
- [ ] Alzados, secciones, detalles, escalas y hojas.
- [ ] Tablas de cantidades, etiquetas y planificación.
- [ ] Cubiertas, forjados, cielos rasos, escaleras, terrenos y habitaciones.
- [ ] Exportación PDF/DXF/IFC con pruebas de precisión.

## Etapa 3 — Disciplinas y colaboración
- [ ] Herramientas estructurales y MEP.
- [ ] Fases, variantes, modelos vinculados y coordinación.
- [ ] Versionado colaborativo con bloqueo/transacciones.
- [ ] API y extensibilidad por complementos.

## Condiciones de calidad
1. Modelo de datos independiente de WPF y renderizado.
2. Nunca confiar en documentos externos sin validar.
3. No afirmar compatibilidad con formatos no implementados.
4. No utilizar marca, código, iconografía o recursos propietarios de terceros.
5. Medir rendimiento en modelos grandes antes de elegir motor 3D definitivo.
