# Ficha de Sakura Assistant en Microsoft Store

Textos listos para copiar en Partner Center, sección por sección. Todo lo que afirman está comprobado
contra el código o contra las políticas publicadas de la Store (ver `MICROSOFT_STORE.md`). Si algo de
Sakura cambia, esta ficha cambia con ello: la política 10.1.1 exige que la ficha describa la app tal
como es.

---

## Pricing and availability

- **Markets:** todos los mercados.
- **Visibility:** Public audience.
- **Pricing:** Free.
- **Free trial:** No free trial.

## Properties

- **Category:** Productivity.
- **Subcategory:** ninguna.
- **Privacy policy URL:** `https://exotara.github.io/Sakura/privacidad/`
- **Website:** `https://exotara.github.io/Sakura/`
- **Support contact info:** `https://github.com/EXOTARA/Sakura/issues`
- **Product declarations:** no marcar ninguna salvo que la pantalla pregunte algo que aplique.
- **System requirements:** Input: Keyboard y Mouse. Microphone: opcional. No declarar memoria mínima
  sin haberla medido: depende del modelo de IA que elija cada persona.

## Age ratings (cuestionario IARC)

Responder con la verdad. Lo relevante de Sakura:

- **No** tiene violencia, sexo, lenguaje soez, drogas, apuestas ni compras.
- **No** permite que usuarios hablen entre sí ni compartan contenido con otros usuarios.
- **Sí** puede mostrar **contenido generado por IA** si la persona configura una IA. Declararlo.
- **No** comparte la ubicación.
- Acceso a internet: sí, sin restricciones (proveedores de IA opcionales, descarga de modelos).

## Store listings — Español (México)

### Nombre

Sakura Assistant

### Descripción

> **Requiere Ollama para la IA local** (gratuito, de otros autores, se descarga en ollama.com) **o una
> clave de un proveedor de IA en la nube**. Sin ninguno de los dos, Sakura funciona como asistente de
> órdenes, tareas, enfoque y dictado, sin respuestas de IA.
>
> Sakura usa **IA generativa**: sus respuestas pueden ser incorrectas. Cualquier respuesta se puede
> reportar desde su menú.

Sakura es un asistente que vive en la bandeja del sistema y aparece cuando lo llamas. Entiende órdenes
normales —«abre la calculadora», «pon un temporizador de veinte minutos», «¿qué ventana tengo
abierta?»— y usa un modelo de lenguaje solo cuando hace falta.

Funciona en tu equipo. La palabra de activación y el dictado se procesan en local: el audio del
micrófono no sale de tu computadora. No tiene cuenta, ni publicidad, ni telemetría.

Cada capacidad tiene su permiso —bloqueado, preguntar o permitido— y hay acciones que te pregunta
siempre, aunque las tengas permitidas.

Es software libre y de código abierto, bajo licencia MIT.

### Novedades de esta versión

Primera versión en Microsoft Store.

### Características (una por línea, máximo 20)

- Palabra de activación «Oye Sakura» y dictado procesados en tu equipo
- Dictado en cualquier aplicación con Ctrl + Shift + D
- Lens: explica la ventana que tienes delante, tapando lo que parezca sensible
- Traductor de un trozo de pantalla con Ctrl + Shift + T
- Tareas, sesiones de enfoque y rutinas
- Permisos por capacidad: bloqueado, preguntar o permitido
- Memoria personal opcional, apagada por defecto y cifrada
- Funciona con Ollama en local o con un proveedor de IA en la nube que elijas
- Sin cuenta, sin publicidad y sin telemetría
- Código abierto bajo licencia MIT

### Palabras de búsqueda (máximo 7)

asistente, voz, dictado, productividad, ia local, automatización, enfoque

### Capturas de pantalla

Mínimo una, PNG de al menos 1366 × 768. Se generan con perfil de prueba, sin datos personales.

### Aviso de copyright y marcas

© EXOTARA. Software libre bajo licencia MIT.

### Términos de licencia adicionales

`https://exotara.github.io/Sakura/terminos/`

---

## Packages

Subir el `.msix` del artefacto `sakura-msix-<versión>` del flujo de publicación de GitHub Actions.

- **Device family availability:** solo **Windows 10/11 Desktop**.

## Submission options — Notes for certification

Pegar tal cual (en inglés, que es lo que leen los revisores):

```
Sakura Assistant is an open source (MIT) desktop assistant for Windows, built with .NET 10 / WPF and
packaged as a full-trust MSIX. Source: https://github.com/EXOTARA/Sakura

HOW TO TEST
- No account or login is needed.
- On first launch a 4-step setup appears; every step can be skipped ("Omitir").
- Press Alt + A to open the main window. Try typed commands that need no AI: "abre la calculadora",
  "pon un temporizador de 5 minutos", "¿qué ventana tengo abierta?".
- AI answers need either Ollama running locally (https://ollama.com, a third-party free app) or an API
  key for a cloud provider configured in Personalizar > Inteligencia artificial. Without either, the
  app still works as a command, task, focus and dictation assistant. This dependency is disclosed at
  the start of the description (policy 10.2.4).

CAPABILITIES
- runFullTrust: standard WPF desktop application.
- microphone: optional wake word ("Oye Sakura") and dictation, processed on the device. Audio is never
  sent anywhere. The wake word is off until the user enables it; dictation only records while the
  user has explicitly started it (Ctrl + Shift + D) and stops when pressed again.

POLICY NOTES
- 10.2.3: the Store build does NOT download or install Ollama; it only links to its website.
- Updates: the Store build has its own updater disabled; updates come only from the Store.
- 10.2.8: Sakura only changes Windows settings through documented APIs (e.g. PowerSetActiveScheme) and
  only after explicit user confirmation, with an undo. UI Automation is used to read the active window
  when the user asks, and to act on other apps only at the user's explicit request, gated by a
  per-capability permission that is "blocked" by default, with a confirmation before acting.
- 10.8.3: cloud AI API keys are optional; the primary functionality does not require them.
- 11.16 (live generative AI): AI answers can be reported from each answer's context menu ("Reportar
  esta respuesta") and from the command palette ("Reportar una respuesta de IA"), which open the
  project's public issue form.
- Privacy policy: https://exotara.github.io/Sakura/privacidad/ — no telemetry, no analytics, no account.
```
