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

### L12 — La versión que Windows muestra se queda vieja tras cada actualización
**Qué:** el registro de "Aplicaciones instaladas" sigue diciendo la versión que puso el instalador.
En el equipo de Adler decía `0.25.0-dev.20260817` con el ejecutable ya en 0.26.9.
**Por qué:** el actualizador reemplaza los archivos de la carpeta de instalación mediante su ayudante;
no ejecuta el instalador, así que nadie toca la entrada de desinstalación del registro.
**Aislamiento:** ninguno. Es cosmético para quien mira la lista de aplicaciones, pero engaña sobre
qué versión está instalada, que es justo lo que esa lista existe para responder.
**Para estable:** que el ayudante actualice `DisplayVersion` al terminar, o que la actualización pase
por el instalador en modo silencioso.

### L13 — Desinstalar después de actualizar puede dejar archivos
**Qué:** el desinstalador de Inno Setup borra lo que su registro de instalación dice que puso. Los
archivos que llegan después, en una actualización, no están en ese registro.
**Por qué:** el conjunto de archivos cambia entre versiones (DLL nuevas, modelos, recursos) y el
actualizador los escribe sin anotarlos donde el desinstalador mira.
**Aislamiento:** ninguno. **No está comprobado todavía**: se dedujo leyendo cómo funcionan las dos
piezas, y hace falta el ciclo completo en una máquina para confirmarlo y medir cuánto queda.
**Para estable:** ejecutar el ciclo instalar → actualizar → desinstalar y dejar la carpeta vacía, o
documentar qué queda y por qué.

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
