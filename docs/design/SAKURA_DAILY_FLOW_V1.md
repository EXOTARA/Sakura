# Sakura Daily Flow v1 — Diseño D3

> Tercer sprint de diseño. Parte de `release/kohana-1.0-rc` con D1 + D1.1 + D2 integrados y
> aprobados. Convierte Inicio, Hoy, Enfoque y Rutinas en un flujo diario real y conectado. No toca
> Asistente, Audio, Captura, Sistema, instalador, actualizador, plugins ni servicios en la nube.

## 1. Estado actual (medido, no supuesto)

Hallazgo central de este descubrimiento: **las cuatro vistas ya son mucho más funcionales de lo
que el encargo asumía.** No son vistas básicas a conectar desde cero — son CRUD reales con
persistencia, que faltan encadenar entre sí y completar en los puntos concretos que se listan
abajo. Este sprint es de **conexión y cierre de huecos puntuales**, no de construcción desde cero.

| Área | Estado real encontrado |
|---|---|
| `NexoTask` (Nexo.Core/Tasks) | Ya tiene `Title`, `Notes`, `DueAt`, `Priority` (`Low/Normal/High`), `ReminderEnabled`, `CompletedAt`, `IsOverdue()`. **No hace falta extender el modelo ni migrar nada.** |
| `TaskManager` | Ya tiene `Create`, `Update` (edición completa), `Complete`, `Delete`, `CollectDueReminders`, `BuildTodaySummary`, `BuildPendingSummary`. **Falta `Reopen`** — no existe en ningún lado del código. |
| `TasksView` | Ya tiene editor inline (crear/editar), filtros Hoy/Pendientes/Hechas, contadores, prioridad, fecha+hora, recordatorio, estado vacío. **Falta:** confirmación al eliminar (hoy borra sin preguntar), reabrir una tarea completada, iniciar enfoque desde una tarea, nombres accesibles en los botones de icono (✓ ✎ ×). |
| `FocusTimer` / `FocusManager` | Ya persiste con **timestamps** (`StartedAt`, `EndsAt`, `PausedRemaining`), no con una cuenta regresiva serializada — cumple el requisito del encargo por construcción. Tiene `Start/Pause/Resume/Cancel/CollectCompletion`. **Falta:** un `Finish` explícito que complete la sesión antes de tiempo conservándola en el historial (hoy solo existe `Cancel`, que descarta sin registrar, y la finalización natural vía `CollectCompletion`, que es pasiva). **Falta:** asociación con una tarea (`TaskId` no existe en `FocusTimer` ni en `FocusHistoryEntry`). |
| `FocusView` | Ya tiene presets, duración personalizada, pausar/reanudar/cancelar, barra de progreso, resumen de hoy. Un único `DispatcherTimer` en `MainWindow` (`_focusTickTimer`) ya conduce el refresco — **no hay que crear otro**. **Falta:** botón "Finalizar" distinto de "Cancelar", mostrar la tarea asociada. |
| `RoutineManager` / `RoutineDefinition` | Ya tiene `Create/Update/Delete/GetAll/FindBestMatch`, con `IsEnabled` y `RequiresConfirmation`. **Falta:** `LastExecutedAt`/`LastExecutionSucceeded` (no existen — se añaden de forma compatible), un método para alternar habilitada/deshabilitada sin abrir el editor completo. |
| `RoutineRunner` | Ya ejecuta con manejo de errores por paso (nunca lanza para un paso individual), aprobación explícita para acciones sensibles, `RoutineExecutionReport.Succeeded`. **No se toca.** |
| `RoutinesView` | Ya tiene editor completo, ejecutar, eliminar **con confirmación** (vía `MessageBox.Show`, ya existe — a diferencia de Tasks). **Falta:** mostrar última ejecución, alternar habilitada/deshabilitada desde la lista. |
| `HomeView` | Ya tiene tarjeta de Pendientes, tarjeta de Enfoque, tarjeta "Mirar ahora", actividad reciente (feed real, no inventado). **Falta:** tarjeta de Rutinas, fila de acciones rápidas explícitas (Nueva tarea / Ver Hoy / Iniciar enfoque / Ver rutinas / Command Center), nombres accesibles en las tarjetas. El valor de Enfoque muestra "25 min" fijo cuando no hay sesión activa — es un valor de relleno que parece una medición; se corrige para mostrar minutos acumulados hoy o un estado vacío honesto. |
| Coordinación entre vistas | **Ya existe un patrón real, no ausente:** `TasksView.TasksChanged`, `FocusView.FocusChanged`, `RoutinesView.ExecuteRequested` son eventos que `MainWindow` ya escucha y traduce en llamadas a `Refresh()` de las vistas hermanas y a `RefreshHomeView()` (que sí centraliza en `MainWindow`, en ~9 puntos de llamada distintos). Es funcional pero deja la coordinación dispersa en `MainWindow.xaml.cs` (~4.700 líneas). Se añade un `DailyFlowEventHub` pequeño (ver §7) para que `HomeView` se refresque reactivamente en un solo lugar, sin sacar la lógica de dominio de los managers ni reescribir el patrón existente. |
| Sakura Command Center (D2) | Ya registra `tasks.create`, `focus.start` (navega), `focus.cancel` (cancela de verdad). **Falta:** `focus.pause`, `focus.resume`, `focus.finish`, e `routine.execute.<id>` por cada rutina habilitada (dinámico, no estático). |
| Persistencia | `JsonTaskStore`, `JsonFocusStore`, `JsonRoutineStore` (Nexo.Windows) — todas escriben a un `.tmp` y lo mueven encima (`File.Move(overwrite)`): eso garantiza que el archivo anterior nunca queda a medias si la escritura falla, **no** que sobreviva a un corte de luz (no hay `fsync`). Apartan el archivo corrupto a un `.corrupt-*` que la app no restaura sola. Tareas y enfoque, además, no escriben encima de un archivo que no pudieron leer (ver L16). Ninguna requiere una base de datos nueva. |
| Componentes compartidos (D2) | Ya existen `WorkspaceInteractiveCardStyle`, `WorkspaceStatusChipStyle`, `WorkspaceEmptyStateTitleStyle/DetailStyle`, `WorkspaceErrorStateStyle`, etc. en `Themes/Controls.xaml`. Se reutilizan; solo se añaden los que Daily Flow necesita y que D2 no cubrió (fila de tarea, badge de vencimiento, temporizador, selector de duración, indicador de progreso, tarjeta de rutina, diálogo de confirmación reutilizable, toast). |
| Pruebas existentes | `TaskManagerTests` (127 líneas), `FocusManagerTests` (168 líneas), `RoutineManagerTests` (93 líneas) en `Nexo.Core.Tests`, con fakes en memoria (`MemoryFocusStore`, etc.) ya establecidos como patrón — se reutiliza. |

## 2. Servicios reutilizados (sin reescribir)

`TaskManager`, `FocusManager`, `RoutineManager`, `RoutineRunner`, `IAutomationActionExecutor`,
`JsonTaskStore`, `JsonFocusStore`, `JsonRoutineStore`, `KohanaCommandRegistry` /
`CommandSearchEngine` (D2), estilos compartidos de `Themes/Controls.xaml` (D2),
`ShellNavigationPolicy`, `StaWpfFixture` (App.Tests).

## 3. Problemas encontrados

1. **Sin `Reopen` en `TaskManager`.** Una tarea completada no puede volver a pendiente.
2. **Sin confirmación al eliminar una tarea** (`TasksView.DeleteTaskButton_Click` borra de
   inmediato) — inconsistente con `RoutinesView`, que sí confirma.
3. **Sin asociación tarea↔enfoque.** No hay forma de saber, desde una sesión activa, a qué tarea
   pertenece.
4. **Sin `Finish` en `FocusManager`.** Solo `Cancel` (descarta) o la finalización pasiva por
   tiempo agotado (`CollectCompletion`). No hay una acción de "terminé antes, cuenta esta sesión".
5. **Sin última ejecución en `RoutineDefinition`.** No hay dato que mostrar aunque se quisiera.
6. **Sin alternar habilitada/deshabilitada sin abrir el editor completo** en `RoutinesView`.
7. **`HomeView` sin tarjeta de Rutinas ni fila de acciones rápidas explícitas.**
8. **Valor de relleno en la tarjeta de Enfoque de Inicio** ("25 min" fijo sin sesión activa).
9. **Botones de icono sin nombre accesible** en `TasksView` (✓ ✎ ×) y tarjetas de `HomeView`.
10. **Coordinación de refresco dispersa** en ~9 puntos de `MainWindow.xaml.cs` — no un defecto
    funcional, pero el punto exacto que el encargo pide no profundizar.

## 4. Flujo propuesto

Inicio (resumen + accesos) → Hoy (crear/elegir tarea, botón "Enfocarme") → Enfoque (inicia con la
tarea asociada, pausa/continúa/finaliza) → vuelta a Hoy (la tarea sigue pendiente, con opción
explícita de completarla) → Rutinas (ejecutar, ver última ejecución) → Inicio (resumen actualizado
sin reiniciar Kohana). El Command Center ofrece atajos a cada paso con disponibilidad dinámica
real.

## 5. Modelo de actualización entre vistas

Se mantiene el patrón de eventos por vista ya existente (`TasksChanged`, `FocusChanged`,
`ExecuteRequested`) — **no se reemplaza**, funciona y tiene superficie de regresión si se toca sin
necesidad. Se añade `DailyFlowEventHub` (`Nexo.App/DailyFlow/DailyFlowEventHub.cs`), una clase de
coordinación **sin lógica de dominio** (solo reenvía eventos), que:

- expone `TasksChanged`, `FocusChanged`, `RoutinesChanged`;
- `MainWindow` construye una única instancia y conecta los eventos existentes de cada vista a ella;
- `HomeView` recibe el hub por constructor y se suscribe para refrescarse sola, en vez de que
  `MainWindow` llame `RefreshHomeView()` manualmente en cada uno de los ~9 sitios.

Esto **reduce** puntos de coordinación en `MainWindow` en vez de añadir una capa nueva paralela:
los manejadores existentes (`FocusView_FocusChanged`, etc.) siguen hicieron su trabajo de dominio
(ej. `CollectCompletion`) y además notifican al hub; el hub no sabe nada de `TaskManager` ni
`FocusManager`.

## 6. Persistencia

Ninguna requiere un almacén nuevo. Extensiones puntuales, todas compatibles hacia atrás porque son
campos nuevos anulables en una lista/objeto JSON ya deserializado con
`PropertyNameCaseInsensitive`:

- `FocusTimer.TaskId` (`Guid?`) y `FocusHistoryEntry.TaskId` (`Guid?`) — un archivo antiguo sin
  este campo simplemente lo deserializa como `null`.
- `RoutineDefinition.LastExecutedAt` (`DateTimeOffset?`) y `LastExecutionSucceeded` (`bool?`) —
  mismo razonamiento. `RoutineState.SchemaVersion` sube de 1 a 2; `RoutineManager.Normalize()` gana
  un escalón de migración (hoy fija `SchemaVersion = 1` sin escalera — se cambia a una escalera
  real seguida del patrón ya usado en `ShellPreferences.Normalize()`).
- `TaskManager.Reopen(Guid id)` — no es un cambio de esquema, es un método nuevo sobre el modelo
  existente (limpia `CompletedAt`).
- `FocusManager.Finish(DateTimeOffset now)` — tampoco cambia el esquema; construye un
  `FocusHistoryEntry` con la duración real transcurrida (`Duration - GetRemaining(now)`), igual
  que hace `CollectCompletion` para la finalización natural.

No se usa `ShellPreferences`/`JsonSettingsStore` para nada de esto: quedan reservados a
configuración visual, como ya exige D2.

## 7. Accesibilidad

`AutomationProperties.Name` en los botones de icono de `TasksView` (✓ "Completar", ✎ "Editar", ×
"Eliminar", nuevo "Enfocarme"), en las tarjetas de `HomeView`, y en los controles nuevos de
`FocusView`/`RoutinesView`. Confirmación de eliminar tarea con foco inicial en "Cancelar" (acción
no destructiva por defecto). El estado de prioridad, vencimiento, completado, sesión activa y
rutina deshabilitada no dependen solo del color (ya es así en su mayoría; se revisa el nuevo
código para mantenerlo).

## 8. Estrategia de pruebas

- **Core (sin WPF):** `TaskManager.Reopen`, `FocusManager.Finish` (incluida su interacción con
  `TrimHistoryLocked` y con una sesión pausada), migración de `RoutineState` v1→v2, disponibilidad
  dinámica de los comandos nuevos del Command Center, asociación tarea↔enfoque a nivel de modelo.
- **WPF real (`StaWpfFixture`, colección compartida ya establecida en D2):** las cuatro vistas
  cargan con servicios reales y fakes seguros, `Measure`/`Arrange`/`UpdateLayout`, confirmación de
  eliminar, reabrir, iniciar enfoque desde una tarea, estados vacíos, recursos tras mutar el
  acento.
- **Ciclo de vida:** un único `DispatcherTimer` de enfoque (ya existe, se verifica que sigue siendo
  uno solo), sin ventanas residuales, sin excepciones de Dispatcher ocultas.

## 9. Riesgos

- `MainWindow.xaml.cs` ya es grande; cada nuevo cableado (hub, comandos nuevos) añade superficie.
  Mitigado manteniendo el hub sin lógica de dominio y los managers como única fuente de verdad.
- Migración de `RoutineState` v1→v2: bajo riesgo (campos anulables), pero se prueba explícitamente
  igual que exige el encargo.
- Asociar una tarea a una sesión de enfoque activa y luego editar o eliminar esa tarea podría dejar
  una referencia colgante (`TaskId` apuntando a nada). Se resuelve tratando `TaskId` como
  informativo — al mostrarlo, si la tarea ya no existe, se muestra la etiqueta de la sesión y ya.

## 10. No objetivos

No se tocan Asistente, Audio, Captura, Sistema. No se introduce un framework MVVM. No se migra la
aplicación a otro patrón. No se añade otra base de datos. No se reescribe `RoutineRunner`,
`AutomationPermissionPolicy` ni el motor de voz/IA. No se construye un editor de rutinas más
avanzado que el que ya existe.

## 11. Plan de commits

1. `docs: define Sakura Daily Flow v1` — este documento.
2. `feat: connect task management to Today workspace` — `Reopen`, confirmación de eliminar,
   accesibilidad, botón "Enfocarme".
3. `feat: connect focus sessions to Daily Flow` — `Finish`, asociación con tarea, botón
   "Finalizar", `DailyFlowEventHub`.
4. `feat: connect routines and daily summary` — última ejecución, alternar habilitada/deshabilitada,
   tarjeta de Rutinas y fila de acciones rápidas en Inicio.
5. `feat: extend Command Center with daily actions` — `focus.pause/resume/finish`,
   `routine.execute.<id>` dinámico.
6. `test: cover Sakura Daily Flow runtime behavior` — pruebas Core y WPF real.
7. `docs: record Sakura Daily Flow D3 implementation` — informe final.

Cada commit compila y pasa la suite completa antes de crearse.

## 12. Aprobación (Diseño D3.2)

**Fecha:** 2026-07-25. **Estado:** smoke manual aprobado por el usuario.

El usuario confirmó Inicio, Hoy y el CRUD de tareas, la asociación tarea↔enfoque,
pausar/continuar/finalizar/cancelar, Rutinas y Command Center, sin crash, pérdida de datos ni
timers duplicados, y autorizó integrar este diseño en `release/kohana-1.0-rc`. Ver la sección de
aprobación en `Kohana-Design-D3-Sakura-Daily-Flow-Informe.md` para el detalle completo. Integrado
mediante `merge: integrate approved Sakura Daily Flow D3`.
