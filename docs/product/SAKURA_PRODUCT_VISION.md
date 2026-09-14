# Visión de producto de Kohana

> Diseño D3.2. Documento de producto, no de implementación: describe qué es Kohana y hacia dónde
> va, no cómo está construido hoy pieza por pieza (eso vive en
> `docs/architecture/SAKURA_CAPABILITY_ARCHITECTURE.md`) ni cuánto de esto ya existe (eso vive en
> `docs/roadmap/SAKURA_CAPABILITY_MATRIX.md`). Ningún enunciado de este documento debe leerse como
> "ya implementado" solo por estar escrito aquí.

## 1. Problema que resuelve

Un usuario de Windows hoy reparte su atención entre docenas de superficies desconectadas: una app
de tareas, un asistente de voz que vive en otra ventana, un gestor de recursos del sistema, un
optimizador de rendimiento, un editor de código con su propia IA, un dictado por separado. Cada
herramienta tiene su propio contexto, su propia memoria y su propia interfaz que hay que abrir a
propósito. El coste no es la falta de funciones individuales — es la fricción de **cambiar de
programa para cada intención** y la pérdida de contexto cada vez que se hace.

## 2. Visión final

> Kohana es una capa inteligente para Windows que puede escuchar, ver contexto autorizado,
> comprender, responder mediante superficies ambientales, optimizar el equipo, trabajar junto al
> usuario, editar proyectos y ejecutar acciones con permisos, auditoría y reversibilidad.

Kohana **no** es una app de chat, una lista de tareas o un gestor de productividad más. Esas son
superficies que Kohana ofrece hoy (Diseño D1–D3.1: Sakura Shell, Command Center, Daily Flow, Focus
Continuity) porque son la base sobre la que se construye lo ambiental — no el destino final.

## 3. Usuario objetivo

Una persona que usa Windows como su entorno principal de trabajo o estudio, que ya tolera tener
varias herramientas abiertas a la vez, y que valora **recuperar tiempo y contexto** más que
coleccionar funciones. No asume conocimientos de IA ni de línea de comandos: cada capacidad nueva
debe explicarse en el momento en que se ofrece, no en un manual aparte.

## 4. Identidad Sakura

"Sakura" es el lenguaje visual y de tono de Kohana, no una función: calma, claridad, ligereza,
tecnológico sin ser frío, discretamente floral sin ser infantil. Una interfaz Sakura evita el
"panel de control" genérico (rejillas densas de widgets, vidrio pesado, iconografía agresiva) y
evita también la ternura excesiva (nada de mascotas, animaciones caprichosas o lenguaje aniñado).
El acento rosa (`#E98AAF` por defecto, personalizable) es la única firma de color fuerte; el resto
de la interfaz es neutro para que ese acento siga significando algo.

## 5. Experiencia sin abrir la aplicación

La aplicación principal (la ventana de Kohana) es una superficie entre varias, no la única puerta
de entrada. La visión final incluye responder y actuar desde superficies ambientales que no
requieren maximizar ni siquiera enfocar una ventana: una "píldora" flotante que aparece cerca de
donde está la atención del usuario, una barra de voz para dictado global, notificaciones nativas
de Windows con acciones rápidas. Hoy (Fase 0) esto no existe todavía — es el objetivo explícito de
la Fase 1 del roadmap tecnológico (Ambient Interaction Foundation).

## 6. La aplicación principal como centro de control

Aunque Kohana deba poder ayudar sin abrirse, la ventana principal (Sakura Shell) sigue siendo el
centro de control: el lugar donde se revisa lo que Kohana hizo, se ajustan permisos, se administran
motores y modelos, se personaliza la experiencia y se accede a todo lo que no cabe en una
interacción ambiental de un vistazo. Command Center (Ctrl+K) es el atajo que conecta ambos mundos:
funciona dentro de la app hoy y está pensado para extenderse a las superficies ambientales mañana.

## 7. Diferenciadores

- **Local primero, nube cuando el usuario lo pide.** Los motores de voz (Whisper, Vosk) corren en
  el equipo; la IA conversacional es opcional y configurable (OpenAI, Ollama local, LM Studio,
  cualquier endpoint compatible, o desactivada).
- **Todo lo que hace queda a la vista.** Ninguna acción relevante ocurre sin un rastro que el
  usuario pueda revisar (ver Sección 12 del Modelo de Confianza y Autonomía).
- **Reversibilidad como requisito, no como característica opcional.** Cambios de sistema
  (optimización adaptativa, Fase 4 del roadmap) están pensados desde el diseño para deshacerse.
- **Adaptado al hardware real del usuario**, no a un perfil genérico: `HardwareCapabilityProfile`
  y `AdaptiveEnginePolicy` ya deciden qué tan exigentes pueden ser los motores según el equipo
  detectado.

## 8. Principios

1. Nunca fabricar datos que Kohana no puede verificar (ni clima, ni noticias, ni estadísticas,
   ni conversaciones inventadas) — un estado vacío honesto es preferible a un dato falso.
2. Nunca pasar silenciosamente de observar a actuar (ver Sección 12).
3. Local antes que híbrido, híbrido antes que nube, siempre con consentimiento explícito para
   salir del equipo.
4. Toda superficie nueva debe integrarse con lo que ya existe (Command Center, Engine Registry,
   preferencias) en vez de crear un sistema paralelo.
5. Ninguna capacidad nueva debe degradar el arranque, la memoria en reposo o la fluidez de lo que
   ya funciona (ver "Qué no debe regresar" en cada informe de sprint).

## 9. Privacidad

Los datos de Kohana viven en `%LocalAppData%\Kohana` salvo que el usuario configure explícitamente
un proveedor de IA en la nube. La captura de pantalla, el micrófono y cualquier fuente de contexto
futura (Fase 1 en adelante) requieren que el usuario los habilite y quedan sujetos al modelo de
permisos descrito en `docs/security/SAKURA_TRUST_AND_AUTONOMY_MODEL.md`. Ninguna funcionalidad de
diagnóstico o validación (ver Diseño D3.2, aislamiento de perfiles) debe tocar los datos reales del
usuario sin que el usuario lo pida.

## 10. Funcionamiento local, híbrido y cloud

| Modo | Qué corre dónde | Estado hoy |
|---|---|---|
| Local | Whisper (voz→texto), Vosk (palabra de activación), SAPI (texto→voz), Engine Registry, Hardware Capability Profile | Implementado |
| Híbrido | Ollama gestionado por Kohana (`ManagedOllamaSupervisor`) — modelo corre en el equipo, orquestación vive en el proceso de Kohana | Implementado |
| Cloud | Proveedor de IA externo (OpenAI u otro compatible) configurado explícitamente por el usuario | Implementado (opt-in, desactivado por defecto) |

Ninguno de los tres modos es obligatorio: `AiProviderKind.Disabled` es el valor por defecto de
`ShellPreferences`.

## 11. Límites

- Kohana no toma decisiones irreversibles sin confirmación cuando el riesgo es alto (ver niveles
  de autonomía, Sección 12 del documento de confianza).
- Kohana no sustituye herramientas especializadas (IDEs completos, DAWs, editores de video) — las
  acompaña.
- Kohana no está pensada para telemetría oculta ni para monitorizar al usuario para terceros.

## 12. Qué Kohana no debe convertirse en

- Un dashboard genérico saturado de widgets sin jerarquía.
- Un asistente que finge saber cosas que no puede verificar.
- Una suite de "optimización" basada en listas genéricas de tweaks sin medir el equipo real.
- Un agente que actúa sobre el sistema, archivos o cuentas del usuario sin que quede registrado y
  sea reversible.
- Una colección de funciones desconectadas entre sí — cada superficie nueva debe sentirse como
  parte de la misma Kohana, no como un plugin ajeno.

## 13. Superficies

| Superficie | Descripción | Estado |
|---|---|---|
| Sakura Shell | Ventana principal: navegación de 9 secciones, personalización, centro de control | Implementado |
| Command Center | Paleta de comandos (Ctrl+K), búsqueda y ejecución de acciones sin ratón | Implementado |
| Sakura Pills | Ventanas ambientales pequeñas, no activables, que no roban foco, para respuestas y estados rápidos | Planeado (Fase 1) |
| Voice Bar | Barra de dictado global con push-to-talk | Planeado (Fase 3) |
| Mini overlays | Indicadores flotantes de estado (p. ej. mini temporizador de Enfoque ya existe dentro de la Shell; una versión fuera de la ventana principal es parte de la Fase 1) | Parcial |
| Panel lateral | Panel de contexto/resultados anexo a la ventana principal | Planeado (Fase 1) |
| Notificaciones | Notificaciones nativas de Windows con acciones rápidas | Infraestructura disponible (`ShowWindowsNotifications`, `PlayNotificationSounds`) |
| Lens Overlay | Superposición visual sobre la ventana activa para observar y guiar | Planeado (Fase 2) |
| App principal | Ventana de Kohana como centro de control (ver Sección 6) | Implementado |
| Historial y auditoría | Registro navegable de lo que Kohana hizo, con reversión | Planeado (Fase 1 en adelante) |

## 14. Documentos relacionados

- `docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md` — fases y sprints.
- `docs/architecture/SAKURA_CAPABILITY_ARCHITECTURE.md` — capas técnicas.
- `docs/security/SAKURA_TRUST_AND_AUTONOMY_MODEL.md` — permisos, estados visibles, autonomía.
- `docs/roadmap/SAKURA_CAPABILITY_MATRIX.md` — inventario capacidad por capacidad.
