# Guía para agentes de IA que trabajen en este repo

Esto es para cualquier asistente de código que trabaje en EXOTARA/Sakura sin el historial de conversación de Claude Code: Codex, Gemini CLI, o cualquier otro. Léelo entero antes de tocar nada.

## Qué es Sakura

Asistente de escritorio para Windows, código abierto, WPF sobre .NET 10. Tres proyectos:

- `src/Nexo.Core` — lógica pura, sin WPF ni Windows. Debe poder probarse sin ninguno de los dos.
- `src/Nexo.Windows` — integración con Windows (audio, brillo, Ollama, voz, archivos).
- `src/Nexo.App` — la interfaz WPF (ventanas, vistas, `MainWindow.xaml.cs` como orquestador central).

Los nombres internos siguen siendo "Nexo" aunque el producto se llama Sakura (cambió de nombre dos veces: Kohana → Nexo → Sakura). No lo renombres, es intencional.

Nada de esto tiene servidor propio ni cuentas de usuario. Todo lo que hace Sakura vive en el equipo de quien la usa, salvo lo que se explica en `docs/PRIVACY.md`.

## El dueño del proyecto

Adler delega las decisiones técnicas y decide qué construir, cuándo publicar y qué aprobar. Explica los cambios por sus resultados y consecuencias, sin exigir que lea un diff.

## Flujo de trabajo (no te lo saltes)

1. **Rama nueva siempre.** Nunca un commit directo a `main`.
2. Si el cambio es una función terminada que se va a publicar como parte de la app:
   - Sube `VersionPrefix` en `Directory.Build.props`.
   - Añade una entrada en `CHANGELOG.md`, **en español**, en el estilo ya usado ahí: qué cambia contado como beneficio para quien usa la app, no como lista de archivos.
3. Abre un PR. **No lo fusiones tú.** Adler aprueba cada fusión y cada publicación explícitamente; termina tu turno pidiéndole que lo revise, no asumiendo que puedes seguir.
4. El cuerpo del PR y los mensajes de commit: sigue el estilo ya presente en el historial (`git log`) — resumen corto tipo commit convencional, cuerpo explicando el porqué. Termina el cuerpo del PR mencionando qué herramienta lo generó (para que quede claro que hubo asistencia de IA), con tu propio nombre, no el de otra herramienta.
5. Antes de dar algo por terminado: `dotnet test Nexo.slnx` tiene que pasar entero (hoy son tres proyectos de pruebas: Core, Windows, App). Si algo falla, dilo — no entregues con pruebas en rojo sin avisar explícitamente.

## Convenciones del código

- Comentarios y textos que ve la persona usuaria: **en español**. El código en sí (nombres de clases, variables) sigue en inglés, como ya está.
- Los comentarios explican **por qué** una decisión se tomó así, no qué hace la línea siguiente — mira cualquier archivo existente para el tono; suelen referenciar quién pidió el cambio y cuándo, y qué pasaba antes.
- No captures pantalla, no envíes nada a un proveedor de IA en la nube, ni escribas en otra ventana sin que ya exista una razón explícita y documentada para hacerlo (revisa `PermissionBroker` y `SakuraCapability` antes de añadir una capacidad nueva).
- Los documentos que Sakura genera (Word/Excel/PowerPoint) llevan una marca de que se hicieron con ayuda de IA. Esa marca **no lleva interruptor para quitarla** y no se debe construir nada pensado para evadir detectores de IA — es una línea que Adler ya trazó explícitamente, no la reabras.

## Límites duros, en cualquier tarea

- Nunca crear cuentas, pedir OAuth, ni guardar contraseñas o claves en texto plano (las claves de proveedores de IA van cifradas con DPAPI, ver `IAiApiKeyStore`).
- Nunca borrado permanente de datos del usuario sin una confirmación explícita en el propio flujo.
- Nunca pagos ni compras.
- Antes de descargar algo (un instalador, un modelo), pregunta o dilo explícitamente en el reporte — no lo des por hecho en silencio.
- Antes de ejecutar la app en vivo, comprueba que League of Legends y Riot Client no estén corriendo. Si alguno está activo, aplaza la prueba para evitar interferir con el uso del equipo.
- No inventes datos personales, fuentes académicas, ni cifras: si algo no se puede verificar, dilo así en vez de rellenar.

## Cómo probar en vivo

No hay una suite de UI automatizada más allá de las pruebas WPF con `StaWpfFixture`. Para ver algo funcionando de verdad:

- Usa `SAKURA_DATA_ROOT` (variable de entorno) para apuntar a una carpeta de datos aislada — nunca pruebes contra `%LOCALAPPDATA%\Sakura`, que son los datos reales de Adler.
- Antes de lanzar una copia de prueba, comprueba y cierra cualquier Sakura/Ollama que ya esté corriendo con esa misma carpeta de datos si vas a reiniciarla, y al terminar limpia lo que hayas creado.
- La app instalada de Adler vive en `%LOCALAPPDATA%\Programs\Kohana` (nombre antiguo, no lo cambies: el desinstalador depende de esa ruta).

## Dónde está el contexto que no cabe aquí

- `CHANGELOG.md` — historial completo de qué se hizo y cuándo, en español, versión por versión.
- `docs/research/`, si existe en el checkout — investigaciones de producto. Lee los documentos disponibles antes de proponer cambios del rediseño diario para no repetir trabajo. Por ahora pueden ser archivos locales sin seguimiento; no asumas que existen en otros equipos ni los publiques sin revisar su contenido.
- `docs/PRIVACY.md` y `site/legal/{es,en}/` — qué sale del equipo, por qué, y el resumen legal en dos idiomas. Cualquier cambio que afecte qué datos salen del equipo tiene que reflejarse aquí también, en los tres sitios a la vez.

## Estado del rediseño diario (contexto vivo, puede quedar desactualizado — revisa el CHANGELOG para lo más reciente)

Se acordó con Adler un rediseño de Inicio/Hoy/Enfoque/Atajos (antes "Rutinas"). Decisiones ya cerradas, no las reabras sin que Adler lo pida:

- Hoy es el centro del día: empieza vacío, sin contador de "vencidas", con captura en una línea que interpreta fecha/hora/importancia en español.
- Enfoque salió del menú lateral; se empieza desde una tarea o desde la paleta de comandos, con duraciones de un toque (5/15/25/última usada).
- Atajos (antes Rutinas) trae plantillas y una fila con ▶ por atajo.
- Hábitos existen como fila opcional dentro de Hoy, apagados de fábrica — Adler dijo explícitamente que él no los usaría.
- Peek y el panel de volumen/brillo del borde vienen apagados en instalaciones nuevas; quien actualiza conserva lo que tenía.
- La IA local (Ollama + un modelo) es opcional y pesa varios GB; la app en sí pesa poco. Esto se explica ya en la bienvenida — no lo ocultes ni lo minimices si tocas esa pantalla.

Prioridad acordada el 2026-09-17: consolidar las funciones existentes antes de añadir otras. Orden: cumplimiento de permisos y exclusiones en Lens/Flow; persistencia y recuperación de Hoy/Enfoque; documentos sin sobrescrituras; instalación y bienvenida; voz medida en condiciones reales. La accesibilidad se verifica dentro de cada bloque. Cada cambio requiere evidencia y pruebas; esta lista no declara que todos los riesgos estén reproducidos.

Las fases 4–5 del "Borrador de tarea" quedan aplazadas (leer el enunciado de la ventana activa, guardar en `Documentos\Sakura\<Asignatura>\<Actividad>.docx`, iteraciones en Excel y botón "rehacer"). Buscar archivos y exportar conversación ya están hechos. Unificar la paleta de comandos con el Command Center sigue aplazado por poco beneficio frente al esfuerzo.
