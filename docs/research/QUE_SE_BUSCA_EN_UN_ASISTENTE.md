# Qué busca la gente en un asistente de escritorio para Windows (y qué la echa para atrás)

Investigación hecha el 16 de septiembre de 2026 para decidir qué le falta y qué le sobra a Sakura como asistente de escritorio en general. El trabajo sobre tareas, enfoque y hábitos ya está en `PRODUCTIVIDAD_QUE_PIDE_LA_GENTE.md` y aquí no se repite.

**Cómo leer este documento.** Las citas entre comillas son textuales, de 15 palabras como máximo, y se dejan en su idioma original. Cada una lleva el enlace del comentario, del issue o de la página de la que sale. Los comentarios de Hacker News se leyeron a través de la API pública de Algolia (`hn.algolia.com/api/v1/items/<id>`), y cada enlace apunta al comentario exacto. Los issues de GitHub se ordenaron por votos con la API de búsqueda de GitHub. Los bloques marcados **Interpretación** son conclusiones mías y no salen de ninguna fuente.

**Límites desde el principio:**

- **Reddit no se pudo leer.** La API devolvió 403 y la versión antigua redirige al inicio de sesión, así que las voces de la comunidad salen de Hacker News, de GitHub y de la documentación oficial.
- **Hacker News tiene sesgo técnico.** Sus usuarios desconfían más de Microsoft y de la IA que el público general. Lo compenso con documentación oficial y con la encuesta de Stack Overflow, pero conviene tenerlo presente.

---

## 1. Resumen ejecutivo

1. **Lo primero que pregunta la gente ante una función de IA es si se puede apagar.** Sobre Recall, un usuario resume así lo que le dicen personas de todas las edades: «I can turn that off, right?» ([ToucanLoucan, HN](https://news.ycombinator.com/item?id=40445335)). Microsoft acabó haciendo Recall opcional y desinstalable ([Windows Experience Blog, sept. 2024](https://blogs.windows.com/windowsexperience/2024/09/27/update-on-recall-security-and-privacy-architecture/)).
2. **Apagarla no basta: la gente quiere poder quitarla.** Un usuario lo explica así: «If I can only disable it, I can't have that trust» ([JohnFen, HN](https://news.ycombinator.com/item?id=41447768)). Otro pide un único interruptor global de «Disable AI» fácil de encontrar ([noir_lord, HN](https://news.ycombinator.com/item?id=47753496)).
3. **Guardar la información en local no convence por sí solo.** Un archivo que lo guarda todo es un botín si alguien entra en el equipo: el atacante tendría «a video record of the last 3 months of your life» ([logrot, HN](https://news.ycombinator.com/item?id=40447336)). Microsoft respondió con Windows Hello, cifrado con TPM y filtro de datos sensibles activado de serie ([blog de Windows](https://blogs.windows.com/windowsexperience/2024/09/27/update-on-recall-security-and-privacy-architecture/)).
4. **Meter IA donde nadie la pidió se percibe como hinchazón, y Microsoft ha tenido que dar marcha atrás.** En marzo de 2026 anunció que reduciría «unnecessary Copilot entry points» en la Herramienta Recortes, Fotos, Widgets y el Bloc de notas ([Windows Insider Blog](https://blogs.windows.com/windows-insider/2026/03/20/our-commitment-to-windows-quality/)). Sobre el Bloc de notas con IA: «Notepad and plain text is the last place I want AI» ([sethammons, HN](https://news.ycombinator.com/item?id=42074464)).
5. **Microsoft reserva lo mejor que ofrece en cualquier aplicación a los Copilot+ PC, y eso deja un hueco enorme.** Click to Do resume y reescribe cualquier texto de la pantalla en local, pero exige una NPU de 40 TOPS y 16 GB de RAM ([Microsoft Support](https://support.microsoft.com/en-us/windows/click-to-do-do-more-with-what-s-on-your-screen-6848b7d5-7fb0-4c43-b08a-443d6d3f5955)). Es la función que más se echa de menos en Windows: herramientas de escritura que funcionen en todo el sistema y no dentro de cada app ([_1tem, HN](https://news.ycombinator.com/item?id=42077005)).
6. **Claude Desktop tampoco da en Windows la entrada rápida con captura y dictado.** Anthropic lo dice sin rodeos: «Quick entry is currently only available for macOS users» ([Claude Help Center](https://support.claude.com/en/articles/12626668-use-quick-entry-with-claude-desktop-on-mac)). Sakura ya tiene en Windows las tres piezas: atajo global, Lens y dictado.
7. **De los asistentes de voz se usa muy poco, y casi siempre lo mismo.** Un usuario de Alexa dice que la usa «only [...] for timers, weather, and controlling lights» ([jccalhoun, HN](https://news.ycombinator.com/item?id=43383089)). Un antiguo empleado de Alexa cuenta solo cuatro usos: tiempo, domótica, música y temporizadores ([nblgbg, HN](https://news.ycombinator.com/item?id=33597991)). La voz tiene que clavar lo básico, sin sorpresas.
8. **Una aplicación de escritorio que solo envuelve la web no convence.** A Claude Desktop le reprocharon ser una app Electron que «opens slower than the webpage and flashes white» ([whywhywhywhy, HN](https://news.ycombinator.com/item?id=42009938)), y otro usuario escribe: «The only real benefit is the hotkey» ([wifipunk, HN](https://news.ycombinator.com/item?id=42009384)). La app de escritorio se justifica por lo que puede hacer en el sistema ([stephencoyner, HN](https://news.ycombinator.com/item?id=42009783)).
9. **Consumir recursos sin avisar genera quejas públicas.** Un issue con 76 reacciones denuncia que Claude Desktop arranca una máquina virtual de 1,8 GB aunque solo se use el chat ([anthropics/claude-code#29045](https://github.com/anthropics/claude-code/issues/29045)). En el hilo, alguien escribe «I won't understand why Cowork isn't simply opt-in» ([tom1337, HN](https://news.ycombinator.com/item?id=48480172)).
10. **La gente no se fía de la precisión de los agentes ni de lo que hacen con sus datos.** Según la encuesta de Stack Overflow de 2025, al 87 % le preocupa la precisión de los agentes de IA y al 81 % la seguridad y privacidad de sus datos. La queja más citada, con un 66 %, son las respuestas «almost right, but not quite» ([Stack Overflow Developer Survey 2025](https://survey.stackoverflow.co/2025/ai)).
11. **Para que un asistente pueda actuar sobre el equipo, la industria ya pide aprobación, registro y mínimo privilegio.** Microsoft escribe que en sus funciones agénticas «Users approve all queries for user data as well as actions taken». Pide además un registro de auditoría a prueba de manipulación, y la función viene desactivada de serie ([Microsoft Support](https://support.microsoft.com/en-us/windows/experimental-agentic-features-a25ede8a-e4c2-4841-85a8-44839191dfb3)). Es casi el mismo modelo que Sakura ya tiene (D16/D17).
12. **Tras una acción destructiva hace falta poder recuperar lo perdido, no solo haber pedido permiso antes.** Un usuario creó una herramienta después de que un LLM «nuked my local SQLite DB file» ([marc92, HN](https://news.ycombinator.com/item?id=47689553)). Open Interpreter se ganó el recelo por esto mismo: «if I ask it to delete my home folder [...] it will delete it» ([chazeon, HN](https://news.ycombinator.com/item?id=38246162)).
13. **Nadie resuelve bien la búsqueda local de archivos por significado.** Uno escribe que lo difícil es una búsqueda semántica «high quality, fast, offline» que no envíe los archivos a ningún servidor ([articulatepang, HN](https://news.ycombinator.com/item?id=46026187)). Otro, que «personal search engines are not a solved problem» ([TheTaytay, HN](https://news.ycombinator.com/item?id=46088341)). Hoy Sakura no busca archivos.
14. **PowerToys Command Palette, gratis y de Microsoft, ya cubre buena parte de lo que Sakura presenta como propio.** Lanza aplicaciones, busca archivos, guarda el historial del portapapeles, muestra métricas del sistema y tiene una barra en el borde de la pantalla que se oculta sola ([Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)). Sakura tiene que diferenciarse por otro lado.
15. **Lo que no sea útil, que no interrumpa.** En PowerToys, «Disable focus stealing» sigue abierta desde 2019 y es una de las peticiones más votadas ([microsoft/PowerToys#65](https://github.com/microsoft/PowerToys/issues/65)). A Alexa le reprochan los «did you know» con los que intenta vender algo ([joshstrange, HN](https://news.ycombinator.com/item?id=40010809)).

---

## 2. Qué quiere delegar la gente en un asistente de escritorio

### 2.1 Lo que las propias empresas han decidido construir (una señal de la demanda, no una prueba)

- **Cambiar ajustes de Windows hablando con naturalidad.** Microsoft creó un modelo local, Mu, para el agente de Configuración. Traduce la petición a la función de ajustes correspondiente y responde en menos de 500 ms. La razón que da es que hay cientos de ajustes difíciles de encontrar ([Windows Experience Blog, junio 2025](https://blogs.windows.com/windowsexperience/2025/06/23/introducing-mu-language-model-and-how-it-enabled-the-agent-in-windows-settings/)). Aprendieron algo útil para Sakura: el modelo acierta más con frases de varias palabras que con órdenes cortas o incompletas (misma fuente).
- **Hacer algo con lo que hay en pantalla.** Click to Do ofrece resumir, hacer una lista, reescribir en tono informal, formal o pulido, buscar en la web y actuar sobre imágenes, con Win+Q o Win+clic ([Microsoft Support](https://support.microsoft.com/en-us/windows/click-to-do-do-more-with-what-s-on-your-screen-6848b7d5-7fb0-4c43-b08a-443d6d3f5955)).
- **Voz, visión y acciones sobre archivos locales.** En octubre de 2025 Microsoft anunció «Hey Copilot», Copilot Vision, Copilot Actions sobre archivos locales y conectores con Outlook y Google ([Windows Experience Blog](https://blogs.windows.com/windowsexperience/2025/10/16/making-every-windows-11-pc-an-ai-pc/)). Afirma que con voz la gente usa Copilot «twice as much as when they use text» (misma fuente; es un dato de la empresa y no se puede comprobar).
- **Un compañero con atajo para preguntar sin cambiar de ventana.** ChatGPT para Windows abre una ventana compañera con Alt+Espacio, donde se puede preguntar, subir un archivo o empezar una conversación (resultado de búsqueda de [OpenAI Help Center](https://help.openai.com/en/articles/9982051-using-the-chatgpt-windows-app); la página devolvió 403 al leerla directamente). Claude en Mac hace lo mismo y añade captura por región, captura de ventana y dictado ([Claude Help Center](https://support.claude.com/en/articles/12626668-use-quick-entry-with-claude-desktop-on-mac)).
- **Un lanzador que haga de todo.** Raycast para Windows trae de serie búsqueda de archivos con un indexador propio, historial del portapapeles, fragmentos de texto que se expanden al escribir, gestión de ventanas e IA ([blog de Raycast](https://www.raycast.com/blog/raycast-for-windows)). Command Palette ofrece algo muy parecido ([Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)).

### 2.2 Lo que pide la comunidad

- **Buscar archivos rápido y sin depender de la búsqueda de Windows.** En los hilos sobre lanzadores se recomienda una y otra vez Everything de voidtools ([the_cramer, HN](https://news.ycombinator.com/item?id=40903054); [stronglikedan, HN](https://news.ycombinator.com/item?id=46025527)). Otros dicen que ir rápido a archivos y carpetas les importa más que lanzar aplicaciones ([oezi, HN](https://news.ycombinator.com/item?id=40902819)).
- **Un historial del portapapeles en el que se pueda buscar.** Muchos instalan Ditto porque Win+V no lo permite: «The Windows clipboard manager doesn't support search» ([MiddleEndian, HN](https://news.ycombinator.com/item?id=29814759)).
- **Expandir fragmentos de texto.** «Text Replacement / expander» tiene 142 votos en PowerToys y sigue abierta desde 2020 ([microsoft/PowerToys#5074](https://github.com/microsoft/PowerToys/issues/5074)).
- **Reescribir y corregir texto en cualquier aplicación.** Hay quien lo prueba en macOS y lo encuentra útil hasta en TextEdit y Stickies ([evilduck, HN](https://news.ycombinator.com/item?id=42080088)). En Windows se pregunta por qué no es algo de todo el sistema ([_1tem, HN](https://news.ycombinator.com/item?id=42077005)).
- **Integraciones que hagan algo de verdad, no solo resúmenes.** Un usuario de Copilot en el trabajo echa de menos crear reuniones a partir de un hilo o crear tareas, y dice que los resúmenes de correo le dan igual ([solsane, HN](https://news.ycombinator.com/item?id=45622955)).
- **Voz para lo básico.** Temporizadores, recordatorios, el tiempo y encender o apagar dispositivos ([thinbeige, HN](https://news.ycombinator.com/item?id=15349868); [joshstrange, HN](https://news.ycombinator.com/item?id=40010809)).
- **Usar la IA con la clave propia.** Sobre Claude Desktop: «I would consider using this if I could just shove my API key in» ([drpossum, HN](https://news.ycombinator.com/item?id=42011150)).
- **Exportar conversaciones.** Lo piden a Anthropic ([becquerel, HN](https://news.ycombinator.com/item?id=42010470)) y a Jan ([janhq/jan#6195](https://github.com/janhq/jan/issues/6195)).
- **Conectar herramientas por MCP en los clientes locales.** Es de lo más votado en LM Studio, con 171 votos ([lmstudio-bug-tracker#365](https://github.com/lmstudio-ai/lmstudio-bug-tracker/issues/365)), y aparece en la hoja de ruta de Jan ([janhq/jan#4824](https://github.com/janhq/jan/issues/4824)).

> **Interpretación.** Lo que la gente quiere delegar se divide en dos grupos. Uno son las **acciones pequeñas, frecuentes y deterministas**: un temporizador, un ajuste, abrir algo, buscar un archivo, pegar algo de antes. Ahí manda la velocidad y la IA sobra. El otro es **trabajar con el texto que ya tiene delante**: reescribir, traducir, resumir o explicar. Ahí la IA sí aporta, pero solo si llega adonde está el texto, sin copiar y pegar en un chat. Lo que casi nadie pide con entusiasmo es un chat genérico más.

---

## 3. Qué decepcionó de los asistentes conocidos y por qué

### 3.1 Cortana

- **Qué pasó:** Microsoft retiró la app independiente de Cortana en la primavera de 2023 y remite a Acceso por voz y a Copilot ([Microsoft Support](https://support.microsoft.com/en-us/topic/end-of-support-for-cortana-d025b39f-ee5b-4836-a954-0ab646ee1efa)). No explica por qué la retiró.
- **Quejas repetidas:** la búsqueda del menú Inicio integrada con Cortana fallaba en lo más básico, «It can't even fuzzy search». Además mandaba a Bing en Edge aunque ninguno de los dos fuera el predeterminado ([eterm, HN](https://news.ycombinator.com/item?id=15759236)). Otros cuentan que con cada letra saltaba entre resultados distintos ([monochromatic, HN](https://news.ycombinator.com/item?id=15759721)) y que no había forma de desactivarla ([neves, HN](https://news.ycombinator.com/item?id=15759403)).
- **Qué enseña:** una búsqueda local lenta o que desvía al buscador web de la empresa destruye la confianza en todo lo demás. Todavía en 2022 un usuario se quejaba de que Windows se come las primeras letras al teclear en Inicio ([rewgs, HN](https://news.ycombinator.com/item?id=34108195)).

### 3.2 Copilot en Windows

- **Qué prometía:** voz con «Hey Copilot», visión, acciones sobre archivos locales y acceso desde la barra de tareas ([blog de Windows, oct. 2025](https://blogs.windows.com/windowsexperience/2025/10/16/making-every-windows-11-pc-an-ai-pc/)).
- **Quejas repetidas:**
  - **Está en todas partes sin que nadie lo pida.** Microsoft acabó reconociéndolo al reducir sus puntos de entrada ([Windows Insider Blog](https://blogs.windows.com/windows-insider/2026/03/20/our-commitment-to-windows-quality/)). Cuando cambió el nombre del ajuste de IA del Bloc de notas por «Advanced features», los usuarios lo vieron como una forma de esconderlo ([saghm, HN](https://news.ycombinator.com/item?id=47753420)). Lo que piden es que venga desactivado: «use it or don't, it's your computer» ([BizarroLand, HN](https://news.ycombinator.com/item?id=47756522)).
  - **No sirve para lo útil.** «Truly a hammer looking for nails scenario», con un chat que no crea reuniones ni tareas ([solsane, HN](https://news.ycombinator.com/item?id=45622955)).
  - **Una tecla robada.** La tecla Copilot sustituye al Ctrl derecho y, según varios usuarios, no se puede volver a convertir del todo en una tecla modificadora ([mrandish, HN](https://news.ycombinator.com/item?id=46548338); [SomeHacker44, HN](https://news.ycombinator.com/item?id=46551752)).
  - **Recursos.** Según la prensa, la nueva app de Copilot incluye una copia completa de Edge y consume más RAM (hilo de HN: [47656146](https://news.ycombinator.com/item?id=47656146); la fuente es Windows Latest, no Microsoft).
  - **Privacidad.** Del Bloc de notas con IA se dijo que «Sends everything you write to Microsoft» ([EVa5I7bHFq9mnYK, HN](https://news.ycombinator.com/item?id=42074352)), y otro usuario recuerda que esas funciones se ejecutan en Azure ([Topfi, HN](https://news.ycombinator.com/item?id=47753478)).

### 3.3 Windows Recall

- **Qué prometía:** buscar todo lo que uno ha visto en el equipo mediante capturas periódicas.
- **Qué pasó:** después de las críticas, Microsoft lo hizo opcional, exigió Windows Hello para activarlo y para consultarlo, y retrasó el lanzamiento para probarlo antes con Windows Insider ([blog de Windows, junio 2024](https://blogs.windows.com/windowsexperience/2024/06/07/update-on-the-recall-preview-feature-for-copilot-pc/)). Más tarde añadió un enclave VBS, cifrado con TPM, filtro de datos sensibles activado de serie, exclusión de apps y webs, borrado por rangos de tiempo y la opción de quitarlo por completo: «remove Recall entirely» ([blog de Windows, sept. 2024](https://blogs.windows.com/windowsexperience/2024/09/27/update-on-recall-security-and-privacy-architecture/)).
- **Miedos concretos:**
  - Que capture contraseñas visibles: guarda una segunda copia fuera del gestor de contraseñas ([ImAnAmateur, HN](https://news.ycombinator.com/item?id=40445597)).
  - Que el contenido con DRM quede protegido y las contraseñas no ([notaustinpowers, HN](https://news.ycombinator.com/item?id=40445595)).
  - Que un intruso, la pareja o un técnico de reparación accedan a todo ([hn8305823, HN](https://news.ycombinator.com/item?id=40445761)).
  - Que lo que hoy es local pase a la nube en una actualización futura ([cesarb, HN](https://news.ycombinator.com/item?id=40446248); [gryn, HN](https://news.ycombinator.com/item?id=40445812)).
  - Que una actualización vuelva a activar lo que el usuario apagó ([noirbot, HN](https://news.ycombinator.com/item?id=41447622); [SirMaster, HN](https://news.ycombinator.com/item?id=41448280)).
- **Rechazo de la idea en sí, no solo de Microsoft.** Ante una propuesta de «una IA que recuerda todo lo que has hecho en el PC», la respuesta fue que, aunque sea local y de código abierto, «the whole candy store is robbed» si alguien entra en el equipo ([JohnFen, HN](https://news.ycombinator.com/item?id=45669042)). Otro escribió «My limited, imperfect human memory is a feature, not a bug» ([lucideng, HN](https://news.ycombinator.com/item?id=45672607)). No todo el mundo piensa así: hay usuarios de Rewind en Mac a quienes la idea les gusta ([CSDude, HN](https://news.ycombinator.com/item?id=40445474)).

### 3.4 Raycast, PowerToys Run / Command Palette y Flow Launcher

- **Raycast para Windows** llegó en 2025. Las críticas: hacen falta más pasos que en Flow Launcher, porque a menudo hay que entrar en una extensión para terminar la acción ([Adrig, HN](https://news.ycombinator.com/item?id=46025173)); se nota menos fluido que PowerToys ([weebao, HN](https://news.ycombinator.com/item?id=46025418)); y hay rechazo a las suscripciones y a la IA metida en un lanzador ([DoctorOW, HN](https://news.ycombinator.com/item?id=46025109)). Lo más echado en falta en la beta fue la gestión de ventanas ([mario_lopez, HN](https://news.ycombinator.com/item?id=46025116)).
- **PowerToys Run** tuvo esta pregunta recurrente: ¿qué aporta frente a pulsar Win y escribir? ([raincole, HN](https://news.ycombinator.com/item?id=40903442); [prmoustache, HN](https://news.ycombinator.com/item?id=40903436)). Microsoft lo sustituyó por Command Palette, que ahora hace mucho más ([Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)).
- **Flow Launcher** recibe sobre todo quejas técnicas: un issue abierto denuncia que dispara cientos de eventos seguidos y hace fallar a otras aplicaciones ([Flow.Launcher#3817](https://github.com/Flow-Launcher/Flow.Launcher/issues/3817)). Sus peticiones más votadas son usar la tecla Win como atajo ([#662](https://github.com/Flow-Launcher/Flow.Launcher/issues/662)) y exportar e importar ajustes ([#2643](https://github.com/Flow-Launcher/Flow.Launcher/issues/2643)).

### 3.5 ChatGPT para Windows y Claude Desktop

- **Envolver la web no basta.** Claude Desktop recibió el reproche de ser una app Electron más lenta que la web ([whywhywhywhy, HN](https://news.ycombinator.com/item?id=42009938)), y alguien la comparó con la PWA de Safari: la PWA usaba menos RAM ([Nesco, HN](https://news.ycombinator.com/item?id=42009915)). La app nativa de ChatGPT en Mac se describió como «buggy and feature-incomplete compared to the web version» ([pants2, HN](https://news.ycombinator.com/item?id=42012178)).
- **Windows recibe menos funciones.** La entrada rápida de Claude, con captura, ventana y dictado, es solo para macOS ([Claude Help Center](https://support.claude.com/en/articles/12626668-use-quick-entry-with-claude-desktop-on-mac)), y la ayuda de OpenAI sobre «Work with Apps» también se titula «on macOS» ([OpenAI Help Center](https://help.openai.com/en/articles/10119604-work-with-apps-on-macos); no se pudo leer entera).
- **Peso y funciones que se imponen.** Una máquina virtual de 1,8 GB en cada arranque ([anthropics/claude-code#29045](https://github.com/anthropics/claude-code/issues/29045)), y otro usuario habla de un paquete de unos 10 GB que no se puede quitar ([tom1337, HN](https://news.ycombinator.com/item?id=48480172)). En abril de 2026 salió un hilo sobre un «native messaging bridge» que la app instalaba sin avisar ([HN 47880697](https://news.ycombinator.com/item?id=47880697); no leí el artículo original).
- **Datos.** Sobre la app de ChatGPT para Windows, lo primero que se comentó fue qué hace OpenAI con los archivos que se suben ([1970-01-01, HN](https://news.ycombinator.com/item?id=41872810)).

### 3.6 Siri y Alexa

- **Promesas sin cumplir.** Apple retrasó la Siri «más personal», capaz de actuar dentro de las apps, y lo reconoció así: «It's going to take us longer than we thought» (comunicado citado por [TechRadar](https://www.techradar.com/computing/artificial-intelligence/apple-officially-delays-the-ai-infused-siri-and-admits-its-going-to-take-us-longer-than-we-thought) y [Daring Fireball](https://daringfireball.net/2025/03/apple_is_delaying_the_more_personalized_siri_apple_intelligence_features); no encontré el texto en una web de Apple).
- **Uso real muy estrecho.** Lo que más se usa son los temporizadores, y se usan poco más, en parte por la «limited capability to understand» ([pflenker, HN](https://news.ycombinator.com/item?id=43186927)). Con el tiempo pasa la novedad y queda solo el tiempo y los temporizadores ([askafriend, HN](https://news.ycombinator.com/item?id=16380466)).
- **Fiabilidad.** Siri acertaba a la primera un recordatorio en un 25 % de los casos, según un usuario ([jonknee, HN](https://news.ycombinator.com/item?id=5074583)). Se activa sola varias veces por semana ([whywhywhywhy, HN](https://news.ycombinator.com/item?id=23271248)). Y pregunta «¿a.m. o p.m.?» cuando el contexto ya lo deja claro ([pflenker, HN](https://news.ycombinator.com/item?id=43186927)).
- **Voz que molesta.** Los «while you wait…» y «did you know…» de Alexa enfadan a los usuarios ([joshstrange, HN](https://news.ycombinator.com/item?id=40010809)). Y la voz no siempre es mejor: «I can read faster than the computer can talk» ([giantrobot, HN](https://news.ycombinator.com/item?id=40207394)).

### 3.7 Asistentes locales: Open Interpreter, Jan, LM Studio, AnythingLLM

- **Open Interpreter** dejó al modelo ejecutar código en el equipo, y la reacción fue de miedo razonado: «even gpt-4 hallucinates libraries and commands» ([peddling-brink, HN](https://news.ycombinator.com/item?id=38244933)). La única salvaguarda era aprobar cada ejecución ([molticrystal, HN](https://news.ycombinator.com/item?id=38245817)), y se pedía un sandbox de verdad ([simonw, HN](https://news.ycombinator.com/item?id=38246804)).
- **LM Studio** recibe quejas por tocar el sistema sin pedir permiso: añade líneas a `.bashrc` y `.zshrc` (55 votos, [lmstudio-bug-tracker#656](https://github.com/lmstudio-ai/lmstudio-bug-tracker/issues/656)).
- **Jan** recibe sobre todo peticiones de integración y portabilidad: modelos de Ollama, MCP, exportar conversaciones ([issues más votados de janhq/jan](https://github.com/janhq/jan/issues?q=is%3Aissue+sort%3Areactions-%2B1-desc)).
- **AnythingLLM** se ganó elogios por instalarse sin complicaciones, pero le preguntaron cómo puede ser «privado por defecto» si también ofrece modelos en la nube. Su autor respondió: «Privacy by default means "if you use the defaults, it's private"» ([tcarambat1010, HN](https://news.ycombinator.com/item?id=41459304)).

> **Interpretación.** Los fracasos comparten patrón. **La empresa decide por el usuario** (activa algo, lo instala o roba una tecla), **promete una capacidad que después no llega** (Siri, Copilot Actions solo en algunos equipos) o **falla en lo básico** (búsqueda, temporizadores, recordatorios). Los asistentes locales fallan por el otro lado: son potentes, pero no ponen límites seguros cuando actúan.

---

## 4. Qué valoran y qué temen

### 4.1 Lo que valoran

| Valor | Evidencia |
|---|---|
| Rapidez por encima de funciones | Flow Launcher «is faster to use» que Raycast y por eso se prefiere ([Adrig, HN](https://news.ycombinator.com/item?id=46025173)); de Everything se valora que es rápido y simple ([illnewsthat, HN](https://news.ycombinator.com/item?id=46025226)). |
| Atajo global | «The only real benefit is the hotkey» ([wifipunk, HN](https://news.ycombinator.com/item?id=42009384)); ChatGPT lo ofrece con Alt+Espacio ([OpenAI](https://help.openai.com/en/articles/9982051-using-the-chatgpt-windows-app)). |
| Poder apagarlo y quitarlo | [ToucanLoucan](https://news.ycombinator.com/item?id=40445335), [JohnFen](https://news.ycombinator.com/item?id=41447768), [noir_lord](https://news.ycombinator.com/item?id=47753496). |
| Que venga desactivado | «I would prefer off by default. No nags» ([BizarroLand, HN](https://news.ycombinator.com/item?id=47756522)); Microsoft lo adoptó para Recall y Copilot Actions ([blog](https://blogs.windows.com/windowsexperience/2025/10/16/making-every-windows-11-pc-an-ai-pc/)). |
| Nada sale del equipo | Para una memoria total, solo aceptarían código abierto «with zero network connectivity» ([selfhoster11, HN](https://news.ycombinator.com/item?id=45670212)); búsqueda semántica sin enviar archivos a servidores ([articulatepang, HN](https://news.ycombinator.com/item?id=46026187)). |
| Elegir el modelo y usar su clave | [drpossum, HN](https://news.ycombinator.com/item?id=42011150); AnythingLLM permite cambiar entre local y nube ([tcarambat1010, HN](https://news.ycombinator.com/item?id=41459336)). |
| Que actúe, pero a la vista | Microsoft promete «visibility into what Copilot Actions is doing» ([blog](https://blogs.windows.com/windowsexperience/2025/10/16/making-every-windows-11-pc-an-ai-pc/)) y que las acciones del agente se distingan de las del usuario ([Microsoft Support](https://support.microsoft.com/en-us/windows/experimental-agentic-features-a25ede8a-e4c2-4841-85a8-44839191dfb3)). |
| Herramientas sencillas que no cambian | El Bloc de notas se valoraba por ser mínimo ([nehal3m, HN](https://news.ycombinator.com/item?id=42074537)). |
| Menos que leer | La IA que convierte todo en más texto aumenta la carga: «give me more to read» ([ActionHank, HN](https://news.ycombinator.com/item?id=48822779)). |

### 4.2 Lo que temen

- **Que la vigilancia se convierta en botín.** Recall ([logrot](https://news.ycombinator.com/item?id=40447336), [hn8305823](https://news.ycombinator.com/item?id=40445761)).
- **Que las promesas cambien con una actualización.** Lo local que acaba en la nube, o lo apagado que vuelve a encenderse ([cesarb](https://news.ycombinator.com/item?id=40446248), [noirbot](https://news.ycombinator.com/item?id=41447622)).
- **Que la IA se equivoque con seguridad.** El 66 % se queja de respuestas «almost right» y solo el 3,1 % confía mucho en ellas ([Stack Overflow 2025](https://survey.stackoverflow.co/2025/ai)).
- **Las inyecciones de instrucciones.** Microsoft advierte de que el contenido de un documento o de la interfaz puede «override agent instructions» ([Microsoft Support](https://support.microsoft.com/en-us/windows/experimental-agentic-features-a25ede8a-e4c2-4841-85a8-44839191dfb3)).
- **Las acciones destructivas.** [marc92](https://news.ycombinator.com/item?id=47689553), [chazeon](https://news.ycombinator.com/item?id=38246162), [godelski](https://news.ycombinator.com/item?id=38246330) («a great way to brick your machine»).
- **Los recursos.** La máquina virtual de Claude Desktop ([#29045](https://github.com/anthropics/claude-code/issues/29045)), Copilot con Edge dentro ([HN 47656146](https://news.ycombinator.com/item?id=47656146)), un Bloc de notas que consume recursos incluso con la IA apagada ([razster, HN](https://news.ycombinator.com/item?id=47755099)) y la fama de que Electron no es rápido ([pjmlp, HN](https://news.ycombinator.com/item?id=42009430)).
- **Que se robe el foco o se interrumpa.** [PowerToys#65](https://github.com/microsoft/PowerToys/issues/65): «when another app has been finished the startup it steals the focus».
- **Los permisos de más.** Microsoft exige mínimo privilegio y permisos por agente: «Allow Always», «Ask every time» o «Never allow» ([Microsoft Support](https://support.microsoft.com/en-us/windows/experimental-agentic-features-a25ede8a-e4c2-4841-85a8-44839191dfb3)). Que la función solo la pueda activar un administrador también indica cuánto riesgo percibe la propia Microsoft.
- **Que la interfaz de chat obligue a pensar demasiado.** Con un prompt hay que planear la tarea entera de antemano, en lugar de apoyarse en botones deshabilitados o diálogos ([graypegg, HN](https://news.ycombinator.com/item?id=37872173)).

---

## 5. Funciones pedidas y mal resueltas por todos

| Función | Quién la pide | Cómo está resuelta hoy |
|---|---|---|
| **Reescribir, corregir, traducir o resumir el texto seleccionado en cualquier app** | [_1tem](https://news.ycombinator.com/item?id=42077005), [evilduck](https://news.ycombinator.com/item?id=42080088) | Click to Do solo en Copilot+ PC ([MS](https://support.microsoft.com/en-us/windows/click-to-do-do-more-with-what-s-on-your-screen-6848b7d5-7fb0-4c43-b08a-443d6d3f5955)); Microsoft la mete dentro de cada app (el Bloc de notas), justo lo que se critica. |
| **Búsqueda local de archivos por significado, sin conexión** | [articulatepang](https://news.ycombinator.com/item?id=46026187), [TheTaytay](https://news.ycombinator.com/item?id=46088341) | Everything busca por nombre, no por significado. Recall busca en capturas, no en archivos, y exige un Copilot+ PC. Copilot usa conectores en la nube. |
| **Expansor de texto** | [PowerToys#5074](https://github.com/microsoft/PowerToys/issues/5074) (142 votos, abierta desde 2020) | Solo Raycast, que cobra suscripción por otras partes, y herramientas sueltas. |
| **Historial del portapapeles con búsqueda** | [MiddleEndian](https://news.ycombinator.com/item?id=29814759) | Resuelto por Ditto, Raycast y Command Palette. **No es un hueco.** |
| **Un interruptor global de «sin IA»** | [noir_lord](https://news.ycombinator.com/item?id=47753496) | En Windows no existe: hay ajustes sueltos en cada app. Apple sí lo tiene ([Shank, HN](https://news.ycombinator.com/item?id=47754620)). |
| **Deshacer lo que hizo el asistente** | [marc92](https://news.ycombinator.com/item?id=47689553) | Microsoft habla de registro y de supervisar las acciones, no de deshacerlas ([MS](https://support.microsoft.com/en-us/windows/experimental-agentic-features-a25ede8a-e4c2-4841-85a8-44839191dfb3)). |
| **Actuar sobre la app de al lado con límites claros** | [solsane](https://news.ycombinator.com/item?id=45622955), [simonw](https://news.ycombinator.com/item?id=38246804) | Copilot Actions es experimental y viene desactivado; Open Interpreter no pone límites. |
| **Entrada rápida con captura y dictado en Windows** | [blixt, HN](https://news.ycombinator.com/item?id=42009599) | Claude solo la da en Mac ([Anthropic](https://support.claude.com/en/articles/12626668-use-quick-entry-with-claude-desktop-on-mac)). |
| **Voz fiable para lo básico, sin falsos despertares ni ventas** | [pflenker](https://news.ycombinator.com/item?id=43186927), [joshstrange](https://news.ycombinator.com/item?id=40010809) | Alexa y Siri fallan en eso. «Hey Copilot» es opcional y no hay datos públicos de su precisión. |
| **Exportar conversaciones** | [becquerel](https://news.ycombinator.com/item?id=42010470), [Jan#6195](https://github.com/janhq/jan/issues/6195) | Varía según el producto. |
| **No robar el foco** | [PowerToys#65](https://github.com/microsoft/PowerToys/issues/65) | Sigue abierta desde 2019. |

---

## 6. Comparación con Sakura

Lo que sigue compara la lista de funciones del encargo con una lectura rápida del código (`src/Nexo.Core/Permissions`, `src/Nexo.Core/ComputerUse`, `src/Nexo.Windows/Flow`). Donde digo «no encontré», quiere decir que no lo vi con una búsqueda por nombres. No es una auditoría.

### 6.1 Lo que ya tiene y la gente valora

- **Funciona en cualquier PC con Windows.** Lens, el explicador de la ventana activa (Ctrl+Shift+Espacio) y el traductor de pantalla hacen lo que Microsoft reserva a Click to Do en Copilot+ PC. **Es la mayor ventaja competitiva que he encontrado, y hoy no se cuenta.**
- **Entrada rápida en Windows.** Sakura tiene atajo global, captura por región o de ventana y dictado local, justo las tres cosas que Claude Desktop solo ofrece en Mac.
- **Dictado que escribe en cualquier aplicación (Flow)**, con tres negativas que atacan miedos reales. No escribe si cambió el foco, no escribe sin una ventana de destino y no escribe en ventanas sensibles (`WindowsFlowTextInserter.cs`). La lista del encargo no lo menciona, y debería.
- **Órdenes locales sin IA** para temporizadores, volumen, brillo y abrir apps. Cubren exactamente lo que la gente usa de verdad en Siri y Alexa, sin latencia ni errores de un modelo.
- **Permisos por capacidad, independientes**, con niveles Bloqueado, Preguntar y Permitido, confirmaciones obligatorias que no se pueden saltar (borrado irreversible, credenciales, pagos…) y registro de auditoría (`SakuraCapability.cs`, `AutonomyLevel.cs`). Coincide casi punto por punto con lo que Microsoft exige a los agentes. La escalera de métodos de Computer Use, con el ratón como último recurso (`ComputerUseMethod.cs`), responde al miedo de Open Interpreter.
- **Deshacer en optimizaciones y packs.** El estado anterior se guarda como texto para poder restaurarlo (`SkillPack.cs`, `OptimizationChange.cs`). Es justo lo que nadie ofrece para las acciones de un asistente.
- **Local-first, sin cuenta y con la clave propia**, con Ollama o con cinco proveedores en la nube. Responde a [drpossum](https://news.ycombinator.com/item?id=42011150) y a [selfhoster11](https://news.ycombinator.com/item?id=45670212).
- **Memoria opcional y cifrada, con «olvidar todo»** (`MemoryManager.ForgetEverything`). Es lo contrario de Recall: la memoria guarda solo lo que se decide guardar, no capturas de todo.
- **Gobernador de recursos o modo juego**, que responde al miedo a los recursos.

### 6.2 Lo que le falta (por orden de lo que pide la gente)

1. **Acciones sobre el texto seleccionado en cualquier app.** Seleccionar, pulsar un atajo y elegir reescribir, corregir, traducir, resumir o explicar, y que el resultado **sustituya la selección o se copie**. Sakura ya tiene las piezas: Lens para leer, Flow para escribir con sus salvaguardas y un portapapeles que es un método permitido. No encontré en el código nada que lea la selección de otra aplicación. **Es el hueco más claro del mercado para un PC sin NPU.**
2. **Buscar en el equipo.** Primero por nombre, delegando en Everything si está instalado o en el índice de Windows. Después, con calma, por significado en carpetas autorizadas. No encontré búsqueda de archivos.
3. **Un interruptor visible de «Sakura sin IA».** Que deje funcionando solo las órdenes locales, la voz local, las capturas y las tareas. No encontré ningún ajuste así. Responde directamente a la petición de un «Disable AI» global.
4. **Exportar conversaciones** a Markdown o al formato de documento que ya tiene. No encontré la función.
5. **Deshacer y un registro legible para toda acción de Computer Use**, no solo para optimizaciones. Lo mínimo es enseñar «esto es lo que hice» con un botón para revertirlo cuando se pueda, y decir claramente cuándo no se puede.
6. **Fragmentos de texto o expansor**, idealmente dentro de Flow («Oye Sakura, pega mi firma»). Es una petición antigua y sin resolver en PowerToys.
7. **Un cliente MCP.** `ComputerUseMethod.Mcp` existe como escalón, pero no encontré un cliente MCP. Es lo más pedido en los clientes locales (LM Studio, Jan).

### 6.3 Lo que le sobra o estorba (interpretación, para discutir)

- **Command Center, Peek y los controles al rozar el borde compiten con Command Palette, que es gratis y de Microsoft** (lanzador, métricas, barra en el borde que se oculta sola, portapapeles). Si Sakura los mantiene, tienen que ser claramente mejores o apoyarse en lo que solo Sakura sabe hacer (voz, IA, permisos). Si no, son superficie que mantener y más cosas que aparecen en pantalla. **Propuesta:** fusionar la paleta y el Command Center en una sola entrada y dejar Peek y los controles de borde desactivados de serie.
- **La grabación de pantalla con audio y la exportación a Word, Excel y PowerPoint con APA** no aparecen en ninguna petición de asistentes de escritorio que haya encontrado. Pueden servir a estudiantes, pero pesan en el instalador y en la lista de funciones. **Propuesta:** tratarlas como packs opcionales, fuera del mensaje principal.
- **Cualquier aparición que robe el foco o ofrezca algo sin que se pida**, como avisos, sugerencias o «¿sabías que…?». La queja contra Alexa y la petición #65 de PowerToys indican que eso resta más de lo que aporta. Revisar que ninguna ventana de Sakura robe el foco al abrirse sola.
- **Cualquier cosa que parezca IA añadida a la fuerza.** Si una función cumple sin IA (temporizador, volumen, captura), no debería pasar por el modelo ni anunciarse como IA.

### 6.4 Lo que debería contar mejor

- **«Funciona en tu PC, no hace falta un Copilot+ PC.»** Con la comparación explícita: explicar la pantalla, traducirla y dictar, en cualquier equipo que cumpla los requisitos de Sakura (confirmar cuáles son antes de publicarlo).
- **«Desactivado hasta que tú lo actives, y se puede quitar entero.»** Enseñar la matriz de permisos en la instalación y explicar qué se borra al desinstalar. Recall enseñó que «se puede apagar» no basta.
- **«Nada sale de tu equipo salvo que elijas un proveedor en la nube, y Sakura te avisa cuando lo hace.»** Un indicador visible de local o nube en cada respuesta responde a la duda que le plantearon a AnythingLLM.
- **«Te dice lo que hizo y lo puede deshacer.»** El registro de auditoría existe; hay que enseñarlo.
- **«Consume esto.»** Publicar la RAM y la CPU en reposo con y sin voz activa. Ante las quejas de Claude Desktop y Copilot, dar una cifra honesta sirve más que cualquier adjetivo.
- **«Nunca escribe en la ventana equivocada.»** Las negativas de Flow son un argumento de venta.

---

## 7. Recomendaciones priorizadas

| # | Recomendación | Impacto | Esfuerzo | Por qué |
|---|---|---|---|---|
| 1 | **Acciones sobre el texto seleccionado** (atajo, menú corto, reemplazar o copiar), reutilizando las salvaguardas de Flow | Alto | Medio | Es el hueco más claro: Microsoft lo limita a Copilot+ PC ([MS](https://support.microsoft.com/en-us/windows/click-to-do-do-more-with-what-s-on-your-screen-6848b7d5-7fb0-4c43-b08a-443d6d3f5955)). |
| 2 | **Mensaje «cualquier PC» + indicador local/nube + cifra de consumo** en la web, la Tienda y la bienvenida | Alto | Bajo | Coste casi nulo; responde a los miedos de la sección 4.2. |
| 3 | **Interruptor «Sakura sin IA»** | Medio-alto | Bajo | Petición explícita ([noir_lord](https://news.ycombinator.com/item?id=47753496)) y fácil sobre lo que ya existe. |
| 4 | **Búsqueda de archivos por nombre** (Everything o el índice de Windows) desde la paleta y por voz | Alto | Medio | Es una de las tareas más habituales; Sakura hoy no la hace. |
| 5 | **Registro visible y deshacer para las acciones de Computer Use** | Medio-alto | Medio | Responde al miedo a las acciones destructivas ([marc92](https://news.ycombinator.com/item?id=47689553)). |
| 6 | **Revisar el robo de foco y las apariciones sin pedir**; Peek y controles de borde desactivados de serie | Medio | Bajo | [PowerToys#65](https://github.com/microsoft/PowerToys/issues/65); competencia directa de Command Palette. |
| 7 | **Exportar conversaciones** | Medio | Bajo | Petición repetida; el exportador de documentos ya existe. |
| 8 | **Unificar la paleta y el Command Center** | Medio | Medio | Menos superficie; diferenciarse de Command Palette. |
| 9 | **Fragmentos de texto o expansor** | Medio | Medio | 142 votos en PowerToys, sin resolver. |
| 10 | **Búsqueda semántica local en carpetas autorizadas** | Alto | Alto | Nadie la resuelve bien; es cara y conviene hacerla después de la 4. |
| 11 | **Cliente MCP** | Medio | Alto | Muy pedido en clientes locales, pero a Adler (sin perfil técnico) le aporta menos que lo anterior. |
| 12 | **Grabación y Office como packs opcionales** | Bajo-medio | Medio | Aligera el mensaje principal; decidir con datos de uso, que Sakura no recoge. |

---

## 8. Lo que no se pudo verificar

- **Reddit** (r/windows, r/Windows11, r/LocalLLaMA, r/ChatGPT, r/productivity): la API devolvió 403 y old.reddit redirige al inicio de sesión. No se usó ninguna voz de Reddit.
- **Feedback Hub:** no tiene API pública legible; no se consultó.
- **La página de ayuda de ChatGPT para Windows** devolvió 403. Lo que se dice de la ventana compañera sale del fragmento del buscador, no de haber leído la página.
- **El comunicado de Apple sobre Siri** está citado por la prensa; no encontré el texto en una web de Apple.
- **El consumo de RAM de la nueva app de Copilot** sale de Windows Latest a través de HN, no de Microsoft.
- **Las declaraciones de Dell en el CES 2026** sobre el poco interés por los «AI PC» ([hilo de HN](https://news.ycombinator.com/item?id=46527706)): el artículo de PC Gamer no se pudo leer entero, así que no cito su contenido.
- **La cifra de «1,8 % de adopción» de Microsoft 365 Copilot** que circula en HN ([mrandish](https://news.ycombinator.com/item?id=45478527)) viene de un análisis de terceros ([perspectives.plus](https://www.perspectives.plus/p/microsoft-365-copilot-commercial-failure)) que no verifiqué. No la uso.
- **Los porcentajes de Stack Overflow 2025** se leyeron con una herramienta que resume la página; las cifras (66 %, 87 %, 81 %, 3,1 %) coinciden con lo publicado, pero conviene confirmarlas en la página antes de citarlas fuera de este documento. Además, la encuesta es de desarrolladores, no de usuarios generales.
- **El dato de Microsoft** de que con voz se usa Copilot «twice as much» es una afirmación de la empresa sin datos publicados.
- **La precisión de «Hey Copilot»** y sus falsas activaciones: no encontré datos.
- **Lo que tiene Sakura**: la sección 6 se basa en la descripción del encargo y en búsquedas por nombre en el código, no en una auditoría ni en ejecutar la app. En particular, conviene confirmar que no existan ya la exportación de conversaciones, la lectura de la selección de otras apps o un cliente MCP con otros nombres.
- **El consumo real de Sakura** (RAM y CPU en reposo, con Vosk y con Whisper) no se midió.
