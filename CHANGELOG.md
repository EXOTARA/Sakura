# Changelog

## [0.30.0-beta]

### Agregado

- **Reportar una respuesta de IA.** Cada respuesta de Sakura tiene en su menú (clic derecho) «Reportar esta respuesta»: copia el texto a tu portapapeles y abre un formulario para avisar si era ofensiva, peligrosa o falsa. No se envía nada por su cuenta. También está como orden en la paleta, para usarlo con el teclado.
- **Sakura se prepara para llegar a Microsoft Store** como «Sakura Assistant». La versión de la Store la firma Microsoft, así que no mostrará el aviso de SmartScreen. La descarga desde GitHub sigue igual.

### Cambiado

- **La versión de la Store se comporta distinto donde tiene que hacerlo:** no usa el actualizador propio, porque actualiza la Store; arranca con Windows por el mecanismo del paquete; y no instala Ollama por su cuenta, sino que lleva a su web. La copia descargada de GitHub no cambia en nada de esto.

## [0.29.3-beta]

### Corregido

- **El Narrador se quedaba callado en dos sitios.** Al recorrer la ventana con Tab, el foco caía en dos cosas invisibles —el marco donde se cargan las vistas y la flor del logo— y el lector de pantalla no decía nada, como si Sakura se hubiera colgado. Ahora cada parada de Tab tiene nombre.
- **La ayuda de «Recoger Sakura» decía «Oculta el shell».** Ahora dice «Oculta la ventana sin cerrar Sakura».
- **El área de la conversación no tenía nombre** para el lector de pantalla. Ahora se llama «Conversación».

## [0.29.2-beta]

### Corregido

- **«Oye Sakura» se despertaba sola con frases normales.** «Voy a sacar la basura», «oye, saca la ropa» o «se acabó el café» abrían la escucha como si la hubieras llamado. Medido con grabaciones reales: ocho despertares falsos en 44 segundos de conversación. Ahora, en esas mismas grabaciones, uno; y ninguno en una tanda nueva de un minuto llena de palabras parecidas. A dos metros y con música puede costar alguna llamada más que antes: dos de cada dieciséis.
- **Los controles de volumen y brillo podían quedarse encima de un juego** si estaban abiertos, o se abrían, justo cuando el juego pasaba a pantalla completa.

### Agregado

- **Accesibilidad en las ventanas secundarias.** El lector de pantalla nombra los botones y las listas de la paleta, el centro de comandos, el historial, los diagnósticos, el gestor de modelos y los selectores de captura.
- **Volumen y brillo con teclado**: flechas de 5 en 5, RePág y AvPág de 10 en 10, Inicio y Fin.
- **Elegir una zona de pantalla sin ratón**: las flechas mueven el recuadro, Mayús + flechas cambia su tamaño y Enter confirma.
- **Entrar con el teclado en los avisos mientras están en pantalla**: Ctrl + Alt + F7 (aviso), F8 (respuesta), F9 (volumen y brillo), F10 (solicitud) y F11 (vistazo). Escape sale.

### Cambiado

- **Los botones principales llevan el texto oscuro sobre el rosa**, porque en blanco no se leía bien. El texto gris secundario también es algo más claro.
- **En la paleta de comandos, autocompletar es Ctrl + Tab.** Tab pasa a moverse entre los controles, como en cualquier otra ventana.

## [0.29.1-beta]

### Corregido

- **Cada actualización borraba el desinstalador.** Después de actualizar desde la propia app, Sakura seguía apareciendo en «Aplicaciones instaladas», pero el botón de desinstalar apuntaba a un archivo que ya no existía. A partir de esta versión el desinstalador se conserva al actualizar, y al desinstalar se va también todo lo que llegó con las actualizaciones. Tus datos, en `%LOCALAPPDATA%\Sakura`, no se tocan.
- **Windows mostraba una versión vieja** en «Aplicaciones instaladas», hasta dos por detrás de la que tenías. Ahora se corrige sola al abrir Sakura.

### Importante si ya tenías Sakura instalado

El arreglo lo lleva la versión nueva, así que **actualizar a esta desde el botón de la app todavía pierde el desinstalador una última vez**. Para dejarlo bien, descarga `Sakura-0.29.1-beta-Setup.exe` y ábrelo encima de tu instalación: la encuentra sola, devuelve el desinstalador y conserva tus datos. No desinstales antes ni borres nada a mano.

## [0.29.0-beta]

### Agregado

- **Traducir un trozo de pantalla.** `Ctrl + Shift + T`, arrastras un recuadro sobre cualquier texto y sale traducido. Sirve donde no sirve copiar y pegar: el menú de un juego, un error dentro de una imagen, un PDF que no deja seleccionar. Usa el modelo que ya tengas configurado —con Ollama no sale nada de tu equipo— y si no tienes ninguno te lo dice en vez de fallar por dentro. Lo que Sakura tapa por sensible sigue tapado.
- **La tarjeta «Ahora»**, que dice en qué estás en este momento. Una ventana marcada como privada nunca se nombra: dice que hay algo abierto, no qué.
- **La pestaña «Voz»**, para contestar de un vistazo a «¿por qué no me oye?». Marca las tres causas —escucha apagada, sin micrófono, modelos sin preparar— y deja los dos atajos a la vista.
- **El hueco del panel acepta un GIF animado**, no solo una imagen fija.

### Cambiado

- **La carátula del Panel gira y tiene su anillo de rayos**, igual que la de la pestaña Media y con el mismo espectro.

### Corregido

- **La página de descargas no se actualizaba al publicar una versión.** No fallaba nada: se quedaba ofreciendo la anterior, en silencio.

## [0.28.0-beta]

### Agregado

- **Halo de voz.** Cuando Sakura escucha aparece un halo en el borde inferior que respira con tu voz: no solo avisa de que está escuchando, muestra que te está oyendo a ti. Sustituye a la cápsula «Te escucho», que decía lo mismo peor y salía a la vez.
- Decir **«nada»** cierra la escucha sin hacer nada. También valen «olvídalo», «déjalo así», «ya no», «cancela» y algunas más, con o sin «Sakura» delante o detrás.
- **`Alt + V`** empieza a escuchar sin decir el nombre, y vuelve a pulsarse para dejarlo. Es la primera forma de **cortar** una escucha abierta.
- **Página de descargas** en https://exotara.github.io/Sakura/, en español y en inglés, con los hashes a la vista y el aviso de SmartScreen explicado antes de que aparezca.
- Se puede **apagar la comprobación diaria de actualizaciones** en Ajustes. Era lo único que salía del equipo sin que nadie lo pidiera y no había forma de quitarlo.
- Políticas publicadas de **privacidad**, **firma de código**, **seguridad** y **conducta**, y solicitud enviada a SignPath Foundation para firmar las versiones.

### Cambiado

- **La barra superior del panel** toma las medidas de la referencia: icono más grande y pegado a su palabra, texto mayor, y subrayado más fino y algo más largo que el nombre. La pestaña activa ya no engorda al seleccionarse.
- **La retícula del panel es un tablero, no una lista.** Media pasa a ser una columna alta a la derecha con la carátula grande; arriba queda la tarjeta de sesión, y abajo el reloj, el calendario y los anillos con sus anchos propios.
- **Sakura lleva por fin su propio logo dentro.** Seis ventanas enseñaban una flor de cinco pétalos que el ejecutable nunca ha tenido.
- **La escucha se rinde a los tres segundos** si no habla nadie, en vez de esperar los veinte del máximo. Una pausa a mitad de una orden sigue sin cortarte.
- Los comandos que enumeran medio sistema —controladores, datos del sistema— tienen ahora **más tiempo** antes de darse por perdidos.

### Corregido

- **El volumen dejaba de avisar y no paraba de avisar a la vez.** Mover el deslizador sacaba una cápsula por paso, y cada una reiniciaba la animación: se leía como un aviso con un tic. Ahora no se te repite lo que acabas de hacer con tu propia mano, y un mensaje repetido se actualiza en vez de volver a entrar.
- **El relleno del carril de volumen se despegaba del fondo** a media altura, y a niveles bajos flotaba como un círculo suelto.
- **La píldora de respuesta no era una píldora**: sus extremos eran esquinas redondeadas y los lados salían rectos.
- El archivo instalado decía **«Kohana 0.9.5»** en sus propiedades de Windows mientras la aplicación decía Sakura 0.27.0.

## [0.9.5-beta-hotfix.1]

### Corregido

- La migración al esquema 16 conserva y normaliza los aliases personales de la palabra de activación en lugar de borrarlos.

## [Unreleased]

- Kohana 0.9.5-beta.

### Agregado (Diseños D10, D11 y D12 — sin integrar a release)

- Memoria personal que se llena desde la conversación: guarda lo que le pides recordar explícitamente y propone (nunca guarda solo) las preferencias que dices de paso.
- Lo recordado acompaña a las consultas, así que hay continuidad real entre sesiones.
- Panel de memoria en Personalizar: activar, tres categorías por separado, días de retención, exclusiones, ver lo guardado y olvidar todo.
- Reversión verificada de las optimizaciones: Kohana relee el estado en vez de fiarse de que la llamada al sistema fuera aceptada.
- Si un paso de la optimización falla, se deshacen los ya aplicados en lugar de dejar el equipo a medias.
- Segundo ajuste reversible: Kohana baja su propio modo de rendimiento cuando el hardware lo justifica.
- Historial de auditoría de optimizaciones, consultable desde comando y desde el nuevo panel de Sistema.
- Panel de optimización en Sistema con los siete escenarios, deshacer e historial.
- Carpeta de proyecto autorizada, de solo lectura, con revocación inmediata.
- Detección de secretos en código y exclusión de archivos de credenciales antes de enviar nada a la IA.
- Comando para que Kohana explique el proyecto autorizado usando su estructura, no su código.

### Cambiado

- Esquema de preferencias v19 → v20. Al actualizar, nadie hereda una carpeta de proyecto autorizada.
- La redacción de datos sensibles reconoce ahora la forma hablada de una contraseña («mi contraseña es …»), además de la de formulario.

### Agregado (Diseños D13, D14 y D15 — sin integrar a release)

- Registro de actividad orientado al usuario: qué hizo Kohana, cuándo, con qué permiso y cómo deshacerlo, en un solo sitio y visible desde Sistema.
- Quedan registradas también las decisiones de permisos: autorizar o revocar una carpeta, cambiar su nivel de autonomía y borrar la memoria.
- Controles del proyecto en Personalizar: autorizar, revocar y elegir hasta dónde puede llegar Kohana.
- Comando para buscar dentro del proyecto autorizado.
- Kohana ya puede modificar un archivo del proyecto, de uno en uno y solo con tu confirmación, guardando antes una copia previa para poder deshacerlo.
- Se niega a deshacer un cambio si editaste el archivo después, para no borrar tu trabajo.
- Comando para deshacer el último cambio hecho en el proyecto.
- Packs **Kohana Study** y **Kohana Dev**: dejan configuradas de una vez varias capacidades que ya existían, y se desactivan devolviendo los ajustes a como estaban.

### Corregido

- Activar un pack ya no podía reactivar Vision por efecto de la normalización de preferencias, que habría encendido un permiso apagado a propósito.

### Agregado (Diseños D16, D17 y D18 — sin integrar a release)

- Permisos por capacidad en Personalizar, con niveles Bloqueado, Preguntar y Permitido. Dar uno no da los demás.
- Hay cosas que Kohana pregunta siempre, aunque las tengas permitidas: borrar sin recuperación, credenciales, pagos, permisos de administrador, enviar algo fuera del equipo y cambios amplios del sistema.
- Ampliar un permiso pide una confirmación nueva; restringirlo, no.
- Kohana puede proponer cómo hacer algo en el equipo eligiendo siempre la forma más segura disponible, y explica por qué esa y no otra.
- Comando para ver los métodos con los que Kohana puede actuar, en orden de más a menos seguro.
- Comandos de diagnóstico de solo lectura (red, DNS, sistema, controladores), ejecutables uno a uno con tu confirmación.
- Kohana puede dejarte algo en el portapapeles y devolverlo a como estaba.
- Botón de deshacer en el registro de actividad, para las acciones que se pueden revertir.

### Cambiado

- Esquema de preferencias v20 → v22. Al actualizar, actuar sobre el equipo llega bloqueado y el resto de permisos en «preguntar».

### Agregado (Diseños D19, D20 y D21 — sin integrar a release)

- Kohana puede pulsar un control concreto de otra aplicación, y se niega si hay varios con el mismo nombre o si la ventana es sensible.
- Las aplicaciones excluidas se editan en Personalizar, con el formato `capacidad: aplicación`.
- Comando para ver todo lo que Kohana guarda en tu equipo, qué es cada cosa y si está cifrada.
- Copia de seguridad verificada de tus datos antes de actualizar, y comando para restaurarla.
- Comando que enseña qué se borraría y qué se conservaría al desinstalar, antes de desinstalar nada.
- Diagnóstico exportable para soporte, sin tus datos dentro y con una lista de lo que dejó fuera.
- Informe de privacidad: qué se guarda, dónde, cifrado o no, y cómo borrarlo.

### Corregido

- La regla de «usa siempre el método más seguro disponible» comparaba métodos que no sirven para lo mismo, y desde que UI Automation está disponible impedía copiar al portapapeles. Ahora se compara solo entre métodos capaces del mismo objetivo.

### Agregado (Diseños D22, D23 y D24 — sin integrar a release)

- Comando «Comprobar que Kohana funciona bien»: revisa permisos, migraciones, cifrado, copias y redacción en tu equipo, sin tocar tus datos, y dice qué NO comprueba.
- Cuatro packs nuevos: **Kohana Support**, **Kohana Creator**, **Kohana Access** y **Kohana Meeting**, con los dos anteriores hacen los seis del roadmap.
- Panel de packs en Personalizar, con lo que le falta a cada uno antes de activarlo.
- Nivel «Colaborar con confirmaciones» para el proyecto: Kohana encadena varios cambios y se para a preguntar en cada archivo.
- Si un cambio de la secuencia falla, Kohana para, deja constancia del punto exacto y ofrece deshacer lo ya aplicado. Nunca reintenta sola.

# Changelog

Todos los cambios públicos de Kohana se documentan aquí. Las entradas anteriores a `0.9.2-beta` conservan el nombre Nexo como registro histórico.

## 0.9.5-beta — Shell Reliability + Voice Reliability v2

### Agregado

- Sensibilidad configurable para la frase de activación: Precisa, Equilibrada y Alta.
- Prueba guiada de wake word desde Ajustes sin ejecutar una orden.
- Variantes locales adicionales para “Ey Kohana”, “Hey Kohana” y errores frecuentes de Vosk.
- Estado persistente de la barra lateral.

### Corregido

- La barra lateral libera completamente el espacio al contraerse.
- El botón de navegación ya no comparte símbolo con Ctrl + Espacio.
- Iconos principales redibujados con un lenguaje funcional y menos decorativo.
- Se sincronizan el ancho del contenedor, las etiquetas y el chevrón durante la animación.

## 0.9.3-beta — Sakura Shell + Chat Refresh

### Agregado

- Pantalla inicial de chat con acciones rápidas para PowerShell, pendientes, enfoque y Vision.
- Composer de conversación rediseñado con placeholder, acceso visual, voz y envío.
- Set vectorial Kohana Core Modules para navegación consistente.
- Acento azul frío secundario para equilibrar la identidad sakura.

### Cambiado

- Paleta Sakura Premium más neutral y con menos rosa dominante.
- Logo público reemplazado por una marca floral lineal inspirada en el sistema de Figma.
- Navegación lateral unificada con trazos funcionales de 1.65 px.
- Burbujas de conversación más amplias y legibles.
- Los nombres internos de streaming ahora usan Kohana, conservando aliases temporales de Nexo.
- El catálogo de voz acepta Kohana, Oye Kohana, Hey Kohana y aliases heredados de Nexo.

### Corregido

- Prueba de comandos de audio que todavía esperaba únicamente el alias Nexo.
- Contraste insuficiente en iconos, bordes y textos secundarios.

## 0.9.2-beta — Kohana Brand Foundation

### Agregado

- Identidad pública centralizada mediante `ProductIdentity`.
- Nombre, lema y mensajes visibles de Kohana en Hub, Capsule, Peek, onboarding y bandeja.
- Diseño Sakura Fluent con nueva paleta grafito y acento rosa sakura.
- Logo floral vectorial y recursos PNG/ICO para aplicación e instalador.
- Ejecutable y artefactos públicos `Kohana.exe` y `Kohana-*-portable.zip`.
- Wake words `Kohana`, `Oye Kohana` y `Hey Kohana`.
- Compatibilidad temporal con las frases anteriores de Nexo.
- Migración conservadora de `%LocalAppData%\Nexo` a `%LocalAppData%\Kohana`.
- Scripts, instalador, CI, release workflow y documentación actualizados.

### Cambiado

- Esquema de preferencias actualizado a 14.
- El color predeterminado migra del morado anterior al acento sakura.
- Los valores antiguos de wake word migran a `Oye Kohana`.
- El inicio con Windows registra Kohana y limpia la entrada heredada al activarse.
- La instancia única usa identificadores de Kohana.
- Buscar actualizaciones conserva temporalmente `EXOTARA/Nexo` como repositorio estable.

### Compatibilidad

- Los namespaces y la solución siguen llamándose `Nexo.*` y `Nexo.slnx` durante esta etapa.
- La carpeta anterior de datos no se elimina ni se sobrescribe.
- El Resource Governor ignora tanto `Kohana.exe` como `Nexo.exe` en pantalla completa.

## Mejoras integradas antes del cambio de marca

### Voice Reliability v1

- Variantes frecuentes para la frase de activación.
- Traspaso Vosk → Whisper con audio previo y ventana posterior separada.
- Orden de hasta 20 segundos y final después de 1.5 segundos de silencio.
- Protección para que la cola de la frase de activación no corte la consulta.
- Micrófono manual con pulsar para iniciar y pulsar para terminar.
- Métricas privadas en `voice-capture.log`, sin audio ni transcripciones.

### Resource Governor v1 y Silent Voice Look

- Estados Normal, Busy y Game basados en pantalla completa y CPU/GPU/RAM.
- Modo Juego protege wake word, Vision, IA y cápsulas transitorias.
- Exclusión de SnippingTool y ScreenClippingHost para evitar falsos positivos.
- Look Mode con contexto visual temporal y silencioso.

## 0.9.0-beta - Primera edición distribuible

- Versión y metadatos centralizados para los ensamblados.
- Ejecutable `Nexo.exe` con icono propio y manifiesto Per-Monitor V2.
- Publicación autocontenida para Windows x64 sin depender de Electron.
- ZIP portable con archivo SHA256.
- Instalador por usuario mediante Inno Setup, sin requerir administrador.
- Desinstalación limpia con opción de conservar o eliminar datos locales.
- CI de GitHub para compilación, pruebas y artefactos portables.
- Workflow de releases para etiquetas `v*`.
- Comprobación manual de actualizaciones desde el centro de diagnóstico.
- Lista de verificación para probar la beta en una instalación limpia.


### Agregado

- Barra lateral modular para Windows.
- Atajo global `Alt + A`.
- Navegación entre IA, Audio, Captura, Sistema y Ajustes.
- Temas reutilizables y personalización persistente.
- Métricas reales de CPU, RAM, GPU, VRAM y almacenamiento.
- Modo Peek mediante `Alt + Shift + A`.
- Proceso con mayor consumo de memoria.
- Intérprete inicial de comandos naturales locales.
- Compatibilidad inicial con los prefijos `Nexo` y `Exo`.
- Cápsula flotante para estados, confirmaciones y errores.
- Apertura de terminales en la carpeta del usuario.
- Historial de conversación opcional y privado.
- Límite predeterminado de ocho mensajes temporales.
- Mezclador de audio real por aplicación.
- Comandos locales de volumen y silencio.
- Entrada de voz push-to-talk con Whisper local.
- Descarga y almacenamiento local del modelo multilingüe `base`.
- Activación experimental con las frases `Nexo` y `Oye Nexo`.
- Detector local Vosk con vocabulario limitado y sin claves externas.
- Grabación automática de la orden después de la activación, con final por silencio.
- Indicador visible mientras la activación está atenta.
- Capa de proveedores de IA compatible con OpenAI, Ollama y LM Studio.
- Prueba de conexión y listado de modelos desde Personalización.
- Consultas abiertas mediante el historial reciente de la conversación.
- Opción explícita para compartir métricas resumidas con la IA.
- Lectura de claves mediante variables de entorno, sin guardarlas en la configuración.
- Streaming de respuestas para mostrar texto mientras el modelo lo genera.
- Respuestas locales para fecha y hora, sin consultar al proveedor de IA.
- Política de contexto que adjunta métricas solo cuando la pregunta trata del equipo.
- Caché temporal de Whisper para acelerar órdenes de voz consecutivas.
- Selector de micrófono compartido por Whisper y la frase de activación.
- Búfer previo para conservar el inicio de órdenes dichas junto con `Nexo`.
- Confirmación por voz cuando la grabación tiene poca claridad.
- Umbral de silencio adaptable al ruido del micrófono.
- Nexo Vision bajo demanda con selector de ventanas y monitores.
- Vista previa obligatoria antes de compartir una captura.
- Envío de imágenes a proveedores multimodales mediante contenido compatible con OpenAI.
- Exclusión inicial de gestores de contraseñas y ventanas de seguridad.
- Reducción automática de capturas grandes para limitar memoria, latencia y tamaño de solicitud.
- Orden local `Nexo, mira esto` para abrir el flujo visual.
- Proveedor nativo de Ollama mediante `/api/chat` y `/api/tags`.
- Extracción estructurada de código, archivo, línea, mensaje y comando visibles antes de explicar un error.
- Contrato de respuesta visual con causa, corrección exacta y forma de comprobarla.
- Detección y reintento de respuestas visuales genéricas.
- Respaldo directo cuando un modelo genera razonamiento pero deja vacío el contenido final.
- Módulo **Hoy** para administrar tareas pendientes, completadas y vencidas.
- Creación y edición manual de tareas con notas, prioridad, fecha y hora.
- Recordatorios locales mediante cápsulas mientras Nexo está ejecutándose.
- Persistencia privada de tareas en `%LocalAppData%\Nexo\tasks.json`.
- Órdenes locales para crear, consultar, completar y eliminar actividades sin llamar al LLM.
- Módulo **Enfoque** con temporizadores persistentes y actualización en tiempo real.
- Presets de 25 y 50 minutos, descansos de 5 y 10 minutos y duración personalizada.
- Pausa, reanudación, cancelación y recuperación de sesiones al volver a abrir Nexo.
- Resumen diario de sesiones completadas y minutos enfocados.
- Órdenes locales para iniciar y controlar temporizadores sin usar el LLM.
- Persistencia privada del estado de enfoque en `%LocalAppData%\Nexo\focus.json`.
- Icono de Nexo en la bandeja del sistema con acciones para abrir, mostrar Peek y salir completamente.
- Ejecución en segundo plano al ocultar o cerrar la barra.
- Inicio opcional con Windows mediante el registro del usuario y el argumento `--background`.
- Coordinación de instancia única para evitar dos procesos y abrir la instancia existente.
- Notificaciones de Windows para tareas y sesiones de enfoque terminadas.
- Sonidos configurables para recordatorios y temporizadores.
- Revisión inmediata de tareas, enfoque y métricas al reanudar Windows después de una suspensión.
- Asistente de configuración inicial para micrófono, Ollama, privacidad y segundo plano.
- Administración local de modelos de Ollama: listar, descargar, seleccionar y eliminar.
- Centro de diagnóstico para voz, IA, Vision, bandeja, inicio y archivos locales.
- Copia segura del diagnóstico sin conversaciones, capturas ni claves.
- Recuperación de archivos JSON dañados mediante respaldos `.corrupt-*`.
- Escritura atómica de configuración e historial para reducir corrupción ante cierres inesperados.

### Cambiado

- La cápsula ahora aparece en la parte superior central.
- La interfaz principal ya no necesita desplazamiento vertical.
- CPU, RAM y GPU sustituyen al almacenamiento en la vista rápida.
- El reconocimiento clásico de Windows se reemplaza por transcripción local con Whisper.
- Las respuestas abiertas son más breves y dejan de convertir consultas no relacionadas en diagnósticos del sistema.
- El silencio final de la escucha automática se reduce para entregar antes la orden.
- La frase de activación puede decirse junto con la orden completa.

### Pendiente

- OCR y copia de texto desde capturas.
- Difuminado manual de información sensible.
- Calibración guiada del micrófono.
- Diccionario personal de correcciones.
- Reducir todavía más el consumo del detector permanente.
- Respaldo semántico para convertir instrucciones ambiguas en acciones locales validadas.


## 0.7.0-alpha - Rutinas y motor de acciones seguras

- Editor local de rutinas con frases de activación.
- Rutinas predeterminadas para programación, estudio y descanso.
- Motor de acciones permitidas con validación, riesgo y resultados por paso.
- Vista previa y confirmación para acciones sensibles.
- Ejecución tolerante a fallos: un paso fallido no cancela los demás.
- Persistencia local en `%LocalAppData%\Nexo\routines.json`.
- Comandos por texto o voz para abrir, listar y ejecutar rutinas.
## 0.8.0-alpha - Integración con Windows

- Bandeja del sistema y funcionamiento real en segundo plano.
- Inicio opcional con Windows en modo oculto.
- Instancia única con activación de la ventana existente.
- Notificaciones y sonidos configurables para recordatorios y enfoque.
- Recuperación tras suspensión y reanudación del equipo.
- Salida completa disponible desde el menú de la bandeja.

