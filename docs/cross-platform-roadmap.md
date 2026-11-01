# Guanacaste 3D — compatibilidad Windows, Linux y macOS

## Estado actual

- **Motor:** `Guanacaste3D.Core` usa `net10.0`, sin depender de WPF. La geometría BIM, el almacenamiento nativo `.g3d` y las transacciones se comparten entre sistemas.
- **Interfaz Windows actual:** `Guanacaste3D.Desktop` usa WPF (`net10.0-windows`). Se conserva íntegra durante la transición.
- **Interfaz multiplataforma inicial:** `Guanacaste3D.CrossPlatform` usa Avalonia 12.1.4, `net10.0`, y referencia el mismo `Guanacaste3D.Core`. Actualmente permite crear, abrir, validar, resumir y guardar proyectos; **no tiene paridad de modelado, anotación ni renderizado con WPF**.
- **CI:** matriz de Windows x64, Linux x64 y macOS Apple Silicon (arm64). El build no sustituye a las pruebas de interacción real en esos sistemas.

## Principios de migración

1. Una sola fuente de verdad para el BIM y archivos `.g3d`, no tres motores distintos.
2. Mantener WPF operativo mientras se desarrollan capacidades multiplataforma en paralelo.
3. Extraer a bibliotecas `net10.0` servicios hoy acoplados a WPF (estado de interfaz, entrada de usuario, navegación, selección, documento reciente, comandos y preferencias).
4. Mantener el modelo 2D/3D independiente de componentes UI, con renderizador y abstracción de GPU/CPU independientes del sistema operativo.
5. Reemplazar las vistas WPF por componentes Avalonia, comenzando por Inicio/Archivo, navegador, propiedades y edición 2D; después viewport 3D, familias, anotaciones, materiales, render y exportaciones.
6. Probar acciones reales de crear/editar/guardar/reabrir y deshacer/rehacer en cada sistema.
7. Publicar paquetes de producción separados: Windows instalable/portable, Linux AppImage/deb/rpm según distro y macOS .app/.dmg firmados y notarizados. Los artefactos CI iniciales son binarios portables, **no instaladores ni apps firmadas**.

## Requisitos de entrega

- No fusionar un cambio si rompe la compilación WPF existente o las pruebas del núcleo.
- Para macOS: validar Apple Silicon e Intel antes de anunciar ambos como compatibles; para firma y notarización de distribución se requiere entorno Apple apropiado.
- Para Linux: comprobar X11 y Wayland/XWayland, rutas sensibles a mayúsculas, permisos, fuentes y bibliotecas nativas.
- Escalabilidad: implementar y comprobar fallback CPU si no hay GPU compatible, especialmente para vistas/render.
- Cada funcionalidad trasladada debe conservar reglas BIM, referencias asociativas y semántica de eliminación independiente de muros y ejes.

## Uso preliminar de la interfaz Avalonia

```sh
dotnet run --project src/Guanacaste3D.CrossPlatform/Guanacaste3D.CrossPlatform.csproj
```

La ventana preliminar no debe comercializarse como editor BIM completo. La interfaz WPF continúa siendo la versión de trabajo principal mientras avanza la migración.
