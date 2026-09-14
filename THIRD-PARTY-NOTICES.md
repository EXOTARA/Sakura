# Componentes de terceros

Sakura se distribuye bajo la licencia MIT (ver `LICENSE`). Esa licencia cubre el código de este
repositorio. Lo que viene de fuera se lista aquí con su licencia propia, que es la que manda sobre
cada uno.

Las licencias que se aceptan en este proyecto son MIT, Apache 2.0 y BSD. Cualquier dependencia con
restricción de uso no comercial queda descartada por decisión de producto, no por casualidad.

## Bibliotecas incluidas en la aplicación

| Componente | Versión | Licencia | Para qué se usa |
|---|---|---|---|
| [Vosk](https://alphacephei.com/vosk/) | 0.3.38 | Apache-2.0 | Reconocimiento de la palabra de activación, en local |
| [Whisper.net](https://github.com/sandrohanea/whisper.net) | 1.9.1 | MIT | Transcripción de voz, en local |
| Whisper.net.Runtime | 1.9.1 | MIT | Binarios nativos de whisper.cpp que usa lo anterior |
| [NAudio](https://github.com/naudio/NAudio) | 2.3.0 | MIT | Captura de micrófono, mezclador de audio y visualizador |
| [WpfAnimatedGif](https://github.com/XamlAnimatedGif/WpfAnimatedGif) | 2.0.2 | Apache-2.0 | Reproducir un GIF animado en el hueco del panel |
| System.Speech | 10.0.10 | MIT | Voz sintetizada del sistema |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | MIT | Contenedor de servicios |

## Modelos de voz

Los modelos **no se redistribuyen** con Sakura: se descargan del sitio de su autor la primera vez
que hacen falta, y se guardan en la carpeta de datos del usuario.

| Modelo | Origen | Licencia |
|---|---|---|
| `vosk-model-small-es-0.42` | alphacephei.com | Apache-2.0 |
| `ggml-base` (Whisper) | ggerganov/whisper.cpp | MIT |

## Página de descargas

La página de `site/` no carga nada de terceros: ni tipografías, ni guiones, ni imágenes externas.

| Componente | Licencia | Para qué se usa |
|---|---|---|
| Iconos de [Lucide](https://lucide.dev/), algunos derivados de [Feather](https://feathericons.com/) | ISC (Lucide) y MIT (Feather) | Iconos de la página, escritos a mano en SVG siguiendo su diseño |

Los avisos completos de las dos licencias se publican en la propia página, en los términos de uso
(apartado «Contenido de la página y créditos»), porque las dos piden conservarlos junto a la copia.

## Herramientas que no se distribuyen

Estas hacen falta para construir o publicar, y no forman parte de lo que se instala:

- [Inno Setup 6](https://jrsoftware.org/isinfo.php) — genera el instalador. Licencia propia de Inno
  Setup, gratuita también para uso comercial.
- SDK de .NET 10 — MIT.
