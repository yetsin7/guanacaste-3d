# Guanacaste 3D

Proyecto de unificación de **Mau Design** y **Mo Design 3D**.

> **Estado: migración preparada, no terminada.** El código de ambas aplicaciones no ha sido publicado todavía aquí y la nueva aplicación no cuenta con una compilación validada.

## Fuentes comprobadas

- [Mau Design](https://github.com/yetsin7/mau-design) — versión 0.5.0, arquitectura BIM local.
- [Mo Design 3D](https://github.com/yetsin7/mo-design-3d) — versión 1.0.2, arquitectura, materiales, objetos, documentos, intercambio de formatos y renderizado progresivo.

Mo Design 3D será la base. Se compararon árboles de archivos de las ramas `main`: 194 archivos en Mau Design, 388 en Mo Design 3D y 80 idénticos. Cuatro archivos de render de Mau Design no existen en la misma ruta de Mo Design 3D, y deberán conservarse como referencia.

## Preparación local segura

En un entorno de desarrollo con GitHub CLI autenticado, Git y Node.js 22.12+:

```bash
bash scripts/prepare-migration.sh /ruta/nueva/guanacaste-3d
```

El script clona las dos fuentes a un directorio temporal, copia el código de Mo Design 3D al proyecto nuevo, guarda los cuatro archivos históricos de render en `legacy-reference/`, y prepara la nueva identidad. El script **no publica** los cambios, **no borra** las fuentes y **no migra automáticamente** los datos de usuario.

**Antes de distribuir o publicar:** resolver los identificadores heredados de IPC, instalación, scripts y documentación, comprobar compatibilidad de proyectos `.mau.json` y `.maar`, ejecutar `npm ci`, `npm run typecheck`, `npm test`, `npm run build`, pruebas Electron y pruebas Windows.

## Protección de datos

No borrar carpetas ni instalaciones antiguas hasta tener backups independientes y pruebas satisfactorias de apertura, edición, exportación y restauración de proyectos. Los repositorios anteriores permanecerán intactos.
