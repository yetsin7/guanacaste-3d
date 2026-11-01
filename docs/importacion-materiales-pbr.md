# Importar materiales PBR en Guanacaste 3D

La biblioteca incluye materiales con parámetros físicos para concreto, ladrillo, cerámica, madera, acero, gypsum, repello, fino y muchos otros acabados.

## Crear un material directamente desde Guanacaste 3D

En **Render → Materiales**, elige **Crear material .g3dm desde imagen…**. Selecciona una fotografía
de albedo y establece nombre, rugosidad, metalicidad, escala y relieve. Guarda el nuevo material
en una carpeta; Guanacaste 3D crea allí el manifiesto `.g3dm` y una copia de la imagen con
sufijo `-albedo`. El material se asigna a la superficie activa al terminar.

La creación evita sobrescribir silenciosamente tanto el manifiesto como su imagen complementaria.
Para compartir materiales, copia ambos archivos. Una imagen de albedo sola no es un conjunto
completo de mapas PBR, aunque sí puede renderizarse con los parámetros físicos uniformes.

## Detección automática de mapas PBR

Si la imagen elegida se llama `madera_basecolor.png` o `madera_albedo.png`,
el asistente ahora identifica archivos compatibles de la misma carpeta:

- `madera_normal.png` (también `_normalgl`, `_normal_gl` o `_nrm`)
- `madera_roughness.png` (también `_rough`)
- `madera_metallic.png` (también `_metalness` o `_metal`)
- `madera_height.png` (también `_disp`, `_displacement` o `_bump`)

Los mapas detectados se copian junto al manifiesto `.g3dm` con nombres coherentes,
y se utilizan en el render PBR. El asistente indica cuáles encontró antes de guardar.
Las imágenes que no coinciden exactamente con el nombre del conjunto no se asignan
automáticamente para evitar mezclar materiales distintos.

La asignación a un elemento seleccionado se conserva: crear o importar un `.g3dm`
desde un material individual no cambia necesariamente los demás objetos del mismo tipo.

## Paquetes PBR desde una carpeta

En la pestaña **Render → Materiales**, abre el material de la categoría deseada y elige **Importar material PBR (.g3dm)…**. Un paquete consta de un archivo de texto JSON `.g3dm` y una imagen de color (PNG/JPEG/BMP/TIFF) en la misma carpeta o una subcarpeta.

Ejemplo de `ladrillo.g3dm`:

```json
{
  "version": 1,
  "name": "Ladrillo rojo artesanal",
  "albedo": "brick_albedo.png",
  "normal": "brick_normal.png",
  "roughnessMap": "brick_roughness.png",
  "metallicMap": "brick_metallic.png",
  "heightMap": "brick_height.png",
  "roughness": 0.87,
  "metallic": 0,
  "ior": 1.5,
  "textureSize": 0.4,
  "relief": 0.25
}
```

- `albedo`: textura de color (ruta **relativa al manifiesto**, no URL).
- `normal` (opcional): mapa normal en convención OpenGL.
- `roughnessMap` (opcional): mapa de rugosidad por píxel.
- `metallicMap` (opcional): mapa de metalicidad por píxel.
- `heightMap` (opcional): mapa de altura en escala de grises que perturba las normales de sombreado, sin desplazar la geometría.
- `roughness`: rugosidad constante de 0.02 a 1.
- `metallic`: metalicidad constante de 0 a 1.
- `ior`: índice de refracción de 1 a 3.
- `textureSize`: metros por repetición de la textura; editable luego.
- `relief`: detalle a partir del brillo de la imagen (no es un mapa normal ni desplazamiento geométrico).

El archivo del proyecto guarda la ruta al manifiesto. Para compartir el proyecto, comparte también la carpeta de texturas. Esta versión interpreta **albedo**, mapas opcionales de **normales, rugosidad, metalicidad y altura (heightMap)**, además de parámetros físicos uniformes. La altura afecta al sombreado, no a la silueta ni a la geometría. Todavía no incorpora AO, desplazamiento geométrico ni paquetes ZIP. Los archivos deben permanecer junto al manifiesto, o dentro de sus subcarpetas.

## Materiales sugeridos para probar

1. Bloque de concreto 20×20×40.
2. Ladrillo artesanal rojo.
3. Repello cemento-arena y fino blanco.
4. Cerámica terracota y porcelanato crema.
5. Teja de barro.
6. Acero galvanizado.
7. Plywood y aluminio negro.

Usa recursos descargados con licencias válidas. La aplicación no incluye fotografías de terceros nuevas en este cambio.

## Biblioteca avanzada y diagnóstico (.g3dm)

En **Render → Materiales → Explorar biblioteca de materiales .g3dm**, selecciona una carpeta.
La ventana muestra los materiales disponibles, sus miniaturas de albedo, categoría, licencia,
autor, etiquetas, dimensiones reales y mapas. Puedes buscar y asignar el resultado al elemento
o categoría que estés editando. Los materiales incompletos aparecen con una advertencia, pero no
pueden asignarse hasta corregir sus archivos.

El auditor está en `Guanacaste3D.Core.Rendering.PbrMaterialAudit` y funciona sin WPF en
Windows, Linux y macOS. Puedes usar `Inspect(path)` para revisar un material o `Scan(folder)`
para obtener un catálogo de manifiestos válidos e inválidos. Valida mapas ausentes,
parámetros PBR fuera de rango, rutas inseguras, enlaces simbólicos, archivos demasiado grandes
y nombres JSON ambiguos. Los mapas admitidos en el render son albedo, normal OpenGL,
rugosidad, metalicidad y altura para relieve aparente.

Un manifiesto puede incluir datos opcionales para bibliotecas profesionales:

```json
{
  "version": 1,
  "name": "Concreto pulido gris",
  "category": "Concreto",
  "description": "Piso interior acabado pulido",
  "author": "Equipo de arquitectura",
  "license": "CC0",
  "tags": ["interior", "piso", "gris"],
  "albedo": "concreto-albedo.png",
  "normal": "concreto-normal.png",
  "roughnessMap": "concreto-roughness.png",
  "metallicMap": "concreto-metallic.png",
  "heightMap": "concreto-height.png",
  "roughness": 0.35,
  "metallic": 0,
  "ior": 1.5,
  "textureSize": 1.2,
  "relief": 0.15
}
```

Los paquetes anteriores que usan claves `Name`, `Albedo` y `Version` en mayúsculas
siguen funcionando; no hace falta convertirlos.

### Crear un paquete desde cualquier plataforma

El motor incluye `PbrMaterialPackageWriter.Create`, que copia los canales de imagen
originales sin recomprimirlos, guarda un manifiesto JSON portátil y se niega a sobrescribir
un paquete existente. Ejemplo de uso desde C#:

```csharp
var path = PbrMaterialPackageWriter.Create(
    "/mis-materiales/concreto.g3dm",
    new PbrMaterialPackageWriter.Options(
        "Concreto gris", Category: "Concreto", Roughness: 0.8, TextureSize: 1.5),
    new Dictionary<string, string>
    {
        ["albedo"] = "/fotos/concreto-albedo.png",
        ["normal"] = "/fotos/concreto-normal.png"
    });
```

Esta API no requiere Windows. La creación comprueba existencia, tamaño y extensión de
archivos, pero **no** sustituye la decodificación de imágenes del renderizador.

El campo `heightMap` sí contribuye al sombreado del render como relieve aparente,
pero **no desplaza físicamente la geometría**. Los campos `ao`, `displacementMap`,
`opacityMap` y `emissionMap` se detectan como información futura y generan una advertencia. Evita confundir una textura de color con mapas de alturas reales o prometer
desplazamiento geométrico sin implementarlo.

