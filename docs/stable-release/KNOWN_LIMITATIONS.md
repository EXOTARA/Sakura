# Limitaciones conocidas — Kohana

> Documento honesto. Si algo no cumple los criterios de estabilidad, aparece aquí — no se oculta.
> Formato: qué es · por qué · cómo está aislado · qué falta para declararlo estable.

## Bloqueantes de RC (deben resolverse)

### L1 — Baseline parcialmente medido ⚠️ (reducido el 2026-07-23)
**Qué:** Build, pruebas y tiempos **ya están medidos** (356 pruebas, 0 fallidas, 0 warnings, build
Release en frío 2.52 s — ver `IMPLEMENTATION_LOG.md`). **Siguen sin medir** el tamaño del portable,
el tamaño del instalador y el SHA-256, porque requieren `dotnet publish` y compilar el instalador.
**Aislamiento:** Presupuestos de latencia siguen marcados `PENDIENTE DE CALIBRAR`: el baseline de
build/test **no** mide latencia de voz, wake word ni TTS, que exigen micrófono y escenarios reales.
**Para estable:** medir artefactos en Fase 10 y calibrar latencias en Fase 3 (Voice Lab).

### L2 — `MainWindow.xaml.cs` como God Object ⚠️ (en extracción desde el 2026-09-14)
**Qué:** medido el 2026-09-14: **9.546 líneas, 254 métodos, 92 campos `readonly`**. La cifra de julio
(4.044) es de antes de todos los sprints de interfaz. Métodos más grandes: `WireSettingsEvents` (628
líneas), `SendPromptToAiCoreAsync` (293), `BuildCommandRegistry` (255), `ExecuteLensAsync` (167).
**Reparto aproximado por capacidad** (por nombre de método): shell y ventanas ~1.490 líneas, IA y
asistente ~970, voz ~950, ajustes ~760, proyecto ~715, tareas/enfoque/rutinas ~585, sistema y
métricas ~570, catálogo de órdenes ~525, Lens y traductor ~500, Computer Use y permisos ~315,
actualizaciones ~240, memoria y packs ~215, sin clasificar ~935.

**Método** (el de `docs/design/SAKURA_WPF_UI_PLAYBOOK.md` §2): una capacidad cada vez; se leen sus
métodos, se escriben pruebas que fijan lo que hacen **antes** de moverlos, se mueven a una clase que se
prueba sin ventana, la ventana se queda con los enganches, y se comprueba en la aplicación publicada.
Como `MainWindow` no se puede construir en una prueba, la caracterización se hace contra el código
leído; cualquier asimetría heredada se conserva y se nombra en la prueba, y si es un fallo se cambia
aparte.

**Hecho:**
1. **Actualizaciones** → `Nexo.App/Updates/UpdateFlowCoordinator` (13 pruebas). −155 líneas. Comprobado
   con la aplicación publicada: «Buscar actualizaciones» consulta GitHub y responde.

**Orden previsto**, de lo más aislado a lo más atado a WPF: memoria y packs → Computer Use, permisos y
auditoría → tareas, enfoque y rutinas → sistema, métricas y optimización → Lens y traductor → proyecto
(con cuidado: escribe archivos de la persona) → voz → IA y asistente → `WireSettingsEvents`, partido
por sección → shell y ventanas, lo último. Cada orden del catálogo viaja con su capacidad.

**Para estable:** que ninguna capacidad tenga decisiones de producto en la ventana.

### L3 — Accesibilidad: el Narrador ya se ha oído ⚠️ (reducido el 2026-09-14)
**Qué había:** la nota original decía «0 `AutomationProperties` en los 22 archivos XAML». Ya no es
cierto: los controles se nombraron a lo largo de los sprints de interfaz.

**Recorrido por Automatización de UI (2026-09-13):** lo que lee el Narrador —tipo y nombre de cada
control— sobre la 0.29.1 instalada: asistente de configuración, ventana principal, Sistema, paleta y
las once secciones de Personalizar. Arreglado en el mismo cambio: filas de Permisos anunciadas como
`Nexo.App.Views.SettingsView+PermissionRow`, cabeceras de sección que decían «desactivado» en vez de
«contraído» (`SectionHeaderToggle`), listas sin nombre y `ScrollViewer` vacíos. Las ventanas
secundarias las cubrió #56.

**El Narrador oído (2026-09-14):** con el Narrador encendido en el equipo de Adler, se recorrió la
ventana principal con Tab y Personalizar → Permisos, grabando el sonido del sistema (loopback WASAPI)
y transcribiéndolo con Whisper. Lo que dijo:
- «Sakura, ventana. Escribe una orden o pregunta, editar.» al abrir.
- Cada botón con su nombre y su ayuda: «Hablar por micrófono, botón», «Recoger Sakura, botón»…
- «Permisos, botón, contraído» y, tras la barra espaciadora, «expandido».
- Cada permiso con nombre, valor y tipo: «Ver la pantalla (Lens), permitido, cuadro combinado».

**Lo que se oyó mal, y se arregló:**
- **Dos paradas de Tab vacías**, donde el Narrador se callaba: el contenedor de las vistas
  (`ModuleHost`, un `ContentControl` que acepta foco por omisión) y la flor de la marca
  (`SakuraFlowerMarkStyle`, usada en seis ventanas). Tras el arreglo, el ciclo completo de Tab de la
  ventana principal son 16 paradas, todas con nombre.
- La ayuda de «Recoger Sakura» decía «Oculta **el shell**»: jerga leída en voz alta.
- El área de la conversación no tenía nombre.

**Qué NO se ha hecho:** oír las ventanas que salen por gesto (panel superior, controles rápidos,
píldora), el contraste con alto contraste de Windows, y una sesión real con alguien que use el
Narrador a diario.

### ~~L12 — La versión que Windows muestra se queda vieja tras cada actualización~~ ✅ (2026-08-23)
**Qué era:** el registro de "Aplicaciones instaladas" seguía diciendo la versión que puso el
instalador. Medido dos veces en el equipo de Adler: `0.25.0-dev.20260817` con el ejecutable en
0.26.9, y `Sakura 0.27.0-beta` con el ejecutable ya dos versiones por delante. El nombre también
llevaba la versión, así que mentía dos veces.
**Por qué:** el actualizador reemplaza los archivos mediante su ayudante; no ejecuta el instalador,
así que nadie tocaba la entrada del registro.
**Arreglado (Diseño D87):** `InstalledVersionPolicy` decide y `WindowsInstalledVersionRegistrar`
escribe, **en cada arranque y no al actualizar**. Hacerlo solo al actualizar habría arreglado las
futuras dejando mintiendo a todas las instalaciones que ya estaban mal. Sin entrada en el registro
no se inventa ninguna: quien usa el zip portable no aparece en esa lista y no debe aparecer.

### ~~L13 — Desinstalar después de actualizar dejaba archivos~~ ✅ (2026-08-23)
**Qué era:** el desinstalador de Inno borra lo que su propio registro dice que puso. El actualizador
no pasa por el instalador, así que lo que hay en la carpeta después de actualizar no es lo que Inno
anotó.

**Y era peor de lo que la nota decía.** Esta limitación se escribió deduciendo, y la deducción daba
por hecho que el ayudante *copiaba archivos encima*. No lo hace: **intercambia la carpeta entera**
—aparta la instalación, pone en su sitio el contenido del zip portable y borra la apartada—. Y ese
zip es la salida de `dotnet publish`, que **no trae `unins000.exe` ni `unins000.dat`**, porque esos
los escribe Inno al instalar.

El resultado real no era una DLL huérfana: era que **cada actualización borraba el desinstalador**,
dejando en «Aplicaciones instaladas» una entrada que apuntaba a un archivo inexistente. Desinstalar
desde Windows no dejaba restos porque no llegaba a empezar. La primera medida de este ciclo
(2026-08-23, antes de leer el ayudante) reprodujo el mecanismo equivocado y describió un
`WpfAnimatedGif.dll` superviviente que en la realidad no llega a existir.

**Arreglado, en dos piezas que se necesitan mutuamente:**

1. **El ayudante se lleva el desinstalador consigo** (`UpdateHelperScript`): copia `unins*` de la
   instalación a la carpeta preparada antes de mover nada, mientras las dos siguen donde estaban.
   Sin esto no hay nada que arreglar, porque no hay desinstalador. Que no haya nada que copiar no es
   un fallo: quien usa el zip portable nunca tuvo uno.
2. **El desinstalador barre `{app}`** (`[UninstallDelete]` en `Sakura.iss`): así se lleva también lo
   que llegó después y él nunca anotó. Es seguro porque `{app}` es una carpeta exclusiva de Sakura
   (`…\Programs\Sakura`); los datos de la persona viven en `%LOCALAPPDATA%\Sakura`, que esa línea no
   toca. Anotar en el registro de Inno desde fuera —lo primero que se intentó— no es posible:
   `unins000.dat` es un formato binario propio sin forma soportada de añadirle entradas.

**Comprobado (2026-08-23):** ciclo completo en Windows Sandbox con instalador y portable construidos
con el arreglo dentro:

| Paso | Resultado |
| --- | --- |
| Instalar | salida 0, 511 archivos, registro dice `0.29.0-beta` |
| Intercambio como el del ayudante | **desinstalador conservado: 2 archivos**; 512 archivos, 1 que el instalador nunca anotó |
| Desinstalar con `unins000.exe` | salida 0 |
| Lo que quedó | **la carpeta ya no existe; 0 archivos**; entrada del registro borrada |

El desinstalador consigue borrarse a sí mismo y a la carpeta que está barriendo, que era la parte
que no se podía afirmar leyendo.

**Lo que esta medida no cubre:** el ciclo instala y actualiza a la **misma** versión, con un archivo
sintético (`llegada-en-la-actualizacion.dll`) haciendo de dependencia que llega después — el
instalador de partida tiene que llevar el barrido que se está probando, así que se construye en el
momento. Y el banco reproduce los pasos del ayudante, no ejecuta el guion que genera
`UpdateHelperScript`; que ese guion emita esos pasos lo cubren las pruebas de unidad.

**Cómo reproducirlo:** `scripts/build-installer.ps1` construye instalador y portable; se dejan junto
a `scripts/sandbox/Invoke-InstallCycle.ps1` en una carpeta, se monta con un `.wsb` y el Sandbox corre
el ciclo solo. Requiere `Containers-DisposableClientVM` activada y la virtualización (SVM/VT-x)
habilitada en la BIOS.

**Las instalaciones que ya estaban rotas se arreglan solas, reinstalando.** El arreglo impide que
vuelva a pasar, pero no devuelve un desinstalador que se borró hace versiones: ahí no hay nada que
copiar. La vía de recuperación es volver a pasar el instalador por encima, y **no hace falta tocar
nada desde la aplicación** — comprobado el 2026-08-23 con `scripts/sandbox/Invoke-RepairCycle.ps1`,
que instala, rompe la instalación igual que lo hacía el fallo, reinstala y desinstala:

| Paso | Resultado |
| --- | --- |
| Rota como lo hacía el fallo | 0 desinstaladores; el registro ofrece uno que no existe |
| Reinstalar por encima | **2 desinstaladores**; el registro ofrece uno que sí existe; 511 archivos |
| Desinstalar | salida 0; **la carpeta ya no existe; 0 archivos**; entrada borrada |

Funciona porque la entrada del registro guarda `Inno Setup: App Path`, y el instalador lo usa para
reinstalar exactamente donde ya estaba.

**Por eso esa entrada rota no se borra.** La primera idea para «limpiar» fue que Sakura quitara al
arrancar la entrada cuyo desinstalador ya no existe. Es mala por dos motivos, y los dos se ven en el
equipo de Adler, que está justo en ese estado: dejaría a la persona **sin ninguna** forma de
desinstalar en vez de con un botón roto, y el siguiente instalador —sin el rastro que le dice dónde
estaba— plantaría una segunda copia en su carpeta por defecto mientras la vieja (261 MB, en
`…\Programs\Kohana` por el nombre anterior del producto) se queda en disco para siempre. La entrada
rota es el hilo del que tirar, no basura.


### L14 — El instalador no llegaba a publicarse
**Qué:** las cinco últimas versiones (0.26.4 a 0.26.9) se publicaron **solo con el zip portable**.
**Por qué:** el flujo de release construía el instalador y luego intentaba *crear* una release que ya
existía; `gh` respondía "a release with the same tag name already exists", el paso fallaba y los
artefactos se perdían con él. Las cinco ejecuciones aparecen en rojo en GitHub Actions.
**Aislamiento:** el zip portable sí se publicaba, así que la actualización automática seguía
funcionando; lo que faltaba era la vía de instalación normal para alguien nuevo.
**Resuelto en el flujo (Diseño D67):** ahora sube los artefactos a la release exista o no, y falla a
propósito si no hay instalador que subir. **Pendiente de comprobar con la próxima versión.**

## Resueltas en la fase 1.1.1 (2026-07-23)

Se dejan registradas para no perder la trazabilidad de por qué existían.

### ~~L9 — Órdenes de enfoque eclipsadas por el parser de rutinas~~ ✅
**Qué era:** cualquier frase que empezara por "ejecuta/inicia/activa/corre" la reclamaba el
parser de rutinas, que corría primero. *"Inicia un temporizador de 20 minutos"* no arrancaba
ningún temporizador.
**Resuelto:** `RoutineMatchConfidence` + `PromptDispatchPolicy` (commit `667a873`).
Verificado en la aplicación real.

### ~~L10 — Ejecución arbitraria sin confirmación vía `OpenApplication`~~ ✅
**Qué era:** `OpenApplication` reenviaba `Arguments` al proceso y estaba clasificada como
`Reversible`. Una rutina con `powershell.exe -Command ...` se ejecutaba sin preguntar,
incumpliendo el escenario 22 de `TEST_MATRIX`.
**Resuelto:** `ShellExecutionPolicy` + `RoutineExecutionApproval` aplicado en `RoutineRunner`
(commit `4e3524d`). Verificado en la aplicación real: el diálogo aparece y cancelar no ejecuta nada.

### ~~L11 — `Dispose` no idempotente~~ ✅
**Qué era:** el segundo `Dispose` de `SingleInstanceCoordinator` lanzaba
`ObjectDisposedException`. Habría aflorado con el contenedor de DI de la fase 1.2.
**Resuelto:** guarda `_disposed` (commit `787db71`).

## No bloqueantes (documentadas, aisladas)

### L4 — Wake word sobre ASR de propósito general
**Qué:** Vosk con gramática de excepciones fonéticas. Techo estructural de precisión.
**Aislamiento:** Declarado `FallbackHeredado` (ADR 0004). `IWakeWordService` ya desacoplada.
**Para estable:** un candidato debe **ganar medido** en Voice Lab. Si ninguno gana, Vosk sigue y se
documenta como limitación aceptada.

**Falsos despertares por la gramática cerrada (medido con voz sintética, 2026-09-13).** La
gramática que recibe Vosk solo contiene variantes de la frase y `[unk]`, así que ante una frase
parecida no escribe lo que oye sino lo más cercano de la lista. «Voy a sacar la basura», «Oye, saca
la ropa», «Oye, ¿sabes a qué hora cierra?», «Oye, se acabó el café» y «Hoy sí cura la herida» salen
escritas como «oye sakura» y despiertan a Sakura, **también en sensibilidad Precisa**. Las pruebas
no lo veían porque comprueban el comparador con texto ya transcrito, nunca con audio.

| Estrategia | Aciertos (5 «Oye Sakura» dichos) | Falsos despertares (15 frases trampa) |
| --- | --- | --- |
| Actual: gramática cerrada | 5 | 9 |
| Gramática + confirmación del reconocedor libre | 5 | 0 |

**Con grabaciones reales de Adler (2026-09-13)**, capturadas con la grabación de escritorio de
NVIDIA —una sola pista, micrófono y audio del sistema mezclados, así que la música de la segunda
entra más limpia de lo que la oiría un micrófono—. Veces dichas contadas con Whisper y tramos de voz.

| Grabación | Dichas | Actual | Híbrido | Confusores en la gramática |
| --- | --- | --- | --- | --- |
| Cerca, en silencio (69 s) | ~12 | 10 | 9 | 10 |
| A dos metros con música (95 s) | ~16 | 14 | **4** | 12 |
| Frases trampa, nunca la dice (44 s) | 0 | **8 falsos** | 0 | 2 falsos |

- **El fallo se confirma con voz real:** ocho despertares falsos en 44 segundos de «voy a sacar la
  basura», «saca la ropa», «se acabó el café».
- **El híbrido queda descartado:** a distancia el reconocedor libre casi nunca escribe «sakura».
- **La confianza por palabra de Vosk no discrimina:** marca 1,00 también en las frases trampa.
- **Confusores** (la gramática ofrece además «saca», «sacar», «sabes», «se» y palabras frecuentes,
  para que Vosk tenga dónde escribir lo que oye) es el mejor equilibrio medido.

**Validación con una tanda nueva (2026-09-13)**, grabada después de elegir la lista: 70 s de un texto
lleno de «sacar», «salero», «salida», «saciarse», que nombra «Sakura» tres veces pero nunca dice «oye
Sakura». Con «Oye Sakura»: actual **2 falsos**, confusores **0**. Con la frase corta «Sakura»: actual
14 despertares, confusores 2 (uno es el «Sakura» dicho de verdad).

**Llevado al producto — Diseño D88.** `WakeWordGrammarConfusers` en Core, usado por
`VoskWakeWordService.BuildGrammar` para las tres frases de Sakura; las heredadas no cambian. El banco
de `scripts/voice/WakeBench` mide la lista del producto, no una copia. Resumen con esa lista:

| | Cerca (~12) | 2 m con música (~16) | Trampas 44 s | Validación 70 s |
| --- | --- | --- | --- | --- |
| «Oye Sakura», antes | 10 | 14 | 8 falsos | 2 falsos |
| «Oye Sakura», D88 | 10 | 12 | 1 falso | 0 |
| «Sakura», antes | 14 | 18 | 10 falsos | 14 |
| «Sakura», D88 | 10 | 14 | 1 falso | 2 |

**Dos trampas encontradas por el camino:**
- La gramática viaja como JSON y `JsonSerializer` escapa lo que no es ASCII; Vosk no lo decodifica y
  **descarta la palabra sin avisar**. Por eso la lista no lleva tildes, y una prueba lo exige.
- Seis variantes fonéticas que la gramática ya ofrecía (`sacura`, `zacura`, `sakuro`, `sakuras`,
  `saqura`, `sagura`) y el prefijo `oye,` **no están en el vocabulario de Vosk**: nunca hicieron nada
  en la gramática. Siguen sirviendo al comparador de texto, así que no se tocan.

**Probado en vivo con la aplicación (2026-09-13, versión de prueba en el equipo de Adler):** unos dos
minutos de frases trampa sin ningún despertar falso, y «Oye Sakura» despertó cada vez que se dijo.

**Alternativas estudiadas después, y por qué no:**
- **openWakeWord.** Solo entrena en inglés (sus voces sintéticas son inglesas) y los datos de
  entrenamiento que usa (`davidscripka/openwakeword_features`, 17,5 GB) son **CC BY-NC-SA 4.0**; sus
  propios modelos heredan esa licencia. Choca con la regla de no aceptar dependencias con restricción
  no comercial (`THIRD-PARTY-NOTICES.md`).
- **Vosk propone y Whisper confirma** (modo `cascada` del banco). Con clips de 2,5 s, Whisper base
  escribe «oye, esa cura» por «Oye Sakura»: 3 de ~12 cerca y 0 de ~16 a distancia sin pista; 3 y 3
  con una pista del nombre. Cero falsos, pero pierde casi todos los aciertos, y tarda ~1,3 s por
  comprobación en CPU.

Lo que quedaría por medir es otro detector entrenado con datos de licencia limpia (voces en español
propias, Common Voice, MUSAN) o el modelo grande de Vosk en español (1,4 GB). Los dos piden descargas
grandes y días de trabajo, y hoy no hay un fallo que lo justifique.

**Límites de la medida:** una sola voz (la de Adler), un micrófono, grabaciones de escritorio de NVIDIA
con micrófono y audio del sistema mezclados en una pista, y «Hey Sakura» sin medir aparte. La
sensibilidad Alta sigue aceptando «basura» o «segura» por parecido, igual que antes.

### L5 — TTS sin naturalidad ni barge-in
**Qué:** SAPI5 (`System.Speech`). Sin streaming, sin interrupción, sin AEC.
**Impacto:** Afecta directamente la experiencia D2 del día 1.
**Aislamiento:** Declarado `FallbackHeredado`.
**Para estable:** Fases 3–4. La experiencia D2 exige voz interrumpible, así que **esto sí bloquea D2**
aunque no bloquee el build.

### L6 — Sin OCR ni UI Automation
**Qué:** Vision depende hoy de enviar la imagen completa al modelo.
**Aislamiento:** Bloqueado por TFM.
**Para estable:** Fase 7 (ADR 0003).

### L7 — Sin recuperación de settings semánticamente corruptos
**Qué:** La escritura es atómica, pero no hay `.bak`. Un JSON válido con contenido inválido no se
puede revertir. Tras una corrupción, `JsonSettingsStore.Load` parte de preferencias nuevas
(`CreateFreshPreferences`, que fija `SchemaVersion = CurrentSchemaVersion`); el defecto D3 de 1.1
—no normalizar en las rutas de archivo ausente o corrupto— **ya está arreglado**. Sigue siendo
cierto que no hay recuperación semántica: los ajustes anteriores quedan en el `.corrupt-*` y nadie
los restaura.
**Aislamiento:** cubierto por `SettingsStoreCharacterizationTests` (escenario 4 de `TEST_MATRIX`).
**No bloquea la fase 1.2.**
**Para estable:** añadir copia previa `.bak` y recuperación semántica.

### L12 — La propiedad del mutex de instancia única es por hilo (D5)
**Qué:** Dos `SingleInstanceCoordinator` en el **mismo hilo** se consideran ambos primarios: el
segundo `WaitOne` es una adquisición recursiva del mismo dueño.
**Impacto real:** ninguno en producción, donde cada instancia es un proceso distinto.
**Aislamiento:** congelado en `MutexOwnershipIsPerThread_NotPerProcess`.
**Para estable:** tenerlo presente si la fase 1.2 comparte o reutiliza el componente.

### L8 — Sin firma Authenticode
**Qué:** No hay certificado. SmartScreen avisa en cada instalación.
**Aislamiento:** Sin actualización silenciosa. El usuario ve versión, notas y hash, y confirma.
**Ruta abierta (2026-08-22):** SignPath Foundation firma gratis proyectos de código abierto que
cumplan sus condiciones. Del lado del repositorio ya está todo: licencia MIT sin doble licencia
comercial, sin componentes propietarios, artefactos construidos solo por CI, metadatos de producto y
versión verificados en cada publicación por `verify-release.ps1`, y las dos páginas que exigen —
[política de firma de código](../CODE_SIGNING_POLICY.md) y [política de privacidad](../PRIVACY.md)—
publicadas y enlazadas desde el README. **Falta la solicitud**, que es de Adler: hay que crear la
cuenta en SignPath con 2FA y enviar el formulario. Después, cablear el flujo de firma en
`release.yml` (firmar los binarios antes de armar el instalador, firmar el instalador, y calcular
los `.sha256` al final, sobre los archivos ya firmados).
**Para estable:** no bloquea RC. La automática se habilita solo con firma + rollback probado.

### L15 — No todas las capacidades pasan por el Permission Broker ⚠️ (2026-09-18)
**Qué:** Personalizar muestra nivel y exclusiones para las seis capacidades, pero no todas se consultan.
**Ya pasan por el broker:** Memoria (borrado total), Proyecto, restaurar copia, Computer Use, **Lens**
(0.30.31: antes de CADA captura de pantalla —modos de Lens, Explicar ventana, contexto visual,
capturas y traducir zona—; las zonas y la pantalla entera no dicen qué aplicación hay debajo, así que
ahí cuenta el nivel pero no las exclusiones por aplicación) y **Flow** (0.30.31: antes de abrir el
micrófono y antes de escribir, Bloqueado y exclusiones).
**Sin resolver:** en **Flow**, el nivel «Preguntar» no pregunta: un diálogo le robaría el foco a la
aplicación destino y el dictado acabaría en el portapapeles. Se comporta como si estuviera permitido.
Adler eligió (2026-09-18) dejarlo así en 0.30.31: preguntar en Flow queda como **bloque aparte**, por el
robo de foco. Bloqueado y aplicación excluida sí se cumplen.
**Rutas de captura de pantalla que TODAVÍA no pasan por Lens (pendientes, bloque siguiente):** la
grabación de pantalla (`StartRecordingAsync`, `MainWindow.Capture.cs`) y la lectura del texto
seleccionado (`MainWindow.Selection.cs`).
**Cambio de comportamiento:** Lens viene en «Preguntar» por omisión, así que ahora pregunta antes de cada
uso salvo que se pase a «Permitido» en Personalizar.
**No se ha comprobado que pasen:** Optimización y el resto de rutas de Memoria y Proyecto; no se afirma
cobertura general.
**Para estable:** decidir cómo preguntar en Flow sin perder el foco y auditar cada capacidad restante.

### L16 — La persistencia de Hoy y Enfoque es más segura, no está resuelta (0.30.32, 2026-09-18)
**Qué se cubre:** tareas y enfoque distinguen «dañado» y «no se pudo abrir» de «vacío», avisan, y no
escriben encima de un archivo que no pudieron leer (se sigue trabajando solo en memoria, y se dice).
Un fallo al escribir avisa una vez en lugar de cerrar la app. Restaurar una copia recarga tareas,
enfoque, rutinas y la píldora en el sitio. Los hábitos entran en la copia previa a actualizar.
**Qué NO se cubre:**
- Los otros siete almacenes JSON (ajustes, conversación, rutinas, checkpoints del proyecto y las
  instantáneas de Computer Use, optimización y packs) comparten el patrón y el defecto de fondo:
  siguen sin distinguir «no pude leer» de «vacío». Los hábitos y las solicitudes ambientales solo
  dejan de renombrar un archivo que no pudieron abrir; su gestor no tiene modo de solo memoria.
- Un `.corrupt-*` **no se restaura desde la app**: es una copia que se guarda al lado. Restaurar
  desde una copia de seguridad sí funciona.
- No hay `fsync`/`WriteThrough`: tras un corte de luz justo al guardar, NTFS puede dejar el archivo
  en ceros. Se detecta como dañado y se avisa, pero no se evita. Es una hipótesis, no un caso visto.
- Tras restaurar una copia con un temporizador en curso, no se ha comprobado que el mini temporizador
  y la vista de Enfoque se actualicen bien.
- Un archivo dañado sigue significando sesión sin guardar: tras reiniciar, Sakura empieza en blanco y
  los datos viejos quedan en el `.corrupt-*`.
- Una sesión de enfoque que terminó con el equipo apagado se cuenta con su duración programada: es
  una estimación.
- `workspace.json` figura como ruta en `NexoDataPaths` pero ningún código lo escribe; no está en el
  inventario de datos.
- Un archivo vacío, de solo espacios o con una forma inesperada (por ejemplo `[null]`) se trata como
  dañado: se aparta y se avisa, aunque no destruya nada. Es deliberado, no se cambia.
- **Segundo arranque tras un archivo dañado:** el archivo ya se apartó, así que sale «no existe», no
  salta ningún aviso y Sakura guarda en blanco. Solo el aviso del primer arranque dice el nombre de la
  copia `.corrupt-*` y que está en la carpeta de datos de Sakura (sin ruta, a propósito).
- Los avisos de este bloque son mensajes normales del asistente: no existe un mecanismo de mensajes
  «solo de interfaz», así que entran en el historial de la conversación (y en lo que se manda al
  proveedor de IA, si se usa uno en la nube). Por eso no llevan rutas ni el mensaje crudo de Windows,
  solo el nombre del archivo y un motivo genérico.
- Una sesión de enfoque vencida hace más de 90 días no se recupera: queda fuera de la ventana del
  historial y no se avisa de ella.
- **Nada de esto se reprodujo ejecutando la app**: hay pruebas automáticas de los almacenes y los
  gestores, pero no una prueba en vivo.

## Fuera de alcance de 1.0 (decidido, no es limitación)

Puentes de mensajería · marketplace comunitario · automatización de navegador (experimental) ·
emparejamiento de dispositivos · clonación de voz · Windows 10.
