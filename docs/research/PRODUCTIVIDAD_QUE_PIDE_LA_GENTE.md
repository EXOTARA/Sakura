# Qué pide la gente en las apps de productividad (y qué la hace abandonarlas)

Investigación hecha el 16 de septiembre de 2026 para rediseñar Hoy, Enfoque, Rutinas e Inicio en Sakura.

**Cómo leer este documento.** Las citas entre comillas son textuales, de 15 palabras como máximo y en su idioma original, y cada una lleva el enlace del comentario o de la página de donde sale. Los comentarios de Hacker News se leyeron completos a través de la API pública de Algolia (`hn.algolia.com/api/v1/items/<id>`), y cada enlace apunta al comentario exacto. Los bloques marcados **Interpretación** son conclusiones mías y no salen de ninguna fuente. Lo que no se pudo comprobar está en la sección 7. Un límite importante desde el principio: **Reddit no se pudo leer** (el sitio bloquea tanto la descarga directa como la herramienta de lectura), así que las voces de la comunidad salen de Hacker News, de foros oficiales y de la documentación de las propias apps.

---

## 1. Resumen ejecutivo

1. **Lo que más mata una lista de tareas es el atraso que se acumula, no que le falten funciones.** Una lista que crece sin parar termina asociada a la culpa, y la gente deja de abrirla: «the list becomes so associated with guilt and stress that I just burn out» ([setopt, HN](https://news.ycombinator.com/item?id=44873136)). Las apps grandes lo reconocen: Todoist escribe «It’s overwhelming to see a list a mile long» ([Todoist, vista Hoy](https://todoist.com/help/articles/plan-your-day-with-the-todoist-today-view-UVUXaiSs)).
2. **Conviene que «Hoy» empiece vacío cada día y que lo que quedó pendiente se ofrezca como sugerencia, no como deuda.** Microsoft To Do lo hace así de forma explícita: «resets every night, so you have a blank slate» ([Microsoft Support](https://support.microsoft.com/en-us/office/my-day-and-suggestions-fc09a1b9-0854-4906-b166-f480ee97a139)).
3. **Posponer tiene que ser un gesto normal y sin castigo.** Todoist lo formula así: «It’s not cheating – it’s about being flexible» ([Todoist](https://todoist.com/help/articles/plan-your-day-with-the-todoist-today-view-UVUXaiSs)). Sunsama permite mandar a otro día lo que sobra con una sola tecla ([Sunsama Help](https://help.sunsama.com/docs/usage-guides/daily-planning/)).
4. **Planificar el día libera la cabeza aunque la tarea no se haga todavía.** Un experimento con varios estudios muestra que hacer un plan concreto elimina los pensamientos intrusivos de las metas pendientes y puede «free cognitive resources for other pursuits» ([Masicampo y Baumeister, 2011, JPSP](https://users.wfu.edu/masicaej/MasicampoBaumeister2011JPSP.pdf)). Por eso una revisión diaria corta tiene base, y una lista infinita no la tiene.
5. **Hay que limitar lo que entra en Hoy.** Todoist pide elegir «the 3 most important tasks» ([Todoist](https://todoist.com/help/articles/plan-your-day-with-the-todoist-today-view-UVUXaiSs)), y Sunsama avisa cuando lo planificado pasa de la hora de cierre ([Sunsama Help](https://help.sunsama.com/docs/usage-guides/daily-planning/)).
6. **Capturar una tarea tiene que costar un atajo y una línea, sin salir de lo que uno estaba haciendo.** Things lo define como poder «capture a thought without switching away from what you’re doing» ([Cultured Code](https://culturedcode.com/things/support/articles/2249437/)). En cambio, reprogramar y etiquetar acaban siendo «more work than actually doing the tasks» ([mackrevinack, HN](https://news.ycombinator.com/item?id=28035430)).
7. **En Enfoque, lo difícil es empezar, no medir.** Un usuario con TDAH explica que las tareas grandes le intimidan tanto que le cuesta arrancarlas, y que el temporizador le sirve justo para eso ([geoelectric, HN](https://news.ycombinator.com/item?id=26629511)). Otro reconoce que acaba procrastinando el propio arranque del temporizador ([muzani, HN](https://news.ycombinator.com/item?id=26631010)).
8. **Un pomodoro rígido de 25 minutos corta la concentración, así que el temporizador tiene que ser flexible.** Un usuario lo dice así: «The 25 mins jerk me out of flow» ([muzani, HN](https://news.ycombinator.com/item?id=26631010)). Otros saltan el descanso por sistema ([starrlordxt, HN](https://news.ycombinator.com/item?id=26627803)), y las pausas cortas tienen un efecto pequeño en el rendimiento ([Albulescu et al., 2022, PLOS ONE](https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0272460)).
9. **Una racha que vuelve a cero por un solo fallo hace que la gente abandone la app.** Un usuario la perdió por un viaje: «back to 0, through no fault of my own», y después dejó la app ([ralferoo, HN](https://news.ycombinator.com/item?id=40903998)). Duolingo, que vive de las rachas, añadió comodines, y al permitir dos a la vez subieron un 0,38 % los usuarios activos diarios ([blog de Duolingo](https://blog.duolingo.com/how-duolingo-streak-builds-habit/)).
10. **Lo que se valora de lo local es poder fiarse de la app, más que sus funciones.** Un usuario lo resume en «It's mine, no company can kill it» ([the_af, HN](https://news.ycombinator.com/item?id=44865237)). Otro elige Todoist pero «I wish it wasn't one someone else's backend» ([pjs_, HN](https://news.ycombinator.com/item?id=44868113)). Si el servicio cierra, «the software stops functioning» ([Ink & Switch](https://www.inkandswitch.com/essay/local-first/)).
11. **Una herramienta que no está a la vista se olvida, y ahí un panel acoplado tiene ventaja.** Un usuario reconoce: «I would forget to check it too, if I hadn't placed a large widget» ([titusjohnson, HN](https://news.ycombinator.com/item?id=44864584)). Otro echa de menos una lista que esté siempre en el mismo sitio y se muestre u oculte con un atajo ([GloriousKoji, HN](https://news.ycombinator.com/item?id=44867694)).
12. **En Sakura, «Rutinas» no es un seguimiento de hábitos.** Es un editor de automatizaciones con frase de activación y pasos (`src/Nexo.Core/Automation/RoutineDefinition.cs`), y en Inicio su tarjeta dice «No hay rutinas configuradas». **Interpretación:** es la fricción clásica de configurar algo antes de sacarle provecho, y probablemente explica buena parte del «no lo uso tanto».

---

## 2. Qué se abandona y por qué

### 2.1 La lista infinita y la culpa

- Juntar pendientes en una lista suma la inquietud de todos: cada pendiente trae «guilt that you should have finished it already» ([tobr, HN](https://news.ycombinator.com/item?id=28033269)).
- La lista acaba asociada a la culpa, y la salida que describe este usuario es tirarlo todo y empezar de cero (lo llama «TODO bankruptcy»). Añade que poder olvidar tareas «is crucial for me» ([setopt, HN](https://news.ycombinator.com/item?id=44873136)).
- Un usuario con TDAH dice que lo que mejor le funciona es un cuaderno de papel: «It works because of the friction of paper and pen», porque así resuelve el atraso acumulado ([Aerbil313, HN](https://news.ycombinator.com/item?id=44878788)).
- En el hilo *Todo apps are meant for robots* la queja central es la misma: se apuntan cosas que «deberíamos hacer algún día» y la lista nunca se poda ([hilo en HN](https://news.ycombinator.com/item?id=28029809)).

### 2.2 Mantener el sistema da más trabajo que hacer las tareas

- «rescheduling or tagging features in most todo apps just ends up being more work» ([mackrevinack, HN](https://news.ycombinator.com/item?id=28035430)).
- Algunos reconocen que probar apps de productividad es otra forma de procrastinar: «trying all sorts of productivity software as a form of procrastination» ([zxexz, HN](https://news.ycombinator.com/item?id=44872132)).
- En el hilo *I tried every todo app and ended up with a .txt file*, con unos 800 comentarios, se repite que el archivo de texto gana por abrirse al instante y no exigir estructura ([hilo en HN](https://news.ycombinator.com/item?id=44864134)). Un comentario lo resume: el archivo obliga a revisar a mano, y «The tool demands that the user manually review, prioritize, and decide what matters» ([imranq, HN](https://news.ycombinator.com/item?id=44877820)).

### 2.3 Rachas que castigan

- Un usuario perdió una racha de más de 1000 días por un viaje sin conexión: «That was it, back to 0, through no fault of my own», y después «just stopped using it entirely» ([ralferoo, HN](https://news.ycombinator.com/item?id=40903998)).
- Según otro, la gamificación con rachas «often cause more problems in building habits than helping them», y su hija con TDAH tuvo que dejar esas apps ([superultra, HN](https://news.ycombinator.com/item?id=40897293)).
- El propio autor de un seguimiento de hábitos lo admite: perder una racha larga «can be quite demotivating» ([pixd, HN](https://news.ycombinator.com/item?id=40904940)).
- Habitica, cuyo diseño castiga las tareas diarias que no se hacen, tuvo que añadir un botón para pausar ese daño («Missed Dailies won't damage you») para rachas malas como una enfermedad o unas vacaciones ([wiki de Habitica](https://habitica.fandom.com/wiki/Rest_in_the_Inn)).

### 2.4 Suscripción, nube y dependencia

- Con un pago único el usuario no queda atado: «You are not hostage. You don't have to upgrade.» ([tjoff, HN](https://news.ycombinator.com/item?id=41615894)).
- También hay matices: un usuario dice que no tiene fatiga de suscripciones sino de apps, «I have "app fatigue"», porque no quiere tener muchas ([jwr, HN](https://news.ycombinator.com/item?id=34955821)).
- La crítica de fondo al modelo en la nube es que, si el servicio cierra, el software deja de funcionar y se pierden los datos ([Ink & Switch](https://www.inkandswitch.com/essay/local-first/)).

### 2.5 El pomodoro que interrumpe

- «The 25 mins jerk me out of flow. I end up procrastinating on starting the timer» ([muzani, HN](https://news.ycombinator.com/item?id=26631010)).
- El registro de interrupciones que propone la técnica casi nadie lo mantiene: «I've never succesfully sustained interruption tracking» ([geoelectric, HN](https://news.ycombinator.com/item?id=26629511)).

**Interpretación.** Los cinco motivos tienen algo en común: la app pide trabajo o hace sentir culpa antes de devolver algo útil. La gente no se va porque falten funciones.

---

## 3. Fricciones concretas por apartado

### 3.1 Tareas de hoy

| Momento | Fricción documentada | Evidencia |
|---|---|---|
| Capturar | Abrir una nota o un formulario y buscar la sección correcta ya se siente como mucho | «It's high friction. I have to go to today's note» ([komali2](https://news.ycombinator.com/item?id=44871614)) |
| Capturar | Sacar el móvil y teclear cuesta más que apuntar en papel | ([thewebguyd](https://news.ycombinator.com/item?id=44865721)) |
| Planificar | Una lista de un kilómetro abruma y resulta irreal | «It’s overwhelming to see a list a mile long» ([Todoist](https://todoist.com/help/articles/plan-your-day-with-the-todoist-today-view-UVUXaiSs)) |
| Planificar | Una tarea sin un siguiente paso claro se aplaza | «You put them off as it's not clear what the next action» ([beermonster](https://news.ycombinator.com/item?id=28034602)) |
| Replanificar | Lo vencido se acumula de un día a otro | «Instead of letting overdue tasks snowball from one day to the next» ([Todoist](https://todoist.com/help/articles/plan-your-day-with-the-todoist-today-view-UVUXaiSs)) |
| Replanificar | Hace falta poder aparcar cosas por un tiempo | «a snooze feature to ignore items for a while» ([mschaef](https://news.ycombinator.com/item?id=44864788)) |
| Replanificar | El arreglo manual que funciona: cada día, lo que quedó sin hacer sube arriba | «Every day I move items I have not yet done to the top of the list.» ([SamCritch](https://news.ycombinator.com/item?id=44864569)) |
| Ejecutar | Solo se puede hacer una cosa a la vez | «i can only work on one thing at a time» ([mackrevinack](https://news.ycombinator.com/item?id=28035430)) |

**Cómo está hoy Sakura** (leído en `src/Nexo.App/Views/TasksView.xaml`): el formulario «Nueva tarea» tiene seis controles (título, notas, fecha, hora, prioridad Baja/Normal/Alta y la casilla de recordatorio), y arriba se muestran los contadores HOY, PENDIENTES y VENCIDAS. **Interpretación:** el contador de VENCIDAS es justo el marcador de culpa de la sección 2.1, y el formulario de seis controles es el tipo de captura que la gente describe como pesada.

### 3.2 Enfoque (pomodoro)

- **Empezar.** El temporizador ayuda sobre todo a vencer el bloqueo inicial: «big tasks get intimidating enough that I have problems starting them». Lo que describe es un compromiso corto con permiso para dejarlo ([geoelectric](https://news.ycombinator.com/item?id=26629511)).
- **Durante la sesión.** La alarma fija rompe la concentración ([muzani](https://news.ycombinator.com/item?id=26631010)). Un usuario prefiere que avise sin obligar a parar: «It creates a lot less frustration as I can finish the task» ([contctlink](https://news.ycombinator.com/item?id=39348994)).
- **El descanso.** Hay quien lo salta sistemáticamente, «every time the alarm goes off hit skip on the 5 break minute timer» ([starrlordxt](https://news.ycombinator.com/item?id=26627803)), y hay quien usa el temporizador solo para acordarse de parar: «I use a pomodoro timer as a break reminder only» ([xyzal](https://news.ycombinator.com/item?id=26626439)).
- **Para qué sirve.** Para algunos, el valor está en volver a preguntarse cada rato qué toca hacer: «Pomo helps me be conscious every half hour about what I want to work on» ([willsmith72](https://news.ycombinator.com/item?id=39349451)).
- **Lo que dice la ciencia.** Un metaanálisis de 22 muestras encontró que las micropausas mejoran algo el vigor y la fatiga (d = .36 y .35). El efecto sobre el rendimiento no fue significativo (d = .16), y cuanto más larga era la pausa, mayor era la mejora ([Albulescu et al., 2022](https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0272460)). **Interpretación:** no hay base para imponer 25/5. Tiene sentido ofrecerlo como opción por defecto y dejar que cada uno lo alargue.
- **Las interrupciones.** La gente compensa las interrupciones trabajando más rápido, «but this comes at a price»: más estrés y más frustración ([Mark, Gudith y Klocke, CHI 2008](https://ics.uci.edu/~gmark/chi08-mark.pdf)).

**Cómo está hoy Sakura:** el historial de enfoque ya guarda `TaskId` (`src/Nexo.Core/Focus/FocusHistoryEntry.cs`) y en cada tarea existe el botón ◎ «Enfocarme en esta tarea». La base técnica para empezar una sesión desde la tarea ya existe.

### 3.3 Hábitos y rutinas

- **Mantener la racha.** El problema es el reinicio a cero ([ralferoo](https://news.ycombinator.com/item?id=40903998)). Para alguien con TDAH, los comodines son «a thing I never knew I needed» ([BolexNOLA](https://news.ycombinator.com/item?id=40897432)).
- **Qué hace la industria.** Duolingo congela la racha un día porque, según un estudio que citan, dar «a little “slack”» motiva más que las reglas rígidas ([Duolingo](https://blog.duolingo.com/how-duolingo-streak-builds-habit/)). Habitica permite pausar el daño ([wiki](https://habitica.fandom.com/wiki/Rest_in_the_Inn)). TickTick mide la racha frente a un objetivo de frecuencia, así que el objetivo puede ser menos que diario, y deja marcar a posteriori un día que se olvidó registrar ([blog de TickTick](https://blog.ticktick.com/2020/12/08/20-lesser-known-ticktick-features/); los detalles de su ayuda oficial no se pudieron leer, ver sección 7).
- **Lo que dice la investigación.** El estudio de referencia sobre formación de hábitos (Lally et al., 2010) informa de que faltar un día no afecta de forma material al proceso, y de que el tiempo hasta automatizar un hábito varió entre 18 y 254 días ([DOI 10.1002/ejsp.674](https://onlinelibrary.wiley.com/doi/abs/10.1002/ejsp.674); la frase exacta no se pudo verificar, ver sección 7).
- **En Sakura, Rutinas no es esto.** Lo que la app llama rutina es una automatización (nombre, frase de activación, pasos y confirmación). Para que sirva, hay que diseñarla antes. **Interpretación:** la sensación de «algo tedioso» encaja con una pantalla que pide configurar antes de devolver nada.

### 3.4 Pantalla de Inicio

- Lo que no está a la vista se olvida ([titusjohnson](https://news.ycombinator.com/item?id=44864584)). Se valora que la lista esté siempre en el mismo sitio y aparezca con un atajo ([GloriousKoji](https://news.ycombinator.com/item?id=44867694)).
- Según las pautas de Microsoft, el panel izquierdo de navegación es adecuado cuando hay «5-10 equally important top-level navigation categories», y la jerarquía debe ser plana: «two levels is ideal» ([WinUI NavigationView](https://learn.microsoft.com/en-us/windows/apps/design/controls/navigationview)).
- **Cómo está hoy Sakura** (`HomeView.xaml`): tiene tres tarjetas de estado (Pendientes con «Nada urgente por ahora», Enfoque con «25 min / Listo para empezar» y Rutinas con «No hay rutinas configuradas»), el acceso «Mirar ahora» y una «Actividad reciente» que solo dura la sesión. **Interpretación:** las tres tarjetas informan, pero no proponen ninguna acción. Si están vacías, Inicio no aporta nada, y la actividad reciente se pierde al cerrar la app.

---

## 4. Qué valora la gente de una app local frente a una de suscripción

| Valor | Evidencia |
|---|---|
| Que nadie pueda quitársela | «It's mine, no company can kill it» y, además, «works offline» ([the_af](https://news.ycombinator.com/item?id=44865237)) |
| Que los datos no dependan de un servidor ajeno | «I wish it wasn't one someone else's backend» ([pjs_](https://news.ycombinator.com/item?id=44868113)) |
| Que siga funcionando si la empresa desaparece | Si el servicio cierra, el software deja de funcionar y se pierden los datos ([Ink & Switch](https://www.inkandswitch.com/essay/local-first/)) |
| No quedar atado a pagos | «You are not hostage» ([tjoff](https://news.ycombinator.com/item?id=41615894)) |
| Privacidad y propiedad | Lo local-first encaja con una filosofía donde «privacy and data ownership are fundamental» ([davepeck](https://news.ycombinator.com/item?id=44473558)) |
| Velocidad | Un gestor en texto plano se sigue usando «because it opens instantly» ([dexterlagan](https://news.ycombinator.com/item?id=44864432)) |

**Lo que se echa de menos sin nube:** sincronizar con el móvil y tener recordatorios fuera del PC. Varios comentarios del hilo del .txt piden alarmas y calendario ([joshmarinacci](https://news.ycombinator.com/item?id=44864700)). Construir la sincronización es difícil, «In practice, it’s hard!» ([davepeck](https://news.ycombinator.com/item?id=44473558)).

**Interpretación para Sakura:** funcionar sin cuenta, sin servidor y sin telemetría ya es un argumento de venta, y conviene decirlo en la propia interfaz (por ejemplo, «Tus datos están en este PC») y no solo en la página de privacidad. La carencia que más se va a notar es no tener el móvil. Si algún día se aborda, lo más coherente es exportar o importar un archivo, no montar una cuenta.

---

## 5. Patrones de diseño que responden a cada fricción

| Fricción | Patrón | App real y documentación |
|---|---|---|
| Lista infinita y culpa | «Hoy» vacío cada mañana, con lo pendiente como sugerencia | Microsoft To Do, *My Day* ([doc](https://support.microsoft.com/en-us/office/my-day-and-suggestions-fc09a1b9-0854-4906-b166-f480ee97a139)) |
| Planificación irreal | Tope de N tareas importantes (3 en P1) | Todoist ([doc](https://todoist.com/help/articles/plan-your-day-with-the-todoist-today-view-UVUXaiSs)) |
| Planificación irreal | Aviso de capacidad frente a la hora de cierre | Sunsama ([doc](https://help.sunsama.com/docs/usage-guides/daily-planning/)) |
| Replanificar | Revisión guiada de lo de ayer al empezar, con opciones de hecho, otro día o *backlog* | Sunsama, *Daily Planning* y *Shutdown* ([doc](https://help.sunsama.com/docs/usage-guides/daily-planning/), [ritual](https://www.sunsama.com/features/daily-planning-and-shutdown)) |
| Replanificar | «Posponer» de un solo gesto, presentado sin culpa, con la meta de cerrar el día en cero | Todoist, *Todoist Zero* ([doc](https://todoist.com/help/articles/plan-your-day-with-the-todoist-today-view-UVUXaiSs)) |
| Capturar | Atajo global y ventana mínima que devuelve el foco a lo que se estaba haciendo | Things, *Quick Entry* ([doc](https://culturedcode.com/things/support/articles/2249437/)); Todoist, *Quick Add* global ([doc](https://www.todoist.com/help/articles/use-task-quick-add-in-todoist-va4Lhpzz)) |
| Empezar a enfocarse | Iniciar la sesión desde la tarea, con estimación opcional | TickTick, *Start Focus* ([blog](https://blog.ticktick.com/2020/12/08/20-lesser-known-ticktick-features/)) |
| Pomodoro rígido | Duración ajustable en el momento; avisar sin cortar | TickTick ([blog](https://blog.ticktick.com/2020/12/08/20-lesser-known-ticktick-features/)); variante con avisos ([contctlink](https://news.ycombinator.com/item?id=39348994)); técnica *Flowtime*, citada en [HN](https://news.ycombinator.com/item?id=39348500) |
| Racha rota | Comodines o congelación de racha | Duolingo ([blog](https://blog.duolingo.com/how-duolingo-streak-builds-habit/)); un seguimiento de hábitos con comodines automáticos ([Show HN](https://news.ycombinator.com/item?id=40893866)) |
| Racha rota | Pausa por viaje o enfermedad | Habitica, *Pause Damage* ([wiki](https://habitica.fandom.com/wiki/Rest_in_the_Inn)) |
| Racha rota | Objetivo por frecuencia (por ejemplo, 3 veces por semana) en lugar de diario | TickTick (ver sección 7) |
| Herramienta olvidada | Presencia fija y atajo para mostrarla u ocultarla | Pedido explícitamente en [HN](https://news.ycombinator.com/item?id=44867694) |
| Navegación | Entre 5 y 10 secciones en panel izquierdo, dos niveles como máximo | Microsoft WinUI ([doc](https://learn.microsoft.com/en-us/windows/apps/design/controls/navigationview)) |

---

## 6. Recomendaciones para Sakura

Todo lo que sigue es **interpretación** apoyada en las secciones anteriores. Cada punto indica en qué evidencia se basa.

### 6.1 Hoy

**Quitar**
- **El contador «VENCIDAS».** Es un marcador de culpa (§2.1). Lo vencido debe aparecer una sola vez, en la revisión de la mañana, y no quedarse como número rojo permanente.
- **Los seis controles al crear una tarea.** La captura debe ser una sola línea. La fecha, la hora y el recordatorio se deducen del texto (el repo ya tiene `SpanishTaskCommandParser`) y solo se editan si hace falta (§3.1).
- **La prioridad Baja/Normal/Alta.** Conviene cambiarla por una sola marca, «Importante hoy», con un tope (ver «Añadir»). Una escala de tres niveles es otra decisión en cada captura, mientras que «¿cuáles son tus 3 de hoy?» es una sola decisión al día (Todoist, §5).

**Añadir**
- **Revisión de la mañana de menos de un minuto**, la primera vez que se abre Sakura en el día. Enseña lo que quedó de ayer, tarea por tarea, con tres botones: *Hoy*, *Otro día* y *Soltar*. «Soltar» archiva la tarea sin reproche (Sunsama y To Do, §5; Masicampo y Baumeister, §1.4).
- **Hoy con tope.** Máximo 3 tareas marcadas como importantes, y un aviso amable si se intentan añadir más de unas 5 en total (Todoist y Sunsama).
- **Posponer a mañana con un solo clic** en cada tarea. Si una tarea se pospone tres veces, Sakura pregunta si se divide, se suelta o se deja para «algún día» (§3.1, «next action»).
- **Captura global.** Un atajo que abre una línea flotante, guarda con Enter y devuelve el foco a la ventana anterior (Things y Todoist). Hay que elegir una combinación que no choque con los atajos que Sakura ya registra (Alt+A, Alt+Shift+A, Alt+V, Ctrl+Shift+Espacio, Ctrl+Shift+D, Alt+Shift+S y Alt+Shift+G, en `MainWindow.xaml.cs` y `MainWindow.Capture.cs`).
- **Cierre del día opcional**, un «¿qué se queda para mañana?» por la tarde, para que al día siguiente la lista empiece limpia (Sunsama *Shutdown*, Todoist Zero).

**Cambiar**
- **Hoy empieza vacío cada día.** Lo no hecho pasa a sugerencias, no a Hoy (Microsoft To Do).
- **Las pestañas Hoy/Pendientes/Hechas.** Pendientes debería funcionar como un almacén que no se muestra por defecto. Hechas puede quedarse como registro de logros, porque la gente valora ver lo que ha tachado: «behold the ever growing list of struck through items» ([ummonk, HN](https://news.ycombinator.com/item?id=28031553)).

### 6.2 Enfoque

**Quitar**
- **El registro de interrupciones**, si existe o está planeado. Casi nadie lo mantiene (§2.5).
- **Tipos de sesión que nadie elige** (`Focus`, `Study`, `Break`, `Custom` en `FocusSessionKind`). Como decisión previa al arranque, sobra: basta con «enfocarme en…» más una duración.

**Añadir**
- **Empezar desde la tarea como camino principal.** El botón ◎ ya existe. Debe ser el gesto más visible de cada tarea de Hoy y arrancar sin pedir nada más (§3.2; TickTick).
- **Modo «solo 5 minutos»** para cuando cuesta empezar, con salida sin culpa al terminarlos: «¿sigo o lo dejo?» (geoelectric, §3.2).
- **Aviso sin corte al terminar el tiempo.** Por defecto, Sakura avisa y ofrece «sigo 10 más» o «pausa», sin detener nada (muzani y contctlink).
- **Al cerrar la sesión, una sola pregunta:** «¿Terminaste la tarea?», con Sí (la marca como hecha) o Todavía no (la deja en Hoy). Así Enfoque y Hoy quedan conectados sin pasos extra.

**Cambiar**
- **Los 25 minutos pasan a ser una sugerencia.** La app recuerda la última duración usada por el usuario (la evidencia no respalda imponer 25/5, §3.2).
- **La tarjeta de Inicio** deja de decir «25 min / Listo para empezar» en abstracto y muestra la próxima tarea de Hoy con el botón «Empezar».

### 6.3 Rutinas

Esta es la recomendación más fuerte del documento.

- **Separar dos conceptos que hoy comparten nombre.** Una cosa son los hábitos, que el dueño describe («Rutinas (hábitos)»), y otra las automatizaciones, que es lo que el código implementa (`RoutineDefinition`, con frase de activación y pasos). Las automatizaciones deberían llamarse «Atajos» o «Automatizaciones» y vivir en un lugar secundario, porque son una herramienta de usuario avanzado que exige configurar antes de aportar (§3.3).
- **Si se añaden hábitos, que sean ligeros:**
  - Crearlos con una línea («leer 10 minutos, 3 veces por semana»), con el objetivo por frecuencia como opción por defecto (TickTick).
  - Rachas con comodines automáticos, por ejemplo uno por semana (Duolingo y el Show HN de §5), y un botón de pausa por viaje o enfermedad (Habitica).
  - Mostrar «días cumplidos este mes» junto a la racha, o en su lugar, para que un fallo no borre el progreso visible (ralferoo, Lally).
  - Marcar el hábito desde Hoy. Los hábitos del día aparecen como casillas dentro de Hoy y no obligan a ir a otro apartado (§3.4, lo que no está a la vista se olvida).
- **Qué quitar:** la tarjeta de Inicio que dice «No hay rutinas configuradas». Recuerda algo pendiente de configurar sin dar ningún valor.
- **Plantillas en vez de un editor en blanco.** Si las automatizaciones se quedan, deberían ofrecer 3 o 4 ya hechas («Empezar a trabajar», «Modo estudio») que se activan con un clic.

### 6.4 Inicio

- **Una sola tarjeta principal, «Ahora»**, que responda qué hacer en este momento: la próxima tarea importante de Hoy con el botón «Empezar» (Enfoque) o, si Hoy está vacío, «Planifica tu día (1 min)» (revisión de la mañana). Esto coincide con la tarjeta «Ahora» que ya está en el roadmap.
- **Quitar o reducir las tres tarjetas de estado** (Pendientes, Enfoque y Rutinas) a una línea de resumen como «2 de 3 hechas · 45 min de enfoque hoy». Hoy solo informan y, cuando están vacías, ocupan espacio sin aportar nada.
- **«Actividad reciente» de solo esta sesión.** O se guarda (en local) como «lo que hiciste hoy», que refuerza la sensación de avance, o se quita.
- **Hacer visible la privacidad** con una línea discreta tipo «Todo se guarda en este PC» (§4).
- **Mantener la barra lateral dentro de 5 a 10 secciones con dos niveles como máximo** (WinUI). Si Hoy, Enfoque y hábitos acaban unidos en un solo flujo diario, Sakura gana espacio para Audio, Captura y Sistema sin superar ese rango.

### 6.5 Prioridades sugeridas, de más a menos impacto

1. Hoy empieza vacío cada día, con revisión de la mañana y el botón Soltar.
2. Captura en una línea con atajo global.
3. Tarjeta «Ahora» con «Empezar» (enfoque desde la tarea).
4. Separar hábitos de automatizaciones.
5. Temporizador flexible y modo de 5 minutos.
6. Rachas con comodines y pausa.

---

## 7. Lo que no se pudo verificar

- **Reddit** (r/productivity, r/ADHD, r/getdisciplined, r/todoist, r/ticktick, r/ObsidianMD, r/Notion y r/Windows): no se pudo leer ningún hilo. Reddit bloquea la descarga automática y la herramienta de lectura no tiene acceso al dominio. El buscador tampoco devolvió hilos de Reddit, solo fichas de la App Store. Por eso la voz de la comunidad sale sobre todo de Hacker News, cuyo público es más técnico que el de Sakura. **Conviene validar las conclusiones con usuarios no técnicos.**
- **Ayuda oficial de TickTick** (help.ticktick.com): se genera con JavaScript y llegó vacía. Los objetivos por frecuencia y el registro de días olvidados solo se conocen por el resumen del buscador y por el blog oficial de TickTick. El blog sí se leyó, pero no detalla la frecuencia semanal.
- **Lally et al. (2010)**: Wiley y PhilPapers devolvieron 403, y la API de Semantic Scholar oculta el resumen. La frase de que faltar un día no afecta de forma material, y el rango de 18 a 254 días, salen del resumen tal como lo muestran los buscadores. Encajan con cómo se cita el estudio, pero no se contrastaron con el PDF.
- **Apple HIG (barras laterales)**: la página se genera con JavaScript y su API de datos devolvió 404. No se incluye ninguna pauta de Apple.
- **Artículo de Todoist sobre posponer y reprogramar**: la página cargó sin el cuerpo del texto. El patrón de posponer se documenta con el artículo de la vista Hoy, que sí se leyó completo.
- **Foros oficiales de Todoist, Notion y Habitica**: no se consultaron. Todoist ya no tiene un foro público propio, y los de Notion y Habitica no se exploraron por falta de tiempo. El foro de Obsidian es accesible (su API de Discourse responde), pero no se usó.
- **Datos de uso reales de Sakura**: no existen, porque la app no tiene telemetría. Qué apartado usa más el dueño es una suposición. La forma de comprobarlo sin romper la promesa de privacidad sería preguntarle directamente o guardar un contador local que solo vea él.
- **Hilos de HN que salieron vacíos en la API de Algolia**: *Show HN: I built a todo app that only shows what you're doing this week* ([45276237](https://news.ycombinator.com/item?id=45276237)) y *Show HN: I built a habit tracker that doesn't shame you for missing a day* ([46840609](https://news.ycombinator.com/item?id=46840609)) devolvieron uno o ningún comentario, así que no se citan como evidencia.
