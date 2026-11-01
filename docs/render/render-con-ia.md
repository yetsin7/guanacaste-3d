# Render con IA (sin API) — Guanacaste 3D

Render con IA es un flujo **manual, privado y neutral respecto al proveedor**. No llama a ChatGPT, Gemini ni a ningún servicio. Guanacaste 3D prepara un mensaje en Markdown y una imagen PNG de referencia; el usuario los copia a un generador externo de imágenes y, si quiere, importa la imagen resultante al proyecto.

## Uso

1. Abre o crea un proyecto, entra al espacio **Render** y espera a que haya una imagen en la vista previa (también sirve un render final).
2. Utiliza el botón flotante **Copiar vista para IA** para copiar **los píxeles del render mostrado**, no una captura de toda la interfaz.
3. Abre **Render IA** desde la barra vertical exclusiva de Render.
4. Selecciona el estilo (fotorrealista, maqueta, interior, nocturno, atardecer, acuarela o conceptual). Marca solo las mejoras que quieras: materiales, luz, sombras, árboles, vegetación, mobiliario, personas, cielo y conservación geométrica. También hay instrucciones adicionales.
5. Pulsa **Preparar paquete**. Se crean automáticamente el Markdown y la imagen base dentro de un directorio de sesión. Copia el prompt y la referencia con los botones correspondientes y pégalos en tu IA externa. No es necesario contratar una API en Guanacaste 3D.
6. Guarda el resultado de la IA en PNG/JPEG, selecciona la sesión en el baúl, pulsa **Importar resultado de IA** y elige el archivo.
7. **Abrir baúl** abre la carpeta con todas las sesiones del proyecto, sus referencias, prompts y resultados, sin sobrescribir los anteriores.

## Organización en disco

Al trabajar con un proyecto guardado, por ejemplo:

    Casa.g3d
    Casa.g3d.renders/
      20261101_150500_d1e2f3a4/
        prompt.md
        referencia.png
        metadata.json
        resultado-20261101_151000-c3d4e5f6.png
        resultado-20261101_152000-a7b8c9d0.jpg

Cada intento tiene su **propio** prompt y referencia. La carpeta *.renders es un archivo auxiliar al lado del .g3d y no altera el formato de los modelos BIM. En proyectos aún sin guardar, los paquetes se almacenan en el baúl local de borradores del usuario, dentro de LocalApplicationData/Guanacaste3D/RenderBaul/Borradores.

Cuando se selecciona una sesión anterior, el programa vuelve a mostrar **su imagen de referencia original**; por ello, copiar esa imagen junto al prompt histórico no mezcla versiones de cámaras distintas.

## Restricciones y buenas prácticas

- La fuente debe ser la imagen de render actualmente visible; si todavía no hay píxeles, el programa solicita activar la vista previa.
- No se exige resultado de IA ni cuenta en ningún proveedor externo; la calidad final depende del modelo y del servicio elegidos.
- Los prompts favorecen la geometría original y la cámara de la referencia. Las casillas que no se activan no añaden artificialmente objetos nuevos.
- Importación: PNG/JPEG con extensión y firma compatibles, hasta 100 MiB. Se almacenan múltiples versiones sin borrar el prompt de origen.
- **Privacidad:** copiar la imagen al portapapeles no la envía por internet. Subirla a una IA externa sí está sujeto a las políticas y condiciones del proveedor que el usuario elija.
- **Guardar como:** al cambiar el nombre o la carpeta del proyecto, los archivos de render de la ruta anterior no se mueven automáticamente. Conserva la carpeta .renders antigua o cópiala al nuevo nombre de proyecto si deseas trasladar también el historial.
- El núcleo del generador de prompts y el baúl usa .NET 10 y es independiente de WPF. La ventana de escritorio actual es WPF y solo funciona en Windows; futuros clientes de macOS y Linux podrán utilizar la lógica del núcleo con su propia interfaz.
