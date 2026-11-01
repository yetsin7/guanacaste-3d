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
- `roughness`: rugosidad constante de 0.02 a 1.
- `metallic`: metalicidad constante de 0 a 1.
- `ior`: índice de refracción de 1 a 3.
- `textureSize`: metros por repetición de la textura; editable luego.
- `relief`: detalle a partir del brillo de la imagen (no es un mapa normal ni desplazamiento geométrico).

El archivo del proyecto guarda la ruta al manifiesto. Para compartir el proyecto, comparte también la carpeta de texturas. Esta versión interpreta **albedo**, mapas opcionales de **normales, rugosidad y metalicidad**, además de parámetros físicos uniformes. Todavía no incorpora AO, displacement ni paquetes ZIP. Los archivos deben permanecer junto al manifiesto, o dentro de sus subcarpetas.

## Materiales sugeridos para probar

1. Bloque de concreto 20×20×40.
2. Ladrillo artesanal rojo.
3. Repello cemento-arena y fino blanco.
4. Cerámica terracota y porcelanato crema.
5. Teja de barro.
6. Acero galvanizado.
7. Plywood y aluminio negro.

Usa recursos descargados con licencias válidas. La aplicación no incluye fotografías de terceros nuevas en este cambio.
