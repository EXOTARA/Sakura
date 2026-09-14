# Accesibilidad de ventanas secundarias — 2026-09-13

Trabajo aislado en `codex/accesibilidad-ventanas`, desde `origin/main` (`0ddef3c`).
No se modifican las rutas reservadas al otro agente. Los cambios de `Colors.xaml` y
`Controls.xaml` son compartidos: mejoran contraste también fuera de estas ventanas.

## Resultado por ventana

| Ventana | Problema encontrado | Cambio | Pendiente con la aplicación y Narrador |
| --- | --- | --- | --- |
| AmbientHistoryWindow | Lista sin nombre; filas con nombre del tipo; foco inicial implícito; glifo de cerrar expuesto. | Lista y desplazamiento nombrados, `HistoryRow.ToString()` devuelve la solicitud, foco en Cerrar al abrir/reabrir, glifo decorativo. | Lectura cronológica de un historial largo y acción Deshacer con datos reales. |
| AnswerPillWindow | Enlace de respuesta completa implementado como texto con evento de ratón; `NOACTIVATE`; cierre por temporizador durante lectura. | Botón accesible con Enter/Espacio, acceso explícito al foco, suspensión del descarte mientras contiene el foco; marca decorativa. | Entrada por atajo, lectura de respuesta progresiva, retorno al programa anterior y comparación visual del enlace. |
| CapsuleWindow | Ventana no enfocable; marca y glifo redundantes; duración limitada para leer. | Nombre accesible, entrada explícita al foco, Escape, temporizador respetuoso con el foco; decoración sin peer. | Orden y voz de los avisos, combinación con nuevos mensajes mientras se lee. |
| CommandCenterWindow | Resultados no enfocables; `CommandCenterRow` volcaba sus campos. | Lista enfocable y filas tituladas; flechas nativas cuando el foco está en la lista, navegación circular existente desde búsqueda; separadores decorativos. | Pronunciación de resultados y retorno del foco entre aplicaciones. Las pruebas WPF heredadas siguen pasando. |
| CommandPaletteWindow | Tab se consumía para autocompletar, botones/lista sin nombre explícito; Enter interceptaba los botones de ajustes; sugerencias con volcado del record. | Tab recorre, Ctrl+Tab completa desde búsqueda; Enter respeta los ajustes y ejecuta la fila enfocada; nombres accesibles, `ToString()` y glifos decorativos. | Recorrido completo con ajustes abiertos, lectura de sugerencias al escribir y combinaciones con IME. |
| DashboardWindow | Se cerraba al alejar el puntero aunque se usara el teclado; foco inicial sin destino explícito. | No se cierra por puntero mientras tiene foco de teclado; foco en el primer control al abrir. | Apertura mediante el shell, cambio de pestañas y permanencia durante lectura real. |
| Views/DashboardView | Listas sin nombre; filas de voz anónimas y calendario con volcado del record; blanco sobre rosa para el día actual; símbolos decorativos. | Nombres de listas, filas de voz con etiqueta y valor, fechas completas con «hoy», tinta oscura para hoy; glifos ocultos; etiqueta del botón de imagen contiene su texto visible. | Lectura de calendario y métricas actualizadas, estados de reproducción y cambio de imagen/GIF. |
| DiagnosticsWindow | Botones y lista sin nombre explícito; filas volcaban el record; Escape y foco implícitos. | Nombres, `ToString()` de diagnóstico, Cerrar como cancelación y foco inicial; desplazamiento nombrado. | Actualización real, copiado, limpieza y anuncios de resultado. No se ejecutan esas acciones durante la auditoría. |
| ModelManagerWindow | Botones/lista sin nombre; modelos volcaban campos; Escape y foco implícitos. | Nombres, `OllamaModelInfo.ToString()`, Cerrar con Escape y foco en el nombre del modelo. | Estados de descarga, selección tras carga, eliminación y cancelación durante operaciones. |
| PeekWindow | Aviso sin vía explícita al foco y con descarte fijo. | Nombre, entrada por teclado, Escape y pausa del descarte durante lectura; marca decorativa. | Lectura de CPU/memoria y retorno de foco. |
| QuickControlsWindow | Barras `Border` sin teclado ni patrón accesible; ventana `NOACTIVATE`; descarte fijo. | `AccessibleLevelTrack`: nombre Volumen/Brillo, rango 0–100, flechas de 5, RePág/AvPág de 10, Inicio/Fin; misma ruta de escritura que el ratón, indicador de foco; acceso explícito y temporizador respetuoso. | Registro del atajo, indicador de foco en el panel real y respuesta de audio/DDC/CI. **Abrir el panel desde cero sin gesto de borde sigue dependiendo del shell**, fuera de estas ventanas; el nuevo atajo entra cuando está visible. |
| RegionPickerWindow | La región solo podía dibujarse arrastrando. | Flechas mueven la región inicial central; Mayús+flechas cambia tamaño; Ctrl permite pasos de 1; Enter confirma por la misma conversión DPI del ratón; Escape cancela; dimensiones accesibles y ayuda. | Selección y captura efectiva en varios monitores/DPI, lectura de coordenadas con Narrador y alternancia ratón/teclado. No describe el contenido de la zona. |
| SakuraPillWindow | Botón de expandir con nombre que no contenía Expandir/Contraer; cierre siempre anunciado como Cerrar aunque cancelaba; falta de foco; descarte fijo. | Nombres dinámicos, entrada explícita, Escape usa la cancelación/cierre existente, temporizador respetuoso; texto desplazable nombrado; decoración oculta. | Respuesta larga, acciones rápidas dinámicas, cancelación, deshacer y anuncios durante streaming. |
| VisionPreviewWindow | Botones e imagen sin nombre accesible, sin Escape ni foco seguro explícito. | Imagen identificada por origen, botones nombrados, Escape descarta y foco inicial en Descartar; visor desplazable nombrado. | Lectura del origen y zoom/desplazamiento con captura real. El nombre no sustituye una descripción del contenido de la imagen. |
| VisionTargetPickerWindow | Lista/botones sin nombre; destinos con volcado del record; aceptación y cancelación dependían de controles sin atajos declarados. | Nombres, `VisionCaptureTarget.ToString()`, foco en lista, Enter captura y Escape cancela; icono decorativo. | Selección real entre ventanas y monitores y retorno del foco al cerrar el diálogo. |

## Entrada en avisos sin robar el foco

Mientras la ventana correspondiente está visible:

- Ctrl+Alt+F7: aviso Capsule.
- Ctrl+Alt+F8: respuesta AnswerPill.
- Ctrl+Alt+F9: volumen/brillo QuickControls.
- Ctrl+Alt+F10: solicitud SakuraPill.
- Ctrl+Alt+F11: resumen Peek.

`PopupKeyboardAccess` registra el atajo al mostrar y lo libera al ocultar/cerrar. Solo al pedir
entrada retira `NOACTIVATE`, activa la ventana y mueve el foco al primer control; al ocultarse
restaura esa protección. Un conflicto de registro se informa en la ayuda accesible de la ventana;
no se reemplaza el atajo de otra aplicación. **Estos atajos todavía requieren verificación manual**
y no abren por sí mismos ventanas ocultas.

Los cuatro visores con contenido desplazable conservan una parada de teclado **con nombre**:
historial, diagnóstico, respuesta ambiental y vista previa. Quitársela impediría usar RePág/AvPág
cuando solo hay texto o imagen y ningún botón dentro. No son las paradas vacías de las vistas
Settings/System revisadas anteriormente.

## Contraste

Medido sobre los valores de `Themes/Colors.xaml`, sin personalización del acento:

- Texto terciario sobre `SurfaceHover`: **3,49:1 → 5,47:1** (`#747986 → #969BA8`).
- Texto seleccionado del ComboBox: **14,25:1** sobre `Input`. La plantilla existente ya fijaba
  `TextElement.Foreground`; no se reemplaza. Se prueba el `TextBlock` generado por WPF y se renderiza.
- Blanco sobre acento rosa: **2,86:1**. Tinta `BrushBackground` sobre el mismo acento: **6,74:1**.
  Se aplica a botones primarios y al día actual. La pulsación del botón ya no baja a opacidad 0,68.

La prueba comprueba los tres colores de texto sobre seis superficies oscuras. No certifica todos
los temas personalizados, transparencias, estados animados ni el alto contraste de Windows.

## Verificación

- `dotnet build Nexo.slnx -c Debug`: 0 errores, 0 avisos.
- `dotnet test Nexo.slnx`: **2.221 correctas**, 0 fallos, 0 omitidas (Core 1.713, Windows 301, App 207).
- `dotnet build Nexo.slnx -c Release`: 0 errores, 0 avisos.
- Cinco pruebas nuevas con `StaWpfFixture`: carga/maquetación de nueve ventanas y nombres de botones;
  filas humanas y decoración sin peer; rango UIA y teclas del mando; límites de región; contraste
  y plantilla real del ComboBox. Las pruebas existentes del centro de comandos siguen pasando.
- Render del ComboBox inspeccionado: texto seleccionado visible, sin fondo claro inesperado.
- Evidencia local no versionada: `artifacts/a11y/*.log` y
  `tests/Nexo.App.Tests/bin/Debug/net10.0-windows10.0.26100.0/a11y-evidence/combo-selected.png`.

No se afirma haber escuchado Narrador ni completado una validación de todas las ventanas en la
aplicación instalada. No se publica ni se reemplaza el ejecutable del usuario. Quedan explícitas
arriba las comprobaciones de interacción real pendientes.
