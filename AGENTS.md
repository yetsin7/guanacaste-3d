# AGENTS.md — Guanacaste 3D

Asistentes de IA (ChatGPT/Codex, Claude Code): **lee `docs/coordinacion-ia.md` antes de cualquier cambio** y apunta tu turno
en `docs/bitacora-ia.md`.

Resumen en cinco líneas:

1. C#/.NET 10 + WPF. El Render vive **dentro** de la aplicación (pestaña Render); no lo redirijas a otro programa.
2. No rompas lo existente: compila, corre las pruebas del núcleo y de interfaz y la «casa del cobogó» (70 pasos) antes de subir.
3. `git fetch` y fusiona antes de empezar y de subir; nunca `--force`; commits pequeños; sin pruebas en verde, rama + PR.
4. Cada función: prueba del núcleo + paso de interfaz desde un proyecto vacío con botones; mira las capturas.
5. Nada de código, recursos ni marcas de Autodesk: solo comportamiento observable, implementado de forma original.

La lista de trabajo con prioridades está en `docs/casa-cobogo-plan-maestro.md`.
