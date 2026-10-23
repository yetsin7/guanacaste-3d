# Guanacaste 3D

**Guanacaste 3D** es un proyecto independiente de software BIM y CAD arquitectónico de escritorio para Windows, desarrollado en **C#** sobre **.NET 10 LTS** y **WPF**.

## Estado

**Versión 0.1.0 — prototipo técnico inicial.** No es todavía un sistema BIM completo ni es apto para cálculos estructurales, planos de construcción validados o entregables profesionales sin revisión independiente.

Implementado en esta primera base:
- Aplicación de escritorio nativa WPF sin Electron ni navegador embebido.
- Documento de proyecto con niveles, muros y aberturas paramétricas vinculadas a su muro anfitrión.
- Puertas y ventanas como huecos geométricos básicos, posicionables mediante clic en el muro (sin hojas, marcos ni familias aún).
- Volumen neto de muros con descuento de aberturas y rechazo de superposiciones.
- Dibujo de muros mediante dos clics sobre la planta.
- Vista de planta 2D y geometría volumétrica 3D del mismo documento.
- Navegación del visor 3D: orbitar arrastrando el ratón, zoom con la rueda y restauración de cámara.
- Selección, inspección, eliminación y deshacer/rehacer.
- Apertura y guardado JSON versionado con validación y respaldo previo al sobrescribir.
- Exportación de cantidades CSV y dibujo geométrico esquemático DXF ASCII R12 (no BIM, sin importación).
- Comprobaciones automatizadas de dominio, persistencia y DXF.

Pendiente: puertas y ventanas como objetos completos (hojas, marcos y familias), restricciones geométricas completas, uniones de muros, familias paramétricas avanzadas, vistas de alzado/sección, acotación asociativa, láminas, tablas, IFC e importación DXF, colaboración multiusuario, MEP, estructura, render físico y motor geométrico de producción.

> **Validación pendiente:** la ejecución [n.º 49](https://github.com/yetsin7/guanacaste-3d/actions/runs/39988219533) falló sin ejecutar ningún paso (y no generó logs de compilación). Se prepararon trabajos separados de núcleo Linux y escritorio Windows, pero la infraestructura de Actions sigue sin validar. No confundas un fallo previo al runner con errores de código. Comprueba manualmente con los comandos siguientes en Windows. No uses esta versión para documentación constructiva real.

## Requisitos

- Windows 10/11, x64.
- SDK **.NET 10** actualizado (estable, LTS).
- Visual Studio con carga de trabajo *Desarrollo de escritorio de .NET* es opcional: la CLI es suficiente.

## Ejecutar

```powershell
git clone https://github.com/yetsin7/guanacaste-3d.git
cd guanacaste-3d
dotnet --version
dotnet restore Guanacaste3D.slnx
dotnet build Guanacaste3D.slnx -c Release
dotnet run --project src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj -c Release
```

## Verificar y generar un paquete portable en un paso (Windows)

Con el SDK .NET 10 instalado, puedes ejecutar en PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-windows.ps1 -Launch
```

El script restaura paquetes, compila, ejecuta comprobaciones y publica
`dist/win-x64/Guanacaste3D.Desktop.exe` junto a `SHA256SUMS.txt`.
**Detiene el proceso** si un comando falla. `-Launch` abre la aplicación;
`-SkipPackage` compila y verifica sin publicar. La aplicación publicada
es **portable, no un instalador**.

## Comprobar

```powershell
dotnet run --project tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj -c Release
```

## Publicar para Windows

```powershell
dotnet publish src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/win-x64
```

Ejecutable: `dist/win-x64/Guanacaste3D.Desktop.exe`. Esta publicación genera un ejecutable de aplicación, **no** un instalador MSI/MSIX.

## Exportar DXF y navegar en 3D

En la aplicación, utiliza **Archivo → Exportar dibujo DXF R12** para generar
contornos de muro y marcas de puertas/ventanas separados por capas y niveles.
El resultado está en **metros** y representa geometría esquemática,
**no** entidades BIM ni hojas de plano certificadas.

En la pestaña **Modelo volumétrico 3D**, arrastra para orbitar la cámara,
usa la rueda para acercar/alejar y elige **Vista → Restablecer cámara 3D**
para volver a la perspectiva inicial.

Consulta [alcance y pérdidas del DXF](docs/interoperabilidad-dxf.md) y
[verificación manual de Windows](docs/qa-windows.md).

## Arquitectura

- `src/Guanacaste3D.Core/`: modelo de documentos, niveles, elementos, validaciones, operaciones e historial.
- `src/Guanacaste3D.Desktop/`: aplicación WPF, vistas e interacción.
- `tests/Guanacaste3D.Checks/`: comprobaciones independientes de UI.
- `docs/roadmap.md`: etapas del desarrollo BIM.

Los archivos de usuario usan la extensión `.g3d.json` y no requieren ninguna aplicación de terceros. Las operaciones de guardado deben mantener versiones anteriores respaldadas; antes de usar software en proyectos reales, exporta copias independientes.
