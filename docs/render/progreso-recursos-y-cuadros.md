# Render progresivo adaptable a equipos pequeños

## Interfaz de progreso

Al iniciar **Renderizar**, Guanacaste 3D muestra una barra horizontal que se llena conforme termina el trabajo, sin exigir al usuario que interprete el número de muestras. En modo cuadros (bucket), el porcentaje se calcula como `cuadros terminados / cuadros totales`, **no** a partir del contador entero de muestras redondeado. Si se detiene, conserva la fracción completada. Si finaliza, llega al 100%.

El tiempo **transcurrido** se mide mediante `Stopwatch` monotónico de .NET desde que acaba la preparación de la estructura de aceleración y comienza el trazado de rayos. No incluye esa preparación, pero sí incluye el tiempo en pausa: se trata de tiempo de reloj de la sesión de renderizado, no de tiempo de CPU. Se muestra hasta décimas de segundo y se congela al finalizar. El tiempo **restante** es una **estimación** basada en velocidad observada y puede cambiar; no se presenta como una cifra exacta.

## Tamaño de los cuadros

En la pestaña **Calidad**, el usuario elige **Automático, 8×8, 16×16, 32×32, 64×64 o 128×128 píxeles**. El tamaño elegido se guarda en `RenderSetup.BucketSize` (0 = automático) y restaura al abrir el proyecto. Por defecto, AUTO usa 16 px si el equipo tiene dos hilos o menos o menos de 2 GiB de RAM libre; usa 32 px en los demás. Los cuadros solo controlan cómo se reparte y revela el trabajo: no cambian la resolución, el número objetivo de muestras ni la calidad física.

Los cuadros pequeños muestran avances granulares, pero muchos cuadros pueden imponer más gestión y actualizaciones. Las capturas de la imagen se espacian de acuerdo con el costo real de producirlas para no agotar la CPU actualizando la ventana. La marca de esquinas también se escala para que un cuadro de 8 px no quede completamente cubierto.

## Monitoreo de recursos

El panel del render tiene tres minigráficas de los últimos ~60 segundos, muestreadas una vez por segundo:

- **CPU**: tiempo de procesador consumido por el **proceso Guanacaste 3D**, dividido entre tiempo de reloj y núcleos lógicos. No representa necesariamente el porcentaje global que informa el Administrador de tareas.
- **RAM**: porcentaje de memoria física **del sistema** actualmente utilizado y, junto al porcentaje, memoria física residente del **proceso** en MB. La RAM almacena modelos, texturas, BVH y búferes; no sustituye la capacidad de cálculo de CPU o GPU.
- **GPU**: una línea plana y `0 % GRE`: el **renderizador actual usa CPU** y no tiene backend GPU. No pretende medir el uso general de GPU por otras aplicaciones ni simular una aceleración inexistente.

Las lecturas se obtienen de `Process.TotalProcessorTime`, `Process.WorkingSet64` y `RenderSafety.ReadMemory()`. No se instalan librerías ni contadores adicionales. La sobrecarga es moderada y se detiene al terminar.

## Equipos con pocos recursos

GRE ya cuenta con un planificador que adapta resolución, muestras y núcleos según las capacidades, ajusta la imagen a la RAM libre, usa caché de irradiancia para previsualización y dispone de un vigilante de memoria que cancela el render antes de agotar el equipo. El render final utiliza este plan y respeta los límites de memoria y batería. Seleccionar **Eco** ayuda a dejar recursos para el resto del sistema.

Esto mejora la accesibilidad, **no garantiza** que una máquina con memoria insuficiente para cargar siquiera la geometría pueda renderizar cualquier escena. La duración exacta futura no puede predecirse: depende de complejidad, temperatura, carga del sistema, memoria y otros procesos.

## Verificación

Ejecutar las regresiones del proyecto en GitHub Actions:

```sh
dotnet run -c Release --project tests/Guanacaste3D.Checks
dotnet build Guanacaste3D.slnx -c Release
```

La primera incluye pruebas para cuadros de 16 px, progreso creciente, finalización, tamaño inválido y validación del proyecto. La compilación WPF y pruebas interactivas requieren Windows. Revisar que el gráfico no bloquee el clic en la imagen y que el render siga siendo cancelable incluso en equipos pequeños.
