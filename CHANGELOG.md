# Changelog

## [0.30.24-beta]

### Cambiado

- **Atajos, más tranquilo.**
  - Cada atajo es una fila con ▶ delante, su frase debajo y lo que hace.
  - Pausar, Editar y Eliminar aparecen al pasar el ratón.
  - Las plantillas y «Nuevo» van en una sola línea arriba.
  - Un atajo en pausa se ve atenuado.
  - La última ejecución se muestra en formato de 12 horas.
- **Audio:** cada aplicación ya no dice «Sesión disponible»; solo se indica «Sonando ahora» cuando suena algo.

### Corregido

- En Atajos, el botón «Desactivar» salía cortado. Ahora dice «Pausar» y cabe.

## [0.30.23-beta]

### Nuevo

- **Alt+Shift+F: buscar archivos.**
  - Se busca por nombre o por palabras de dentro, con el índice de Windows (lo mismo que usa el Explorador).
  - Busca en Documentos, Escritorio, Descargas, Imágenes, Vídeos y Música.
  - Enter abre el archivo y Ctrl+Enter abre su carpeta.
  - Todo ocurre en el equipo. También está en la paleta como «Buscar archivos».
- **Hábitos, si los quieres.** Se activan en Personalizar → Módulos («Hábitos») y aparecen como una fila dentro de Hoy.
  - Tocar un hábito lo marca hecho hoy; volver a tocarlo lo desmarca. Clic derecho para quitarlo.
  - La racha no se rompe por no haberlo marcado todavía hoy, y no hay avisos insistentes.
  - Máximo ocho hábitos. Vienen apagados.

## [0.30.22-beta]

### Nuevo

- **Exportar una conversación.** En el **+** de la última respuesta, o desde la paleta con «Exportar la conversación», el chat se guarda como archivo Markdown en `Documentos\Sakura\Conversaciones` y se abre la carpeta.
- **«Apuntar una tarea» también está en la paleta.**
- **Casilla propia para el panel de volumen y brillo** en Personalizar («Volumen y brillo en el borde contrario»).

### Cambiado

- **En una instalación nueva, Peek y el panel de volumen y brillo del borde vienen apagados.** Quien actualiza conserva lo que tenía.

### Corregido

- **En una instalación nueva, Inicio y Hoy no aparecían en el menú lateral** (0.30.19). El cambio solo se aplicaba al actualizar.

## [0.30.21-beta]

### Nuevo

- **Alt+Shift+R: hacer algo con el texto seleccionado en cualquier aplicación.**
  - Seleccionas un texto, pulsas el atajo y eliges: Más claro, Corregir, Más corto, Resumir, Al inglés o Al español.
  - El resultado aparece conforme llega; **Reemplazar** lo pone en lugar de lo seleccionado y **Copiar** lo deja en el portapapeles.
  - La selección se lee primero sin tocar el portapapeles; si hace falta copiarla, se devuelve lo que había. Si el portapapeles tiene una imagen o archivos, no se toca.
  - No funciona en ventanas marcadas como sensibles.
  - Necesita un modelo de IA, y el texto se le envía tal cual. Así consta ya en la política de privacidad.
- **Plantillas en Atajos:** Estudiar, Programar y Descanso abren el editor ya rellenado.

### Cambiado

- **Enfoque más flexible:**
  - Duraciones de un toque: «Solo 5 min para empezar», 15, 25 y la última que usaste, que Sakura recuerda.
  - Al terminar, **Seguir 10 min más** continúa con la misma tarea sin volver a elegir.
  - Ya no hay tipos de sesión (enfoque, descanso, temporizador): un rato es un rato.
- **Rutinas ahora se llama Atajos.** Los atajos de texto del dictado pasan a llamarse **Textos rápidos**, para no confundirlos.

## [0.30.20-beta]

### Nuevo

- **Repaso de la mañana.** La primera vez que abres Sakura cada día, si quedó algo pendiente de antes, Inicio empieza preguntando qué hacer con cada cosa: **Hoy**, **Mañana** o **Soltar**. Sale una vez al día y solo si hay algo.
- **Alt+Shift+N para apuntar desde cualquier aplicación.** Se abre una cajita arriba: escribes, Enter y listo, sin abrir Sakura.

### Cambiado

- **Las horas se muestran como se dicen:** «5:00 pm», «9:30 am», en vez de 17:00.
- **Editar una tarea es más claro** (Adler: «casi no se ve nada al seleccionar el día»):
  - Cada caja dice qué va en ella.
  - La fecha y la hora se escriben juntas y como se dicen («hoy 5 pm», «viernes», «20/09»), con botones Hoy, Mañana y Sin fecha, en lugar del calendario de Windows.
  - Debajo se ve lo que se entendió.
- **Enfocarse ya no arranca 25 minutos sin preguntar.** ▶ solo aparece en lo importante y pregunta cuánto rato (desde «solo 5 min para empezar»). En Inicio, una tarea que no es importante ofrece **Hecha** en lugar de **Empezar**: un recordatorio como «comprar tortillas» no pide sentarse a trabajar.
- El aviso «Soltaste…» se va solo a los ocho segundos.

### Corregido

- El ejemplo de los atajos de texto en Personalizar mostraba un correo con el nombre del autor; ahora es genérico.

## [0.30.19-beta]

### Nuevo

- **Hoy, rehecho como el centro del día.**
  - **Se apunta en una línea**, como se diría: «entregar U2A2 el viernes a las 5 !». Sakura deduce la fecha y la hora, y «!» o «importante» la marcan como importante. Antes de guardar se ve lo que entendió; si no entiende una fecha, no inventa ninguna.
  - **Secciones tranquilas:** Importantes (tres como máximo), Hoy, Luego (plegada) y Hechas hoy (plegada). Ya no hay contador de vencidas: lo atrasado dice «Quedó pendiente».
  - **Cada tarea tiene un círculo para marcarla**, y al pasar el ratón aparecen ▶ (empieza el enfoque ya), **Mañana** y **Soltar**.
  - **Soltar** saca la tarea de la vista sin borrarla, y se puede deshacer. Eliminar sigue dentro de «Editar».
- **El calendario del panel de arriba ya sirve:** un punto marca los días con algo pendiente, y tocar un día abre Hoy en ese día. Lo que apuntes ahí queda para esa fecha.
- **Instalación nueva:** Inicio invita con un ejemplo en vez de mostrar ceros, y Hoy explica cómo apuntar.

### Cambiado

- **Enfoque sale del menú lateral.** Se empieza desde una tarea (en Hoy o en Inicio) o desde la paleta.
- **Inicio y Hoy se ven en el menú lateral**, también al actualizar. Se pueden volver a ocultar en Personalizar.

## [0.30.18-beta]

### Nuevo

- **Inicio nuevo, más tranquilo.** Solo queda lo que responde a «¿qué hago ahora?»:
  - Un saludo con la fecha.
  - Una burbuja **Ahora** con lo más importante del día. Al tocarla se parte en dos: **Empezar**, que arranca 25 minutos de enfoque en esa tarea, y **Mañana**, que la pasa al día siguiente sin reproches.
  - Dos cifras del día: tareas hechas y minutos de enfoque.
  - **¿Un resumen de ayer?**: se abre como un hilo con lo que hiciste ayer y a qué hora. Sale solo de lo que hay en este equipo; no lee tus conversaciones.
  - Las tres últimas cosas que pasaron, en pequeño.
- Las tareas atrasadas dicen «Quedó pendiente», no «vencida».

### Cambiado

- **Debajo de cada respuesta ya no hay dos filas de botones.** «Más corto», «¿Primer paso?», «Guardar como Word»… siguen ahí, detrás de un **+** pequeño.

### Corregido

- **El panel de volumen y brillo a veces seguía sin irse** (Adler, en la 0.30.17). Vive en el borde, justo donde se queda el ratón que lo abrió, y el ratón parado encima lo mantenía. Ahora solo lo mantiene un ratón que se mueve: parado, se va a los ocho segundos. Tampoco lo retiene un foco de teclado viejo si el panel no está delante.

## [0.30.17-beta]

### Corregido

- **El panel de volumen y brillo «no se quitaba».** Con algo pegado al borde —las pestañas verticales de un navegador, por ejemplo—, el panel salía, se cerraba a los cuatro segundos y volvía a salir enseguida porque el ratón seguía ahí. Ahora el borde abre algo una vez por visita: no vuelve a hacerlo hasta que el ratón se aparta de verdad. Vale también para el panel de Sakura.
- **El panel ya no se queda puesto si Windows no avisa de que el ratón se fue.** Al cumplirse la espera, Sakura mira dónde está el ratón de verdad: si no está encima ni se está arrastrando una barra, el panel se va.

## [0.30.16-beta]

### Nuevo

- **Fuentes reales en los borradores.** Al guardar como Word o PowerPoint, Sakura busca trabajos en español y de los últimos ocho años sobre el tema en dos catálogos académicos abiertos, OpenAlex y Crossref, y los deja al final en «Fuentes para consultar», en formato APA 7 y con sangría francesa. Cada referencia se escribe con los datos que devuelve el catálogo: si falta el volumen o las páginas, va sin ellos; si un nombre no se puede separar con seguridad en apellidos y nombre, se deja como vino.
- **Se pregunta una sola vez por lo que sale a internet**: las imágenes y las fuentes van en el mismo aviso, cada una guarda su respuesta y las dos se cambian en Ajustes.
- Si no aparece ninguna fuente que valga, el documento sale sin la lista y Sakura lo dice, en vez de rellenarla a la fuerza.

## [0.30.15-beta]

### Corregido

- **El cajón de arriba y el mando de volumen dejaban de salir**, aunque no hubiera nada a pantalla completa (Adler, en la 0.30.12; ocurría de vez en cuando y no se podía reproducir). Los dos solo aparecen al rozar el borde, y ese camino se calla mientras Sakura cree que hay un juego delante. Si el ciclo que lee el equipo se quedaba colgado —una lectura del sistema que no vuelve, el motor de voz ocupado—, esa creencia se congelaba y ya no volvía a comprobarse.
  - Una decisión que nadie ha refrescado en quince segundos deja de mandar: se vuelve al comportamiento normal en vez de quedarse mudo.
  - La lectura del equipo tiene tope de tiempo, como ya lo tenía la del reproductor, así que el ciclo no puede quedarse a medias con su cerrojo tomado.
  - Si una vuelta sigue dentro, la siguiente se salta en lugar de hacer cola.

## [0.30.14-beta]

### Nuevo

- **Las fórmulas van al editor de ecuaciones de Word.** Escribe `$x^2 + 1$` dentro de una frase o `$$…$$` en su propio renglón, con notación de calculadora (`x^2`, `x_1`, `sqrt(x)`, `pi`, `<=`) o de LaTeX (`rac{a}{b}`, `\sqrt[3]{x}`, `\pi`, `\leq`), y en el documento sale una ecuación de verdad: fracciones con su raya, raíces, sumatorias, integrales, límites y letras griegas, editables desde Word.
- **En el chat las fórmulas se leen en claro**, sin los signos de dólar ni las barras de LaTeX.
- **Si alguna fórmula no se puede convertir**, el documento se guarda igual y Sakura dice cuál quedó como texto, en vez de dibujar algo incorrecto.

### Cambiado

- Sakura escribe las fórmulas con esa notación, para que se conviertan solas al guardar.
- Lo común de las consultas a catálogos públicos —identificarse, cortar a tiempo, leer el JSON— deja de estar copiado en cada servicio.

## [0.30.13-beta]

### Corregido

- **El chat se quedaba mudo tras explicar una ventana.** Al seguir preguntando (por ejemplo con «Más fácil»), la captura seguía en el contexto, la pregunta iba al modelo con visión y el proveedor la rechazaba entera por el tamaño previsto de la respuesta: «Request too large… reduce max_tokens». Ahora Sakura lee el tope que el propio proveedor indica, repite la petición pidiendo menos y contesta. Si ni así cabe, lo explica en español y dice qué hacer, en vez de enseñar el error del proveedor en inglés.
- **Los botones de seguimiento ya no reenvían la captura.** «Más corto», «Más fácil», «Hazlo lista» y los demás trabajan sobre la respuesta anterior, que ya está en la conversación: no necesitan la imagen y así no obligan a usar el modelo con visión.

## [0.30.12-beta]

### Nuevo

- **Los documentos de Word también llevan imágenes y gráficas.** Una línea «Imagen: …» coloca ahí la foto, centrada y con su pie («Figura 1. Título — Autor · Licencia»), y una tabla de cifras se queda tal cual con su gráfica debajo, editable desde Word porque lleva sus datos dentro.
- **Nota de uso de IA en todo lo que Sakura escribe**: en el pie de cada página del Word, en el pie de impresión de las hojas de Excel y en la última diapositiva. Sakura deja borradores para trabajar encima, no trabajos terminados, y eso queda dicho en el propio archivo.

### Cambiado

- La pregunta de las imágenes y su casilla en Ajustes hablan de «documentos», porque ahora valen para Word y para PowerPoint.

## [0.30.11-beta]

### Nuevo

- **Imágenes en las presentaciones.** Si la respuesta pide una foto para una diapositiva («Imagen: turbinas eólicas en el mar»), Sakura la busca en Wikimedia Commons, de licencia libre, y la coloca: en la portada, a un lado del texto o de fondo en los separadores y en la conclusión. Cada foto lleva su crédito y la presentación termina con una diapositiva de créditos.
- **Sakura pregunta la primera vez** antes de buscar imágenes, porque es salir a internet. Solo sale de tu equipo el término de búsqueda. Se cambia cuando quieras en Ajustes › «Buscar imágenes para las presentaciones».

### Cambiado

- **Sakura puede pedir la foto de cada diapositiva** con una línea «Imagen: …» que describe lo que debería verse.
- Si una búsqueda no encuentra nada, se prueba con menos palabras; si aun así no hay imagen aprovechable —o PowerPoint no sabría dibujarla—, esa diapositiva sale sin foto y el documento se guarda igual.

## [0.30.10-beta]

### Nuevo

- **Gráficas de verdad en PowerPoint y Excel.** Una tabla de cifras (etiquetas en la primera columna y números o porcentajes en las demás) se dibuja como gráfica nativa: líneas si son años o meses, pastel si son partes de un todo, barras o columnas en lo demás, o el tipo que diga el título («evolución», «distribución», «barras»). En PowerPoint se editan con «Editar datos»; en Excel la gráfica va al lado de su tabla. Las tablas con texto o con cifras de distinta clase se quedan como tabla.
- **Presentaciones con más diseño**: portada con panel de color, índice numerado cuando hay de cuatro a ocho apartados, fila con los pasos en las listas de pasos, separadores numerados cuando la respuesta tiene partes, citas en grande, dos columnas para muchos puntos cortos y la conclusión sobre fondo oscuro. Cada diapositiva lleva abajo el título y su número.
- **Notas del orador**: un párrafo que empieza por «Notas:» va a las notas de esa diapositiva y no se ve al presentar.

### Cambiado

- **Sakura escribe las presentaciones pensando en ellas**: de tres a cinco puntos por diapositiva, las cifras en tablas, notas del orador y un apartado final de conclusión, sin inventar cifras ni fuentes.
- **Las tablas largas** siguen en otra diapositiva con la cabecera repetida en vez de perder filas, y sus columnas tienen el ancho de su contenido.
- **La portada** ya no toma como subtítulo frases del chat como «Claro, aquí tienes…».

## [0.30.9-beta]

### Nuevo

- **Guardar una respuesta como Excel.** Cada tabla es una hoja con la cabecera fija, filtros y columnas ajustadas, y los números y porcentajes se guardan como números para poder sumarlos y ordenarlos. Una respuesta sin tablas se guarda con una fila por apartado o punto.
- **Guardar una respuesta como PowerPoint.** Una portada, una diapositiva por apartado o por paso, las tablas en su propia diapositiva y un diseño sobrio con el color de Sakura.
- **Botones «Guardar como» bajo la última respuesta** del chat: Word, Excel o PowerPoint, en el escritorio. También están en el clic derecho.

## [0.30.8-beta]

### Cambiado

- **Las imágenes funcionan aunque tu modelo solo lea texto.** Si una pregunta lleva una captura y el modelo elegido no admite imágenes, Sakura usa para esa pregunta un modelo con visión del mismo proveedor (con Groq, qwen3.8-27b). Tu modelo normal no cambia.
- **Los documentos de Word salen con formato**: títulos, listas numeradas y con viñetas, negritas, cursivas, enlaces, tablas, código y citas, sin asteriscos ni guiones sueltos.
- **Sakura ya no explica cómo copiar a Word**: sabe que puede guardar la respuesta como documento y escribe directamente el contenido.
- **Ctrl + Shift + Espacio marca solo lo que hay que pulsar**: los botones o menús que los pasos nombran, como mucho tres y nunca los de la barra de la ventana.
- **Las citas** («> …») se ven como citas en el chat y en Word.

### Corregido

- **El menú del clic derecho** sobre una respuesta salía blanco y casi no se leía.
- **En la píldora**, el enlace «Ver la conversación en Sakura» se ponía blanco al pasar el ratón, y el cuadro para seguir preguntando quedaba cortado por abajo.

## [0.30.7-beta]

### Nuevo

- **Grabar la pantalla**, con Alt + Shift + G o desde la pestaña Captura. Graba en MP4 a 60 fps con la tarjeta gráfica, con el sonido del equipo y, si lo marcas, el micrófono, y lo guarda en Vídeos › Sakura. Mientras graba se ve arriba una píldora con el tiempo y un botón para parar, que no sale en el vídeo.
- **Captura de una región** (Alt + Shift + S) y **captura con retraso** (3, 5 o 10 segundos) en la pestaña Captura. Puedes guardarla en Imágenes › Sakura, copiarla o preguntarle a Sakura sobre ella.

### Cambiado

- **Ctrl + Shift + Espacio explica la ventana que tienes delante**: qué es, qué está pasando, cómo resolverlo y los pasos, en la píldora de respuesta. Funciona también con modelos que no admiten imágenes, leyendo el texto de la ventana, y las preguntas siguientes saben de qué ventana hablas.
- **Personalizar dice si ya hay una clave de IA guardada** y en qué termina, en vez de mostrar un cuadro vacío que parecía que la clave se había borrado.
- **La política de privacidad** explica que las capturas guardadas y las grabaciones se quedan en tu equipo, y qué se envía al proveedor de IA al explicar una ventana.

## [0.30.6-beta]

### Cambiado

- **Puedes seguir la conversación desde la píldora de Ctrl + Espacio.** Debajo de la respuesta hay botones (Más corto, Más fácil, ¿Primer paso?, Con ejemplos, Ponlo a prueba) y un cuadro para seguir preguntando; la nueva respuesta llega a la misma píldora y Sakura recuerda de qué hablabais.
- **Los enlaces de las respuestas se pueden pulsar**, en la píldora y en el chat. Se ven acortados y la dirección completa aparece al pasar el ratón. Solo abren páginas web.
- **Las respuestas salen parejas, sin tirones**, y con su formato desde la primera palabra.
- **Mientras Sakura piensa** se ven cuatro pétalos que giran, se juntan y se abren.
- **El chat también tiene los botones para seguir** debajo de la última respuesta.

### Corregido

- **En el panel superior quedaba un hueco** debajo del reloj, el calendario y los anillos; ahora llegan hasta abajo, a la altura del GIF.

## [0.30.5-beta]

### Cambiado

- **El gestor de modelos locales tiene el aspecto del resto de Sakura**: barra de título oscura, el estado de Ollama con un punto de color, cada modelo con su tamaño y «En uso» en el que Sakura tiene elegido, y la selección con tu color de acento. Doble clic en un modelo para usarlo.
- **Las descargas de modelos se entienden mejor**: una barra que avanza suave, los pasos en castellano y cuánto lleva de cuánto («1,3 GB de 3,1 GB»). Hay botones con los nombres de los modelos recomendados.
- **Eliminar un modelo se confirma dentro de la ventana** y dice cuánto espacio libera.

### Corregido

- **No había forma de cancelar la descarga de un modelo.** Ahora hay un botón Cancelar.
- **Si Sakura no encontraba Ollama**, mostraba el mensaje técnico de Windows; ahora explica qué hacer y deja el detalle en la ayuda emergente.

## [0.30.4-beta]

### Cambiado

- **Peek aparece como una burbuja** desde su lado de la pantalla, con los mismos anillos con icono del panel superior; las cifras suben mientras el anillo se llena. Si tienes el ratón encima, espera a que lo quites para irse.
- **El Command Center (Ctrl + K) tiene el mismo aspecto que la paleta de Ctrl + Espacio**: esquinas redondeadas, el icono de su sección en cada fila, los atajos dibujados como teclas y las filas entrando una detrás de otra.
- **Los resaltados de Lens usan tu color de acento** con un halo suave y la flor de Sakura en la esquina; aparecen uno detrás de otro y se apagan poco a poco en vez de desaparecer de golpe.
- **Elegir qué verá Lens y la vista previa de la captura** tienen la barra de título oscura, la selección con el acento en lugar del azul de Windows e iconos distintos para ventanas y monitores.
- **El punto de «Mirando» late** mientras Lens observa.

### Corregido

- **El aviso para elegir una zona de la pantalla** (Ctrl + Shift + T) salía en la esquina del escritorio; ahora está arriba en el centro de la pantalla principal y muestra los atajos como teclas.
- **En el Command Center aparecía una barra de desplazamiento dentro del cuadro de búsqueda** y lo escrito no quedaba alineado con el texto de ayuda.

## [0.30.3-beta]

### Cambiado

- **La paleta de Ctrl + Espacio tiene el estilo de Sakura.** Esquinas redondeadas de verdad, sin el canto de color que Windows le ponía arriba; la flor de Sakura en lugar de una estrella, que gira un poco mientras la paleta aparece; y las sugerencias entran una detrás de otra al abrirse la lista.
- **Los atajos del pie de la paleta ya no se pisan**: se dibujan como teclas, cada uno con una etiqueta corta.
- **Las opciones de movimiento de la paleta parecen botones**, y la elegida se marca con el color de acento.

### Corregido

- **Algunos paneles de Personalizar dejaban de atenuarse** al desactivar su opción después de haber visto la sección animada.
- **La píldora de Sakura podía quedarse con una altura fija** si volvía a aparecer justo mientras se ocultaba.

## [0.30.2-beta]

### Cambiado

- **La píldora de respuesta también muestra el formato.** Lo que preguntas con Ctrl + Espacio llega con negritas y listas bien dibujadas, como en el chat, en vez de con asteriscos.
- **La primera vez que usas la voz se ve cuánto falta.** Mientras se descarga el modelo, la burbuja de Sakura muestra un anillo que se va llenando y el porcentaje debajo, en lugar de un aviso arriba sin avance. Si pediste escuchar, al terminar la burbuja se queda y empieza a escucharte sin desaparecer.
- **El panel de arriba se organiza al bajar**: las tarjetas entran una detrás de otra, y lo mismo al cambiar entre Panel, Media, Rendimiento y Cheats.
- **Una bienvenida más cuidada**: la barra de título ya no toma el color de acento de Windows, el avance se ve en cuatro píldoras, cada paso entra con movimiento y su contenido llega escalonado, y ya no se repite el título de cada paso. Si algo no cabe, se desplaza en vez de cortarse.
- **Las secciones de Personalizar**, al abrirse, muestran sus opciones una detrás de otra.

### Corregido

- **La bienvenida decía que Sakura instala la IA local «con un solo botón»**, y en la versión de Microsoft Store no es así: ahí lleva a la web de Ollama. Ahora el texto vale para las dos versiones.

## [0.30.1-beta]

### Cambiado

- **La voz se descarga cuando la usas, no al abrir Sakura.** El modelo de transcripción (unos 140 MB) ya no se baja la primera vez que abres la app: espera a que pulses el micrófono, Alt + V, digas «Oye Sakura» o dictes. Quien solo escribe no gasta esos datos.
- **Las respuestas se leen con su formato.** Las negritas, las listas, los pasos numerados y el código se ven como tales, sin asteriscos ni barras. Las tablas se muestran como una tarjeta por fila, que en un panel estrecho se leen mucho mejor.
- **Sakura responde más ordenada**: párrafos cortos y listas en lugar de tablas, y avisa cuando algo que pregunta puede haber cambiado, como quién ocupa un cargo o un precio.
- **Los volúmenes de la pestaña Audio usan el mismo control líquido** que el volumen y el brillo del borde de la pantalla.
- **La pestaña Voz del panel ahora se llama «Cheats»**: todos los atajos de Sakura (Alt + A, Ctrl + Espacio, Alt + V…) y unos cuantos de Windows dibujados como teclas, las frases que Sakura entiende sin IA y el estado de la escucha en una línea.
- **El logo que aparece cuando Sakura te escucha ya se ve**: va dentro de una burbuja oscura con borde, que se infla al aparecer y se desinfla al irse. Antes era un contorno fino que se perdía sobre el fondo de pantalla.

### Corregido

- **Al hablarle de corrido, «oy Sakura» o «oy esa Sakura» se colaban en la pregunta** que se mandaba a la IA.

## [0.30.0-beta]

### Agregado

- **Reportar una respuesta de IA.** Cada respuesta de Sakura tiene en su menú (clic derecho) «Reportar esta respuesta»: copia el texto a tu portapapeles y abre un formulario para avisar si era ofensiva, peligrosa o falsa. No se envía nada por su cuenta. También está como orden en la paleta, para usarlo con el teclado.
- **Sakura se prepara para llegar a Microsoft Store** como «Sakura Assistant». La versión de la Store la firma Microsoft, así que no mostrará el aviso de SmartScreen. La descarga desde GitHub sigue igual.

### Cambiado

- **La versión de la Store se comporta distinto donde tiene que hacerlo:** no usa el actualizador propio, porque actualiza la Store; arranca con Windows por el mecanismo del paquete; y no instala Ollama por su cuenta, sino que lleva a su web. La copia descargada de GitHub no cambia en nada de esto.
- **La ventana de Sakura tiene las mismas esquinas redondeadas que el panel de arriba.** A cambio, la transparencia ya no difumina lo que hay detrás: se ve a través, sin desenfoque.
- **Ya no hay destello gris al abrir la ventana.** Antes se veía un instante un rectángulo vacío antes del contenido.
- **Cuando baja el panel de arriba, la ventana de Sakura se encoge hacia abajo** para que no la tape, y recupera su tamaño en cuanto el panel se recoge.
- **Volumen y brillo nuevos.** Una línea que se abomba alrededor del mando como si fuera líquido, con el valor en grande al lado y en el color que tengas elegido. El mando lleva la flor de Sakura y gira con el valor.
- **La primera vez que abres el asistente en cada sesión, se organiza solo:** las tarjetas crecen, el saludo aparece palabra a palabra y las sugerencias entran una detrás de otra. Las veces siguientes se abre rápido, como siempre.
- **Los avisos y las píldoras aparecen como burbujas:** nacen como un círculo que se estira hasta su forma, y al irse se recogen igual.
- **La imagen o el GIF del panel tiene las esquinas redondeadas**, como la tarjeta de música, en vez de un rectángulo con picos.
- **Textos más cortos.** El plan adaptativo de Sistema nombra cada motor una vez en lugar de cuatro, y Personalizar explica cada opción en una línea.

### Corregido

- **Mientras Sakura respondía en la píldora salían dos avisos** que decían lo mismo. Ahora sale uno.
- **Los botones llevaban el texto pegado al borde**, y algunos, como «Siguiente» en la bienvenida, parecían cortados.
- **Lo que se desplaza se cortaba en seco** contra el borde redondeado de las tarjetas. Ahora se desvanece.
- **Algunos textos de Sistema salían cortados**: el procesador, la gráfica y la descripción de Sakura Runtime.

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

