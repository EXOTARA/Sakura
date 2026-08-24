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

### L2 — `MainWindow.xaml.cs` como God Object
**Qué:** 4.044 líneas, 49 campos `readonly` (25 siguen instanciados con `new` en la declaración
tras la fase 1.2; los seis servicios de interfaz ya no). *(Cifra de líneas medida el 2026-07-23
sobre el checkpoint `82a36fb`, antes de tocar nada en 1.2: 4.027 — no coincide con las 3.532 que
documentaba la revisión de 1.1.1; discrepancia no investigada, ver `IMPLEMENTATION_LOG.md` riesgo
#14. La cifra de 119 métodos tampoco se remidió en esta fase.)*
**Por qué:** Crecimiento incremental. La fase 1.2 (2026-07-23) añadió un composition root
(`Nexo.Windows/Composition/KohanaCompositionRoot.cs` + `Microsoft.Extensions.DependencyInjection`)
y desacopló los **seis** servicios de interfaz que bloqueaban el Adaptive Engine Registry
(`IAiChatService`, `IAudioMixerService`, `IVoiceInputService`, `IVoiceOutputService`,
`IWakeWordService`, `IScreenCaptureService`). El archivo **no se redujo** — ese es el trabajo de
1.3–1.7 — pero ya no es imposible seleccionar motor por hardware para esos seis.
**Aislamiento:** Los seis servicios de interfaz ya se resuelven desde un contenedor DI real,
verificado por prueba. El resto del God Object (25 campos restantes con `new`, navegación,
tareas/enfoque/rutinas, IA y Vision fusionados con la vista) sigue intacto.
**Para estable:** completar los pasos 1.3–1.7 de la Fase 1 (ADR 0001).

### L3 — Accesibilidad ausente
**Qué:** 0 `AutomationProperties` en los 22 archivos XAML. Sin soporte de lector de pantalla.
**Aislamiento:** Ninguno.
**Para estable:** Fase 9 — es criterio de salida explícito.

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
puede revertir. Se le suma el defecto **D3** medido en 1.1: `JsonSettingsStore.Load` **no llama a
`Normalize()`** en las rutas de archivo ausente o corrupto, así que devuelve `SchemaVersion = 0` y
el siguiente `Save` reejecuta todas las migraciones desde cero. Tras una corrupción, el shell no
puede marcar el onboarding como completado en ese mismo arranque. La degradación dura un ciclo y
no pierde datos del usuario, porque ya eran ilegibles.
**Aislamiento:** cubierto por `SettingsStoreCharacterizationTests` (escenario 4 de `TEST_MATRIX`).
**No bloquea la fase 1.2.**
**Para estable:** añadir copia previa `.bak` y normalizar también en las rutas de recuperación.

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

## Fuera de alcance de 1.0 (decidido, no es limitación)

Puentes de mensajería · marketplace comunitario · automatización de navegador (experimental) ·
emparejamiento de dispositivos · clonación de voz · Windows 10.
