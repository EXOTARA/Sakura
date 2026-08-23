# Guía de capacidades de Sakura

> **Tu Windows, en flor.**
> Estado de este documento: describe la rama `design/kohana-sprints-d7-d9`, HEAD `67fcc47`.
> El ejecutable sigue reportando `0.9.5-beta` porque nada de esto se ha integrado todavía a
> `release/kohana-1.0-rc` ni a `main` — ver «Qué falta antes de usarlo en serio» al final.

Este documento reúne **todo lo que Sakura sabe hacer hoy**, cómo funciona por dentro en una frase,
dónde se activa y un ejemplo real de uso. Es la foto completa; para el detalle de diseño de cada
pieza, `docs/stable-release/IMPLEMENTATION_LOG.md` tiene el porqué de cada decisión.

---

## Cómo hablarle a Sakura

Todo lo de este documento se pide de dos formas, indistintamente:

- **Escribiendo o dictando en el chat de Asistente**, en lenguaje normal.
- **Con la paleta de comandos** (`Ctrl + Espacio`), buscando por nombre.

Los nombres entre comillas (`"algo así"`) son los títulos exactos que aparecen en la paleta.

---

## 1 · La base: shell, tareas, enfoque y rutinas

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Navegación** | Nueve secciones (Inicio, Asistente, Tareas, Enfoque, Automatizaciones, Audio, Captura, Sistema, Personalizar) en una barra lateral que se puede plegar. | Siempre disponible. `Alt + A` abre/cierra la ventana; `"Alternar barra lateral"` la contrae. | — |
| **Tareas** | CRUD local con prioridad y fecha, sin servidor. | Sección **Tareas**, o pídelo por chat. | «recuérdame comprar pan mañana» → crea una tarea, no un recuerdo (ver más abajo la diferencia). |
| **Enfoque** | Temporizador de sesiones con historial, asociable a una tarea. | Sección **Enfoque**, o `"Iniciar enfoque · 25 min"`. | «empieza una sesión de enfoque de 45 minutos» |
| **Rutinas** | Secuencias de acciones locales (abrir apps, silenciar audio, iniciar enfoque) guardadas con nombre. | Sección **Automatizaciones**. | «ejecuta mi rutina de trabajo» |
| **Vista rápida (Peek)** | Panel flotante con CPU/RAM/GPU/disco sin abrir la ventana. | `Alt + Shift + A`. Se configura en Personalizar. | — |
| **Métricas del equipo** | CPU, RAM, GPU, VRAM, disco y proceso con mayor consumo, en vivo. | Sección **Sistema**. | «¿cuánta RAM estoy usando?» |
| **Resource Governor** | Baja la actividad de Sakura durante juegos o carga alta, para no competir por recursos. | Personalizar → Resource Governor. | — |

---

## 2 · Voz

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Wake word local** | Vosk detecta `Oye Sakura` / `Sakura` / `Hey Sakura` sin enviar audio a ningún lado. | Personalizar → Voz. Sensibilidad ajustable. | Di «Oye Sakura, ¿qué hora es?» |
| **Push-to-talk** | Whisper local transcribe lo que dictas. | Botón de micrófono en Asistente. | — |
| **Traducir un trozo de pantalla** | Arrastras un recuadro sobre cualquier texto —un menú de un juego, un error dentro de una imagen, un PDF que no deja seleccionar— y sale traducido. Usa el modelo que ya tengas configurado; con Ollama no sale nada del equipo. Lo que el redactor tapa por sensible sigue tapado. | Atajo global `Ctrl + Shift + T`. | Pulsa `Ctrl+Shift+T`, arrastra sobre un mensaje en inglés. |
| **Escucha por atajo** | Empieza a escuchar una orden sin decir el nombre, y **la misma tecla la corta**. Útil cuando no puedes hablar en voz alta o cuando la activación saltó sola. | Atajo global `Alt + V`. | Pulsa `Alt + V`, di «abre PowerShell». Pulsa otra vez para dejarlo. |
| **Respuestas en voz alta** | Sakura lee sus respuestas con SAPI. | Personalizar → «Sakura lee sus respuestas en voz alta». | — |
| **Sakura Flow (dictado global)** | Dicta en CUALQUIER aplicación de Windows, no solo en Sakura. Tres estilos: Texto, Correo, Código. Diccionario y atajos personales. | Atajo global `Ctrl + Shift + D`, on/off en Personalizar → Dictado global. | Abre el Bloc de notas, pulsa `Ctrl+Shift+D`, dicta, vuelve a pulsar para insertar. |

**Cuándo se rinde la escucha:** si nadie habla, a los **3 segundos** — no espera los veinte del
máximo. Ese máximo sigue existiendo, pero sólo cuenta una vez que ya empezaste a hablar: una
pausa a mitad de una orden no la corta.

**Lo que Flow NO hace:** insertar texto si cambiaste de ventana a mitad del dictado, o si la ventana activa está marcada como sensible (gestor de contraseñas). En esos casos copia al portapapeles en vez de escribir a ciegas.

---

## 3 · Sakura Lens (ver la pantalla)

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Captura bajo demanda** | Lee la ventana activa o una región, con OCR y estructura de UI Automation. Nunca captura en segundo plano. | Comando de captura, o automático si la pregunta lo sugiere. | «¿qué error me está saliendo en pantalla?» |
| **Tres modos** | Soporte (explica lo que ves), Estudio (te guía), Desarrollo (lee errores de código). | Se elige según el contexto de la pregunta. | — |
| **Redacción antes de enviar** | Contraseñas, tarjetas y tokens visibles se tapan antes de que la captura salga del equipo. | Automático, siempre. | — |
| **Exclusión de ventanas sensibles** | Gestores de contraseñas y diálogos de credenciales nunca se capturan, ni se pulsan (ver §7). | Automático. Ampliable con exclusiones (§6). | — |

---

## 4 · Memoria personal

Apagada por omisión. Nada se guarda hasta que la activas.

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Activar memoria** | Interruptor general + tres categorías independientes (Preferencias, Conversación, Hábitos) + retención en días + exclusiones. | Personalizar → **Memoria personal**. | — |
| **Recordar algo explícitamente** | Guarda al instante, sin preguntar de más — la orden ya es el consentimiento. | Dilo directamente. | «recuerda que mi editor es Visual Studio» |
| **Recordar una preferencia dicha de paso** | Nunca se guarda sola: Sakura la propone y espera un «sí». | Responde «sí» a la propuesta. | Dices «prefiero respuestas cortas» → Sakura pregunta si lo recuerda. |
| **Usar lo recordado** | Se incluye como contexto en las siguientes consultas, redactado y acotado. | Automático mientras la memoria esté activa. | — |
| **Ver lo guardado** | Lista completa en texto claro. | `"Ver lo que Sakura recuerda"`. | — |
| **Olvidar todo** | Borrado irreversible, con confirmación obligatoria aunque la memoria esté en «permitido». | `"Olvidar todo lo que Sakura recuerda"`. | — |

**Cifrado:** el archivo (`memory.dat`) usa DPAPI de tu cuenta de Windows — no se puede leer desde otra cuenta ni otro equipo.

---

## 5 · Optimización del equipo

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Proponer un plan** | Lee el hardware real (CPU, RAM, GPU, batería) y solo propone lo que ese hardware justifica. Si falta un dato, no propone nada sobre eso y lo dice. | Sistema → **Optimización del equipo**, o pídelo. | «optimiza mi equipo para jugar» |
| **Aplicar con verificación** | Cambia el plan de energía y, si el hardware está justo, el modo de rendimiento de la propia Sakura. Relee el estado tras cada cambio — nunca da algo por hecho porque la llamada no fallara. | Confirmación explícita tras ver el plan. | — |
| **Deshacer** | Devuelve el equipo al estado guardado antes de aplicar. | `"Deshacer la última optimización"`, o desde el registro (§9). | — |
| **Historial** | Qué se aplicó, se deshizo o falló, con fecha. | `"Ver el historial de optimizaciones"`. | — |

Los siete escenarios: jugar, programar, editar video, videollamada, batería, uso general, restaurar.

---

## 6 · Acompañante de proyecto

Aquí Sakura lee, explica, busca y —con tu permiso explícito— modifica archivos de un proyecto de código.

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Autorizar una carpeta** | Una sola carpeta a la vez. La confirmación dice qué se concede y qué no. | Personalizar → **Proyecto autorizado** → «Autorizar una carpeta». | — |
| **Explicar el proyecto** | Envía la estructura de archivos, no el código, para explicar de qué trata. | `"Explicar el proyecto autorizado"`. | — |
| **Buscar en el proyecto** | Búsqueda literal, archivo:línea. | `"Buscar en el proyecto autorizado"` → te pide qué buscar por chat. | «TODO» |
| **Nivel de autonomía** | Cinco niveles: Ver, Guiar, Proponer, **Ejecutar un paso**, **Colaborar**. Llega en «Guiar» y no sube solo. | Personalizar → Proyecto autorizado → radio buttons. Ampliar pide confirmación. | — |
| **Modificar un archivo (nivel 4+)** | Un cambio, con copia previa verificada antes de tocar nada, verificación releyendo después, y negativa a deshacer si editaste el archivo tú mismo después. | `"Pedir un cambio en el proyecto"` → describe el cambio → revisa la vista previa → confirma. | «agrega un comentario al README que diga X» |
| **Varios cambios seguidos (nivel 5)** | Encadena cambios en distintos archivos, **preguntando en cada uno**. Si uno falla, para ahí, anota el punto exacto y ofrece deshacer lo ya aplicado. Nunca reintenta sola. | Sube el nivel a «Colaborar», luego pide un cambio que toque varios archivos. | «actualiza el README y el CHANGELOG con la nueva versión» |
| **Deshacer un cambio** | Por checkpoint individual. | `"Deshacer el último cambio en el proyecto"`, o desde el registro. | — |
| **Revocar el acceso** | Inmediato, en el mismo sitio que autorizar. | `"Revocar el acceso al proyecto"`. | — |

**Protecciones automáticas, siempre activas:** nunca lee `.env`, claves privadas ni certificados (se
rechazan por el nombre, sin abrirlos); nunca entra en carpetas de dependencias (`node_modules`,
`bin`, `obj`, `.git`…); nunca se sale de la carpeta autorizada aunque la ruta tenga `..`; oculta
valores que parecen secretos de código antes de enviar nada; y **desde esta corrección**, se niega a
modificar un archivo existente si no leyó su contenido actual en esa misma consulta — ver el
apartado de correcciones al final.

---

## 7 · Permisos y actuar sobre el equipo

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Permisos por capacidad** | Cada capacidad (Lens, Flow, Memoria, Proyecto, Optimización, Actuar en el equipo) tiene su propio nivel: Bloqueado / Preguntar / Permitido. Dar uno no da los demás. | Personalizar → **Permisos de Sakura**. | — |
| **Confirmaciones obligatorias** | Borrado irreversible, credenciales, pagos, elevación de administrador, publicación externa y cambios amplios de sistema se preguntan **siempre**, aunque la capacidad esté en «Permitido». | Automático, no se puede desactivar. | — |
| **Exclusiones por aplicación** | Una capacidad permitida igual se niega si la aplicación de destino coincide con una exclusión. | Personalizar → Permisos → «Aplicaciones excluidas», formato `Capacidad: nombre`. | `Lens: bitwarden` |
| **Proponer una acción en el equipo** | Elige siempre el método más seguro disponible entre los implementados (UI Automation, un comando de la lista de permitidos, o el portapapeles) y explica por qué. | `"Proponer cómo hacer algo en el equipo"`. | «cómo copio esta selección al portapapeles» |
| **Ejecutar (nivel 4)** | Una acción confirmada cada vez: pulsar un control (se niega si hay varios con el mismo nombre), un comando de solo lectura, o cambiar el portapapeles con vuelta atrás. | Sube «Actuar sobre el equipo» a Permitido/Preguntar y el nivel a «Ejecutar un paso». | `"Ver la configuración de red"` |
| **Ver los métodos disponibles** | Lista los ocho métodos posibles, en orden de más a menos seguro, y cuáles están implementados hoy. | `"Ver cómo puede actuar Sakura en el equipo"`. | — |

**Actuar sobre el equipo llega bloqueado por omisión** — es el permiso más alto del roadmap y no se
concede por instalar Sakura. Ratón y teclado simulados no están implementados: son el último
recurso a propósito, y quedan cerrados hasta que haya una razón real para ellos.

---

## 8 · Packs

Un pack deja configuradas de una vez varias capacidades que ya existen. **Nunca enciende un
permiso**: lo que le falta lo declara, y dice dónde se da.

| Pack | Para qué |
|---|---|
| **Sakura Study** | Estudiar: dictado en Texto, respuestas en voz alta, vista rápida apagada. |
| **Sakura Dev** | Programar: dictado en Código, Sistema y Captura visibles, máximo rendimiento. |
| **Sakura Support** | Resolver un problema: métricas delante, Sistema y Captura a mano. |
| **Sakura Creator** | Crear sin interrupciones: máximo rendimiento, sin avisos ni sonidos. |
| **Sakura Access** | Manos libres: responde en voz alta, escucha siempre, sin animaciones. |
| **Sakura Meeting** | En reunión: se calla, no interrumpe, baja su propio consumo. Es el único que no pide ningún permiso nuevo. |

**Cómo activarlos:** Personalizar → **Packs de Sakura**, o `"Activar Sakura Dev"` (etc.) por
comando. Solo uno activo a la vez; `"Desactivar el pack activo"` devuelve tus ajustes exactamente a
como estaban antes.

---

## 9 · Registro de actividad (auditoría)

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Ver todo lo que hizo Sakura** | Qué, cuándo, con qué permiso y cómo deshacerlo — un solo registro para todas las capacidades. | Sistema → **«Qué ha hecho Sakura»**, o `"Ver todo lo que Sakura ha hecho"`. | — |
| **Deshacer desde el registro** | Botón «Deshacer esto» en cada entrada que lo permite. | Directamente en el panel. | — |

No es un log técnico de depuración: está escrito para que una persona entienda qué pasó con su
equipo. Solo se recorta por antigüedad — nunca se puede borrar una entrada suelta, para que no se
convierta en una versión de los hechos.

---

## 10 · Mantenimiento y privacidad

| Capacidad | Cómo funciona | Cómo activarla | Ejemplo |
|---|---|---|---|
| **Ver qué guarda Sakura** | Lista completa de archivos, qué contiene cada uno, si es tuyo y si está cifrado. | `"Ver qué guarda Sakura en tu equipo"`. | — |
| **Copia antes de actualizar** | Copia y **verifica comparando contenido** (no tamaño). Si algo no se verifica, no declara la actualización segura. | `"Preparar una copia antes de actualizar"`. | — |
| **Restaurar una copia** | Devuelve tus datos a como estaban. Pide reiniciar Sakura después. | `"Restaurar la última copia de tus datos"`. | — |
| **Ver qué pasa al desinstalar** | Enseña las dos listas: lo que se borraría y lo que se conservaría. | `"Ver qué pasaría al desinstalar Sakura"`. | — |
| **Exportar diagnóstico para soporte** | Redactado con dos herramientas distintas, sin tu nombre de usuario en las rutas, sin memoria ni conversaciones. Enumera al final lo que dejó fuera. | `"Exportar un diagnóstico para soporte"`. | — |
| **Informe de privacidad** | Qué se guarda, dónde, cifrado o no, y cómo borrarlo. | `"Ver el informe de privacidad"`. | — |
| **Comprobar que Sakura funciona bien** | Dieciséis comprobaciones reales sobre este equipo: migraciones, permisos, cifrado, contención del proyecto, copias… Dice explícitamente qué NO comprueba (la interfaz, el dictado, la voz — eso solo lo comprueba una persona usándola). | `"Comprobar que Sakura funciona bien"`. | — |

---

## Mapa rápido: ¿dónde se activa cada cosa?

```
Personalizar
├── Memoria personal ................... §4
├── Dictado global (Flow) ............... §2
├── Proyecto autorizado ................. §6
├── Permisos de Sakura .................. §7
│   └── Aplicaciones excluidas
└── Packs de Sakura ...................... §8

Sistema
├── Optimización del equipo ............. §5
└── Qué ha hecho Sakura .................. §9

Comandos sin panel propio (paleta o chat):
├── Buscar/explicar/pedir un cambio en el proyecto ... §6
├── Proponer/ejecutar/ver métodos del equipo .......... §7
└── Mantenimiento y privacidad (§10 entero)
```

---

## Qué falta antes de usarlo en serio

Dicho sin adornos, porque callarlo sería peor:

- **Nada de esto está integrado a `release/kohana-1.0-rc` ni a `main`.** Vive en la rama
  `design/kohana-sprints-d7-d9`.
- **Falta la validación manual completa.** Hay una autocomprobación (§10) que verifica la
  maquinaria interna, pero no sustituye usar la interfaz un rato: dictado, píldora, voz.
- **Se acaba de corregir un defecto real** encontrado probando el acompañante de proyecto a mano:
  pedir un cambio pequeño podía sustituir el archivo entero si Sakura no había leído su contenido
  actual. Ya está arreglado con dos capas — una que intenta leer el archivo correcto antes de pedir
  el cambio, y una salvaguarda que se niega a aplicar cualquier cambio a un archivo existente cuyo
  contenido no viajó en esa consulta —, cubierto con pruebas nuevas y verificado con la
  autocomprobación ejecutada de verdad sobre este equipo. Sigue mereciendo que lo pruebes tú mismo
  antes de confiar en él con un proyecto real.
- **La escalera de métodos para actuar en el equipo** tiene 3 de 8 escalones implementados
  (UI Automation, un comando de solo lectura, portapapeles). Los cuatro primeros (API oficial, App
  Actions, MCP, integración nativa) no existen todavía.
- **El nivel 6 de autonomía** (automatizar una secuencia entera sin confirmar por el camino) sigue
  cerrado para todas las capacidades, y así debería seguir hasta que haya una razón mejor que el
  calendario.

Para el detalle técnico completo de cada decisión: `docs/stable-release/IMPLEMENTATION_LOG.md`.
Para el estado fase por fase del roadmap: `docs/roadmap/KOHANA_TECHNOLOGY_ROADMAP.md`.
