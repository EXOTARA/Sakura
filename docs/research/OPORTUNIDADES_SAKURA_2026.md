# Oportunidades para Sakura en 2026

**Investigación cerrada el 17 de septiembre de 2026.**

## Conclusión ejecutiva

Sakura no debería intentar ganar por cantidad de funciones. PowerToys y Raycast ya cubren con más
recursos el lanzador, la búsqueda de archivos, los fragmentos, el portapapeles y las extensiones;
ChatGPT, Copilot, Claude, NotebookLM y el propio Ollama ya cubren el chat general y los archivos. La
oportunidad propia de Sakura es otra:

> **Ser el acompañante local y tranquilo que convierte lo que un estudiante tiene delante en el
> siguiente paso realizable, le ayuda a aprender sin hacerle trampa y deja el trabajo ordenado en su
> equipo.**

El siguiente bloque de producto debería cerrar un recorrido completo que Sakura ya empezó:

1. leer, con permiso, el enunciado de la ventana activa;
2. confirmar qué entendió —actividad, asignatura, fecha y entregable—;
3. crear una tarea y dividirla en pasos pequeños;
4. ofrecer enfoque de 5/15/25 minutos sobre el primer paso;
5. producir un borrador que andamia, no resuelve;
6. guardarlo en `Documentos\Sakura\<Asignatura>\<Actividad>.docx`;
7. permitir rehacer una sección o continuar una iteración sin regenerar todo;
8. convertir el material resultante en repaso, preguntas o práctica.

Esto reutiliza casi todas las ventajas reales del producto —Lens, Hoy, Enfoque, documentos, voz,
permisos y ejecución local— en vez de añadir otra isla. Antes de construirlo, conviene hacer una
pausa corta de estabilización y pruebas con personas ajenas al desarrollo. El propio README dice
que lo que falta para 1.0 no son funciones, sino instalación limpia, latencia de voz, accesibilidad
y uso sostenido ([README](../../README.md#estado)).

## 1. Qué cambió desde las investigaciones anteriores

Las dos investigaciones previas siguen siendo útiles, pero su lista de pendientes ya quedó en gran
parte obsoleta. Entre las versiones 0.30.18 y 0.30.30 Sakura construyó, entre otras cosas:

- Hoy como centro del día, captura en una línea, revisión de la mañana y posponer o soltar sin culpa;
- enfoque desde una tarea con 5/15/25 minutos y continuación opcional;
- hábitos opcionales y Atajos con plantillas;
- texto seleccionado en cualquier aplicación;
- búsqueda de archivos con el índice de Windows;
- exportación de conversaciones;
- borradores por tipo de trabajo, fuentes para consultar y documentos Word, Excel y PowerPoint;
- explicación de la ventana activa y mejoras importantes a la instalación de la IA local.

La evidencia está en el [CHANGELOG](../../CHANGELOG.md). Por tanto, no corresponde volver a
recomendar texto seleccionado, búsqueda por nombre, exportación, el rediseño de Hoy ni los cambios
de Enfoque: ya existen.

La trayectoria también revela un riesgo. El repositorio pasó de 0.30.0 a 0.30.30 en pocos días y
el flujo principal de la interfaz sigue concentrado en `MainWindow.xaml.cs`, que actualmente supera
las diez mil líneas, aunque Captura, Explicar y Selección ya empezaron a salir a archivos parciales
([MainWindow.xaml.cs](../../src/Nexo.App/MainWindow.xaml.cs),
[MainWindow.Explain.cs](../../src/Nexo.App/MainWindow.Explain.cs),
[MainWindow.Selection.cs](../../src/Nexo.App/MainWindow.Selection.cs)). Una nueva función grande
puesta directamente ahí aumentaría el riesgo de regresiones y haría más difícil que el tester
distinga un error de flujo de uno visual.

## 2. Qué está pasando alrededor de Sakura

### 2.1 El lanzador genérico dejó de ser un hueco

PowerToys Command Palette ya abre aplicaciones, ejecuta comandos, busca archivos, abre herramientas
de Windows y admite extensiones desde una interfaz de teclado
([Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)).
Raycast para Windows ya ofrece búsqueda de archivos, historial de portapapeles, snippets,
quicklinks, IA y extensiones, y en agosto de 2026 salió de beta
([sitio de Raycast para Windows](https://www.raycast.com/windows),
[changelog de Windows](https://www.raycast.com/changelog/windows)). Raycast incluso usa C#,
.NET y WPF para su host de Windows, además de React, Node y Rust
([explicación técnica oficial](https://www.raycast.com/blog/a-technical-deep-dive-into-the-new-raycast)).

**Implicación para Sakura:** unificar la paleta y el Command Center puede reducir mantenimiento,
pero competir con más funciones de lanzador, portapapeles o marketplace no es una buena siguiente
apuesta. Es una categoría atendida por equipos y ecosistemas mucho mayores.

### 2.2 Mirar la pantalla y actuar sobre texto ya son expectativas normales

Click to Do analiza localmente lo que aparece en pantalla y ofrece acciones sobre texto e imágenes,
pero exige un Copilot+ PC con NPU de 40 TOPS, 16 GB de RAM y otros requisitos
([Microsoft Support](https://support.microsoft.com/en-us/windows/ai/ai-features/click-to-do-do-more-with-what-s-on-your-screen)).
Copilot Vision ya permite compartir aplicaciones en Windows y dar orientación visual, aunque no
hace clic, escribe ni desplaza por la persona
([Microsoft Support](https://support.microsoft.com/en-us/microsoft-copilot/using-copilot-vision-with-microsoft-copilot)).

Sakura ya cubre una parte valiosa de esta expectativa sin exigir un Copilot+ PC: Lens, explicación
de la ventana activa, traducción de pantalla y acciones sobre la selección. La mejora no es añadir
otra forma de mirar; es conectar lo que mira con un resultado estudiantil concreto.

### 2.3 Los agentes que controlan el equipo siguen siendo una zona de alto riesgo

Copilot Actions seguía en vista previa para Windows Insiders y se ejecutaba en un espacio de agente
separado y auditable. Microsoft enumera como principios la contención, el registro resistente a
alteraciones, la supervisión del plan, el privilegio mínimo y permisos granulares y temporales
([Microsoft Support](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features),
[Windows Insider Blog](https://blogs.windows.com/windows-insider/2025/11/17/copilot-on-windows-copilot-actions-begins-rolling-out-to-windows-insiders/)).

El `PermissionBroker` y las confirmaciones obligatorias de Sakura van en la dirección correcta
([PermissionBroker.cs](../../src/Nexo.Core/Permissions/PermissionBroker.cs),
[SakuraCapability.cs](../../src/Nexo.Core/Permissions/SakuraCapability.cs)), pero Sakura no tiene el
aislamiento de cuenta y escritorio que Microsoft está usando. **No conviene ampliar ahora Computer
Use a acciones generales autónomas.** Las acciones estrechas, visibles y reversibles siguen siendo
la frontera sensata.

### 2.4 La IA para estudiantes se está moviendo de “dar respuestas” a “hacer aprender”

Los competidores grandes convergieron en la misma dirección:

- ChatGPT Study Mode usa preguntas guiadas, andamiaje, comprobaciones de comprensión y práctica, y
  reconoce que todavía puede cometer errores o dar respuestas directas de manera inconsistente
  ([OpenAI](https://openai.com/index/chatgpt-study-mode/),
  [ayuda oficial](https://help.openai.com/en/articles/11780217)).
- Microsoft Study and Learn trabaja con los materiales del estudiante y ofrece tarjetas,
  cuestionarios, correspondencias y ejercicios de completar, bajo la idea de que el estudiante
  piensa y el agente acompaña
  ([Microsoft Support](https://support.microsoft.com/en-us/education/copilot/study-learn-agent)).
- NotebookLM genera guías, tarjetas y cuestionarios desde fuentes seleccionadas y puede explicar
  por qué una respuesta está mal señalando el material original
  ([Google](https://blog.google/innovation-and-ai/models-and-research/google-labs/notebooklm-student-features/),
  [actualización móvil](https://blog.google/innovation-and-ai/models-and-research/google-labs/notebooklm-app-quizzes-flashcards/)).

Esto confirma que la regla que ya existe en `AssignmentDraft` —“andamiar, no resolver”— no es una
limitación tímida: es una dirección competitiva correcta
([AssignmentDraft.cs](../../src/Nexo.Core/Assistant/AssignmentDraft.cs)). Sakura puede diferenciarla
con privacidad local, contexto de Windows y continuidad con Hoy y Enfoque.

### 2.5 El chat local por sí solo también dejó de ser diferenciador

Desde julio de 2025 la aplicación oficial de Ollama para Windows permite descargar modelos, chatear
y arrastrar texto o PDF; advierte además que aumentar el contexto para documentos grandes consume
más memoria ([Ollama](https://ollama.com/blog/new-app)). Claude Desktop empaqueta extensiones MCP
para instalarlas con un clic y declara herramientas y configuración por adelantado
([Anthropic](https://www.anthropic.com/engineering/desktop-extensions)).

**Implicación para Sakura:** “chat local” ya no basta como propuesta. Sakura debe ocultar la
complejidad del modelo dentro de recorridos útiles y honestos, no competir por tener el catálogo de
modelos o extensiones más grande.

## 3. Patrones repetidos en comunidades

Las publicaciones comunitarias se usan aquí solo como testimonio de lo que esas personas expresan,
no como estadísticas de población.

### 3.1 Menos sistema, más siguiente paso

En varios hilos de productividad de 2025 se repite que demasiadas aplicaciones, pestañas,
categorías y configuraciones producen más mantenimiento y abandono. Las respuestas más apoyadas
describen volver a una lista corta, un bloc de notas o una única cosa para hoy
([hilo 1](https://www.reddit.com/r/productivity/comments/1nij3xb/anyone_else_feel_overwhelmed_by_all_the/),
[hilo 2](https://www.reddit.com/r/productivity/comments/1m0l14x/i_bought_every_productivity_app_and_planner_known/),
[hilo 3](https://www.reddit.com/r/productivity/comments/1kg2hr6/tired_of_overcomplicated_time_management_apps/),
[hilo 4](https://www.reddit.com/r/productivity/comments/1jy342t/are_productivity_apps_actually_making_us_less/)).
Un hilo específico de una estudiante pide exactamente una vista simple de entregas próximas,
calendario y fechas, porque las herramientas complejas no se sostienen
([r/ProductivityApps](https://www.reddit.com/r/ProductivityApps/comments/1k6k1lv/productivity_app_with_simple_features_only_good/)).

Sakura ya respondió bien con Hoy, Ahora y “solo 5 minutos”. La conclusión no es añadir otra sección,
sino hacer que el enunciado de una tarea termine en esos lugares sin duplicar captura.

### 3.2 La dificultad real está entre entender una tarea y empezar

En comunidades de estudio y TDAH aparecen la parálisis ante una actividad grande, la necesidad de
dividirla y el deseo de saber qué hacer primero. Una persona describe una actividad vencida que ni
siquiera logra sentarse a entender; otra propone una herramienta específica que convierta el
enunciado en pasos y tiempo estimado
([r/ADHD](https://www.reddit.com/r/ADHD/comments/1jo559t/please_help/),
[r/GetStudying](https://www.reddit.com/r/GetStudying/comments/1cx6vjv/adhd_students_how_do_you_break_down_your/)).
En otro sistema compartido por una estudiante con TDAH, la sección “next to work on” es la parte que
reduce la indecisión
([r/ProductivityApps](https://www.reddit.com/r/ProductivityApps/comments/1k4wm7g/my_productivity_system/)).

Éste es el puente que le falta a Sakura: Lens entiende la ventana y Hoy decide el siguiente paso,
pero hoy son recorridos separados.

### 3.3 Existe rechazo real a los trabajos completos y a las fuentes inventadas

Profesores y estudiantes repiten dos daños: entregar texto completo sin aprender y citar obras o
frases inexistentes. Un profesor relata trabajos con bibliografía y citas inventadas; otros hilos
distinguen explícitamente entre usar IA para comprender y copiar una tarea completa
([profesor de historia](https://www.reddit.com/r/ChatGPT/comments/1kzzyb2/professor_at_the_end_of_2_years_of_struggling/),
[r/Teachers](https://www.reddit.com/r/Teachers/comments/1kd5eec/cheating_with_chatgpt/),
[r/GradSchool](https://www.reddit.com/r/GradSchool/comments/1ojr04m/chatgpt_is_making_my_students_stupider/)).

Sakura ya tomó mejores decisiones que muchos competidores: no resuelve un examen detectado, deja
huecos para que la persona complete, no inventa datos personales y marca los documentos como
borradores hechos con ayuda de IA. Eso debe convertirse en identidad de producto, no retirarse para
parecer más capaz.

### 3.4 “IA local” no significa automáticamente “rápida” o “fácil”

En comunidades de Ollama se repiten modelos demasiado grandes para la memoria disponible,
respuestas de minutos y dudas sobre si la GPU está funcionando
([equipo con 8 GB](https://www.reddit.com/r/ollama/comments/1iyt6jc/ran_a_9gb_model_on_a_laptop_with_8gb_of_ram_and/),
[CPU sin GPU](https://www.reddit.com/r/ollama/comments/1kkt4s4/i_wonder_if_ollama_is_too_slow_with_cpu_only/),
[Windows y GPU](https://www.reddit.com/r/ollama/comments/1hw1l9g/ollama_running_slow/)).
También hay fallos de descarga o bloqueos específicos de Windows documentados en el repositorio de
Ollama
([#2850](https://github.com/ollama/ollama/issues/2850),
[#8779](https://github.com/ollama/ollama/issues/8779)).

Las últimas mejoras de Sakura —tamaños honestos, espacio libre, progreso legible y consejo de
Groq— atacan el problema correcto. Falta cerrar el círculo con una prueba de experiencia real, no
solo una clasificación teórica del hardware.

## 4. Posición recomendada

Sakura debería describirse y diseñarse alrededor de esta promesa:

> **“Lo que tienes que hacer, entendido y empezado sin salir de tu PC.”**

No “todo en uno”, no “agente autónomo”, no “chat privado”. La experiencia principal sería:

```text
Enunciado visible
    ↓ permiso y vista previa
Qué entendí: actividad · fecha · entregable
    ↓ confirmación
Hoy: primer paso · empezar 5 min
    ↓ trabajo acompañado
Borrador local con huecos, fuentes honestas y marca de IA
    ↓ continuar
Rehacer una sección · registrar iteraciones · practicar lo aprendido
```

La suma es más defendible que cada pieza aislada. Copilot puede mirar, ChatGPT puede estudiar,
NotebookLM puede usar fuentes y Raycast puede lanzar herramientas; ninguno de ellos une de forma
local, sin cuenta y en Windows el enunciado visible con una tarea diaria, un rato de enfoque y un
documento guardado en la carpeta de la actividad.

## 5. Recomendaciones priorizadas

### P0 — estabilizar y observar antes de añadir otra superficie

**Qué hacer**

- Cumplir lo que el propio README declara pendiente: instalación limpia, medición de latencia de
  voz, revisión con Narrador y uso sostenido.
- Probar con 5–8 estudiantes o familiares que no hayan visto el desarrollo. Sin telemetría: una
  hoja manual basta. Darles cuatro tareas: instalar; capturar una entrega; empezar cinco minutos;
  crear y encontrar un Word. Registrar dónde preguntan, abandonan o interpretan algo distinto.
- Hacer una pasada de teclado, foco, escalado 125/150 %, dos monitores y equipos de 8 GB antes de
  ampliar la función de documentos.
- Extraer solo el próximo flujo grande de `MainWindow.xaml.cs` a un coordinador de aplicación
  (`AssignmentWorkflowCoordinator` o equivalente). No hacer una reescritura general.

**Por qué ahora.** El proyecto tiene una suite amplia, pero casi no tiene señal pública de uso: el
repositorio no muestra issues de usuarios y el botón de comentarios acaba de llegar. Treinta
versiones pequeñas no sustituyen una instalación observada de principio a fin.

**Complejidad:** baja-media. **Coste monetario:** cero si se hace con personas cercanas.
**Privacidad:** sin cambio; las notas pueden ser anónimas y locales.

### P1 — terminar “Borrador de tarea” como recorrido completo

**Qué construir**

1. Entrada “Usar el enunciado de la ventana activa” desde la paleta y desde el diálogo del borrador.
   Reutilizar el contexto temporal y el permiso de Lens; enseñar el texto leído antes de enviarlo.
2. Extraer de forma conservadora actividad, asignatura, fecha, tipo de entrega e instrucciones. Todo
   campo dudoso queda vacío y se confirma en una sola pantalla.
3. Ofrecer tres acciones juntas: **Apuntar en Hoy**, **Empezar 5 min** y **Preparar borrador**. No
   crear automáticamente ninguna.
4. Guardar directamente en `Documentos\Sakura\<Asignatura>\<Actividad>.docx`, saneando nombres y sin
   sobrescribir: si existe, guardar una versión nueva o pedir confirmación.
5. En ejercicios iterativos, convertir la tabla del procedimiento en Excel; la persona introduce
   datos, no Sakura.
6. Añadir **Rehacer esta sección** y **Continuar desde mi documento**, conservando el original. No
   regenerar todo el trabajo por un cambio pequeño.

**Reutiliza:** `AssignmentDraft`, `AssignmentDraftWindow`, Lens, `TaskManager`,
`FocusManager`, `WordDocumentBuilder`, `SpreadsheetDocumentBuilder`, `DocumentDestination` y el
servicio de guardado. Es la continuación natural de lo que el proyecto ya marca como pendiente en
[AGENTS.md](../../AGENTS.md#estado-del-rediseño-diario-contexto-vivo-puede-quedar-desactualizado--revisa-el-changelog-para-lo-más-reciente).

**Complejidad:** media. **Coste monetario:** ninguno; usa el proveedor que la persona ya eligió.
**Privacidad:** media. La ventana activa solo se lee tras una acción explícita; debe mostrarse qué
texto se enviará cuando el proveedor sea remoto. Los documentos y versiones permanecen locales.

### P2 — añadir “Estudiar esto”, no un segundo chat general

**Qué construir**

- Un modo invocable sobre la ventana activa, texto seleccionado o un archivo elegido.
- Preguntar solo objetivo, nivel y tiempo disponible; después trabajar una pregunta por turno.
- Ofrecer “Explícame”, “Dame una pista”, “Ponme uno parecido”, “Comprueba mi respuesta” y “Repaso de
  10 minutos”.
- Generar preguntas y tarjetas desde el material elegido, pero sin crear una sección permanente de
  flashcards ni otra base de datos que mantener al principio.
- Al final, ofrecer crear en Hoy el siguiente repaso o iniciar Enfoque. No generar rachas.
- Reutilizar el modo tutor de examen; si parece una evaluación activa, usar otro ejemplo y no revelar
  la respuesta.

La primera versión debería aceptar texto visible/seleccionado y documentos de texto o Word. PDF
puede llegar después: hoy el repositorio no tiene un lector general de PDF, y añadirlo bien exige
OCR, páginas, errores y pruebas. No conviene prometer “estudia cualquier archivo” antes de tenerlo.

**Complejidad:** media-alta. **Coste monetario:** ninguno para el software; el coste de inferencia
depende del proveedor elegido. **Privacidad:** media-alta. Solo archivos elegidos; nunca indexación
automática de Documentos. Si se usa nube, vista previa clara del fragmento que saldrá.

### P3 — volver las fuentes comprobables, no solo reales

El comportamiento actual es honesto: `ScholarSourceService` busca trabajos reales por el tema y el
Word los llama “Fuentes para consultar”, no bibliografía de las afirmaciones
([ScholarSourceService.cs](../../src/Nexo.Windows/Documents/ScholarSourceService.cs),
[WordDocumentBuilder.cs](../../src/Nexo.Core/Documents/WordDocumentBuilder.cs)). Hay que conservar
esa distinción.

**Mejora propuesta**

- En el modo de estudio, cada explicación basada en material aportado debe indicar página, sección o
  fragmento de origen.
- Una fuente externa solo pasa de “para consultar” a “usada” si Sakura pudo leer el resumen o texto
  relevante y relacionarlo con una afirmación concreta.
- Añadir “No encontré respaldo” como estado válido; nunca rellenar una cita por parecido de título.
- En “Rehacer”, permitir seleccionar una afirmación y pedir: “compruébala con el material”.

Esto responde directamente al patrón de referencias inventadas y acerca a Sakura al valor de
NotebookLM sin fingir una precisión que un modelo local pequeño no tiene.

**Complejidad:** media para materiales locales; alta para texto completo académico externo.
**Coste monetario:** ninguno usando catálogos abiertos. **Privacidad:** baja con material local;
media si la comprobación usa un modelo remoto.

### P4 — prueba real y explicación simple de la IA local

Sakura ya detecta hardware y recomienda motores, pero una ficha de CPU/RAM no responde a la pregunta
de la persona: “¿cuánto tardará aquí?”.

**Qué construir**

- Después de instalar el modelo, una prueba opcional con una petición fija que mida arranque,
  primer texto y final.
- Mostrar un resultado humano: “rápida para preguntas cortas”, “usable pero tarda”, o “mejor usa un
  proveedor remoto para documentos largos”; conservar los tiempos exactos en Diagnóstico.
- Etiquetar el encabezado actual como **En este equipo** o **En la nube**, no solo con nombre de
  proveedor y modelo.
- Antes de una operación pesada, estimar honestamente si el modelo local y el contexto caben; ofrecer
  cancelar o elegir otra configuración, nunca cambiar a nube por cuenta propia.
- Si el servicio queda ocupado o colgado, ofrecer **Reiniciar la IA local** con explicación y sin
  perder la conversación.

**Complejidad:** media. **Coste monetario:** cero. **Privacidad:** baja; la prueba usa texto fijo y
no necesita red.

### P5 — cerrar el ciclo de comentarios sin telemetría

El botón “Enviar comentarios” es un buen comienzo, pero GitHub impone una barrera a estudiantes que
no tienen cuenta. Mantener “Copiar” y añadir un resumen opcional de diagnóstico, redactado y visible,
haría el reporte útil por cualquier canal.

No añadir analítica silenciosa. Si hacen falta datos de uso, usar contadores **solo locales**, con
una pantalla “Lo que usé esta semana” y un botón separado para incluirlos en el comentario. La
persona debe ver el texto exacto antes de copiarlo.

**Complejidad:** baja. **Coste monetario:** cero. **Privacidad:** baja si sigue siendo opt-in y
visible.

## 6. Qué no construir ahora

| Idea | Decisión | Motivo |
|---|---|---|
| Marketplace de extensiones o cliente MCP general | Aplazar | Claude y Raycast ya tienen ecosistemas; para el público estudiantil de Sakura añade permisos, soporte y riesgo antes que valor. |
| Historial de portapapeles, snippets y más lanzador | No priorizar | Raycast y PowerToys ya lo resuelven; no completa el recorrido central de Sakura. |
| Búsqueda semántica de todo el disco | No ahora | Coste de índice, almacenamiento y privacidad; empezar por archivos elegidos. |
| Agente autónomo que haga clic por toda la sesión | No ahora | Microsoft todavía lo aísla en un escritorio de agente en vista previa. Sakura no tiene ese aislamiento. |
| Cuenta, sincronización o app móvil | No ahora | Rompe la ventaja sin cuenta/local-first y crea coste de servidor y soporte. |
| Más paneles que aparezcan solos | Evitar | Las comunidades piden menos interrupción y menos sistema; Peek y controles de borde ya están apagados de fábrica. |
| Generar el trabajo completo o quitar la marca de IA | No construir | Daña el aprendizaje, la confianza y contradice una decisión ética ya cerrada. |

## 7. Orden sugerido de ejecución

1. **Validación de 0.30.30** en instalaciones limpias y con personas nuevas; corregir los bloqueos
   observados.
2. **Extraer el coordinador del flujo de tarea** sin reescribir el resto de `MainWindow`.
3. **Borrador de tarea completo:** ventana activa → confirmación → Hoy/Enfoque → carpeta propia →
   versiones → rehacer sección.
4. **Estudiar esto v1** sobre texto visible, seleccionado y Word/texto elegido.
5. **Fuentes comprobables** dentro de ese modo.
6. **Prueba real de IA local** y etiquetas En este equipo/En la nube.
7. Volver a decidir después de observar uso; no comprometer todavía MCP, PDF universal ni Computer
   Use autónomo.

## 8. Criterios para saber si funcionó

Sin telemetría y sin inventar cifras de mercado, se puede evaluar cada prueba observada con preguntas
binarias y tiempos locales:

- ¿La persona completa una instalación limpia sin ayuda?
- ¿Entiende qué pesa la app y qué pesa la IA local?
- ¿Puede convertir un enunciado visible en una tarea correcta sin volver a escribirlo?
- ¿Sabe qué va a salir del equipo antes de usar un proveedor remoto?
- ¿Empieza el primer paso en menos interacciones que hoy?
- ¿Encuentra el documento después de guardarlo?
- ¿“Rehacer” conserva la versión anterior?
- ¿El modo de estudio hace que la persona responda y corrija, o solo lea?
- ¿Una fuente marcada como usada respalda de verdad la afirmación?
- ¿Sakura devuelve siempre el foco y nunca escribe en otra ventana?

La decisión de continuar no debería depender de que “se vea completa”, sino de que personas nuevas
logren estos recorridos sin explicación del desarrollador.

## 9. Límites de la investigación

- Las comunidades consultadas sesgan hacia personas que publican en Reddit, Hacker News y GitHub;
  no representan por sí solas al conjunto de estudiantes.
- No hay telemetría de Sakura y el repositorio no muestra issues públicos de usuarios. No se puede
  afirmar qué función de Sakura se usa más.
- No se ejecutó una prueba de usabilidad ni se midieron RAM, CPU o latencia. Son precisamente parte
  de P0 y P4.
- Las páginas oficiales describen lo que cada empresa afirma que su producto hace; no prueban que la
  experiencia sea fiable en todos los equipos.
- El análisis del código fue dirigido a capacidades y puntos de integración, no una auditoría de
  seguridad ni una revisión completa de cada archivo.

## Veredicto

La dirección tomada en las últimas versiones es mejor que la del producto amplio de sus primeras
fases: menos culpa, menos configuración, más acciones sobre lo que la persona ya está viendo. La
mejor siguiente mejora no es una función aislada. Es **hacer que Sakura acompañe una tarea desde el
enunciado hasta el primer rato de trabajo, el archivo guardado y el repaso**, con límites académicos
y de privacidad visibles.

Si solo se elige una cosa después de estabilizar: **terminar Borrador de tarea como flujo completo**.
Si se elige la siguiente: **Estudiar esto con el material de la persona y referencias comprobables**.
