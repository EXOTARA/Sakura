# Política de privacidad

*Privacy policy — an English summary follows at the end of this document.*

Sakura es una aplicación de escritorio que se ejecuta en el equipo de quien la usa. No hay servidor,
no hay cuenta, no hay registro y no hay nadie al otro lado. Esta página enumera, sin excepciones,
todo lo que sale del equipo.

Última revisión: 22 de agosto de 2026, para la versión 0.27.0-beta.

## Lo que nunca sale del equipo

- **El audio del micrófono.** La palabra de activación (Vosk) y la transcripción (Whisper) se
  ejecutan en local, contra modelos que están en el disco. No hay reconocimiento de voz en la nube.
- **Las pulsaciones, el portapapeles y lo que se lee de la pantalla.** El OCR y la automatización de
  interfaz son las que trae Windows, y se quedan en el proceso.
- **Los datos de la aplicación**: tareas, rutinas, sesiones de enfoque, memoria, historial de
  conversación y preferencias. Viven en `%LocalAppData%\Sakura` y lo sensible se cifra en reposo con
  DPAPI de Windows, atado a la cuenta de usuario.
- **Cualquier forma de telemetría, analítica, informe de errores o medición de uso.** No existe en el
  código. No hay ninguna, ni anónima ni agregada ni opcional.

## Lo que sale del equipo, y por qué

### 1. Comprobación de actualizaciones — automática

Es el único caso que ocurre **sin que el usuario lo pida en ese momento**. Como máximo una vez cada
24 horas, Sakura consulta la lista de versiones publicadas del repositorio:

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

### 2. Descarga de modelos de voz — cuando el usuario los instala

La primera vez que se activa la voz hay que traer los modelos, y son grandes, así que no van dentro
del instalador:

- El modelo de palabra de activación, de [alphacephei.com](https://alphacephei.com/vosk/models)
  (Vosk, Apache-2.0).
- El modelo de transcripción, de [Hugging Face](https://huggingface.co/) (Whisper `ggml`, MIT).

Se descargan una vez, se quedan en el disco y a partir de ahí todo el reconocimiento es local.

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

Nada de esto está activo por omisión: hay que elegir el proveedor y poner una clave. La clave se lee
de una variable de entorno del usuario; no se guarda en `settings.json` ni viaja a ningún sitio que
no sea el proveedor elegido.

Qué se manda y cuándo:

- El texto de la conversación, cuando se habla con Sakura teniendo un proveedor en la nube activo.
- Una captura de pantalla, **solo** al usar Lens o al compartir una ventana a propósito. Las
  capturas se redactan antes de salir: se tapa lo que el detector reconoce como dato sensible.
- Nunca el audio. La transcripción ya ocurrió en local; lo que viaja es texto.

### 4. Descarga de Ollama — solo si el usuario lo instala desde Sakura

Si se acepta que Sakura instale Ollama, consulta su versión publicada en
`api.github.com/repos/ollama/ollama/releases/latest` y la descarga. Es una acción explícita del
usuario, no ocurre sola.

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
no analytics and no crash reporting. Microphone audio, screen contents, and all application data
(tasks, routines, focus sessions, memory, conversation history, preferences) stay on the machine,
under `%LocalAppData%\Sakura`, encrypted at rest with Windows DPAPI.

Everything that leaves the machine is listed above. In short:

1. **Update check (automatic, at most once every 24 h):** an unauthenticated read of
   `api.github.com/repos/EXOTARA/Sakura/releases`. No installation identifier, no device data.
   Updates are never installed without the user accepting them. Can be disabled in Settings.
2. **Voice model download (on user action):** Vosk models from alphacephei.com, Whisper `ggml`
   models from Hugging Face. Downloaded once, then all recognition is local.
3. **Cloud AI providers (opt-in only):** if the user configures one, conversation text — and, only
   when Lens or window sharing is used, a redacted screenshot — is sent to that provider. Audio is
   never sent. API keys are read from a user environment variable. Providers and their privacy
   policies are listed above. The default local option is Ollama on `127.0.0.1`, which sends
   nothing anywhere.
4. **Ollama installation (on user action):** version lookup and download from GitHub.

No advertising, no profiling, no sale or sharing of user data.
