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
- Selección, inspección, eliminación y deshacer/rehacer.
- Apertura y guardado JSON versionado con validación y respaldo previo al sobrescribir.
- Comprobaciones automatizadas de dominio y persistencia.

Pendiente: puertas y ventanas como objetos completos (hojas, marcos y familias), restricciones geométricas completas, uniones de muros, familias paramétricas avanzadas, vistas de alzado/sección, acotación asociativa, láminas, tablas, interoperabilidad IFC/DXF, colaboración multiusuario, MEP, estructura, render físico y motor geométrico de producción.

> **Validación pendiente:** GitHub Actions está terminando sus trabajos sin asignar runner ni ejecutar pasos; aún no hay evidencia de compilación. Comprueba manualmente con los comandos siguientes en Windows. No uses esta versión para documentación constructiva real.

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

## Comprobar

```powershell
dotnet run --project tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj -c Release
```

## Publicar para Windows

```powershell
dotnet publish src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/win-x64
```

Ejecutable: `dist/win-x64/Guanacaste3D.Desktop.exe`. Esta publicación genera un ejecutable de aplicación, **no** un instalador MSI/MSIX.

## Arquitectura

- `src/Guanacaste3D.Core/`: modelo de documentos, niveles, elementos, validaciones, operaciones e historial.
- `src/Guanacaste3D.Desktop/`: aplicación WPF, vistas e interacción.
- `tests/Guanacaste3D.Checks/`: comprobaciones independientes de UI.
- `docs/roadmap.md`: etapas del desarrollo BIM.

Los archivos de usuario usan la extensión `.g3d.json` y no requieren ninguna aplicación de terceros. Las operaciones de guardado deben mantener versiones anteriores respaldadas; antes de usar software en proyectos reales, exporta copias independientes.
