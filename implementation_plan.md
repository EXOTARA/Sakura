# Instalar, dar la bienvenida y actualizar de verdad (0.30.35-beta)

Cuarto bloque de consolidación acordado el 2026-09-17: «instalación y bienvenida de extremo a
extremo, y que una actualización real de una versión a otra funcione». Plan verificado archivo por
archivo y con bancos ejecutables contra el código real el 2026-09-20. Sustituye al plan de la
limpieza de tokens de estilo, entregado en el PR 105 y ya fusionado.

Consolidar lo que ya existe. Ninguna función nueva.

**Cómo leer las afirmaciones de este plan.** Cada defecto lleva marcado su nivel de evidencia:
- **[MEDIDO]** — reproducido ejecutando código en este equipo, con el resultado anotado.
- **[LEÍDO]** — la cadena de llamadas está leída entera en el código; no se ejecutó la app.
- **[HIPÓTESIS]** — razonamiento sin reproducir.

Las medidas se hicieron con dos proyectos de consola aparte (Windows 11 26200, .NET 10.0.400), los
dos referenciando `Nexo.Core` y `Nexo.Windows` de verdad:
- un banco de preferencias que carga `settings.json` sintéticos con `JsonSettingsStore`;
- un **banco del actualizador** que genera el guion real con `UpdateHelperScript.Build` y lo
  **ejecuta** sobre carpetas de mentira con la forma `<tmp>\Programs\Sakura` (profundidad 2, que es
  lo que `UpdateSwapPathPolicy` exige).

No se ejecutó la app. No se tocó la instalación de Adler (`%LOCALAPPDATA%\Programs\Kohana`) ni su
carpeta de datos.

**Estado de partida:** `dotnet test Nexo.slnx` en verde — 2 147 (Core) + 377 (Windows) + 248 (App) =
**2 772 pruebas, 0 fallos**, con un aviso de compilación preexistente (`CS0108` en
`src/Nexo.App/MainWindow.Selection.cs:291`).

**Este bloque cubre solo el canal directo (instalador Inno + zip portable).** La copia de Microsoft
Store no tiene actualizador propio: `DistributionPolicy.UsesOwnUpdater`
(`src/Nexo.Core/Distribution/DistributionPolicy.cs:25-26`) solo devuelve `true` para
`DistributionChannel.Direct`, y `UpdateFlowCoordinator.CheckInBackgroundAsync`
(`src/Nexo.App/Updates/UpdateFlowCoordinator.cs:117-121`) sale antes de mirar nada. La ruta MSIX
A→B la hace Windows y **queda explícitamente fuera de alcance**: no hay paquete enviado todavía
(`docs/distribution/MICROSOFT_STORE.md:70-71` — ni WACK ni revisión real), así que no hay nada que
consolidar ahí que no sea especular.

---

## Qué está roto hoy

### A. Si la actualización falla, Sakura se cierra y no vuelve a abrirse

Es el defecto grave del bloque y es otra vez el patrón de los tres anteriores: la interfaz promete
algo que el código no sostiene.

La promesa está escrita en el propio flujo:
`src/Nexo.App/Updates/UpdateFlowCoordinator.cs:221` — «**Sakura se va a cerrar para terminar de
instalarse…**». Justo después se llama a `_requestExit()` (`:226`) y la aplicación se va.

Lo que hace el guion (`src/Nexo.Windows/Updates/UpdateHelperScript.cs`), leído entero: la única
llamada a `Start-Process` está en la **línea 174**, y solo se llega a ella si todo salió bien. Las
tres salidas por fallo están **antes**:
- `:97-100` → `exit 2` «Sakura sigue abierta tras la espera» (aquí está bien: sigue viva);
- `:104-107` → `exit 3` «No hay carpeta preparada que instalar»;
- `:151-165` → `exit 1`, el `catch` que deshace el intercambio y devuelve la carpeta anterior.

**[MEDIDO]** con el banco que ejecuta el guion de verdad, cuatro escenarios:

| Escenario | Salida | Qué quedó instalado | Desinstalador | ¿Volvió a abrirse? |
| --- | --- | --- | --- | --- |
| Camino feliz (instalador Inno) | 0 | **0.30.35** | 2 archivos | **sí** |
| Camino feliz (portable, sin desinstalador) | 0 | **0.30.35** | 0 archivos | **sí** |
| Sin carpeta preparada | 3 | 0.30.34 (intacta) | 2 archivos | **NO** |
| La promoción falla (algo retiene `.new`) | 1 | 0.30.34 (intacta) | 2 archivos | **NO** |

La vuelta atrás funciona perfectamente —la instalación anterior queda entera, con su desinstalador y
sin `.old` colgando—, y aun así **el resultado que ve la persona es el peor posible**: le dijeron que
Sakura se cerraba para instalarse y Sakura ya no está. Es literalmente el síntoma que el comentario
de `:36-39` dice haber arreglado en su día («el resultado visible fue que Sakura se cerró y no
volvió»), por otra causa y sin cerrar la salida general.

**Además nadie se entera.** El guion escribe su registro en
`%LOCALAPPDATA%\Sakura\actualizaciones\ultima-actualizacion.log`
(`UpdateHelperScript.cs:73-77`, sobre `UpdateWorkFolder`, `src/Nexo.App/MainWindow.xaml.cs:6118-6119`)
y **ningún código lo lee**: búsqueda global de `ultima-actualizacion` en `src/` → un solo resultado,
el que lo escribe. Al volver a abrir a mano, Personalizar → Actualizaciones dice «No se ha
comprobado todavía» y vuelve a ofrecer la misma versión, que volverá a fallar igual.

### B. La huella se empareja con el paquete por casualidad alfabética

`GitHubReleaseReader.ReadOne` recorre los adjuntos de la publicación
(`src/Nexo.Core/Updates/GitHubReleaseReader.cs:118-142`) y se queda con **el último** que termine en
`.zip` (`:134-141`) y **el último** que termine en `.sha256` (`:130-133`). No comprueba que los dos
sean el mismo archivo. El comentario de `:128-129` solo razona sobre el orden `.zip` vs `.zip.sha256`
dentro de un mismo nombre.

Cada publicación lleva **cuatro** adjuntos (`.github/workflows/release.yml:88-93`): instalador,
huella del instalador, zip portable y huella del zip. Consultada la API de verdad para
`v0.30.34-beta`, los devuelve en este orden:

```
Sakura-0.30.34-beta-Setup.exe
Sakura-0.30.34-beta-Setup.exe.sha256
Sakura-0.30.34-beta-win-x64-portable.zip
Sakura-0.30.34-beta-win-x64-portable.zip.sha256
```

**[MEDIDO]** pasando ese JSON real a `GitHubReleaseReader.Read`: hoy empareja bien,
`…portable.zip` con `…portable.zip.sha256`. Funciona **solo porque `Setup` va antes que `win`
alfabéticamente** y la huella buena queda la última.

**[MEDIDO]** con los mismos cuatro archivos en otro orden (el instalador al final): devuelve el
paquete `…portable.zip` con la huella `…Setup.exe.sha256`. Con eso,
`WindowsUpdateDownloader` baja los cien megas enteros y los rechaza:
«Lo descargado no coincide con lo publicado. No se va a instalar.»
(`src/Nexo.Windows/Updates/WindowsUpdateDownloader.cs:76-83`). **Nadie podría volver a
actualizarse**, y el mensaje apunta a que alguien manipuló la descarga.

**[MEDIDO]** también: si algún día la publicación llevara un segundo `.zip` cuyo nombre ordene
después (se probó con `…-symbols.zip`), **ese** sería el que se descargue e instale en vez de Sakura.

Basta con renombrar el zip portable, añadir un adjunto, o que GitHub cambie el orden en que los
devuelve. Es el tipo de fallo que no se nota hasta el día que importa.

### C. La bienvenida promete una vista previa que no siempre existe

`src/Nexo.App/OnboardingWindow.xaml:210` dice, en el paso de privacidad: «Las capturas **siempre**
requieren una acción explícita y **una vista previa**.»

La parte de «acción explícita» se sostiene. La de la vista previa, no. **[LEÍDO]**, cadena completa:

- `PrepareVisualContextAsync` (`src/Nexo.App/MainWindow.xaml.cs:6664`) solo enseña la miniatura
  cuando `silentContext` es falso: `_assistantView.SetVisionAttachment(...)` está dentro de
  `if (!silentContext)` (`:6779-6785`).
- `ProcessPromptAsync` la llama con `silentContext: true` (`:6220-6227`) cada vez que
  `VisualContextPromptPolicy.ShouldAcquireVisualContext` dice que la pregunta necesita ver la
  pantalla («¿qué es esto?»). Ahí se captura la ventana y se manda al modelo **sin enseñar nada**.
- `ExplainForegroundWindowAsync` (`src/Nexo.App/MainWindow.Explain.cs:26`) —Ctrl + Shift + Espacio—
  captura, lee y contesta sin pasar en ningún momento por una vista previa.

Sí se pide permiso por el broker en las dos rutas (`TryGetLensPermission`,
`MainWindow.xaml.cs:6726-6737` y `MainWindow.Explain.cs:80-86`), que es lo que L15 dejó hecho en
0.30.31. Lo que no hay es la vista previa que el texto promete, y es una promesa sobre lo que se ve
de la pantalla: de las que más caro cuesta incumplir.

### D. La bienvenida dice 5 GB y el código exige 9 GB libres

`OnboardingWindow.xaml:131`: «la IA local (Ollama y el modelo) **ocupa unos 5 GB** y descarga cerca
de 4,8 GB».

`LocalAiDiskPolicy.RequiredBytes` (`src/Nexo.Core/Ai/LocalAiDiskPolicy.cs:12-14`) pide **9 GB**
libres si el motor no está instalado, y 4 GB si ya lo está. Tiene su razón, escrita en `:3-7`:
mientras se descomprime, el zip y la copia descomprimida conviven (pico de ~5 GB) y además viene el
modelo de 3,2 GB. El número no está mal; lo que está mal es que la pantalla que decide si alguien
pulsa «Instalar IA local» diga 5 y el aviso que sale después diga 9.

**[LEÍDO]**: quien tenga entre 5 y 9 GB libres lee «ocupa unos 5 GB», pulsa, y recibe «No hay espacio
suficiente: la IA local necesita unos 9 GB libres» (`LocalAiDiskPolicy.cs:25-26`, disparado desde
`OnboardingWindow.xaml.cs:441-454`). Los dos textos son correctos por separado y se contradicen.

### E. El candado de instancia única se toma después de mover carpetas de gigas

`src/Nexo.App/App.xaml.cs`: el orden de arranque es

1. rastro del perfil de validación (`:30-48`),
2. `LegacyDataMigrator.MigrateIfNeeded()` y **`ConsolidateHeavyFolders()`** (`:55-67`),
3. **y solo entonces** `new SingleInstanceCoordinator()` (`:68-74`).

`ConsolidateHeavyFolders` (`src/Nexo.Windows/Storage/LegacyDataMigrator.cs:43-85`) hace
`Directory.Move` de `Models` y `Runtime` desde las carpetas de los nombres anteriores — los modelos
de voz de quien viene de lejos son tres giga y medio (`ProductIdentity.cs`, nota de
`PreviousDataDirectoryNames`). Dos procesos de Sakura arrancando a la vez ejecutan los dos ese
movimiento antes de que ninguno descubra que es el secundario. **[LEÍDO]**; que eso deje las
carpetas a medias es **[HIPÓTESIS]**, no se reprodujo.

Importa ahora y no antes porque el paso 1 de este plan hace que el ayudante **abra Sakura siempre**,
incluida la vez en que la persona ya la abrió a mano durante la espera. El candado hace que esa
segunda copia se retire sola (`App.xaml.cs:69-74` → `SignalPrimaryInstance(); Shutdown();`), pero hoy
se retira **después** de haber podido mover tres giga y medio.

### F. Un intento fallido deja carpetas que el desinstalador nunca se lleva

El intercambio trabaja con `<instalación>.new` y `<instalación>.old`
(`src/Nexo.Core/Updates/UpdateSwapPaths.cs:37-38`). El desinstalador barre `{app}` entero
(`installer/Sakura.iss:85-86`), que es el arreglo de L13, pero `{app}.new` y `{app}.old` son
**hermanas** de `{app}`, no están dentro, y nadie las borra al desinstalar.

**[MEDIDO]** en el banco, escenario 4: tras la promoción fallida queda `Sakura.new` en disco (en la
vida real, unos 250 MB). Se limpia sola en el **siguiente** intento
(`WindowsUpdateService.cs:157-160`), así que solo sobrevive si no vuelve a haber actualización — que
es exactamente lo que pasa si la persona desinstala. La política 10.2.7 de la Store pide
desinstalación limpia (`docs/distribution/MICROSOFT_STORE.md:40`).

### G. Tres cosas que los documentos afirman y el código no

- **`KNOWN_LIMITATIONS.md:307-309` (L8)** dice: «El usuario ve versión, notas **y hash**, y
  confirma». La tarjeta de la oferta enseña versión, notas y carpeta de destino
  (`src/Nexo.App/Views/SettingsView.xaml.cs:1612-1634`) — **no enseña la huella**, ni el tamaño de la
  descarga. La huella **sí se comprueba** (`WindowsUpdateDownloader.cs:76-83`): lo falso es que la
  persona la vea. **[LEÍDO]**
- **`KNOWN_LIMITATIONS.md` tiene dos L12**: la resuelta de la versión en «Aplicaciones instaladas»
  (`:74`) y la del mutex por hilo (`:300`). Dos limitaciones distintas con el mismo número en el
  documento que existe para poder citarlas.
- **`RELEASE_CHECKLIST.md` está en la etapa Kohana**: pide que la ventana muestre «Kohana» (`:15`),
  que el ejecutable sea `Kohana.exe` (`:16`, hoy es `Sakura.exe`,
  `src/Nexo.Core/Branding/ProductIdentity.cs`), que funcione «Oye Kohana» (`:18`, hoy la migración 30
  lo cambia a «Oye Sakura», `ShellPreferences.cs:626-645`) y que `publish.ps1` genere `Kohana.exe`
  (`:33`). Y, siendo la lista de lanzamiento, **no tiene ni un paso que ensaye actualizar de la
  versión anterior a la nueva**, que es justo lo único que este bloque existe para asegurar.

---

## Lo que NO hay que construir (ya está bien, comprobado)

1. **La vuelta atrás del ayudante funciona.** **[MEDIDO]**, escenarios 3 y 4: la instalación anterior
   queda intacta, con sus archivos y su desinstalador, y `.old` se borra. **No se toca la lógica del
   intercambio**; el paso 1 solo añade el relanzamiento.
2. **El desinstalador se conserva al actualizar (L13).** **[MEDIDO]**: «desinstalador conservado: 2
   archivos» y la carpeta promocionada los lleva. Y una instalación portable sin `unins*` se
   actualiza igual (escenario 2, salida 0). **No se toca** `UpdateHelperScript.cs:124-128` ni
   `installer/Sakura.iss:85-86`.
3. **La huella se comprueba de verdad y la descarga está acotada.**
   `WindowsUpdateDownloader.WriteAndHashAsync` calcula el SHA-256 mientras escribe, corta si el
   servidor sirve más de lo anunciado (`:137-140`), descarga a `.part` con `FileMode.CreateNew` y
   limpia los paquetes viejos al empezar (`:167-186`). **No se toca nada de esto.**
4. **La política de carpetas del intercambio está bien y tiene pruebas.**
   `UpdateSwapPathPolicy.Resolve` rechaza la raíz de una unidad, una letra suelta, las carpetas
   conocidas del perfil y cualquier ruta de menos de dos niveles
   (`src/Nexo.Core/Updates/UpdateSwapPaths.cs:46-117`), y se resuelve **antes** de descargar
   (`WindowsUpdateService.cs:139-146`), así que una carpeta imposible no cuesta cien megas.
   10 pruebas en `tests/Nexo.Core.Tests/UpdateSwapPathPolicyTests.cs`. **No se toca.**
5. **La entrada de «Aplicaciones instaladas» ya se corrige sola (L12/D87).**
   `WindowsInstalledVersionRegistrar.Reconcile` corre en cada arranque y fuera del hilo de interfaz
   (`MainWindow.xaml.cs:5006-5030`), usa `ReleaseMetadata.CurrentVersion` —sin el `+hash` del commit
   (`src/Nexo.App/ReleaseMetadata.cs:17-21`)— y no inventa entrada donde no hay
   (`InstalledVersionPolicy.cs:43-47`). **No se toca.**
6. **Los datos y los modelos no viven en la carpeta de instalación**, así que el intercambio no puede
   llevárselos. Búsqueda global de `AppContext.BaseDirectory` en `src/`: **dos** resultados, y solo
   uno es de actualizaciones (`MainWindow.xaml.cs:6138`). Ollama, sus modelos, Vosk y Whisper
   cuelgan todos de `NexoDataPaths.RootDirectory`
   (`src/Nexo.Core/Diagnostics/NexoDataPaths.cs:165-194`). **No hace falta ninguna copia de
   seguridad previa al intercambio.**
7. **«Repetir configuración inicial» existe y reaplica todo.** El botón está en
   `src/Nexo.App/Views/SettingsView.xaml:1086-1089`, el evento en `SettingsView.xaml.cs:799-800`, y
   `MainWindow.ShowOnboardingAsync` (`:1577-1595`) vuelve a aplicar preferencias, proveedor de IA,
   supervisor de Ollama, Lens, micrófonos y palabra de activación. La frase de
   `OnboardingWindow.xaml.cs:702` («Puedes repetirla después desde Personalización») **es cierta**.
   No se construye nada aquí.
8. **La primera instalación no pasa por las migraciones.** **[MEDIDO]**: sin `settings.json`,
   `JsonSettingsStore.Load` devuelve `SchemaVersion = 32` y deja Peek y el panel del borde apagados;
   un archivo de esquema 31 conserva `HasCompletedOnboarding` y enciende el panel del borde, que es
   lo que la migración 32 promete. `CreateFreshPreferences`
   (`src/Nexo.Windows/Settings/JsonSettingsStore.cs:59-68`) y las 5 pruebas de
   `tests/Nexo.Windows.Tests/JsonSettingsStoreFirstRunTests.cs` ya lo fijan. **No se toca.**
9. **Que un `settings.json` sin `SchemaVersion` vuelva a enseñar la bienvenida NO es un defecto, y
   no se arregla.** Es el aviso que motivó este bloque, y medido queda así:

   | `settings.json` de partida | Tras `Load()` |
   | --- | --- |
   | sin `SchemaVersion`, con `HasCompletedOnboarding: true` | **bienvenida otra vez** (y `ShowAudioModule` reimpuesto) |
   | `{}` | bienvenida, y el panel del borde **encendido** (no es una instalación nueva) |
   | `"SchemaVersion": 9` | bienvenida otra vez |
   | `"SchemaVersion": 10` o más | se conserva |
   | archivo inexistente | bienvenida (correcto: es una instalación nueva) |

   La causa es la migración 10 (`src/Nexo.Core/Settings/ShellPreferences.cs:388-392`), y **para un
   archivo viejo de verdad es lo correcto**: una instalación anterior a la versión 10 del esquema no
   ha visto ninguna de las pantallas actuales. Lo que no puede pasar por un archivo así es una
   instalación real: `JsonSettingsStore.Save` normaliza siempre antes de escribir (`:44-57`), así que
   **todo `settings.json` que haya escrito Sakura declara el esquema actual** — medido: el primer
   campo del archivo guardado es `"SchemaVersion": 32`. El único modo de tener un archivo sin
   `SchemaVersion` es escribirlo a mano, que es justo lo que se hizo al montar el perfil de prueba.
   El paso 7 deja una prueba que lo congela y el paso 8 lo advierte en `AGENTS.md`. **No se cambia
   `Normalize()`.**
10. **El candado de instancia única y el ayudante se entienden bien.** El guion espera por
    identificador y nunca mata nada (`UpdateHelperScript.cs:91-100`), el mutex es del proceso y se
    libera al morir (`SingleInstanceCoordinator.cs:29-43`), y si el ayudante abriera Sakura estando
    ya abierta, la segunda copia avisa a la primera y se cierra (`App.xaml.cs:69-74`). **Eso es lo
    que hace seguro el relanzamiento incondicional del paso 1**; no hay que inventar ninguna
    comprobación extra en el guion.
11. **La copia de Microsoft Store ya se comporta distinto y está probado.**
    `DistributionPolicy` decide en un solo sitio (`src/Nexo.Core/Distribution/DistributionPolicy.cs`),
    `WindowsDistributionChannel.Detect` pregunta a Windows (`:45-63`), y Personalizar quita el botón
    y la casilla en vez de dejarlos sin efecto (`SettingsView.xaml.cs:1653-1660`). Pruebas en
    `tests/Nexo.Core.Tests/DistributionPolicyTests.cs` y
    `tests/Nexo.Windows.Tests/WindowsDistributionChannelTests.cs`. **No se toca.**
12. **Las 13 pruebas de `tests/Nexo.App.Tests/UpdateFlowCoordinatorTests.cs` y las 13 de
    `tests/Nexo.Windows.Tests/Updates/UpdateHelperScriptTests.cs` fijan el orden del intercambio.**
    Se **añaden** casos; **no se cambia ninguno de los existentes**.

---

## Paso 1 — el ayudante vuelve a abrir Sakura pase lo que pase

Es el arreglo del defecto A y el corazón del bloque.

- [MODIFY] `src/Nexo.Windows/Updates/UpdateHelperScript.cs`
  - `Build` (`:50-54`) gana un parámetro **antes** de `packagePath`:
    `string executableIfRollback`. Es la ruta del ejecutable que estaba corriendo, que tras la vuelta
    atrás vuelve a existir seguro. **No se adivina el nombre dentro del guion** ni se duplica
    `ProductIdentity.ExecutableName`: una instalación que todavía no ha saltado el cambio de nombre
    tiene `Kohana.exe` y acertar por lista sería empezar a mantener dos listas.
  - Emitir `$exeAnterior = {Quote(executableIfRollback)}` junto a las demás rutas (`:80-86`).
  - Una función única de reapertura, declarada justo después de `Apunta` (`:73-78`):
    prueba `$exe`, luego `$exeAnterior`, abre el primero que exista, y si no existe ninguno lo
    **apunta en el registro** en vez de callarse.
  - Llamarla en **tres** sitios: en el camino feliz (donde hoy está `Start-Process`, `:174`), antes
    del `exit 3` de «no hay carpeta preparada» (`:104-107`) y **dentro del `catch`, después de
    devolver la carpeta anterior y antes del `exit 1`** (`:151-165`).
  - **Deliberadamente NO** en el `exit 2` de «Sakura sigue abierta tras la espera» (`:97-100`):
    ahí Sakura nunca se cerró, y abrir otra copia sería pedirle al candado que la eche.
  - El comentario que se añade tiene que decir **por qué**, con la medida: la vuelta atrás dejaba la
    instalación anterior entera y aun así el resultado visible era «Sakura se cerró y no volvió»,
    que es el mismo síntoma que ya se persiguió dos veces (`:36-42`) por otras causas.
- [MODIFY] `src/Nexo.Windows/Updates/WindowsUpdateService.cs:184-191` — pasar
  `Environment.ProcessPath` como `executableIfRollback`. Es exacto y no hace falta buscar nada: es
  el ejecutable que se está ejecutando ahora mismo, dentro de la carpeta que el `catch` devuelve a
  su sitio. Si viniera `null` (no ocurre en un proceso normal), pasar cadena vacía; el guion ya
  trata la cadena vacía como «no hay candidato».

**Instrucción explícita para el implementador:** *no* añadir al guion un `Stop-Process`, una segunda
espera, ni una comprobación de «¿hay otra Sakura corriendo?». El candado de instancia única ya
resuelve el caso de la copia abierta a mano (ver «lo que NO hay que construir», punto 10). Si al
implementar parece que hace falta, **`coder` se detiene y lo reporta**.

## Paso 2 — la huella es la del paquete, no la que quede la última

Arreglo del defecto B.

- [MODIFY] `src/Nexo.Core/Updates/GitHubReleaseReader.cs`, bucle de adjuntos (`:114-154`):
  1. Primera pasada: quedarse con el adjunto `.zip` **de mayor tamaño** en vez de con el último.
     El paquete de Sakura son cien megas y cualquier otro zip que llegue a una publicación va a ser
     más pequeño; elegir por tamaño no depende de cómo se llame ni de en qué orden venga.
     (**[MEDIDO]**: hoy `…portable.zip` mide 100 788 001 bytes y el instalador 67 695 548.)
  2. Segunda pasada: buscar el adjunto cuyo nombre sea **exactamente** el del paquete elegido más
     `.sha256`, sin distinguir mayúsculas. Nada de `EndsWith`.
  3. Si no aparece esa huella concreta, **rechazar** con el mensaje que ya existe (`:149-154`):
     «La publicación no trae la huella para comprobarla.» Un paquete sin su huella no se instala, que
     es la regla que ya estaba escrita ahí.
  - Reescribir el comentario de `:128-129`: lo que importa no es el orden, es el emparejamiento.
- **No se toca** `ReadChecksum` (`:207-226`), ni `ReadBest` (`:172-198`), ni
  `UpdateManifestReader.MatchesFingerprint`, ni el flujo de `WindowsUpdateService.LookForUpdateAsync`
  (`:61-113`): la forma de pedir las cosas no cambia, solo cuál de los cuatro adjuntos se elige.
- **No se toca** `.github/workflows/release.yml`. El flujo publica bien; el que elegía mal era el
  lector.

## Paso 3 — decir que la última actualización no se pudo aplicar

Arreglo de la segunda mitad del defecto A.

- [NEW] `src/Nexo.Core/Updates/UpdateHelperLog.cs` — nombre **libre** (búsqueda global en `src/`,
  `tests/`, `docs/`, `scripts/`, `installer/`: cero resultados). Lógica pura, sin disco:

  `static string? Describe(string? logText)`

  Devuelve el aviso para la persona, o `null` si no hay nada que decir. Reglas sobre la **última
  línea no vacía** del registro, que es lo que el guion garantiza porque lo reescribe entero en cada
  intento (`UpdateHelperScript.cs:77`):
  - termina en `Terminado.` → `null` (fue bien);
  - registro vacío, ausente o con una forma que no se reconoce → `null` (**no se inventa un fallo**);
  - contiene `Sakura sigue abierta tras la espera` → «La última actualización no se aplicó porque
    Sakura seguía abierta. Vuelve a intentarlo.»;
  - contiene `No hay carpeta preparada` → «La última actualización no llegó a prepararse. Vuelve a
    intentarlo.»;
  - contiene `FALLO:` → «La última actualización no se pudo aplicar y Sakura se quedó en la versión
    que tenías. Vuelve a intentarlo.»
  - **El texto crudo de Windows que el guion apuntó detrás de `FALLO:` no se enseña**, y va en el
    comentario por qué: es el mismo criterio de L16 y L17 —nada de rutas ni de mensajes del sistema
    en lo que la persona lee—, y aquí además el registro está en disco para quien quiera leerlo.

- [MODIFY] `src/Nexo.App/Views/SettingsView.xaml` — un `TextBlock` nuevo,
  `LastUpdateProblemText` (nombre **libre**), justo después de `UpdateStatusText` (`:1108-1112`),
  `Visibility="Collapsed"`, con `TextWrapping="Wrap"` y el estilo de aviso que ya usa la sección.
  **Línea propia y no reutilizar `UpdateStatusText`**: la comprobación en segundo plano sobrescribe
  ese texto a los 45 segundos (`UpdateFlowCoordinator.cs:155`) y el aviso desaparecería solo.
- [MODIFY] `src/Nexo.App/Views/SettingsView.xaml.cs` — `SetLastUpdateProblem(string? message)`
  (nombre **libre**) junto a `SetUpdateStatus` (`:1597-1601`): enseña u oculta esa línea. Y
  `ShowStoreManagedUpdates` (`:1653-1660`) la **oculta también**, con el resto de la interfaz del
  actualizador propio: en la Store no hay ayudante y un registro viejo no puede hablar por él.
- [MODIFY] `src/Nexo.App/MainWindow.xaml.cs:1353-1356` — después de `SetCurrentVersion`, leer
  `Path.Combine(UpdateWorkFolder, "ultima-actualizacion.log")` (la misma carpeta de `:6118-6119`),
  pasarlo por `UpdateHelperLog.Describe` y llamar a `SetLastUpdateProblem`. La lectura va **dentro de
  un `try`** que trague `IOException`/`UnauthorizedAccessException`: no poder leer un registro de
  diagnóstico no puede estorbar al arranque, que es el mismo criterio de
  `WindowsInstalledVersionRegistrar` (`:19-21`).

**Se avisa en cada arranque hasta que una actualización salga bien**, a propósito: no se guarda
ningún «ya avisé». Mientras el fallo siga ahí, la frase sigue siendo verdad, y el único sitio donde
se lee es Personalizar → Actualizaciones, que nadie abre sin querer. Queda anotado en L18.

## Paso 4 — tomar el candado antes de mover nada

Arreglo del defecto E. Es mover tres líneas.

- [MODIFY] `src/Nexo.App/App.xaml.cs` — subir el bloque `_singleInstance = new
  SingleInstanceCoordinator(); if (!_singleInstance.IsPrimaryInstance) { … }` (`:68-74`) a **justo
  antes** del `try` de la migración (`:55`). El rastro del perfil de validación (`:30-48`) se queda
  donde está: es lo primero que debe constar y no toca nada de nadie.
- El comentario nuevo dice el porqué con el número: `ConsolidateHeavyFolders`
  (`src/Nexo.Windows/Storage/LegacyDataMigrator.cs:43-85`) mueve `Models` y `Runtime`, que para quien
  viene de la etapa Nexo son varios gigabytes, y hasta ahora una segunda copia hacía ese movimiento
  **antes** de descubrir que sobraba. Con el paso 1, una segunda copia es un escenario normal.
- **No se toca** `LegacyDataMigrator`, ni `SingleInstanceCoordinator`, ni el orden del resto del
  arranque.

## Paso 5 — la bienvenida dice lo que de verdad pasa

Arreglo de los defectos C y D. **Solo texto**; ninguna capacidad cambia.

- [MODIFY] `src/Nexo.App/OnboardingWindow.xaml:210` — sustituir «Las capturas siempre requieren una
  acción explícita y una vista previa» por algo que el código sostenga: que Sakura **solo mira cuando
  se lo pides** (atajo, botón o una pregunta sobre lo que tienes delante), que **pide permiso antes de
  cada captura** y que **puedes decidir qué se permite** en Personalizar → Permisos. Redáctalo en el
  tono del resto del panel, sin jerga y sin decir «broker».
- [MODIFY] `src/Nexo.App/OnboardingWindow.xaml:131` — añadir al texto de la IA local que, mientras se
  instala, hacen falta **unos 9 GB libres** (4 GB si el motor ya está), porque durante la
  descompresión conviven el comprimido y el descomprimido. El número sale de
  `LocalAiDiskPolicy.RequiredBytes` (`src/Nexo.Core/Ai/LocalAiDiskPolicy.cs:12-14`); **no lo
  recalcules ni lo redondees a otra cosa**.
- **No se toca** ninguna casilla, ningún valor por omisión ni el orden de los pasos. El resto de las
  cifras del panel se comprobaron y son honestas: el motor descarga ~1,45 GB y el modelo ~3,2 GB
  (`LocalAiDiskPolicy.cs:3-6`), que es lo que dicen `OnboardingWindow.xaml.cs:460` y `:301`.

## Paso 6 — desinstalar se lleva también lo que dejó un intento fallido

Arreglo del defecto F.

- [MODIFY] `installer/Sakura.iss`, sección `[UninstallDelete]` (`:85-86`) — dos entradas más:
  `{app}.new` y `{app}.old`, con `Type: filesandordirs`.
- El comentario (que ya explica L13 en `:74-84`) se amplía diciendo de dónde salen esos sufijos:
  `UpdateSwapPathPolicy` (`src/Nexo.Core/Updates/UpdateSwapPaths.cs:37-38`). **Si allí cambian, aquí
  hay que cambiarlos**, y no hay forma de que el compilador lo avise.
- Es seguro por lo mismo que `{app}`: son carpetas que crea y destruye solo el actualizador de
  Sakura, hermanas de una carpeta exclusiva de Sakura. Los datos de la persona viven en
  `%LOCALAPPDATA%\Sakura` y esto no los toca.

## Paso 7 — pruebas

- [MODIFY] `tests/Nexo.Windows.Tests/Updates/UpdateHelperScriptTests.cs` — **sin tocar las 13
  existentes**:
  - tras un fallo de promoción, el guion **vuelve a abrir Sakura**: la llamada de reapertura aparece
    dentro del `catch`, antes del `exit 1` (comprobación por posición, como ya hacen
    `APartialPromotionIsClearedBeforePuttingTheOldOneBack` y
    `TheOldVersionIsOnlyDeletedAfterTheNewOneIsInPlace`);
  - lo mismo antes del `exit 3` de «no hay carpeta preparada»;
  - **no** se reabre en la rama de «Sakura sigue abierta» (`exit 2`);
  - la ruta de vuelta atrás va citada y entrecomillada, y un apóstrofo en ella no parte el guion
    (mismo patrón que `APathWithAnApostropheCannotBreakTheScript`).
- [MODIFY] `tests/Nexo.Core.Tests/GitHubReleaseReaderTests.cs` (existe, 12 pruebas) — añadir:
  - los cuatro adjuntos reales de una publicación, **en tres órdenes distintos**, dan siempre el zip
    portable emparejado con **su** `.sha256`;
  - un segundo `.zip` más pequeño en la misma publicación no gana;
  - un paquete cuyo `.sha256` no está se **rechaza** con el mensaje que ya existe, en vez de
    aceptar la huella de otro archivo.
  El JSON de las pruebas se escribe a mano con la forma medida; **no se consulta la red desde una
  prueba**.
- [NEW] `tests/Nexo.Core.Tests/UpdateHelperLogTests.cs` — nombre libre. Cubre las cinco reglas del
  paso 3 con registros de ejemplo copiados **literalmente** de los que produjo el banco (están en la
  tabla del defecto A), incluido el de éxito que tiene que devolver `null`, el vacío y uno con una
  forma inesperada.
- [MODIFY] `tests/Nexo.Windows.Tests/JsonSettingsStoreFirstRunTests.cs` — una prueba que **congela**
  lo medido en el punto 9 de «lo que NO hay que construir»: un `settings.json` sin `SchemaVersion` y
  con `HasCompletedOnboarding: true` se trata como una instalación anterior a la versión 10 del
  esquema y **vuelve a enseñar la bienvenida**. Va con el comentario de por qué está bien y de por
  qué un archivo escrito por Sakura nunca puede estar así.
- **No hace falta prueba para el paso 4** (mover tres líneas de `App.OnStartup`): `App` no se
  construye en pruebas, es lo que dice L2. Se verifica en vivo.
- **No hay forma automática de probar el paso 6**: Inno no se ejecuta en `dotnet test`. Se verifica
  con el ciclo de Windows Sandbox que ya existe (`scripts/sandbox/Invoke-InstallCycle.ps1`).

## Paso 8 — la lista de lanzamiento, al día y con el ensayo que faltaba

- [MODIFY] `RELEASE_CHECKLIST.md` — corregir la identidad heredada: `Kohana` → `Sakura`,
  `Kohana.exe` → `Sakura.exe` (`:15-16`, `:33`), «Oye Kohana» → «Oye Sakura» (`:18`), y la migración
  de datos citada tal como es hoy: `Nexo` → `Kohana` → `Sakura`, la cadena de
  `ProductIdentity.PreviousDataDirectoryNames`. Quitar el punto de `:19` («El modo Kohana no acepta
  Nexo por error») si ya no describe nada del producto actual.
- [MODIFY] `RELEASE_CHECKLIST.md` — una sección nueva, **«Actualización de la versión anterior»**,
  que es lo que faltaba:
  - [ ] la publicación tiene sus **cuatro** adjuntos y cada `.sha256` se llama exactamente como su
        archivo más `.sha256`;
  - [ ] instalada la versión **anterior**, «Buscar actualizaciones» encuentra la nueva;
  - [ ] instalarla deja la versión nueva en la carpeta, **vuelve a abrir Sakura sola**, y
        `%LOCALAPPDATA%\Sakura\actualizaciones\ultima-actualizacion.log` termina en «Terminado.»;
  - [ ] tras actualizar, **no vuelve a salir la bienvenida** y se conservan tareas, rutinas, enfoque
        y el modelo de IA elegido;
  - [ ] tras actualizar, «Aplicaciones instaladas» de Windows dice la versión nueva;
  - [ ] tras actualizar, el desinstalador sigue existiendo y desinstalar deja la carpeta vacía;
  - [ ] no quedan `Sakura.new`, `Sakura.old` ni zips en la carpeta de actualizaciones.
- [MODIFY] `AGENTS.md`, sección «Cómo probar en vivo» (`:47-53`) — una línea: para montar un perfil
  con `SAKURA_DATA_ROOT`, **dejar que Sakura cree el `settings.json` ella sola** o copiar uno
  completo; un archivo escrito a mano sin `SchemaVersion` se toma por una instalación prehistórica y
  vuelve a enseñar la bienvenida, con el resultado de que se «descubren» fallos que no existen.
  (Ya hay un guion para esto: `scripts/New-SakuraValidationProfile.ps1`.)

---

## Riesgos conocidos, con instrucción explícita

**1. Se está tocando la pieza con más poder de la aplicación.** El guion mueve la carpeta donde vive
Sakura. El paso 1 **solo añade llamadas a una función de reapertura**: no cambia el orden de los
movimientos, ni la vuelta atrás, ni la copia del desinstalador, ni las guardas. Si al implementar
aparece la tentación de reordenar algo del intercambio «ya que estamos», **`coder` no lo hace** y lo
anota como trabajo aparte.

**2. El banco del actualizador no es la app.** Lo **[MEDIDO]** son carpetas de mentira con archivos
de texto y un `.cmd` haciendo de ejecutable, con `pid` inexistente para no esperar. Reproduce los
pasos del guion de verdad —lo genera `UpdateHelperScript.Build`— pero **no** prueba que Sakura se
cierre a tiempo, ni que el runtime de IA administrado muera antes. Eso solo lo dice una actualización
real de 0.30.34 a 0.30.35, y está en «Verificación».

**3. El paso 2 cambia cuál de los adjuntos se descarga.** Hoy acierta por orden alfabético; después
acertará por tamaño y por nombre. Si al implementar el criterio de «el `.zip` más grande» resultara
ambiguo en alguna publicación real —dos zips de tamaño parecido—, **`coder` se detiene y lo
reporta** en vez de añadir una lista de nombres esperados: eso sería empezar a acoplar el lector al
nombre que hoy usa `publish.ps1` (`scripts/publish.ps1:26`), y es una decisión de diseño que no se
toma a mitad de camino.

**4. El aviso del paso 3 puede salir cuando no toca.** Si alguien borra a mano el registro, o si un
antivirus corta el guion a mitad, la última línea puede no ser ninguna de las cinco conocidas. Por
eso `Describe` devuelve `null` ante cualquier forma que no reconoce: **antes callarse que inventar un
fallo**. Si al implementar aparece la tentación de una sexta regla «por si acaso», no se añade.

**5. El texto de la bienvenida del paso 5 es una decisión de producto, no una corrección técnica.**
Está en «Decisiones para Adler». Se implementa con la redacción recomendada; si él prefiere otra
cosa, se cambia el texto y ya, sin tocar código.

**6. El defecto E no se reprodujo.** Que dos arranques simultáneos dejen `Models` o `Runtime` a
medias es **[HIPÓTESIS]**. El paso 4 se hace igual porque cuesta mover tres líneas y porque el paso 1
hace más probable el doble arranque. **No se debe afirmar en el PR que se arregló una pérdida de
datos observada.**

**7. Nada de esto se reprodujo ejecutando la app.** La verificación en vivo es obligatoria y puede
desmentir algo de aquí; si lo hace, se dice.

---

## Fuera de alcance, decidido

- **La actualización A→B del paquete MSIX.** La hace Windows, no Sakura, y la copia de la Store ni
  siquiera tiene el botón (`DistributionPolicy.cs:25-26`,
  `SettingsView.xaml.cs:1653-1660`). Además no hay paquete enviado todavía: faltan WACK, el micrófono
  dentro del paquete y la revisión real (`docs/distribution/MICROSOFT_STORE.md:70-71`). Consolidar
  una ruta que no se ha ejecutado nunca sería inventar.
- **Medir el tamaño del portable y del instalador y su SHA-256** (L1). Los datos están en la
  publicación de GitHub (100 788 001 y 67 695 548 bytes para 0.30.34-beta) pero cerrar L1 pide el
  baseline completo de la fase 10; este bloque solo anota la cifra donde hace falta.
- **Enseñar la huella o el tamaño de la descarga en la oferta de actualización.** Se arregla la
  **afirmación** de L8 en la documentación (paso «Producto»), no la interfaz: añadir un SHA-256 de 64
  caracteres a una tarjeta que hoy se lee de un vistazo tiene su propia decisión de diseño.
- **Un manejador global `DispatcherUnhandledException`.** Sigue sin existir (búsqueda global en
  `src/`: cero resultados), así que una excepción en `App.OnStartup` mata la primera ejecución sin
  decir nada. Es un bloque propio, con su propia decisión sobre qué hacer cuando salta; ya quedó
  anotado así en el plan anterior.
- **Reparar instalaciones que ya perdieron el desinstalador.** La vía sigue siendo reinstalar por
  encima, y el porqué de no borrar la entrada rota del registro está razonado en
  `KNOWN_LIMITATIONS.md:154-160`. No se reabre.
- **El constante duplicado del repositorio.** `WindowsUpdateService` pide a `EXOTARA/Nexo`
  (`:40-41`) mientras `ProductIdentity.RepositoryName` dice `Sakura`. Hoy funciona (la consulta real
  a `EXOTARA/Nexo` devuelve las publicaciones), así que tocarlo es arriesgar una ruta que anda por
  una que no se ha probado. Anotado abajo.
- **El rediseño visual** de cualquier pantalla, incluida la bienvenida.
- **La deuda de los bloques anteriores**: `JsonHabitStore` y `JsonAmbientRequestHistoryStore` sin modo
  de solo memoria (L16); grabación de pantalla y lectura de selección sin pasar por Lens (L15).
- **Alt+A durante juegos a pantalla completa**: bloque aparte, pendiente de datos.
- **La marca de uso de IA.** No se toca, no se hace configurable, no se discute.

### Para después, anotado, no se hace aquí

- `WindowsUpdateService.cs:40-41` codifica `EXOTARA/Nexo` mientras `ProductIdentity` dice `Sakura`.
  Dos constantes que nombran lo mismo y no coinciden; funciona por el redirección de GitHub al
  renombrar un repositorio.
- El aviso de compilación `CS0108` de `src/Nexo.App/MainWindow.Selection.cs:291` es preexistente y
  contradice el «0 warnings» que L1 declara (`KNOWN_LIMITATIONS.md:9-11`).
- Un paquete descargado y un `.new` de un intento fallido se quedan en disco hasta el siguiente
  intento (`WindowsUpdateDownloader.cs:167-186`, `WindowsUpdateService.cs:157-160`). No molesta a
  nadie salvo por los ~350 MB; limpiarlos en el momento del fallo es justo cuando menos se puede
  confiar en que se ejecute nada más, que es el razonamiento que ya está escrito ahí.
- Nadie comprueba que el zip descargado sea de la **arquitectura** correcta. Hoy solo se publica
  `win-x64` y el manifiesto no lo dice.

---

## Producto

- [MODIFY] `Directory.Build.props` — `VersionPrefix` de `0.30.34` a `0.30.35`.
- [MODIFY] `CHANGELOG.md` — entrada `## [0.30.35-beta]` **en español**, contada como beneficio, en el
  estilo de las de arriba. Lo que tiene que quedar dicho:
  - **Si una actualización no se puede aplicar, Sakura vuelve a abrirse sola.** Antes se cerraba para
    instalarse y, si algo fallaba, ya no volvía: la versión que tenías seguía entera en el disco, pero
    la aplicación no arrancaba hasta que la abrías a mano.
  - **Y ahora te lo dice.** Personalizar → Actualizaciones avisa de que la última actualización no se
    pudo aplicar y de que sigues en la versión de antes.
  - **Se comprueba que la huella sea la del archivo que se descargó.** Antes se cogía la última
    huella de la publicación; funcionaba por el orden en que GitHub devuelve los archivos, y con
    cualquier archivo nuevo habría dejado de funcionar para todo el mundo a la vez.
  - **La bienvenida ya no promete una vista previa de cada captura.** Sakura solo mira cuando se lo
    pides y pide permiso antes de cada captura, pero hay veces —cuando preguntas por lo que tienes
    delante— en que no te enseña la imagen antes de usarla.
  - **La bienvenida dice el espacio que hace falta de verdad para la IA local:** unos 9 GB libres
    mientras se instala, aunque al final ocupe unos 5.
  - **Desinstalar se lleva también lo que dejó una actualización a medias.**
  - Sección **«Lo que este cambio NO cubre»**, como en 0.30.33 y 0.30.34: la copia de Microsoft Store
    se actualiza sola por la Store y nada de esto la afecta; el aviso de actualización fallida sale en
    cada arranque hasta que una salga bien; la oferta sigue sin decir cuánto pesa la descarga ni
    enseñar la huella; nada se reprodujo ejecutando la app salvo lo que se diga tras la prueba en
    vivo.
- [MODIFY] `docs/stable-release/KNOWN_LIMITATIONS.md`:
  - limitación **L18** (la última es L17, `:377`), con el formato «Qué se cubre / Qué NO se cubre»:
    - qué se cubre: reapertura en toda salida por fallo; el aviso en Personalizar; la huella
      emparejada; el candado antes de la migración pesada; `.new`/`.old` en la desinstalación;
    - qué **no** se cubre: la ruta MSIX no se ha ejercitado nunca y queda fuera; el aviso repite en
      cada arranque; el registro `ultima-actualizacion.log` sigue siendo la única traza y no se
      enseña entero a propósito; la oferta no dice el tamaño ni la huella; si el ayudante muere de
      golpe —el equipo se apaga a mitad del intercambio— puede quedar la carpeta a medias y ahí no
      hay reapertura que valga; el paso 4 arregla una carrera **no reproducida**; un
      `settings.json` escrito a mano sin `SchemaVersion` sigue enseñando la bienvenida otra vez, y
      es a propósito.
  - **L8** (`:307-309`): corregir «ve versión, notas y hash» — lo que se ve es versión, notas y la
    carpeta que se va a reemplazar; la huella **se comprueba** pero no se enseña.
  - **La duplicidad de L12**: hay dos (`:74` resuelta, `:300` la del mutex). Renumerar la del mutex a
    **L19** —el número libre tras L18— y dejar una nota de una línea donde estaba, para que
    cualquier referencia anterior siga encontrándose.
  - **L1** (`:8-14`): anotar los dos tamaños ya conocidos de la publicación 0.30.34-beta (portable
    100 788 001 bytes, instalador 67 695 548 bytes) como dato, **sin** declarar L1 cerrada: sigue
    faltando el SHA-256 medido en local y las latencias.
- [MODIFY] `RELEASE_CHECKLIST.md` y `AGENTS.md` — paso 8.

**No cambia qué datos salen del equipo.** No se tocan `docs/PRIVACY.md` ni `site/legal/`: la consulta
de versiones a GitHub ya está descrita ahí y sigue siendo la misma consulta, con la misma casilla
para apagarla (`ShellPreferences.cs:92-102`).

---

## Verificación

1. `dotnet test Nexo.slnx` entero en verde (Core, Windows, App). La base son **2 772** pruebas; si
   alguna queda en rojo, se dice.
2. **La prueba que de verdad cierra este bloque: una actualización real 0.30.34 → 0.30.35.** No se
   puede hacer antes de publicar, así que el PR se abre sin ella y se hace después, con la versión ya
   publicada. Requisitos y avisos:
   - comprobar antes que League of Legends y Riot Client no están corriendo;
   - **no** se prueba sobre la instalación de Adler (`%LOCALAPPDATA%\Programs\Kohana`). Se usa una
     copia aparte: el zip portable de 0.30.34 extraído en una carpeta con al menos dos niveles
     (`UpdateSwapPathPolicy` rechaza menos), con `SAKURA_DATA_ROOT` apuntando a datos aislados;
   - **camino feliz:** Buscar actualizaciones → Instalar. Sakura tiene que cerrarse, reemplazarse,
     **volver a abrirse sola**, mostrar la versión nueva en Personalizar, **no** enseñar la
     bienvenida, y conservar tareas, enfoque y el modelo de IA elegido. `ultima-actualizacion.log`
     tiene que terminar en «Terminado.» y la carpeta de actualizaciones quedar sin zip;
   - **camino de fallo:** repetir dejando un símbolo del sistema abierto **dentro** de la carpeta
     `…\Sakura.new` (así es como el banco forzó el fallo). Sakura tiene que **volver a abrirse con la
     versión anterior**, y Personalizar → Actualizaciones tiene que decir que la última actualización
     no se pudo aplicar;
   - **doble arranque:** durante los 180 segundos de espera del ayudante, abrir Sakura a mano. La
     segunda copia que el ayudante lance después tiene que retirarse sola sin duplicar ventanas.
3. **Instalación desde cero**, con el instalador de 0.30.35 en una carpeta de datos aislada: la
   bienvenida sale, los cuatro pasos se recorren, «Omitir» funciona, y al volver a abrir **no vuelve a
   salir**. Comprobar también «Repetir configuración inicial» desde Personalizar.
4. **Desinstalación:** con `scripts/sandbox/Invoke-InstallCycle.ps1` en Windows Sandbox (requiere
   `Containers-DisposableClientVM` y la virtualización activada en la BIOS). Tras un ciclo
   instalar → actualizar → desinstalar, **no debe quedar ni `{app}`, ni `{app}.new`, ni `{app}.old`**.
5. **Accesibilidad, dentro del bloque:** recorrer la bienvenida con Tab y con el Narrador y comprobar
   que el texto nuevo del paso de privacidad se anuncia entero, y que la línea nueva de
   Personalizar → Actualizaciones tiene nombre. Si algo no se anuncia, **se anota en L18 tal cual** y
   no se cambia el destino del aviso por iniciativa propia.
6. Del PR: qué se reprodujo en vivo y qué no. **No afirmar que el bloque deja «las actualizaciones
   resueltas»**: deja resuelto que una actualización fallida no deja a la persona sin aplicación y sin
   explicación, que la huella que se comprueba es la del archivo que se bajó, y que la bienvenida no
   promete lo que no hay.

---

## Decisiones para Adler

No frenan la implementación: el plan lleva una recomendación tomada para cada una. Solo cambian lo
que la persona nota, así que las decide él.

**1. Qué decir en la bienvenida sobre las capturas.** Hoy dice «Las capturas siempre requieren una
acción explícita y una vista previa», y la segunda mitad no es cierta: cuando preguntas por lo que
tienes delante, Sakura mira y contesta sin enseñarte antes la imagen.
- *Recomendada — cambiar el texto:* decir que solo mira cuando se lo pides, que pide permiso antes de
  cada captura y que en Personalizar → Permisos decides qué se permite. Coste: queda claro que a
  veces no ves la imagen antes de que se use.
- *Alternativa — cambiar el código y enseñar siempre la miniatura:* cumpliría la frase al pie de la
  letra. Coste: mete un paso entre «¿qué es esto?» y la respuesta, justo en la ruta que existe para
  ser rápida, y es trabajo de otro bloque.
- Se recomienda cambiar el texto porque la promesa importante —no mirar sin que lo pidas, y
  preguntarte antes— **sí** se cumple, y porque una frase que promete de más es peor que una que
  describe bien.

**2. Avisar de que la última actualización falló, y cuántas veces.**
- *Recomendada — avisar en cada arranque hasta que una salga bien*, en Personalizar →
  Actualizaciones, en una línea propia. Coste: si la actualización sigue sin poder aplicarse, el
  aviso sigue ahí. Eso es lo que pasa.
- *Alternativa — avisar una sola vez:* menos insistente. Coste: hay que recordar en disco que ya se
  avisó, y quien no abra Personalizar ese día no se entera nunca.
- Se recomienda avisar siempre mientras el problema exista: hoy no se avisa de nada y Sakura
  simplemente no vuelve.

**3. El espacio que pide la IA local.** La bienvenida dice «ocupa unos 5 GB»; el código exige 9 GB
libres para instalarla (el comprimido y el descomprimido conviven un rato).
- *Recomendada — decir los dos números en la bienvenida:* «necesita unos 9 GB libres mientras se
  instala; al final ocupa unos 5».
- *Alternativa — bajar el requisito a 5 GB:* la frase cuadraría sola. Coste: la instalación se
  quedaría a medias con el disco lleno en el peor momento, que es de lo que ese margen protege.
- Se recomienda decir los dos números. Un requisito alto explicado se entiende; un fallo a mitad de
  una descarga de cinco gigas, no.
