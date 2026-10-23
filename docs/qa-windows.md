# Verificación de Guanacaste 3D en Windows

La interfaz requiere **Windows 10/11 y SDK .NET 10**. El proyecto no utiliza servicios externos para ejecutar las funciones del prototipo.

## 1. Compilación limpia

En PowerShell, dentro del repositorio:

```powershell
dotnet --info
git status --short
dotnet restore Guanacaste3D.slnx
dotnet build Guanacaste3D.slnx -c Release --no-restore
dotnet run --project tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj -c Release --no-build
dotnet run --project src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj -c Release --no-build
```

**No interpretar ausencia de logs de GitHub Actions como pruebas aprobadas.** La compilación y los resultados de ejecución locales son la fuente de verificación.

## 2. Lista de comprobación manual

- [ ] La ventana abre y muestra Guanacaste 3D.
- [ ] Crear dos muros de cinco metros y cambiar de planta 2D a modelo 3D.
- [ ] Seleccionar un muro; colocar una puerta y comprobar el hueco en 2D y 3D.
- [ ] Seleccionar una zona distinta del mismo muro; colocar una ventana.
- [ ] Intentar superponer puertas: debe mostrarse un error sin alterar el proyecto.
- [ ] Editar espesor y altura de un muro. Una altura incompatible con una abertura debe rechazarse.
- [ ] Deshacer y rehacer una inserción de ventana.
- [ ] Guardar en un archivo `.g3d.json`; cerrar y reabrir sin perder niveles, muros ni aberturas.
- [ ] Sobrescribir el proyecto y confirmar que exista una copia `.bak`.
- [ ] Cancelar la creación de un proyecto nuevo cuando hay cambios sin guardar.
- [ ] Borrar un muro anfitrión y confirmar que sus aberturas también desaparezcan.
- [ ] Abrir un JSON malformado o incompatible: debe rechazarse sin reemplazar el documento abierto.
- [ ] En el modelo 3D, arrastrar para orbitar y usar la rueda del ratón para zoom; al editar un muro, la orientación debe mantenerse.
- [ ] Restablecer la cámara desde el menú Vista; comprobar que no cambia la geometría BIM.
- [ ] Exportar DXF R12 con una puerta y ventana; importar en un visor CAD externo y verificar metros, capas y nivel Z.
- [ ] Exportar DXF con dos niveles y revisar las capas G3D-L001-* y G3D-L002-*.
- [ ] Guardar, cerrar y volver a abrir un proyecto después de exportar; DXF no debe cambiar el documento original.

## 3. Diagnóstico

Si falla la compilación, conserva la salida completa de `dotnet build`: código de error, archivo y línea.

Si falla el editor, registra la acción, los pasos de reproducción, una captura de pantalla sin información sensible, el mensaje de error y la versión del SDK (`dotnet --info`).

La [ejecución 49](https://github.com/yetsin7/guanacaste-3d/actions/runs/39988219533) de GitHub Actions
finalizó con error **sin pasos** en el trabajo Windows; no se pudo descargar el log del trabajo.
Las causas posibles incluyen políticas de ejecución, cuotas/minutos de Actions,
configuración de facturación o problemas transitorios de aprovisionamiento del runner;
ninguna está confirmada. El flujo ahora separa una prueba de núcleo Linux
de la compilación WPF Windows, con cancelación de ejecuciones obsoletas.

Revisar en GitHub: **Settings → Actions → General**, disponibilidad de runners
alojados, **Settings → Billing** (cuotas de Actions) y el detalle de los dos
trabajos de la ejecución más reciente. Si ambos terminan sin pasos, revisar
configuración del repositorio/cuenta antes de atribuir el problema al código.
Si ejecutan pasos, conservar la salida exacta del primer comando que falla.

## 4. Límites técnicos actuales

Las aberturas son huecos paramétricos, no hojas, marcos o familias completas. No hay uniones complejas ni motor booleano general de sólidos, y esta etapa no valida cálculo estructural, estabilidad, normativas ni presupuestos. No utilizar como entregable constructivo profesional.
