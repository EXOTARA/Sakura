# Política de privacidad

*Privacy policy — an English summary follows at the end of this document.*

Sakura es una aplicación de escritorio que se ejecuta en el equipo de quien la usa. No hay servidor,
no hay cuenta, no hay registro y no hay nadie al otro lado. Esta página enumera, sin excepciones,
todo lo que sale del equipo.

La política de la página de descargas, junto con un resumen de esta, está en
[exotara.github.io/Sakura/privacidad/](https://exotara.github.io/Sakura/privacidad/). Si una cambia,
la otra cambia en el mismo commit.

Última revisión: 13 de septiembre de 2026, para la versión 0.29.1-beta.

## Lo que nunca sale del equipo

- **El audio del micrófono.** La palabra de activación (Vosk) y la transcripción (Whisper) se
  ejecutan en local, contra modelos que están en el disco. No hay reconocimiento de voz en la nube.
- **El portapapeles y lo que se lee de la pantalla**, salvo lo que el usuario mande a un proveedor
  en la nube (apartado 3). El OCR y la automatización de interfaz son las que trae Windows, y se
  quedan en el proceso.
- **Los datos de la aplicación**: tareas, rutinas, sesiones de enfoque, historial de conversación,
  copias previas de los archivos de un proyecto y preferencias. Viven en `%LocalAppData%\Sakura`
  como archivos normales de la cuenta de usuario. **Solo la memoria personal y las claves de los
  proveedores de IA se cifran además con DPAPI** de Windows, atado a la cuenta. *(Hasta el 13 de
  septiembre de 2026 esta línea decía que se cifraba «lo sensible» de todo lo anterior; no era
  exacto.)*
- **Las capturas que guardas y las grabaciones de pantalla.** Una captura guardada va a
  `Imágenes\Sakura` y una grabación a `Vídeos\Sakura`, como archivos normales. La grabación incluye
  el sonido del equipo y, solo si se marca, el del micrófono; se codifica en el propio equipo y
  Sakura no la envía a ningún sitio.
- **Los documentos y las conversaciones que guardas.** Un documento guardado con «Guardar como» va al
  **escritorio**, con el nombre del primer apartado de la respuesta, y una conversación exportada va a
  `Documentos\Sakura\Conversaciones`; los dos como archivos normales. Sakura no sobrescribe ninguno:
  si el nombre está ocupado, numera.
- **Cualquier forma de telemetría, analítica, informe de errores o medición de uso.** No existe en el
  código. No hay ninguna, ni anónima ni agregada ni opcional.

## Lo que sale del equipo, y por qué

### 1. Comprobación de actualizaciones — automática, solo en la versión de GitHub

Ocurre **sin que el usuario lo pida en ese momento**. La versión de Microsoft Store no la hace: la actualiza la Store. Como máximo una vez cada
24 horas, la versión de GitHub consulta la lista de versiones publicadas del repositorio:

```
https://api.github.com/repos/EXOTARA/Sakura/releases
```

Es una petición de lectura, sin autenticación. Se envía lo que cualquier petición HTTPS envía —
dirección IP y agente de usuario— y nada más: ni identificador de instalación, ni datos del equipo,
ni nada de lo que haya dentro de la aplicación. Nunca se instala nada solo: Sakura avisa, enseña la
versión, las notas y el hash, y el usuario decide.

La descarga del paquete de actualización, si se acepta, se hace desde `github.com`. Rigen entonces
la [declaración de privacidad de GitHub](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).

Quien no quiera ni eso puede desactivar la comprobación en Ajustes.

### 2. Descarga de modelos de voz

Son grandes, así que no van dentro del instalador:

- El modelo de transcripción, de [Hugging Face](https://huggingface.co/) (Whisper `ggml` base, MIT,
  unos 140 MB). **Se descarga la primera vez que se usa la voz**: el micrófono, Alt + V, «Oye Sakura»
  o el dictado (`PrepareVoiceAsync`). En 0.30.0 se descargaba al arrancar aunque no se usara la voz;
  desde 0.30.1, al arrancar solo se carga si ya está en el disco.
- El modelo de palabra de activación, de [alphacephei.com](https://alphacephei.com/vosk/models)
  (Vosk, Apache-2.0). Se descarga cuando el usuario activa o prueba «Oye Sakura».

Se descargan una vez, se quedan en el disco y a partir de ahí todo el reconocimiento es local. La
petición no lleva nada del usuario: es la descarga de un archivo público.

Los modelos de lenguaje de Ollama que el usuario elija descargar desde Sakura los trae el propio
Ollama desde su registro; Sakura solo se lo pide.

### 3. Proveedores de IA — solo si el usuario configura uno

Sakura funciona contra un [Ollama](https://ollama.com/) local (`127.0.0.1`), y en ese caso nada sale
del equipo. También puede conectarse a un proveedor en la nube, y entonces **lo que se le mande sale
del equipo**, que es lo que significa usar un modelo remoto:

| Proveedor | Política de privacidad |
|---|---|
| Anthropic | https://www.anthropic.com/legal/privacy |
| OpenAI | https://openai.com/policies/privacy-policy |
| Google (Gemini) | https://policies.google.com/privacy |
| Groq | https://groq.com/privacy-policy/ |
| OpenRouter | https://openrouter.ai/privacy |

Nada de esto está activo por omisión: hay que elegir el proveedor y poner una clave. La clave se
guarda cifrada con DPAPI en un archivo propio, aparte de `settings.json`, o se lee de una variable
de entorno del usuario si se prefiere; no viaja a ningún sitio que no sea el proveedor elegido.

Qué se manda y cuándo:

- El texto de la conversación, cuando se habla con Sakura teniendo un proveedor en la nube activo.
- Una captura de pantalla, **solo** al usar Lens, al compartir una ventana a propósito, al elegir
  «Preguntar a Sakura» con una captura o al pulsar `Ctrl + Shift + Espacio` para que explique la
  ventana activa. Las capturas se redactan antes de salir: se tapa lo que el detector reconoce como
  dato sensible. Con `Ctrl + Shift + Espacio` también viajan el texto leído de esa ventana y los
  nombres de sus botones y menús, redactados igual; si el modelo no admite imágenes, solo ese texto.
- El texto leído de un recuadro de la pantalla, **solo** al usar el traductor
  (`Ctrl + Shift + T`), después de pasar por el mismo redactor.
- El texto que el usuario tenga seleccionado en otra aplicación, **solo** al pulsar
  el atajo de texto seleccionado (`Alt + Shift + E` por omisión) o usar «Texto seleccionado» en la
  paleta, y elegir una acción (reescribir, corregir, resumir o traducir). Se envía tal
  cual, sin redactar, porque el resultado puede sustituir a la selección. Sakura no lee texto de
  ventanas marcadas como sensibles.
- Nunca el audio. La transcripción ya ocurrió en local; lo que viaja es texto.

### 4. Imágenes de las presentaciones — solo si se autoriza

Cuando una respuesta pide una imagen para una diapositiva («Imagen: …») y se guarda como
PowerPoint, Sakura puede buscarla en Wikimedia Commons, donde las imágenes son de licencia libre.
Llega desactivado: la primera vez Sakura pregunta, y la respuesta se guarda en Ajustes, donde se
cambia cuando se quiera.

Si está activado, lo único que sale del equipo es el término de búsqueda (por ejemplo «turbinas
eólicas en el mar»); nunca la respuesta, el documento ni ningún dato del equipo. La presentación
incluye el autor y la licencia de cada imagen, como piden esas licencias.

### 5. Fuentes de los borradores — solo si se autoriza

Al guardar un borrador como Word o PowerPoint, Sakura puede buscar fuentes reales sobre su tema en
dos catálogos académicos públicos: [OpenAlex](https://openalex.org/) y
[Crossref](https://www.crossref.org/). No hacen falta cuenta ni clave. Llega desactivado y se
pregunta la primera vez, junto con lo de las imágenes; la respuesta se guarda en Ajustes.

Si está activado, lo único que sale del equipo es el tema a buscar —normalmente el título del
documento—; nunca el contenido del borrador. Las referencias se escriben con los datos que devuelven
esos catálogos, sin inventar ninguna.

### 6. Descarga de Ollama — solo si el usuario lo instala desde Sakura

Si se acepta que Sakura instale Ollama, consulta su versión publicada en
`api.github.com/repos/ollama/ollama/releases/latest` y la descarga. Es una acción explícita del
usuario, no ocurre sola.

### 7. Órdenes de red que pida el usuario

Algunas órdenes del equipo usan la red por definición. «Comprobar si hay conexión» ejecuta
`ping -n 4 1.1.1.1`. Solo se ejecutan cuando se piden y con el permiso de «Actuar sobre el equipo».

## Menores, publicidad y venta de datos

No hay publicidad. No hay perfilado. No hay venta ni cesión de datos a nadie, porque no hay datos
que ceder: no se recoge ninguno.

## Cambios

Esta página vive en el repositorio y su historial es el historial de git. Cualquier cambio en lo que
sale del equipo se refleja aquí en el mismo cambio que lo introduce.

## Contacto

[github.com/EXOTARA/Sakura/issues](https://github.com/EXOTARA/Sakura/issues)

---

## Privacy policy (English summary)

Sakura is a local-first Windows desktop application. There is no server, no account, no telemetry,
no analytics and no crash reporting. Microphone audio, screen contents (unless sent to a cloud
provider by the user) and all application data (tasks, routines, focus sessions, memory,
conversation history, preferences) stay on the machine, under `%LocalAppData%\Sakura`. Only personal
memory and AI provider keys are additionally encrypted at rest with Windows DPAPI; the rest are
ordinary files in the user's profile.

Everything that leaves the machine is listed above. In short:

1. **Update check (automatic, at most once every 24 h, GitHub copy only):** an unauthenticated read of
   `api.github.com/repos/EXOTARA/Sakura/releases`. No installation identifier, no device data.
   Updates are never installed without the user accepting them. Can be disabled in Settings. The
   Microsoft Store version does not check; the Store updates it.
2. **Voice model download:** the Whisper `ggml` base model from Hugging Face (about 140 MB) is
   downloaded the first time voice is used (on first launch in 0.30.0); the Vosk wake word model from alphacephei.com when the
   user turns on or tests the wake word. Downloaded once, then all recognition is local.
3. **Cloud AI providers (opt-in only):** if the user configures one, conversation text — and, only
   when Lens or window sharing is used, a redacted screenshot, or with the screen translator the
   redacted text — is sent to that provider. Audio is never sent. API keys are stored encrypted with
   DPAPI or read from a user environment variable. Providers and their privacy
   policies are listed above. The default local option is Ollama on `127.0.0.1`, which sends
   nothing anywhere.
4. **Presentation and draft lookups (opt-in only):** when an answer asks for a slide image and it is saved as a
   PowerPoint file, Sakura can look it up on Wikimedia Commons. Sakura asks the first time; only the
   search term leaves the machine, and the deck credits each image's author and licence.
   Sakura can also look up real sources for a draft on OpenAlex and Crossref; only the topic leaves
   the machine, and every reference is written from what those catalogues return.
5. **Ollama installation (on user action):** version lookup and download from GitHub.

No advertising, no profiling, no sale or sharing of user data.
