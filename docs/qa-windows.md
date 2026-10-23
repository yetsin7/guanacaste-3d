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

## 3. Diagnóstico

Si falla la compilación, conserva la salida completa de `dotnet build`: código de error, archivo y línea.

Si falla el editor, registra la acción, los pasos de reproducción, una captura de pantalla sin información sensible, el mensaje de error y la versión del SDK (`dotnet --info`).

La ausencia de un runner asignado en GitHub Actions es un problema de infraestructura/ejecución todavía por diagnosticar; no demuestra si el código compila o falla.

## 4. Límites técnicos actuales

Las aberturas son huecos paramétricos, no hojas, marcos o familias completas. No hay uniones complejas ni motor booleano general de sólidos, y esta etapa no valida cálculo estructural, estabilidad, normativas ni presupuestos. No utilizar como entregable constructivo profesional.
