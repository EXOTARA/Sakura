# Registro de implementación — Kohana 1.0

> **Este archivo es la memoria persistente del proyecto.**
> Todo agente que retome el trabajo debe leerlo **primero** y actualizarlo **antes** de quedarse sin
> contexto. Las decisiones registradas aquí y en `PRODUCT_VISION.md` **no vuelven a preguntarse**.

---

## ESTADO ACTUAL

| Campo | Valor |
|---|---|
| **Fase actual** | **Diseños D7, D8 y D9** implementados y probados en `design/kohana-sprints-d7-d9`; sin integrar a `release/kohana-1.0-rc` todavía |
| **Siguiente fase** | Smoke test manual del usuario sobre los tres, luego integración a release |
| **Rama** | `design/kohana-sprints-d7-d9`, creada desde `release/kohana-1.0-rc` (`56e0d3a`, D6 ya integrado) |
| **Versión base** | **0.9.5-beta** (verificada en `Directory.Build.props`) |
| **Última actualización** | 2026-07-30 |
| **Bloqueador activo** | Ninguno. Falta que el usuario pruebe D7/D8/D9 a mano antes de declararlos aprobados |

### ✅ Baseline medido — 2026-07-23

Medido en Windows sobre el repositorio real. Ningún valor es estimado; los campos que **todavía no
pueden medirse** en esta fase se marcan `NO MEDIDO` en lugar de rellenarse.

```
[x] Sistema operativo:        Microsoft Windows NT 10.0.26200.0 (Windows 11 Pro, build 26200)
                              → cumple el mínimo de la decisión B (26100+)
[x] Ruta del repositorio:     C:\Dev\Nexo
[x] Rama:                     release/kohana-1.0-rc
[x] dotnet --info (SDK):      10.0.302  (MSBuild 18.6.11, host 10.0.10, RID win-x64)
                              global.json fija 10.0.302 con rollForward=latestFeature
[x] git status (limpio?):     limpio (sin cambios pendientes)
[x] Commit inicial (hash):    144bb13e794dcf085fb31df0fba171896583f169
[x] Versión real del código:  0.9.5-beta
[x] Total de pruebas:         356   (Nexo.Core.Tests 353 + Nexo.Windows.Tests 3)
[x] Pruebas fallidas:         0
[x] Pruebas omitidas:         0
[x] Warnings de compilación:  0
[x] Errores de compilación:   0
[x] Tiempo de restore:        1.70 s   (en frío, tras borrar bin/obj)
[x] Tiempo de build Release:  2.52 s   (MSBuild, en frío; 2.80 s de reloj)
[x] Tiempo de test:           Core 210 ms · Windows 308 ms  (2.81 s de reloj, `--no-build`)
[ ] Tamaño del portable:      NO MEDIDO — requiere `dotnet publish` (Fase 10)
[ ] Tamaño del instalador:    NO MEDIDO — requiere compilar `installer/Kohana.iss` (Fase 10)
[ ] SHA-256 del portable:     NO MEDIDO — depende del portable (Fase 10)
```

Comandos exactos ejecutados:

```powershell
dotnet restore .\Nexo.slnx
dotnet build   .\Nexo.slnx -c Release --no-restore
dotnet test    .\Nexo.slnx -c Release --no-build
```

**Incidencia registrada durante la medición:** el primer `dotnet build` falló con `MSB3021`/`MSB3027`
porque una instancia de `Kohana.exe` (PID 31064) lanzada desde `src\Nexo.App\bin\Release` mantenía
bloqueados `Nexo.Core.dll` y `Nexo.Windows.dll`. Se cerró el proceso con autorización del
propietario y se repitió la medición en frío. **No es un defecto del código**, pero conviene
documentarlo: compilar con la app corriendo desde el directorio de salida siempre fallará.

### Verificación de versión (0.9.5 vs 0.9.6)

La versión real del código es **0.9.5-beta**. La cadena `0.9.6-beta` aparece **una sola vez** en el
repositorio, en `docs/ROADMAP.md:54`, como encabezado de un sprint **planificado y no implementado**.
No hay código, entrada de `CHANGELOG` publicada ni etiqueta de 0.9.6. Por tanto **no se rebaja código
ni se sustituye base**: documentación y código ya coinciden en 0.9.5-beta.

Se corrigieron en cambio las **cifras estáticas** de `CURRENT_STATE_AUDIT.md`, que se habían tomado
del ZIP `Kohana-0_9_5-foundation-base.zip` y no del repositorio con los commits 0.9.3–0.9.5
consolidados. Ver `CURRENT_STATE_AUDIT.md` §0. Resumen: `MainWindow.xaml.cs` mide **3,532** líneas
(no 4,007) y hay **356** pruebas (no 196). La conclusión cualitativa —God Object y bloqueador raíz—
no cambia.

---

## DECISIONES TOMADAS (no volver a preguntar)

Todas registradas en detalle en `PRODUCT_VISION.md`. Resumen ejecutable:

| # | Decisión | Valor |
|---|---|---|
| A | Fuentes de verdad | Claude Code en Windows local **+** GitHub Actions `windows-latest` |
| B | Windows mínimo | **Windows 11 24H2 (build 26100)+**. TFM objetivo `net10.0-windows10.0.26100.0` |
| C | Licencia | **MIT** propio. Dependencias: MIT/Apache-2.0/BSD. Sin restricción no comercial |
| D | Experiencias día 1 | "abre X" sin LLM · "¿qué es esto?" interrumpible · instalación sin terminal |
| E | Wake words | Estables: `Oye Kohana`, `Kohana`. Experimentales: `Ey Kohana`, `Hey Kohana` |
| F | Permisos | Abrir PowerShell = **sin** confirmación; ejecutar comando dentro = **con** confirmación |
| G | Memoria | **Opt-in**. Nada se escribe sin aprobación. Diario automático off por defecto |
| H | Dataset de audio | Opt-in, local, revisable, exportación manual, **nunca** subida automática |
| I | Firma/updates | **Sin Authenticode hoy**. GitHub Releases. Notificar, no actualizar en silencio |
| J | Telemetría | **Ninguna remota**. Logs locales anonimizados, exportación manual |
| K | Skills 1.0 | 5 estables + 1 experimental (dev tools). **Sin marketplace** |
| — | Extracción de `MainWindow` | **7 pasos**, cada uno compilable + probado + commit propio |

Decisiones estructurales adicionales:
- `Nexo.Core` permanece **sin dependencias** y en `net10.0` puro. Invariante.
- Namespaces internos `Nexo.*` **se conservan**. No hay fugas visibles al usuario.
- Vosk y SAPI pasan a `FallbackHeredado`; **no se retiran** hasta que un sustituto gane medido.

---

## TAREAS TERMINADAS

### Fase 0 (2026-07-22)
- [x] Auditoría estática completa de la base 0.9.5 (222 `.cs`, 22 `.xaml`, 16 `.md`)
- [x] Delta 0.9.4 → 0.9.5 identificado (6 nuevos, 31 modificados, 0 eliminados)
- [x] Hallazgo crítico documentado: `MainWindow.xaml.cs` = 4,007 líneas / 224 métodos / ~28 servicios
- [x] Bloqueador de TFM identificado (WinRT/OCR inaccesible con `net10.0-windows`)
- [x] Verificado: 0 `AutomationProperties` en XAML (accesibilidad ausente)
- [x] Verificado: sin fugas de marca "Nexo" en UI (solo claves de estilo internas)
- [x] Verificado: escritura de settings es atómica; falta `.bak` de recuperación
- [x] `PRODUCT_VISION.md` con decisiones A–K
- [x] `CURRENT_STATE_AUDIT.md`
- [x] `STABLE_RELEASE_PLAN.md` (Fases 0–10 + 7 pasos de extracción)
- [x] `ACCEPTANCE_CRITERIA.md` (presupuestos marcados `PENDIENTE DE CALIBRAR`)
- [x] `TEST_MATRIX.md` (24 escenarios; 7 marcados como bloqueantes de seguridad)
- [x] `SECURITY_MODEL.md` (matriz de permisos + modelo de amenaza)
- [x] `PRIVACY_BOUNDARIES.md` (matriz local/remoto)
- [x] `MIGRATION_PLAN.md`
- [x] `KNOWN_LIMITATIONS.md`
- [x] ADR 0001 (runtime), 0002 (hardware), 0003 (TFM), 0004 (wake word)

---

### Fase 1.1 — pruebas de caracterización (2026-07-23)

Objetivo: congelar la conducta observable **antes** de mover nada de `MainWindow`.
Resultado: **520 pruebas, 0 fallidas, 0 warnings.** 164 pruebas nuevas.

| Área exigida | Dónde queda congelada |
|---|---|
| Navegación | `ShellNavigationCharacterizationTests` (destinos, alternador de Ajustes, módulo oculto) |
| Inicio y cierre | `ShellLifecycleCharacterizationTests` (`--background`, bandeja, salida explícita) |
| Segunda instancia | `SingleInstanceCharacterizationTests` |
| Comandos locales | `PromptDispatchCharacterizationTests` (orden de despacho completo) |
| PowerShell: abrir vs. ejecutar | `LocalActionPermissionCharacterizationTests` |
| Wake word y voz | `VoiceRuntimeCharacterizationTests` |
| Tareas, enfoque, rutinas | `PromptDispatchCharacterizationTests` (precedencia real entre parsers) |
| Vision y privacidad | `ShellLifecycleCharacterizationTests` (ventanas sensibles) |
| Resource Governor | `ShellLifecycleCharacterizationTests` (Normal/Busy/Game, umbrales 88/92/92) |
| Preferencias y migraciones | `PreferencesMigrationCharacterizationTests` (v0→v16, clamps, idempotencia) |
| Settings corruptos | `SettingsStoreCharacterizationTests` (escenario 4 de `TEST_MATRIX`) |

**Seams extraídos** (mínimos, sin cambio de conducta, verificados con build + 520 pruebas +
arranque real de la aplicación):

1. `Nexo.Core/Shell/ShellNavigationPolicy.cs` — **nuevo**. Reglas puras de navegación que ya
   aplicaba `MainWindow`: destinos conocidos, alternador de Ajustes, caída al Asistente al
   ocultar el módulo activo, y mapa de órdenes de navegación. `MainWindow` delega en él y usa
   sus constantes para construir `_views`, de modo que no puede haber deriva entre ambos.
2. `SingleInstanceCoordinator` movido de `Nexo.App` a
   `Nexo.Windows/WindowsIntegration/`. No tenía ninguna dependencia de WPF y desde `Nexo.App`
   era inalcanzable para las pruebas. Se añadió un parámetro opcional `instanceKey` cuyo valor
   por defecto (`null`) **conserva exactamente** los nombres históricos del mutex y del evento;
   solo las pruebas pasan una clave propia, para no colisionar con una instancia real de Kohana.

> Se descartó referenciar `Nexo.App` desde el proyecto de pruebas: arrastra `UseWPF`, que
> cambia el conjunto de *implicit usings* y provoca `MSB3277` (conflicto de `WindowsBase`).
> Habría subido los warnings de 0 a 1 y el baseline exige 0.

**Ninguna prueba depende de píxeles, layout ni temporización de animación**, conforme a la
instrucción de la fase.

---

### Fase 1.1.1 — correctiva (2026-07-23)

Corrige los tres defectos que la caracterización destapó y que bloqueaban o ensuciaban la
fase 1.2. **606 pruebas, 0 fallidas, 0 warnings.** +86 pruebas sobre 1.1.

#### Auditoría del diff previa a los cambios

El contador de la interfaz mostraba **+10.512 / −31.997**, cifra que no correspondía al trabajo
realizado. Comprobado:

| Comparación | Resultado |
|---|---|
| `144bb13..HEAD` (trabajo de fases 0 y 1.1) | **+2.268 / −106**, 19 archivos |
| `main..HEAD` | +14.473 / −1.137 |
| **`origin/main..HEAD`** | **+10.512 / −31.997** ← coincide exactamente con la interfaz |

Conclusión: **el contador compara contra `origin/main`, no contra `144bb13`.** Las 31.997
eliminaciones son **reales pero ajenas a este trabajo**: provienen de `264c525`
*"chore: remove tracked patch backups"*, que borró `.nexo-patch-backup/` y es **ancestro de
`144bb13`**, es decir, anterior a la fase 0. La rama local `main` está además por detrás de
`origin/main`.

Verificaciones adicionales, todas negativas (sin problema):
- **Cero** archivos de `.nexo-patch-backup/` tocados en `144bb13..HEAD`.
- **Cero** binarios, artefactos de build o archivos generados añadidos.
- **Sin conversión masiva de finales de línea**: `git diff --shortstat` y
  `--ignore-all-space` dan cifras idénticas (+2.268 / −106).
- Un único renombrado, **intencional y documentado**: `SingleInstanceCoordinator` de
  `Nexo.App` a `Nexo.Windows` (fase 1.1).
- Working tree **limpio** antes y después.
- No hay pérdida accidental ni archivos ajenos a las fases 0 y 1.1.

> Nota operativa: `core.autocrlf=true` sin `.gitattributes`. Los archivos están en LF en el
> árbol de trabajo y Git los almacena en LF, así que hoy no hay diferencia. Conviene añadir un
> `.gitattributes` antes de que alguien haga un `checkout` limpio en otra máquina.

#### D1 — Rutinas eclipsaban las órdenes de enfoque ✅ `667a873`

`RoutineMatchConfidence` distingue el reclamo **explícito** ("la rutina X", "modo X") del
**inferido** ("inicia X"), y `PromptDispatchPolicy` concentra el orden normativo: rutina
explícita → enfoque → tareas → rutina inferida **solo si existe** → comando local → IA.
`MainWindow` evalúa los cuatro parsers y delega la decisión, en vez de quedarse con el primer
reclamo de la cascada. Sin listas de excepciones.

#### D2 — Ejecución arbitraria sin confirmación ✅ `4e3524d`

`ShellExecutionPolicy` incorpora los argumentos a la evaluación tipada de riesgo. Abrir un
intérprete sin argumentos sigue sin pedir confirmación; con cualquier argumento pasa a
`Sensitive`. La detección normaliza rutas, comillas, variables de entorno, separadores,
mayúsculas, extensión omitida y los espacios y puntos finales que Windows descarta, e inspecciona
también los argumentos para detectar el rodeo de invocar un intérprete desde otro programa.

`RoutineExecutionApproval` viaja como argumento de cada ejecución y **nunca** se guarda en la
rutina ni en la acción: aprobar una rutina al crearla no concede permiso permanente. `RoutineRunner`
rechaza los pasos sensibles sin aprobación explícita, de modo que el permiso se aplica **en el
ejecutor** y no se confía a que la interfaz haya preguntado.

#### D4 — `Dispose` no idempotente ✅ `787db71`

Guarda `_disposed` en `SingleInstanceCoordinator`, que **previene** la excepción en lugar de
capturarla. Mismo patrón que ya usaban `ManagedOllamaSupervisor` y `TrayIconController`, que no
necesitaban cambios.

#### Smoke test manual (aplicación real, 2026-07-23)

Conducido sobre la app compilada en Release, dirigiendo la interfaz con UI Automation.

| Paso | Resultado |
|---|---|
| Abrir Kohana | ✅ arranca y la ventana responde |
| Navegación | ✅ los 8 destinos del riel: Inicio · Asistente · Hoy · Enfoque · Rutinas · Audio · Captura · Sistema, y vuelta a Inicio |
| Crear/iniciar temporizador | ✅ *"Inicia un temporizador de 20 minutos"* → **"Inicié temporizador por 20 minutos."**; la vista Enfoque muestra `EN CURSO · 19:40`. **D1 verificado en vivo** |
| Rutina explícita | ✅ *"inicia la rutina estudio"* llega al subsistema de rutinas y la ejecuta |
| Abrir PowerShell sin ejecutar | ✅ procesos 1 → 2, **sin confirmación**, "PowerShell abierto en C:\Users\Usuario" |
| Rutina con shell exige confirmación | ✅ diálogo *"Ejecutar Smoke Shell — Kohana ejecutará estas acciones: 1. Abrir powershell.exe"*; al pulsar **No** → "Cancelé la rutina Smoke Shell." y **cero** procesos PowerShell. **D2 verificado en vivo** |
| Cerrar y reabrir | ✅ reabre correctamente y conserva el estado |
| Segunda instancia | ✅ la segunda termina sola; sobrevive el PID original |

Observación honesta: al ejecutar *"inicia la rutina estudio"* el informe fue "0 de 3 acciones".
Las tres causas son **del entorno, no del cambio**: Spotify y Discord no estaban en ejecución
(0 procesos) y ya había un temporizador activo creado por el paso anterior. Los mensajes de la
propia app lo dicen literalmente.

Para probar la confirmación se añadió temporalmente una rutina "Smoke Shell" a
`%LOCALAPPDATA%\Kohana\routines.json`, con copia de seguridad previa, y **se restauró el archivo
original** al terminar (4 rutinas: Programación, Estudio, Descanso, test).

---

### Checkpoint portable — 2026-07-23

> ⚠️ **Esto es un checkpoint de desarrollo, NO una Release Candidate ni una versión estable.**
> Se publica únicamente para poder probar el estado actual fuera del entorno de compilación.
> No cumple los criterios de salida de `ACCEPTANCE_CRITERIA.md` §1 y **no debe distribuirse como
> 1.0 ni como RC**. Siguen abiertos, entre otros, el perfil de hardware, la memoria transparente,
> las skills, OCR/UI Automation y la accesibilidad (`AutomationProperties` = 0).

| Campo | Valor |
|---|---|
| **Commit** | `3edba24` (`docs: record phase 1.1.1 corrective work`) |
| **Rama** | `release/kohana-1.0-rc` |
| **Fecha** | 2026-07-23 |
| **SDK** | .NET 10.0.302 (host 10.0.10, RID win-x64) |
| **Versión del producto** | `0.9.5-beta` — **sin cambios**, no se versionó el checkpoint |
| **Pruebas** | **606** (576 `Nexo.Core.Tests` + 30 `Nexo.Windows.Tests`), **0 fallidas**, 0 omitidas |
| **Warnings de compilación** | **0** |
| **Portable** | `artifacts\Kohana-0.9.5-beta-checkpoint-win-x64\` — self-contained win-x64, 489 archivos, 224,17 MB |
| **ZIP** | `artifacts\Kohana-0.9.5-beta-checkpoint-win-x64.zip` — 91.963.645 bytes (87,7 MB) |
| **SHA-256** | `6f4da7bf97a6a17f3d4b0fae550470f71b9bf6832d010165cd08e1d6693c61fb` |

Comando de publicación:

```powershell
dotnet publish .\src\Nexo.App\Nexo.App.csproj -c Release -r win-x64 --self-contained true -o artifacts\Kohana-0.9.5-beta-checkpoint-win-x64
```

**Smoke test sobre el portable publicado** (no sobre `bin\Release`):

| Paso | Resultado |
|---|---|
| `Kohana.exe` existe | ✅ 285.696 bytes |
| La aplicación abre | ✅ ventana «Kohana», vista inicial «Inicio» |
| Navegación | ✅ Hoy · Enfoque · Asistente |
| Comando local | ✅ *"cómo está mi PC"* → «CPU 31% · RAM 50% · GPU 3%. Mayor uso de memoria: zen · 1380 MB.» — resuelto localmente, sin LLM |
| Cierre correcto | ✅ cerrado por PID exacto; no se tocó ningún otro proceso |

`artifacts/`, `bin/` y `obj/` ya estaban cubiertos por `.gitignore` (líneas 2–4): **no se añadió
ninguna regla** y **ningún artefacto se versiona**.

---

### Checkpoint portable — Fase 1.2 (2026-07-23)

> ⚠️ **Esto es un checkpoint de desarrollo, NO una Release Candidate ni una versión estable.**
> Mismas salvedades que el checkpoint anterior: no cumple `ACCEPTANCE_CRITERIA.md` §1, no
> distribuir como 1.0 ni como RC. El smoke test manual **interactivo** (navegación por clics, voz,
> wake word) no se repitió en esta sesión — ver riesgo #13.

| Campo | Valor |
|---|---|
| **Commit** | `2195805` (`docs: record composition root migration`) |
| **Rama** | `release/kohana-1.0-rc` |
| **Fecha** | 2026-07-23 |
| **SDK** | .NET 10.0.302 (host 10.0.10, RID win-x64) |
| **Versión del producto** | `0.9.5-beta` — sin cambios, no se versionó el checkpoint |
| **Pruebas** | **615** (576 `Nexo.Core.Tests` + 39 `Nexo.Windows.Tests`), **0 fallidas**, 0 omitidas |
| **Warnings de compilación** | **0** |
| **Portable** | `artifacts\Kohana-0.9.5-beta-phase1.2-win-x64\` — self-contained win-x64, 491 archivos |
| **ZIP** | `artifacts\Kohana-0.9.5-beta-phase1.2-win-x64.zip` — 89.272.344 bytes (85,1 MB) |
| **SHA-256** | `29c32a43e1e50d4755c8d5a2ceba233becd2c22cfe7a0463354a9ba34533181f` |

Comando de publicación:

```powershell
dotnet publish src\Nexo.App\Nexo.App.csproj -c Release -r win-x64 --self-contained true -o artifacts\Kohana-0.9.5-beta-phase1.2-win-x64
```

**Smoke test sobre el portable publicado** (no sobre `bin\Release`; sin herramienta de
automatización de UI de escritorio disponible en esta sesión):

| Paso | Resultado |
|---|---|
| `Kohana.exe` existe | ✅ 285.696 bytes (idéntico al checkpoint anterior) |
| La aplicación abre | ✅ proceso alcanza `Responding=True` en <4 s, sin `WerFault.exe` |
| Cierre correcto | ✅ cerrado por PID exacto iniciado por este agente; ningún otro proceso tocado |
| Navegación por clics, voz, wake word, rutinas | ⚠️ no probado — ver riesgo #13 |

---

## TAREAS PENDIENTES

### Fase 0 — cierre (en Windows) ✅ 2026-07-23
- [x] `git checkout -b release/kohana-1.0-rc`
- [x] Confirmar SO, ruta, rama, `dotnet --info`, `git status`
- [x] `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`
- [x] Rellenar el bloque de baseline de arriba
- [x] Corregir cifras estáticas de `CURRENT_STATE_AUDIT.md` contra el código real
- [x] Commit: `docs: record measured Kohana 1.0 baseline`

### Fase 1 — extracción (7 pasos, en orden)
- [x] 1.1 Pruebas de caracterización de `MainWindow` ✅ 2026-07-23
- [x] 1.2 Composition root + DI **sin cambiar comportamiento** ✅ 2026-07-23
- [ ] 1.3 Extraer coordinador de voz — **1.3A parcial** ✅ 2026-07-23 (coordinador aislado
      + wiring en el composition root); **1.3B pendiente** (migrar `MainWindow`)
- [ ] 1.4 Extraer coordinador de navegación
- [ ] 1.5 Extraer tareas, enfoque y rutinas
- [ ] 1.6 Extraer IA y Vision
- [ ] 1.7 `MainWindow` como vista mínima (objetivo < 500 líneas)

### Fases 2–10
Ver `STABLE_RELEASE_PLAN.md`. No adelantar fases.

---

## PRUEBAS

| Fase | Total | Fallidas | Warnings | Nota |
|---|---|---|---|---|
| Baseline (2026-07-23) | **356** | **0** | **0** | Core 353 + Windows 3. Commit `144bb13`. Build Release en frío 2.52 s |
| Fase 1.1 (2026-07-23) | **520** | **0** | **0** | Core 494 + Windows 26. +164 pruebas de caracterización. Cero regresiones |
| Fase 1.1.1 (2026-07-23) | **606** | **0** | **0** | Core 576 + Windows 30. +86 pruebas. Correcciones D1, D2 y D4 |
| Fase 1.2 (2026-07-23) | **615** | **0** | **0** | Core 576 (sin cambios) + Windows 39. +9 pruebas de composition root e invariantes |
| Fase 1.3A (2026-07-23) | **638** | **0** | **0** | Core 576 (sin cambios) + Windows 62. +23 pruebas: `VoiceCoordinator` aislado (17) e invariantes de composition root/estructurales (6) |
| Fase 1.3B1 (2026-07-23) | **645** | **0** | **0** | Core 576 (sin cambios) + Windows 69. +7 pruebas estructurales de inyección y migración parcial |
| Fase 1.3B2A (2026-07-23) | **658** | **0** | **0** | Core 576 (sin cambios) + Windows 82. +8 pruebas de la API de transición + 5 invariantes de frontera |
| Fase 1.3B2 runtime (2026-07-23) | **663** | **0** | **0** | Core 576 (sin cambios) + Windows 87. 3 invariantes obsoletas sustituidas por 8 nuevas que verifican el runtime real |
| Fase 1.3B3 (2026-07-24) | **660** | **0** | **0** | Core 576 (sin cambios) + Windows 84. Ámbitos de exclusión, propiedad única y cierre. Windows repetida 5 veces sin intermitencia |
| Fase 1.3B3.1 (2026-07-24) | **666** | **0** | **0** | Core 576 (sin cambios) + Windows 90. +6 invariantes de la ruta de salida. Windows repetida 5 veces sin intermitencia |
| Fase 1.3C (2026-07-24) | **664** | **0** | **0** | Core 576 (sin cambios) + Windows 88. Consolidación: servicios obligatorios, docs sin fases, 2 pruebas obsoletas sustituidas + 1 invariante reforzada. Windows repetida 5 veces sin intermitencia. Rama `nightshift/phase1-finalization` |

---

## RIESGOS ACTIVOS

| # | Riesgo | Severidad | Mitigación |
|---|---|---|---|
| 1 | La extracción de `MainWindow` rompe conducta no cubierta por pruebas | **Alta** | Paso 1.1 (caracterización) **antes** de mover nada |
| 2 | El cambio de TFM rompe resolución de paquetes | Media | Aplazado a Fase 7, aislado a `Nexo.Windows`/`Nexo.App` |
| 3 | Ningún motor candidato gana medido al fallback | Media | Aceptable: Vosk/SAPI siguen. Documentar y seguir |
| 4 | Alcance de 1.0 no cabe responsablemente | Media | Entregar RC honesta + bloqueadores, nunca 1.0 falsa |
| 5 | Accesibilidad ausente descubierta tarde | Media | Es criterio de salida; Fase 9 dedicada |
| 6 | Presupuestos de latencia irreales sin baseline | Media | Marcados `PENDIENTE DE CALIBRAR`; no reportar como logrados |
| 7 | ~~Rutinas eclipsan órdenes de enfoque (D1)~~ | — | ✅ **Resuelto** en 1.1.1 (`667a873`), verificado en vivo |
| 8 | ~~`OpenApplication` permite ejecución arbitraria sin confirmación (D2)~~ | — | ✅ **Resuelto** en 1.1.1 (`4e3524d`), verificado en vivo |
| 9 | `JsonSettingsStore.Load` no normaliza en las rutas de recuperación (D3) | Media | **Abierto.** Congelado en prueba. No bloquea 1.2. Corregir junto al `.bak` de L7 |
| 10 | ~~`SingleInstanceCoordinator.Dispose` no es idempotente (D4)~~ | — | ✅ **Resuelto** en 1.1.1 (`787db71`) |
| 11 | Propiedad del mutex por hilo, no por proceso (D5) | Informativo | **Abierto.** No es un defecto; es una trampa para quien comparta el componente en 1.2. Congelado en prueba |
| 12 | ~~Sin `.gitattributes` con `core.autocrlf=true`~~ | — | ✅ **Resuelto** en 1.2 (`af13d7f`): `.gitattributes` mínimo, sin renormalización masiva (2 archivos de cambio real, `.gitattributes` nuevo) |
| 13 | Smoke test manual interactivo (clics, voz, wake word) no repetido en 1.2 | Media | Esta sesión no tuvo herramienta de automatización de UI de escritorio. Ver detalle en "Fase 1.2" arriba. **Recomendado antes de considerar el checkpoint apto para uso diario** |
| 14 | Discrepancia de líneas en `MainWindow.xaml.cs`: la auditoría de 1.1.1 documentaba 3.532, medido ahora en el checkpoint `82a36fb` (antes de tocar nada en 1.2) da **4.027** | Baja | Descubierto incidentalmente al medir para 1.2, no causado por esta fase. `CURRENT_STATE_AUDIT.md` se corrige con la cifra medida hoy; no se investigó la causa de la discrepancia anterior por estar fuera de alcance de 1.2 |

### Defectos descubiertos por la caracterización (1.1)

Se congelaron tal cual en 1.1, y **D1, D2 y D4 quedaron corregidos en la fase 1.1.1**.
D3 y D5 siguen abiertos y **no bloquean la fase 1.2**.

**D1 — Las rutinas se comen las órdenes de enfoque. (Alta, visible) — ✅ RESUELTO en 1.1.1**
`SpanishRoutineCommandParser` usa `^(?:ejecuta|inicia|activa|corre)\s+(?:la\s+)?(?:rutina\s+)?(?<name>.+)$`,
que captura *cualquier* frase que empiece por "inicia". Como el parser de rutinas corre
**primero** en `MainWindow.HandlePromptAsync`, y `MainWindow` **no reintenta** cuando
`FindBestMatch` no encuentra nada, decir *"Inicia un temporizador de 20 minutos"* responde
*"No encontré una rutina que coincida con..."* y **no arranca ningún temporizador**.
Afecta también a *"Inicia un descanso"* e *"Inicia un pomodoro"*.
`SpanishFocusCommandParserTests` no lo detecta porque prueba el parser **aislado**, sin la
precedencia real del shell. Es justamente el tipo de fallo que solo aparece al caracterizar la
composición.

**D2 — `OpenApplication` es ejecución arbitraria sin confirmación. (Alta, seguridad) — ✅ RESUELTO en 1.1.1**
`NexoAutomationActionExecutor.OpenApplication` reenvía `action.Arguments` al proceso, y
`AutomationPermissionPolicy` clasifica esa acción como `Reversible`, es decir **sin
confirmación**. Un paso de rutina con `Target="powershell.exe"` y `Arguments="-Command ..."`
ejecuta lo que sea sin el paso de aprobación que exige `SECURITY_MODEL` (escenario 22).
*Mitigación existente:* las rutinas las crea el propio usuario en la interfaz y esa creación es
la aprobación (`PRODUCT_VISION` §F). No es una vía de explotación remota. Pero el invariante
**no está aplicado técnicamente**, que es lo que `SECURITY_MODEL` §4 exige explícitamente.
En contraste, `OpenTerminal` **sí** es seguro: ignora `Arguments` y construye siempre su propia
línea de comandos. Esa asimetría queda congelada como invariante.

**D3 — `Load` no normaliza en las rutas de recuperación. (Media) — ABIERTO, no bloquea 1.2**
`JsonSettingsStore.Load` solo llama a `Normalize()` en la ruta de éxito. Con archivo ausente o
corrupto devuelve `new ShellPreferences()` con `SchemaVersion = 0`. Consecuencia: el siguiente
`Save` reejecuta **todas** las migraciones desde 0, incluida la de v10
(`HasCompletedOnboarding = false`), así que un valor asignado justo antes de guardar se pierde
en ese ciclo. Tras un archivo corrupto los valores por defecto son aceptables —los datos ya
eran ilegibles— pero el shell no puede marcar el onboarding como completado en ese arranque.
La degradación dura un solo ciclo.

**D4 — `SingleInstanceCoordinator.Dispose` no es idempotente. (Baja) — ✅ RESUELTO en 1.1.1**
Un segundo `Dispose` lanza `ObjectDisposedException` (`_cancellation.Cancel()` sobre un CTS ya
liberado). Hoy no se manifiesta porque `App.OnExit` pone el campo a `null`, pero un contenedor
de DI que libere de forma genérica —justo lo que llega en 1.2— sí puede sacarlo a la luz.

**D5 — La propiedad del mutex es por hilo, no por proceso. (Informativo) — ABIERTO, no bloquea 1.2**
Dos `SingleInstanceCoordinator` en el **mismo hilo** se consideran ambos primarios, porque el
segundo `WaitOne` es una adquisición recursiva del mismo dueño. En producción no ocurre —cada
instancia es un proceso distinto— pero quien extraiga o comparta este componente en 1.2 debe
saberlo. Queda congelado en `MutexOwnershipIsPerThread_NotPerProcess`.

---

## PLAN DE 1.2 EJECUTADO (histórico — ver "Fase 1.2" arriba para el resultado real)

> Esta sección documenta el plan **tal como se escribió antes de ejecutarlo**. El resultado real,
> con una desviación documentada (dónde vive el paquete DI y la clase de composición), está en la
> sección "Fase 1.2 — composition root + DI (2026-07-23)" más arriba. Se conserva sin editar por
> trazabilidad.

Fases 1.1 y 1.1.1 cerradas y **verdes**: 606 pruebas, 0 fallidas, 0 warnings, smoke test manual
completo sobre la aplicación real. La red de seguridad existe y los defectos que la propia red
destapó (D1, D2, D4) están corregidos.

El siguiente paso es **1.2 — composition root + inyección de dependencias, sin cambiar
comportamiento**. Plan exacto:

1. Añadir `Microsoft.Extensions.DependencyInjection` (MIT) **solo** a `Nexo.App`.
   `Nexo.Core` sigue con **cero** `PackageReference` — invariante verificada por prueba.
2. Crear `Nexo.App/Composition/KohanaServiceCollection.cs` que registre exactamente los
   servicios que hoy `MainWindow` instancia en la declaración de sus campos, con **el mismo
   tipo concreto y el mismo tiempo de vida efectivo** (singleton por ventana).
3. Construir el contenedor en `App.OnStartup`, **antes** de crear `MainWindow`, y pasarlo por
   constructor. `MainWindow` mantiene su firma actual (`startHidden`, `managedOllamaSupervisor`)
   más el proveedor, para no tocar el orden de arranque.
4. Sustituir los **6 servicios de interfaz** primero (`IAiChatService`, `IAudioMixerService`,
   `IVoiceInputService`, `IVoiceOutputService`, `IWakeWordService`, `IScreenCaptureService`),
   que son los que bloquean el Adaptive Engine Registry. Los 25 campos restantes instanciados
   con `new` se migran después, en el mismo paso pero en commits separados si crece.
5. **No** introducir interfaces nuevas, **no** renombrar tipos, **no** cambiar el orden de
   suscripción de eventos del constructor: ese orden es conducta observable y no está cubierto
   por pruebas.
6. ~~Resolver antes D4~~ ✅ ya resuelto en la fase 1.1.1.
7. Criterio de salida: build en Release con **0 warnings**, **606 pruebas verdes**, arranque
   real de la aplicación verificado, y `MainWindow` sin ningún `= new` de servicio en la
   declaración de campos.

Riesgo principal de 1.2: el orden de construcción. Hoy los campos se inicializan en orden de
declaración y luego el constructor cablea eventos. Un contenedor cambia *cuándo* se construye
cada servicio. Mitigación: registrar todo como singleton y resolver de forma **ansiosa** en el
mismo orden que hoy, antes de cablear eventos.

---

## SIGUIENTE PASO EXACTO

**Fase 1.3B3 completada: sincronización única, propiedad única y cierre del subsistema de voz.**
Ver la sección "Fase 1.3B3" más abajo para el resultado exacto. 660 pruebas, 0 fallidas, 0
warnings, suite de Windows repetida 5 veces sin intermitencia. Los dos únicos semáforos del
subsistema viven en `VoiceCoordinator` y se sostienen por ámbitos; la propiedad y el `Dispose` de
Whisper, TTS y Vosk viven en `KohanaCompositionRoot`; `MainWindow` no tiene semáforos de voz ni
campos de servicio. **Con esto se cierra por completo el paso 1.3 (extracción del coordinador de
voz) del ADR 0001**, a falta del smoke test manual.

El siguiente paso es **1.4 — la siguiente extracción según `STABLE_RELEASE_PLAN.md`** (coordinador
de navegación). Antes de empezar, quien retome debe:

1. Repetir el smoke test manual interactivo (riesgo #13, heredado de 1.2) sobre el portable de
   1.3B3 antes de dar la Fase 1.3 por cerrada del todo: clic para iniciar/detener el Mic, ambas
   frases de wake word, orden de corrido, TTS, cambio y persistencia de micrófono, y cierre /
   reapertura / instancia única. Es el único verificador del comportamiento visible.
2. Releer las secciones "Fase 1.3A", "Fase 1.3B1", "Fase 1.3B2 runtime" y "Fase 1.3B3" para
   conocer la API real de `VoiceCoordinator` (ámbitos) y el modelo de propiedad definitivo.
3. No reabrir la sincronización de voz: el diseño de ámbitos es el final de la fase 1.

**No iniciar 1.4 sin que 1.3 (A, A.1, B1, B2A, B2 runtime y B3) esté verde y con el smoke test
manual de 1.3B3 aprobado.**

---

### Fase 1.2 — inventario previo de los seis servicios (2026-07-23)

Registro exigido por el plan **antes** de tocar `MainWindow.xaml.cs`. Estado tal como existía en
el checkpoint `82a36fb`.

| # | Interfaz | Implementación actual | Lifetime | Orden de construcción (campo) | Eventos asociados | `IDisposable` |
|---|---|---|---|---|---|---|
| 1 | `IAiChatService` | `AiChatRouterService` | Singleton por ventana (una instancia durante toda la vida de `MainWindow`) | 8º inicializador de campo (tras `_settingsStore`, `_startupService`, `_conversationStore`, `_commandParser`, `_taskCommandParser`, `_focusCommandParser`, `_routineCommandParser`) | Ninguno suscrito por `MainWindow` | La interfaz **no** extiende `IDisposable`; la implementación concreta sí (`AiChatRouterService : IAiChatService, IDisposable`). `Window_Closed` comprueba `is IDisposable` antes de liberar |
| 2 | `IAudioMixerService` | `WindowsAudioMixerService` | Singleton por ventana | 9º inicializador de campo | Ninguno | Ni la interfaz ni la implementación son `IDisposable`. No se libera en `Window_Closed` |
| 3 | `IVoiceInputService` | `WhisperVoiceInputService` | Singleton por ventana | 10º inicializador de campo | Ninguno suscrito directamente (se consulta por métodos: `GetInputDevices`, `IsReady`, `StartListeningAsync`, etc.) | Interfaz extiende `IDisposable`. Se libera explícitamente en `Window_Closed` (línea ~808) |
| 4 | `IVoiceOutputService` | `WindowsTextToSpeechService` | Singleton por ventana | 11º inicializador de campo | Ninguno | Interfaz extiende `IDisposable`. Se libera explícitamente en `Window_Closed` (línea ~807) |
| 5 | `IWakeWordService` | `VoskWakeWordService` | Singleton por ventana | 12º inicializador de campo | `WakeWordDetected` y `RecognitionObserved`, suscritos en el constructor (líneas 191-192) y **desuscritos antes** de `Dispose()` en `Window_Closed` (líneas 800-802) | Interfaz extiende `IDisposable`. Se libera explícitamente en `Window_Closed` |
| 6 | `IScreenCaptureService` | `WindowsScreenCaptureService` | Singleton por ventana | 13º inicializador de campo (último de los seis) | Ninguno | Ni la interfaz ni la implementación son `IDisposable`. No se libera en `Window_Closed` |

**Consecuencia para el diseño de la fase:** de los seis, solo tres (`IVoiceInputService`,
`IVoiceOutputService`, `IWakeWordService`) tienen liberación explícita hoy en `Window_Closed`, y
`MainWindow` **ya** conoce y aplica ese contrato exacto (incluida la comprobación condicional de
`IAiChatService`). Cualquier contenedor de DI que también intente liberar estas instancias al
cerrarse causaría una doble liberación no probada. Decisión tomada para evitarlo: el contenedor
registra las **seis instancias ya construidas** (`services.AddSingleton<TInterface>(instancia)`),
no sus *tipos*. Un `ServiceProvider` de `Microsoft.Extensions.DependencyInjection` **no** libera
instancias que no creó él mismo — solo libera lo que construye a partir de un tipo o fábrica — así
que `Window_Closed` sigue siendo la única ruta que llama a `Dispose()` sobre estos seis servicios,
exactamente como hoy. El contenedor solo libera su propio `ServiceProvider`, no los servicios.

### Fase 1.2 — composition root + DI (2026-07-23) ✅

Objetivo cumplido **sin cambiar comportamiento observable**: `MainWindow` ya no fija ningún motor
con `new` en la declaración de campos para los seis servicios de interfaz.

**Paquete añadido:** `Microsoft.Extensions.DependencyInjection` **10.0.10**, licencia **MIT**
(`https://licenses.nuget.org/MIT`), autor Microsoft, verificada contra el `.nuspec` del paquete
restaurado. Misma familia de versión que el resto de paquetes `Microsoft.*` ya presentes en
`Nexo.Windows` (`System.Diagnostics.PerformanceCounter`, `System.Drawing.Common`, `System.Speech`,
todos en `10.0.10`).

**Dónde vive el paquete — desviación documentada del plan original:** el plan de
`SIGUIENTE PASO EXACTO` (arriba) proponía `Nexo.App/Composition/KohanaServiceCollection.cs`. Se
implementó en cambio como `Nexo.Windows/Composition/KohanaCompositionRoot.cs`, y el paquete se
referenció **solo en `Nexo.Windows`, no en `Nexo.App`**. Motivo: la fase 1.1 ya estableció (ver
más arriba, "Seams extraídos") que referenciar `Nexo.App` desde un proyecto de pruebas arrastra
`UseWPF` y provoca `MSB3277`, subiendo los warnings de 0 a 1 — inaceptable contra el baseline de 0
warnings. Igual que se hizo con `SingleInstanceCoordinator` en 1.1, la clase que hace el trabajo
real de composición vive en `Nexo.Windows` (testeable, sin `UseWPF`), y `Nexo.App/App.xaml.cs`
sigue siendo el **único punto de la aplicación** que instancia `KohanaCompositionRoot` — sigue
existiendo un único composition root y un único `ServiceProvider` para toda la vida del proceso;
solo cambió en qué proyecto vive la *clase* que lo implementa. `Nexo.App` no necesitó ninguna
referencia directa al paquete DI: solo consume las propiedades tipadas de
`KohanaCompositionRoot`, nunca `IServiceProvider`.

**Diseño de liberación (evita doble `Dispose`):** el contenedor registra las seis instancias ya
construidas (`AddSingleton<TInterface>(instancia)`), no los tipos. `Window_Closed` en
`MainWindow.xaml.cs` sigue siendo, sin ningún cambio, la única ruta que llama a `Dispose()` sobre
`IWakeWordService`, `IVoiceInputService`, `IVoiceOutputService` y condicionalmente `IAiChatService`.
`KohanaCompositionRoot.Dispose()` (llamado desde `App.OnExit`) solo libera el `ServiceProvider` en
sí — que, al no haber creado esas instancias, no vuelve a llamar `Dispose()` sobre ellas. Verificado
con la prueba `Dispose_DoesNotDisposeTheUnderlyingServiceInstances`.

**Archivos creados:**
- `src/Nexo.Windows/Composition/KohanaCompositionRoot.cs` — construye los seis servicios en el
  mismo orden relativo que los antiguos inicializadores de campo, los registra como instancia
  singleton y los resuelve de forma ansiosa desde el `ServiceProvider`.
- `tests/Nexo.Windows.Tests/Composition/KohanaCompositionRootTests.cs` — 6 pruebas.
- `tests/Nexo.Windows.Tests/Composition/CompositionInvariantTests.cs` — 3 pruebas.

**Archivos modificados:**
- `src/Nexo.Windows/Nexo.Windows.csproj` — añade el `PackageReference`.
- `src/Nexo.App/App.xaml.cs` — crea `_compositionRoot` en `OnStartup` (antes de `MainWindow`),
  pasa sus seis propiedades al constructor de `MainWindow`, y lo libera en `OnExit`.
- `src/Nexo.App/MainWindow.xaml.cs` — los seis campos de servicio pierden su inicializador `new`
  y se reciben por constructor (con valor por defecto `?? new Concreto()` solo para permitir la
  construcción sin argumentos que WPF usa en el diseñador; `App.OnStartup` siempre provee los seis
  explícitamente). Ningún otro campo, orden de suscripción de eventos ni firma pública cambió.

**Orden de construcción — verificado, no solo asumido:** los seis servicios ahora se construyen
en el cuerpo del constructor de `MainWindow` (antes de `_startHidden = startHidden` y de cualquier
uso, incluido `_routineRunner`/`_audioView` que dependen de `_audioMixerService`), lo que sigue
cumpliendo el invariante crítico: **construidos antes de cablear eventos**. Su posición relativa
frente a los demás inicializadores de campo (`_settingsStore`, `_homeView`, semáforos, etc.) sí
cambió — pasaron de intercalarse entre inicializadores de campo a construirse justo al principio
del cuerpo del constructor — pero se verificó que ninguno de los seis constructores concretos
(`AiChatRouterService`, `WindowsAudioMixerService`, `WhisperVoiceInputService`,
`WindowsTextToSpeechService`, `VoskWakeWordService`, `WindowsScreenCaptureService`) toca estado
compartido, estático o cualquiera de los demás campos: todos son `new()` independientes que solo
leen rutas de modelos o el dispositivo de audio por defecto. No hay acoplamiento observable que ese
cambio de orden pueda romper.

**Pruebas añadidas (9 nuevas en `Nexo.Windows.Tests`):**
- Resolución de los seis servicios con el tipo concreto esperado (motores actuales preservados).
- Las propiedades del composition root coinciden exactamente con lo que resuelve el `Provider`.
- Identidad singleton: resolver dos veces devuelve la misma instancia.
- Las seis instancias son mutuamente distintas (sin aliasing accidental).
- `Dispose()` no lanza y es idempotente.
- `Dispose()` del contenedor no libera las instancias subyacentes (evita doble `Dispose`).
- Invariante: `Nexo.Core.csproj` sigue sin ningún `PackageReference`.
- Invariante: `MainWindow.xaml.cs` no contiene la cadena `IServiceProvider`.
- Invariante: `MainWindow.xaml.cs` ya no construye los seis servicios con `= new ...()`.

Las pruebas de caracterización de 1.1/1.1.1 (`PromptDispatchCharacterizationTests`,
`LocalActionPermissionCharacterizationTests`, etc.) no se tocaron y siguen verdes sin
modificación: cubren que los comandos locales siguen resolviéndose sin LLM y que la precedencia de
parsers no cambió, algo ajeno a esta fase pero que confirma que el cambio de composición no la
afectó.

**Resultado de build y pruebas (Release, en frío tras cerrar una instancia de `Kohana.exe` que
bloqueaba `Nexo.Core.dll`/`Nexo.Windows.dll` — mismo tipo de incidencia ya documentada en el
baseline, PID distinto, cerrado con autorización explícita del propietario):**

```
dotnet build Nexo.slnx -c Release    → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll →  39 superadas, 0 con error, 0 omitidas   (30 + 9 nuevas)
Total: 615 pruebas, 0 fallidas, 0 warnings.
```

**Smoke test (portable `bin\Release`, sin herramienta de automatización de UI de escritorio
disponible en esta sesión — limitado a lo verificable por proceso y logs):**

| Paso | Resultado |
|---|---|
| Arranque | ✅ `Kohana.exe` alcanza estado `Responding=True` en <4 s, sin `WerFault.exe` |
| Segunda instancia | ✅ el segundo proceso termina solo con código 0; el original sigue vivo — `SingleInstanceCoordinator` intacto |
| Cierre por PID exacto | ✅ `Stop-Process` sobre el PID iniciado por este agente; sin procesos huérfanos |
| Reapertura | ✅ nuevo proceso alcanza `Responding=True` normalmente |
| Runtime de IA administrado | ✅ `Logs\ollama-runtime.log` registra `ManagedRunning` en ambos arranques, sin errores — confirma que `ManagedOllamaSupervisor` y el `IAiChatService` resuelto por el contenedor siguen intercambiando datos correctamente |
| Navegación, comando local, temporizador, wake word, rutinas | ⚠️ **NO probado en esta sesión** — requiere control de UI de escritorio (clics, teclado, micrófono) que esta sesión no tiene disponible. La fase 1.1 ya caracterizó y congeló este comportamiento en pruebas automatizadas (`PromptDispatchCharacterizationTests`, `VoiceRuntimeCharacterizationTests`), que siguen verdes sin modificación, pero eso **no sustituye** una verificación manual interactiva |

**Riesgo residual explícito:** el smoke test manual interactivo completo (navegación por clics,
orden de voz, wake word en vivo) que sí se hizo en la fase 1.1.1 **no se repitió** en 1.2 por falta
de herramienta de automatización de UI en esta sesión. Mitigación: las 615 pruebas automatizadas
cubren la lógica pura y la composición; el arranque real confirma que el grafo de objetos se
construye sin excepciones. Recomendado repetir el smoke test manual completo de 1.1.1 antes de
considerar este checkpoint apto para uso diario, no solo para verificación técnica.

---

### Fase 1.3A — `VoiceCoordinator` aislado (2026-07-23)

Subfase aprobada tras revisar `artifacts\Kohana-Fase-1.3-Auditoria.md`, con una corrección
obligatoria sobre propiedad de recursos respecto a lo que proponía esa auditoría: en 1.3A,
`VoiceCoordinator` **no es dueño** del ciclo de vida de los tres servicios de voz. Objetivo
cumplido: el coordinador existe, está probado de forma aislada y está conectado al composition
root, pero **`MainWindow.xaml.cs` y `App.xaml.cs` no se tocaron**.

**Corrección de propiedad aplicada:** `VoiceCoordinator.Dispose()` libera únicamente los dos
`SemaphoreSlim` que el propio coordinador crea (`_voiceGate`, `_wakeWordGate`). No llama
`Dispose()` sobre `IVoiceInputService`, `IVoiceOutputService` ni `IWakeWordService` en ningún
punto de su código — verificado tanto por prueba de comportamiento
(`Dispose_DoesNotDisposeTheUnderlyingServiceInstances` en `KohanaCompositionRootTests` y
`Dispose_DoesNotDisposeTheUnderlyingServices` en `VoiceCoordinatorTests`) como por prueba
estructural sobre el código fuente (`VoiceCoordinator_DoesNotDisposeTheThreeInjectedServices`).
`MainWindow.Window_Closed` sigue siendo, sin ningún cambio, la única ruta que libera esos tres
servicios, en el mismo orden que fijó la fase 1.2 — verificado por
`MainWindow_StillOwnsTheThreeVoiceServicesAndTheirDisposalOrder`, que localiza el bloque de
liberación en el código fuente y confirma el orden: desuscribir `WakeWordDetected` → desuscribir
`RecognitionObserved` → `_wakeWordService.Dispose()` → `_aiChatService` condicional →
`_voiceOutputService.Dispose()` → `_voiceInputService.Dispose()`.

**API real de `VoiceCoordinator`** (`src/Nexo.Windows/Voice/VoiceCoordinator.cs`):
- Constructor: `(IVoiceInputService, IVoiceOutputService, IWakeWordService)` — sin valores por
  defecto; `null` lanza `ArgumentNullException` en vez de construir un motor de reemplazo.
- Eventos `WakeWordDetected` y `RecognitionObserved`: **accessors de paso directo**
  (`add => _wakeWordService.WakeWordDetected += value;`), no una suscripción interna propia. No
  hay nada que desuscribir en `Dispose()` para estos dos eventos.
- Solo lectura: `IsVoiceInputReady`, `IsVoiceInputListening`, `IsWakeWordReady`, `IsWakeWordListening`.
- Paso a través de configuración (sin conocer preferencias): `InputDeviceNumber` (aplica a los dos
  servicios), `WakeWordSensitivity`, `WakeWordCustomAliases`.
- `GetInputDevices()`, `PrepareVoiceInputAsync`, `PrepareWakeWordAsync`.
- `StartPushToTalkAsync`, `StopPushToTalkAsync`, `CancelPushToTalkAsync`.
- `StartWakeWordAsync(phrase, ct)`, `StopWakeWordAsync()`, `PauseWakeWordAsync()` (alias semántico
  de `StopWakeWordAsync`: no existe una operación distinta de "pausar" en los servicios
  subyacentes).
- `ListenAfterWakeWordAsync(maximumDuration, trailingSilence, preRoll, postWake, ct)`.
- `Speak(text)`, `StopSpeaking()`.

**Decisión deliberada — no auto-preparación:** `StartPushToTalkAsync` y
`ListenAfterWakeWordAsync` **no** llaman `PrepareVoiceInputAsync` internamente antes de escuchar,
a diferencia de las ramas equivalentes en `MainWindow` hoy. Se verificó en
`WindowsVoiceInputService.StartListeningAsync` (línea 226) que el propio servicio **ya** se
autoprepara si `!IsReady`; la comprobación manual en `MainWindow` solo existe para mostrar texto de
progreso en la UI antes de la llamada. El coordinador expone `PrepareVoiceInputAsync` como
operación independiente para que quien lo use (1.3B) decida si quiere ese texto de progreso, sin
duplicar lógica que el servicio ya garantiza.

**Estrategia de candados:** `_voiceGate` se adquiere **siempre antes** que `_wakeWordGate` cuando
una operación necesita ambos (`StartPushToTalkAsync` y `ListenAfterWakeWordAsync` adquieren
`_voiceGate` y, dentro de su bloque, `StopWakeWordCoreAsync` adquiere `_wakeWordGate`). Las
operaciones que solo tocan wake word (`StartWakeWordAsync`, `StopWakeWordAsync`,
`PauseWakeWordAsync`) **nunca** tocan `_voiceGate`, así que no existe ninguna ruta que adquiera
`_wakeWordGate` primero y `_voiceGate` después — se eliminó estructuralmente la posibilidad de
interbloqueo por orden invertido. Cada `WaitAsync()` está seguido de un `try/finally` que libera
exactamente el candado que adquirió esa misma llamada; ningún camino libera un semáforo que no
adquirió. Verificado con `ConcurrentStartWakeWordAndPushToTalk_DoNotDeadlock` (con límite de
tiempo del propio `[Fact(Timeout = 5000)]`, sin `Task.Delay`) y
`TwoSimultaneousPushToTalkCalls_NeverOverlapInTheUnderlyingService` (contador de concurrencia
máxima observada, sin sleeps).

**Archivos creados:**
- `src/Nexo.Windows/Voice/VoiceCoordinator.cs`.
- `tests/Nexo.Windows.Tests/Voice/VoiceCoordinatorFakes.cs` — dobles de prueba controlables
  (`FakeVoiceInputService`, `FakeVoiceOutputService`, `FakeWakeWordService`) con ganchos
  (`BeforeStartListeningReturns`, `BeforeStopListeningReturns`) para forzar suspensión real en
  pruebas de concurrencia/cancelación sin `Task.Delay`, y un `VoiceCallLog` compartido para
  afirmar orden relativo de llamadas entre los tres dobles.
- `tests/Nexo.Windows.Tests/Voice/VoiceCoordinatorTests.cs` — 17 pruebas.

**Archivos modificados:**
- `src/Nexo.Windows/Composition/KohanaCompositionRoot.cs` — construye **una sola** instancia de
  `VoiceCoordinator` envolviendo exactamente las mismas tres instancias de voz que ya construía
  (no un cuarto motor), la registra como instancia singleton en el mismo `ServiceProvider`, y la
  expone como propiedad `VoiceCoordinator`. `Dispose()` del composition root ahora también libera
  `VoiceCoordinator` (sus dos semáforos propios) antes de liberar el `Provider` — no libera los
  seis servicios de voz/IA/etc., que siguen sin ser de su propiedad, exactamente como en 1.2.
- `tests/Nexo.Windows.Tests/Composition/KohanaCompositionRootTests.cs` — +4 pruebas: instancia
  única del coordinador, identidad de sus tres servicios verificada por comportamiento (no hay
  forma de exponer los campos internos sin romper la superficie mínima a propósito), ausencia de
  un cuarto conjunto de motores registrados, y que `Dispose()` del root libera los recursos
  propios del coordinador sin tocar los tres servicios de voz.
- `tests/Nexo.Windows.Tests/Composition/CompositionInvariantTests.cs` — +4 pruebas: el código
  fuente de `VoiceCoordinator` no menciona WPF/`Dispatcher`/la vista principal/preferencias/
  decisión del gobernador de recursos/`IServiceProvider`; no llama `Dispose()` sobre los tres
  servicios; y `MainWindow` conserva tanto los tres parámetros de constructor de voz como el
  orden exacto de liberación en `Window_Closed`.

**`MainWindow.xaml.cs` y `App.xaml.cs`: sin cambios en esta subfase.** Confirmado por
`git diff --stat`, que solo lista los cinco archivos de arriba.

**Resultado de build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release    → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas   (sin cambios)
  Nexo.Windows.Tests.dll →  62 superadas, 0 con error, 0 omitidas   (39 + 23 nuevas)
Total: 638 pruebas, 0 fallidas, 0 warnings.
```

Dos correcciones de prueba durante la implementación (documentadas por disciplina, no ocultadas):
`Assert.ThrowsAsync<OperationCanceledException>` no acepta la subclase `TaskCanceledException`
que realmente lanza `SemaphoreSlim.WaitAsync` cancelado — se cambió a `ThrowsAnyAsync`; y
`VoskWakeWordService.CustomAliases` devuelve una copia defensiva en cada lectura, así que la
prueba de identidad de aliases compara contenido (`Assert.Equal`) en vez de referencia
(`Assert.Same`).

**No se generó portable en esta subfase**, según instrucción explícita.

**Riesgos pendientes para la migración 1.3B:** ver la sección "Fase 1.3A" de este mismo bloque —
en particular, la decisión aún no tomada sobre quién libera los tres servicios de voz tras 1.3B
(punto 3 de "Siguiente paso exacto"), y el smoke test manual interactivo, todavía sin repetir
desde la fase 1.1.1.

---

### Fase 1.3A.1 evaluada y revertida antes de integración (2026-07-23)

La revisión externa del informe de 1.3A.1 confirmó, con evidencia sobre el código real de
`MainWindow.xaml.cs`, que **la premisa de "sesión persistente" no coincidía con el comportamiento
de `MainWindow`**: `AssistantView_VoiceInputStarted` adquiere `_voiceGate`, pausa wake word, inicia
la escucha y **libera `_voiceGate` al terminar ese método**; `AssistantView_VoiceInputStopped`
vuelve a adquirirlo por separado, detiene la escucha, procesa el resultado, reanuda wake word y lo
libera. No hay ningún mecanismo que retenga el candado entre esos dos eventos. La regla de la Fase
1.3 es extraer la coordinación **sin cambiar comportamiento observable ni semántica existente**, y
la sesión persistente introducida en 1.3A.1 era una modificación deliberada de concurrencia, no
una preservación de lo existente — la misma evidencia ya se había presentado y la desviación se
había aprobado explícitamente al implementarla. Tras una segunda revisión externa, se decidió
revertir en vez de mantenerla.

**El cambio revertido nunca llegó a ser consumido por `MainWindow`**: durante toda su vida (desde
`147871e` hasta el revert), `MainWindow.xaml.cs` y `App.xaml.cs` no cambiaron ni una vez —
`VoiceCoordinator` seguía existiendo de forma aislada, sin que ninguna vista lo invocara. Por esa
misma razón, **el cambio no produjo ninguna regresión visible ni funcional**: no había ningún
consumidor real en producción cuya conducta pudiera haberse alterado; el único efecto era interno
a `VoiceCoordinator` y a sus propias pruebas aisladas.

**Commits revertidos:**
| Hash | Mensaje |
|---|---|
| `147871e` | fix: preserve push-to-talk session serialization |
| `c11feed` | docs: record push-to-talk session correction |

**Commits de revert** (mediante `git revert --no-edit`, sin reset/rebase/force-push, historial
preservado):
| Hash | Mensaje |
|---|---|
| `d44523c` | Revert "docs: record push-to-talk session correction" |
| `cdcd65e` | Revert "fix: preserve push-to-talk session serialization" |

Tras ambos reverts, `git diff 2b42695..HEAD` (donde `2b42695` es el último commit de la Fase 1.3A
original) **no muestra ninguna diferencia**: el árbol de trabajo es idéntico, byte a byte, al
estado exacto en que quedó 1.3A. `VoiceCoordinator` no tiene `PushToTalkSession` ni
`_activeSession`; `StartPushToTalkAsync`, `StopPushToTalkAsync` y `CancelPushToTalkAsync` vuelven a
adquirir y liberar `_voiceGate` dentro de cada llamada individual, sin retenerlo entre eventos.

**La Fase 1.3A original permanece aceptada tal como se cerró**: composition root con
`VoiceCoordinator` construido a partir de los mismos tres servicios, sin liberarlos, con exclusión
por operación dentro de cada método del coordinador. Nada de eso se tocó por este revert.

**Para 1.3B:** debe preservar la exclusión **por operación** que existe hoy —tanto en
`VoiceCoordinator` (candado adquirido y liberado dentro de cada llamada) como en `MainWindow`
(`_voiceGate` adquirido y liberado por separado en `AssistantView_VoiceInputStarted` y en
`AssistantView_VoiceInputStopped`)— y no introducir semántica de sesión persistente entre eventos
independientes salvo que se apruebe explícitamente como cambio de comportamiento deliberado, con
el mismo nivel de evidencia y aprobación que exigió esta corrección.

---

### Fase 1.3B1 — VoiceCoordinator inyectado; configuración y preparación migradas (2026-07-23)

Primer paso de consumo real de `VoiceCoordinator` por `MainWindow`. Antes de tocar código se leyó
`VoiceCoordinator.cs`, `KohanaCompositionRoot.cs`, `App.xaml.cs`, el constructor y campos de
`MainWindow.xaml.cs`, y los cuerpos exactos de `ConfigureVoiceInputDevices`,
`ChangeVoiceInputDeviceAsync`, `PrepareVoiceAsync` e `InitializeVoiceFeaturesAsync`, para registrar
su comportamiento antes de sustituir ninguna llamada.

**Cambio de constructor:** `MainWindow` gana un noveno parámetro, al final de la firma para
minimizar el cambio: `VoiceCoordinator? voiceCoordinator = null`. Los ocho parámetros existentes
no cambian de orden. Si no se provee (solo ocurre en construcción directa fuera de
`App.OnStartup`, p. ej. el diseñador de XAML), el valor por defecto envuelve los **mismos** tres
campos ya resueltos (`_voiceInputService`, `_voiceOutputService`, `_wakeWordService`) —
`voiceCoordinator ?? new VoiceCoordinator(_voiceInputService, _voiceOutputService,
_wakeWordService)` — nunca construye un motor nuevo ni un segundo `VoiceCoordinator` "real".
`App.OnStartup` entrega exactamente `_compositionRoot.VoiceCoordinator`, el mismo singleton que
expone el composition root desde la fase 1.3A. `MainWindow` no recibe `IServiceProvider` en
ningún punto. `MainWindow` conserva los tres campos directos `_voiceInputService`,
`_voiceOutputService`, `_wakeWordService` — todavía son necesarios para las partes no migradas.

**Métodos migrados (solo mecánica de dispositivo y preparación, comportamiento verificado
idéntico antes de sustituir):**

- `ConfigureVoiceInputDevices()` — `_voiceInputService.GetInputDevices()` → `_voiceCoordinator.GetInputDevices()`
  (paso directo, sin diferencia). Las dos asignaciones separadas
  `_voiceInputService.InputDeviceNumber = selectedDeviceNumber;` y
  `_wakeWordService.InputDeviceNumber = selectedDeviceNumber;` se sustituyen por una sola
  `_voiceCoordinator.InputDeviceNumber = selectedDeviceNumber;` — se confirmó leyendo
  `VoiceCoordinator.cs` que su setter hace exactamente esas dos asignaciones, en el mismo orden
  (entrada de voz primero, wake word después), antes de sustituir.
- `ChangeVoiceInputDeviceAsync(int deviceNumber)` — mismas dos sustituciones de dispositivo que
  arriba, más `_voiceInputService.IsReady` → `_voiceCoordinator.IsVoiceInputReady` (paso directo).
  **`await _voiceInputService.CancelAsync();` se deja sin migrar, deliberadamente**: el único
  método del coordinador que envuelve `CancelAsync` es `CancelPushToTalkAsync`, que además adquiere
  su propio `_voiceGate` interno — un efecto adicional que la llamada actual, directa y sin
  candado, no tiene en este punto. No es una equivalencia exacta (regla de "PRESERVACIÓN
  OBLIGATORIA" punto 3: documentar la diferencia y no usarla ciegamente), así que se conservó la
  llamada directa. Queda para 1.3B2, cuando se migre push-to-talk como unidad completa.
- `PrepareVoiceAsync()` — `!_voiceInputService.IsReady` → `!_voiceCoordinator.IsVoiceInputReady`;
  `await _voiceInputService.PrepareAsync(progress, _lifetimeCancellation.Token)` →
  `await _voiceCoordinator.PrepareVoiceInputAsync(progress, _lifetimeCancellation.Token)` — paso
  directo con la misma firma, mismo `IProgress<VoicePreparationProgress>`, mismo
  `CancellationToken`, mismo `catch (OperationCanceledException)` sin cambios.
- `InitializeVoiceFeaturesAsync()` — **sin cambios**, tal como exigía el alcance: sigue llamando
  `await PrepareVoiceAsync();` textualmente igual; su comportamiento cambia solo como consecuencia
  indirecta de que `PrepareVoiceAsync` ahora usa el coordinador internamente.

**Deliberadamente no migrados en 1.3B1** (fuera de alcance, verificado que siguen intactos):
`AssistantView_VoiceInputStarted`, `AssistantView_VoiceInputStopped`, `HandleWakeWordDetectedAsync`,
`HandleVoiceRecognitionResultAsync`, `PauseWakeWordAsync`, `ResumeWakeWordIfEnabledAsync`,
`ApplyWakeWordPreferenceAsync`, `StartWakeWordTestAsync`, `SpeakVoiceResult`, el puente con
Resource Governor, las suscripciones a `WakeWordDetected`/`RecognitionObserved` en el constructor,
`Window_Closed` y el orden de `Dispose()`, y la propiedad de los tres servicios de voz (sigue
siendo de `MainWindow`, no del coordinador).

**Comportamiento anterior vs. resultante:** ninguno de los tres métodos migrados cambia su salida
observable. Las sustituciones son pasos directos verificados leyendo `VoiceCoordinator.cs` antes
de aplicarlas: `GetInputDevices()`, `IsVoiceInputReady` y `PrepareVoiceInputAsync(...)` delegan
exactamente en el mismo campo/llamada que sustituyen, sin lógica adicional. La única sustitución
que colapsa dos líneas en una (`InputDeviceNumber`) se verificó de la misma forma antes de usarla.

**Archivos modificados:**
- `src/Nexo.App/MainWindow.xaml.cs` — campo `_voiceCoordinator`, parámetro de constructor,
  `ConfigureVoiceInputDevices`, `ChangeVoiceInputDeviceAsync`, `PrepareVoiceAsync`.
- `src/Nexo.App/App.xaml.cs` — pasa `_compositionRoot.VoiceCoordinator` como noveno argumento.
- `tests/Nexo.Windows.Tests/Composition/CompositionInvariantTests.cs` — +7 pruebas estructurales.

`VoiceCoordinator.cs` y `KohanaCompositionRoot.cs` **no se modificaron** — solo se leyeron para
confirmar equivalencia antes de sustituir llamadas.

**Pruebas nuevas (7, todas basadas en símbolos/bloques de texto, no en números de línea):**
`App_PassesTheCompositionRootsVoiceCoordinatorToMainWindow`,
`MainWindow_ReceivesVoiceCoordinatorAsATypedConstructorDependency`,
`MainWindow_FallbackWrapsExistingServices_NeverBuildsAFourthEngineSet`,
`ConfigureVoiceInputDevices_RoutesEnumerationAndSelectionThroughTheCoordinator`,
`ChangeVoiceInputDeviceAsync_UsesCoordinatorForDeviceSelection_ButKeepsDirectCancelCall`,
`PrepareVoiceAsync_RoutesReadinessAndPreparationThroughTheCoordinator`,
`PushToTalkWakeWordAndTtsMethods_RemainUnmigrated`. Las pruebas ya existentes
`MainWindow_DoesNotReferenceIServiceProvider`, `MainWindow_StillOwnsTheThreeVoiceServicesAndTheirDisposalOrder`
y `NexoCoreProject_HasNoPackageReferences` no se tocaron y siguen verdes: cubren directamente los
criterios 3, 8 y 9 del prompt sin necesidad de duplicarlas. Un ajuste durante la implementación:
`ChangeVoiceInputDeviceAsync_UsesCoordinatorForDeviceSelection_ButKeepsDirectCancelCall` comprobaba
inicialmente `"_voiceCoordinator.GetInputDevices()"` como una sola cadena contigua, pero esa
llamada está encadenada en dos líneas (`_voiceCoordinator` y `.GetInputDevices()` en la siguiente,
por el formato de la sustitución de `_voiceInputService`); se corrigió a dos comprobaciones
separadas para no depender del salto de línea exacto, tal como exige el prompt.

**Resultado de build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release    → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas   (sin cambios)
  Nexo.Windows.Tests.dll →  69 superadas, 0 con error, 0 omitidas   (62 + 7 nuevas)
Total: 645 pruebas, 0 fallidas, 0 warnings.
```

**No se generó portable.** No se hizo smoke test manual: no lo exigía la validación de esta
subfase, y una instancia de `Kohana.exe` ya estaba corriendo con un PID que esta sesión no
inició, así que no se tocó (consistente con la regla de solo terminar procesos propios).

**Riesgos pendientes para 1.3B2:** `CancelAsync()` en `ChangeVoiceInputDeviceAsync` sigue sin
equivalencia exacta en el coordinador — 1.3B2 deberá decidir si migra push-to-talk como unidad
completa (momento en que ese `CancelAsync` directo probablemente desaparezca junto con el resto de
la lógica de push-to-talk) o si se le da a `VoiceCoordinator` un método de cancelación sin candado
para casos como este. El smoke test manual interactivo (riesgo #13, heredado desde 1.2) sigue sin
repetirse. La decisión de propiedad definitiva de los tres servicios de voz tras la migración
completa sigue sin tomarse.

---

### Fase 1.3B2A — operaciones de coordinación externa preparadas, sin consumidor (2026-07-23)

Primer paso de la Fase 1.3B2, según la **auditoría correctiva** de 1.3B2 (`artifacts\Kohana-Fase-1.3B2-Auditoria-Correctiva.md`), que refutó la recomendación de la auditoría original: migrar los cuatro métodos de push-to-talk a los métodos compuestos del coordinador dejaría **dos dominios de `_wakeWordGate`** sobre la misma instancia de `IWakeWordService` (el interno del coordinador vía `StopWakeWordCoreAsync`, y el de `MainWindow` vía `ApplyWakeWordPreferenceAsync`/`PauseWakeWordAsync`). La estrategia aprobada (Alternativa B) es: **`MainWindow` conserva sus tres semáforos como única fuente de exclusión** durante toda la fase 1.3B2, y el coordinador expone operaciones *sin candado* que la vista podrá consumir sin cambiar todavía el propietario de la sincronización.

**Los candados siguen en `MainWindow`.** No se tocó `_voiceGate`, `_wakeWordGate` ni `_resourceGovernorVoiceGate`, ni sus call sites. **No cambió ningún comportamiento real:** esta subfase solo añade API y pruebas; `MainWindow.xaml.cs` y `App.xaml.cs` no se modificaron (confirmado por `git diff`).

**API de transición añadida a `VoiceCoordinator`** (seis delegaciones transparentes, cuerpo de expresión, sin adquirir candados, sin pausar wake word, sin detener TTS, sin preparar, sin capturar excepciones, sin fire-and-forget):
- `StartVoiceInputUnderExternalCoordinationAsync(CancellationToken)` → `StartListeningAsync`
- `StopVoiceInputUnderExternalCoordinationAsync(CancellationToken)` → `StopListeningAsync`
- `CancelVoiceInputUnderExternalCoordinationAsync()` → `CancelAsync`
- `ListenForUtteranceUnderExternalCoordinationAsync(...)` → `ListenForUtteranceAsync` (mismo orden de argumentos)
- `StartWakeWordUnderExternalCoordinationAsync(WakeWordPhrase, CancellationToken)` → `StartListeningAsync`
- `StopWakeWordUnderExternalCoordinationAsync()` → `StopListeningAsync`

Cada una lleva XML-doc que advierte: no adquiere los candados internos, solo debe llamarla un orquestador que ya garantice la exclusión (hoy la vista principal), y **no debe combinarse en la misma sección crítica con los métodos compuestos** (`StartPushToTalkAsync` y equivalentes), que sí adquieren los candados internos. La API se declara **de transición, no definitiva**.

**Nomenclatura:** el prompt prohibió el sufijo `Core`. El helper privado `StopWakeWordCoreAsync` (que **sí** adquiere `_wakeWordGate`) se renombró a `StopWakeWordWithinCoordinatorGateAsync`, actualizando sus dos call sites internos, sin cambiar comportamiento, orden, candados ni visibilidad. Así el nombre refleja que se ejecuta dentro del dominio de candados del coordinador, en contraste con las operaciones `…UnderExternalCoordinationAsync`.

**Los métodos compuestos continúan sin consumidor.** `StartPushToTalkAsync`, `StopPushToTalkAsync`, `CancelPushToTalkAsync`, `ListenAfterWakeWordAsync`, `StartWakeWordAsync`, `StopWakeWordAsync`, `PauseWakeWordAsync` se conservan intactos, probados por su cobertura existente, pero ninguna ruta real los invoca. Un invariante estructural nuevo **falla si `MainWindow` los consume** — el límite queda aplicado técnicamente, no solo declarado.

**Pruebas nuevas (13):** 8 de la API de transición en `VoiceCoordinatorTests.cs` (delegación exacta una vez, preservación de todos los argumentos, ausencia de Stop/pausa/preparación, y no-serialización de dos entradas concurrentes — determinista con `TaskCompletionSource`, sin `Task.Delay`); 5 invariantes de frontera en `CompositionInvariantTests.cs` (MainWindow no consume las seis operaciones nuevas; no consume los siete compuestos con candado; conserva `_voiceGate`/`_wakeWordGate`; las operaciones nuevas no tocan los candados en su cuerpo; el helper usa el nombre desambiguado). Los fakes ganaron captura de argumentos. **Ninguna prueba existente se debilitó ni se eliminó.**

Estabilidad verificada: la suite de Windows se corrió **5 veces consecutivas** sin intermitencia (82/82 en cada corrida).

**Resultado de build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release    → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas   (sin cambios)
  Nexo.Windows.Tests.dll →  82 superadas, 0 con error, 0 omitidas   (69 + 13 nuevas)
Total: 658 pruebas, 0 fallidas, 0 warnings.
```

**1.3B2B migrará únicamente push-to-talk** (`AssistantView_VoiceInputStarted`/`Stopped`) para que sus llamadas directas a los servicios pasen por estas operaciones de coordinación externa, **manteniendo los candados actuales de `MainWindow`**. **La transferencia definitiva de candados al coordinador queda para una fase posterior** (1.3B2C y siguientes), momento en que también se resolverán el `CancelAsync` de cambio de dispositivo, las escrituras de `Sensitivity`/`CustomAliases` sin candado, el tercer dominio `_resourceGovernorVoiceGate`, la revisión de `Window_Closed` y el fallback no liberado del constructor — todos documentados en la auditoría correctiva, ninguno abordado aquí.

---

### Fase 1.3B2 runtime — operaciones principales de voz canalizadas por VoiceCoordinator (2026-07-23)

Continuación directa de 1.3B2A (donde se creó la API preparatoria de seis operaciones `…UnderExternalCoordinationAsync` sin consumidor real). Esta subfase hace que **`MainWindow` sí consuma** esa API: se migran las llamadas a los servicios dentro de `AssistantView_VoiceInputStarted`, `AssistantView_VoiceInputStopped`, `HandleWakeWordDetectedAsync`, `ChangeVoiceInputDeviceAsync`, `ApplyWakeWordPreferenceAsync`, `PauseWakeWordAsync`, y las rutas auxiliares (test de wake word, aliases, reinicio, diagnóstico, dashboard, TTS general, onboarding, Resource Governor, constructor).

**Rutas migradas** (todas verificadas equivalencia-exacta contra la lectura de `VoiceCoordinator.cs` antes de sustituir, siguiendo el mapeo literal del prompt): las trece sustituciones — `Stop()`→`StopSpeaking()`, `IsReady`(voz)→`IsVoiceInputReady`, `IsListening`(voz)→`IsVoiceInputListening`, `StartListeningAsync()`→`StartVoiceInputUnderExternalCoordinationAsync()`, `StopListeningAsync()`→`StopVoiceInputUnderExternalCoordinationAsync()`, `CancelAsync()`→`CancelVoiceInputUnderExternalCoordinationAsync()`, `ListenForUtteranceAsync(...)`→`ListenForUtteranceUnderExternalCoordinationAsync(...)`, `StopListeningAsync()`(wake word)→`StopWakeWordUnderExternalCoordinationAsync()`, `IsReady`(wake word)→`IsWakeWordReady`, `IsListening`(wake word)→`IsWakeWordListening`, `PrepareAsync(...)`(wake word)→`PrepareWakeWordAsync(...)`, `Sensitivity`→`WakeWordSensitivity`, `CustomAliases`→`WakeWordCustomAliases`, `StartListeningAsync(...)`(wake word)→`StartWakeWordUnderExternalCoordinationAsync(...)`, `SpeakShort`→`Speak`, `GetInputDevices()`→`GetInputDevices()` del coordinador.

**Candados que permanecen en `MainWindow`, sin cambios:** `_voiceGate`, `_wakeWordGate` y `_resourceGovernorVoiceGate` — los tres siguen siendo la única fuente de exclusión real. Ningún método adquiere un candado del coordinador: se usaron exclusivamente las seis operaciones de coordinación externa (sin candado interno) y las propiedades de paso directo ya existentes desde 1.3A/1.3B1.

**Métodos compuestos que continúan sin consumidor:** `StartPushToTalkAsync`, `StopPushToTalkAsync`, `CancelPushToTalkAsync`, `ListenAfterWakeWordAsync`, `StartWakeWordAsync`, `StopWakeWordAsync`, `PauseWakeWordAsync` (del coordinador) — probados en `VoiceCoordinatorTests.cs`, sin tocar, sin ningún consumidor real todavía. Verificado por invariante estructural que falla si `MainWindow` los llegara a usar.

**Preservación de comportamiento y orden:** en cada método migrado se conservó textualmente el orden de operaciones, los textos, las cápsulas, los estados visuales (`SetVoiceState`, `SetVoiceAvailability`, `SetWakeWordIndicator`), la bandera `listeningStarted`, la comprobación de disponibilidad antes y después de preparar, la comprobación de `IsListening`/`IsVoiceInputListening` antes de detener (evita el mensaje "no estaba escuchando" que el hueco de equivalencia D de la auditoría correctiva había señalado), la reanudación condicional en Start frente a la incondicional en Stop, `RememberForegroundWindow()`, las duraciones (20 s / 1.5 s), `PreRollAudio`/`PostWakeAudio`, `_lifetimeCancellation.Token`, el orden Stop→Prepare→Start en `ApplyWakeWordPreferenceAsync`, y las condiciones y llamadas de Resource Governor (`_resourceGovernorVoiceGate`, `PauseWakeWordAsync`, `ResumeWakeWordIfEnabledAsync`) intactas.

**Servicios directos conservados solo por propiedad/eventos/Dispose:** tras esta subfase, los únicos usos directos restantes de `_voiceInputService`, `_voiceOutputService` y `_wakeWordService` en `MainWindow.xaml.cs` son: la asignación/fallback del constructor (líneas 159-161, 169-170), la suscripción a `WakeWordDetected`/`RecognitionObserved` (líneas 218-219), la desuscripción de ambos eventos y `Dispose()`/orden de propiedad en `Window_Closed` (líneas 827-829, 834-835). Ninguna llamada operativa directa queda fuera de ese conjunto — verificado por invariante estructural que enumera explícitamente cada llamada prohibida.

**Pruebas:** 3 invariantes de 1.3B2A que asumían el estado "sin consumidor" se sustituyeron (no se eliminaron sin reemplazo) por 8 invariantes que verifican el runtime real, incluyendo un invariante que cuenta ocurrencias de cada operación de coordinación externa en todo el archivo y las compara contra su presencia dentro del método aprobado, para detectar un segundo uso fuera de alcance. Ninguna prueba de `VoiceCoordinatorTests.cs` se tocó.

**ZIP de smoke test:** `Kohana-0.9.5-beta-phase1.3B2-runtime-smoke-win-x64.zip` (ver detalle de tamaño y SHA-256 en el informe de esta subfase, `artifacts\Kohana-Fase-1.3B2-Runtime-Sprint-Informe.md`). No se afirma haber hecho el smoke test manual interactivo.

**Resultado de build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas   (sin cambios)
  Nexo.Windows.Tests.dll →  87 superadas, 0 con error, 0 omitidas   (82 + 8 nuevas − 3 sustituidas)
Total: 663 pruebas, 0 fallidas, 0 warnings. Suite de Windows repetida 4 veces sin intermitencia.
```

**Riesgos pendientes:** la transferencia definitiva de candados al coordinador (1.3B3+) sigue pendiente, junto con todo lo que la auditoría correctiva de 1.3B2 dejó documentado y sin resolver: el `CancelAsync` de cambio de dispositivo ya tiene equivalencia exacta (`CancelVoiceInputUnderExternalCoordinationAsync`, resuelto en esta subfase), pero el tercer dominio de candados (`_resourceGovernorVoiceGate`), la revisión de `Window_Closed` frente a operaciones en vuelo, y el fallback no liberado del constructor de `MainWindow` siguen abiertos. El smoke test manual interactivo (riesgo #13, heredado desde 1.2) sigue sin repetirse.

---

### Checkpoint Fase 1.3B2 — smoke test manual aprobado (2026-07-23)

El riesgo #13 (smoke test manual interactivo, abierto desde 1.2) queda **cerrado para el
runtime migrado en 1.3B2**: el usuario ejecutó la prueba interactiva sobre el ZIP publicado y la
aprobó sin regresiones visibles.

| Campo | Valor |
|---|---|
| **Commit probado** | `cd31699` (`docs: record voice runtime coordinator migration`) |
| **Rama** | `release/kohana-1.0-rc` |
| **Fecha del smoke test** | 2026-07-23 |
| **ZIP probado** | `Kohana-0.9.5-beta-phase1.3B2-runtime-smoke-win-x64.zip` |
| **SHA-256** | `EAC06AB7BB329B807F4455B7A2BD648F294CA14C542392126A77B2243956C0E9` |
| **Pruebas automatizadas** | **663**, 0 fallidas, 0 warnings (ver "Fase 1.3B2 runtime" arriba) |

**Funciones aprobadas manualmente:**

| Función | Resultado |
|---|---|
| Apertura de la aplicación | ✅ |
| Navegación (Ajustes, Asistente) | ✅ |
| Control Mic — clic para iniciar/detener | ✅ primer clic inicia escucha, segundo clic detiene y transcribe |
| Whisper (transcripción y procesamiento) | ✅ |
| Wake word "Kohana" | ✅ |
| Wake word "Oye Kohana" | ✅ |
| Orden de corrido | ✅ |
| TTS | ✅ |
| Cambio y persistencia del micrófono | ✅ |
| Prueba de frase y sensibilidad | ✅ |
| Cierre y reapertura | ✅ |
| Instancia única | ✅ |

**Aclaración sobre la interacción del control Mic:** el comportamiento actual y esperado del
producto es **clic para iniciar, clic para detener** — no "mantener presionado". El control **no**
responde a mantener presionado, y **esto no es una regresión**: es la interacción vigente del
producto. Ningún documento de esta fase describe el control Mic como "mantener presionado"; no fue
necesario corregir texto obsoleto.

**Sin regresiones visibles.** No se observaron bloqueos ni cierres inesperados.

**Riesgos pendientes** (sin cambios respecto a "Fase 1.3B2 runtime" arriba, y sin resolver por este
checkpoint, que fue puramente documental): la transferencia definitiva de sincronización y
propiedad de los tres servicios de voz al `VoiceCoordinator` (1.3B3+); el tercer dominio de
candados `_resourceGovernorVoiceGate`; el fallback no liberado del constructor de `MainWindow`; la
decisión, aún no tomada, de si `Window_Closed` pasará a delegar la liberación de los tres servicios
en `VoiceCoordinator.Dispose()`.

Ningún código de producción ni prueba se modificó para este checkpoint — es un cierre puramente
documental sobre el commit ya probado y publicado en 1.3B2 runtime.

---

### Fase 1.3B3 — sincronización, propiedad y cierre del subsistema de voz (2026-07-24)

Cierre del paso 1.3 del ADR 0001: se transfiere la **sincronización** y el **ciclo de vida** del
subsistema de voz a su sitio definitivo, sin cambiar ninguna capacidad ni comportamiento visible
(clic para iniciar/detener el Mic, ambas frases de wake word, orden de corrido, TTS, cambio y
persistencia de micrófono, cierre/reapertura e instancia única, todos idénticos a 1.3B2).

**Alternativa arquitectónica elegida.** Se evaluaron cuatro (coordinador dueño de todo; coordinador
dueño solo de la sincronización con la propiedad en el composition root; ámbitos opacos con la
orquestación visual en MainWindow; métodos compuestos con callbacks neutrales a WPF). Se eligió la
combinación **B + ámbitos opacos**: el `VoiceCoordinator` posee la sincronización y expone
**leases** (`IVoiceInputScope` / `IWakeWordScope : IAsyncDisposable`); `KohanaCompositionRoot` posee
y libera los tres servicios. Motivo: un objeto no debe liberar dependencias que no creó, la
orquestación visual es intrínsecamente WPF y no puede vivir en el coordinador, y los ámbitos
preservan la duración exacta de cada sección crítica sin exponer ningún `SemaphoreSlim`.

**Verificación del ciclo de vida antes de decidir.** `App` usa `ShutdownMode.OnExplicitShutdown` y
tiene icono de bandeja: cerrar la ventana puede solo ocultarla; la salida real es
`RequestExit → Application.Shutdown() → Window_Closed → App.OnExit`, con `Window_Closed`
ejecutándose **antes** que `App.OnExit`. Se comprobó empíricamente que el `ServiceProvider` **no**
libera instancias registradas con `AddSingleton(instance)` (0 disposals), de modo que hacer del
composition root el liberador explícito da una **ruta única** de `Dispose` sin doble liberación
(y, además, los tres `Dispose` concretos son idempotentes).

**Sincronización — un solo dominio por servicio.** Los dos únicos `SemaphoreSlim` del subsistema
(`_voiceGate`, `_wakeWordGate`) viven, privados, en `VoiceCoordinator`. `MainWindow` ya **no** tiene
`_voiceGate` ni `_wakeWordGate`: adquiere `AcquireVoiceInputScopeAsync` / `AcquireWakeWordScopeAsync`,
hace su sección crítica (incluida toda la orquestación visual) dentro del `await using`, y el ámbito
libera el candado al desecharse. Las operaciones mutantes de cada servicio (start/stop/cancel/listen
de Whisper; start/stop de Vosk) solo son alcanzables a través de su ámbito. El orden de adquisición
es siempre **entrada de voz → wake word**, nunca al revés (un ámbito de voz envuelve al de wake word
en push-to-talk, en la escucha tras la palabra de activación y en el cambio de dispositivo). La
sección crítica conserva la misma duración que en 1.3B2 y no se retiene ningún candado entre el
primer y el segundo clic del Mic (el ámbito es local a cada handler).

**Resource Governor.** `_resourceGovernorVoiceGate` se renombró a `_resourceGovernorDecisionGate` y
**permanece en MainWindow**: serializa las *decisiones* del governor (pausar/reanudar wake word y la
bandera `_resourceGovernorWakeWordPaused`), no el acceso físico a Vosk — las operaciones reales del
motor pasan después por el ámbito de wake word del coordinador (vía
`PauseWakeWordAsync`/`ApplyWakeWordPreferenceAsync`). No se tocaron umbrales, estados
Normal/Busy/Game, mensajes ni el comportamiento visible de Modo Juego.

**Propiedad y cierre.** `KohanaCompositionRoot.Dispose` es la **única** ruta de `Dispose` del
subsistema: libera el coordinador (sus dos semáforos) y después los tres servicios en el mismo orden
relativo que antes usaba `Window_Closed` (wake word → salida de voz → entrada de voz), y por último
el `ServiceProvider`. `Window_Closed` ahora solo marca `_isClosed`, cancela `_lifetimeCancellation`
y desuscribe los eventos de wake word **a través del coordinador** (paso directo); ya no libera los
tres servicios. La guardia `_isClosed` (al inicio de `HandleWakeWordDetectedAsync`) y el token
cancelado siguen protegiendo las operaciones en vuelo y los eventos ya encolados en el Dispatcher.

**Fallback resuelto.** El coordinador pasa a ser **dependencia obligatoria** de `MainWindow`: si no
se provee, se lanza `ArgumentNullException` en vez de construir un cuarto conjunto de motores sin
liberar. `MainWindow` ya no recibe, declara ni libera los tres servicios; su constructor pierde esos
tres parámetros y `App.OnStartup` deja de pasarlos. Los eventos de wake word se suscriben y
desuscriben por el coordinador, así que `MainWindow` no necesita ninguna referencia directa a los
servicios.

**API temporal eliminada.** Se retiraron del coordinador las seis operaciones
`…UnderExternalCoordinationAsync`, los siete métodos compuestos con candado interno
(`StartPushToTalkAsync`, `StopPushToTalkAsync`, `CancelPushToTalkAsync`, `ListenAfterWakeWordAsync`,
`StartWakeWordAsync`, `StopWakeWordAsync`, `PauseWakeWordAsync`) y el helper privado
`StopWakeWordWithinCoordinatorGateAsync`: ninguno tenía consumidor. La superficie pública final es
los ámbitos más los miembros de paso directo (disponibilidad, configuración, preparación, TTS).

**Pruebas.** Las pruebas de los métodos compuestos y de la API de transición se sustituyeron por
pruebas del diseño de ámbitos (delegación exacta con preservación de argumentos, exclusión real de
dos ámbitos concurrentes por servicio, independencia de dominios y no-interbloqueo en la anidación
voz→wake word, `DisposeAsync` que libera exactamente una vez aunque se deseche dos, y cancelación
que no retiene el candado). Se añadieron invariantes estructurales: propiedad y orden de `Dispose`
en el composition root, ausencia de semáforos de voz y de campos de servicio en `MainWindow`, cada
método de voz sobre su ámbito, orden de cierre de `Window_Closed`, guardia `_isClosed`, gate del
governor como *decision gate*, coordinador sin API de transición, y (conservadas) sin
`IServiceProvider` en `MainWindow` ni `PackageReference` en `Nexo.Core`. Ninguna prueba se eliminó
sin reemplazo.

**Resultado de build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas   (sin cambios)
  Nexo.Windows.Tests.dll →  84 superadas, 0 con error, 0 omitidas
Total: 660 pruebas, 0 fallidas, 0 warnings. Suite de Windows repetida 5 veces adicionales sin intermitencia.
```

**Riesgos restantes.** El smoke test manual interactivo de 1.3B3 (riesgo #13) queda pendiente del
usuario. Con la sincronización, la propiedad y el cierre ya centralizados, no quedan riesgos
arquitectónicos abiertos de la auditoría correctiva de 1.3B2: el tercer dominio se aclaró como
*decision gate*, el fallback se eliminó y el orden de cierre quedó documentado y verificado. Detalle
completo en `artifacts\Kohana-Fase-1.3B3-Voice-Lifecycle-Sprint-Informe.md`.

---

### Fase 1.3B3.1 — hotfix de salida completa desde bandeja (2026-07-24)

**Síntoma manual.** Tras aprobar todas las funciones de voz de 1.3B3, el smoke test encontró una
regresión bloqueante de cierre: al elegir **Salir** desde la bandeja, la interfaz y el icono
desaparecían pero `Kohana.exe` **permanecía en segundo plano**; **Alt + A** ya no abría la
aplicación; ejecutar `Kohana.exe` de nuevo no creaba ventana (la instancia fantasma conservaba la
coordinación de instancia única); y el usuario tenía que matar el proceso desde el Administrador de
tareas. El checkpoint de 1.3B3 quedó **no aprobado** hasta este hotfix.

**Causa exacta (con evidencia).** Se instrumentó temporalmente la ruta de cierre (traza del último
paso alcanzado + disparo automático de la ruta real de salida + clave de instancia aislada para no
colisionar con una instancia real). La traza mostró que el último paso alcanzado era
`ollama.Dispose before` y nada después: **el proceso se bloqueaba dentro de
`ManagedOllamaSupervisor.Dispose`**, que hacía
`_runtimeService.StopManagedAsync(CancellationToken.None).GetAwaiter().GetResult()` en el hilo de
UI durante `App.OnExit`. En ese momento el `Dispatcher` de WPF ya **no bombea** (estamos dentro del
callback síncrono de `Application.Shutdown`), y `StopManagedAsync` tiene `await`s sin
`ConfigureAwait(false)` (espera del gate, `process.WaitForExitAsync`, verificación HTTP del
endpoint) cuyas continuaciones se publican en el `SynchronizationContext` de la UI. Con el hilo de
UI bloqueado en `GetResult()`, esas continuaciones no pueden ejecutarse: **interbloqueo clásico
sync-sobre-async.**

**Por qué Alt + A dejaba de funcionar y por qué el proceso permanecía.** El bloqueo ocurría
**antes** de `_singleInstance.Dispose()`. Por tanto: el mutex de instancia única nunca se liberaba
(un nuevo `Kohana.exe` cedía y se cerraba en vez de convertirse en primario), y el hilo de UI —
bloqueado en `GetResult()`— no podía procesar la petición de activación de Alt + A (que se despacha
vía `Dispatcher.BeginInvoke`). El proceso quedaba vivo pero inerte: un fantasma. No era un problema
de la propiedad de voz de 1.3B3 (las tres liberaciones de Whisper/TTS/Vosk se alcanzan y completan
sin bloqueo en cuanto se desatasca el paso de ollama, confirmado por la traza tras la corrección).

**Solución aplicada (cambio mínimo seguro).** Se traslada la parada del runtime de IA administrado
a una fase **asíncrona previa a `Application.Shutdown`**, mientras el `Dispatcher` aún bombea:

- `ManagedOllamaSupervisor.StopAsync()` — parada asíncrona e idempotente (`_stopRequested`): cancela
  el token de vida y `await _runtimeService.StopManagedAsync(...)` con continuaciones que sí se
  procesan.
- `ManagedOllamaSupervisor.Dispose()` — ya **no** hace sync-sobre-async: solo libera recursos
  propios (idempotente, no bloquea).
- `MainWindow.RequestExit` — inicia el apagado **una sola vez** (`_exitRequested`) y delega en
  `RequestExitAsync`, que `await`ea `StopAsync()` y luego llama `Application.Current.Shutdown()` en
  un `finally` (el cierre continúa aunque la parada falle). No se inician nuevas operaciones tras el
  inicio del apagado.
- `App.OnExit` — sin cambios de orden (ollama → composition root → instancia única); ahora
  `ollama.Dispose` no bloquea, así que `OnExit` alcanza `_singleInstance.Dispose()` y libera la
  instancia única.

**Diferencia entre ocultar y salir (preservada).**
- **X con "Minimizar a bandeja" activado** → oculta Kohana; el proceso sigue vivo; Alt + A la vuelve
  a mostrar. (Verificado en el portable de producción: `WM_CLOSE` ocultó a bandeja, el proceso
  siguió vivo.)
- **Salir en el menú de bandeja** → cierra por completo; `Kohana.exe` desaparece; una nueva
  instancia se convierte en primaria. (Verificado por el smoke instrumentado: la ruta real
  `RequestExit → RequestExitAsync → Application.Shutdown → OnExit` completó, terminó el proceso con
  código 0 y una segunda instancia con la misma clave se volvió primaria.)

**Propiedad y sincronización preservadas.** No se revirtió nada de 1.3B3: `KohanaCompositionRoot`
sigue siendo dueño y liberador de Whisper, TTS y Vosk; `VoiceCoordinator` sigue siendo dueño de sus
dos semáforos; los ámbitos `IAsyncDisposable` no se tocaron; `MainWindow` sigue sin poseer ni
liberar los servicios y sin `IServiceProvider`.

**Pruebas.** +6 invariantes estructurales de la ruta de salida (Windows 84 → 90): `Dispose` sin
sync-sobre-async; `StopAsync` asíncrono e idempotente; `RequestExit` de un solo inicio que no llama
`Shutdown` directamente; `RequestExitAsync` que detiene el runtime antes de `Shutdown` (en el
`finally`); `App.OnExit` en orden no bloqueante que alcanza la instancia única; y propiedad de voz
de 1.3B3 conservada. La recuperación de instancia única y la distinción ocultar/salir ya están
cubiertas por `SingleInstanceCharacterizationTests` (`ReleasingThePrimary_LetsTheNextInstanceTakeOver`,
`Dispose_ReleasesTheMutexExactlyOnce`) y por los tests de `WindowsClosePolicy`
(`ClosePolicy_HidesWhenTrayModeIsEnabled`, `ClosePolicy_AllowsExplicitExit`).

**Instrumentación temporal.** El tracer `ShutdownTrace`, el disparo automático de salida, la clave
de instancia por variable de entorno y el helper `TriggerExitForDiagnostics` se usaron solo para
localizar el bloqueo y se **eliminaron antes del commit final**. El diff enviado contiene únicamente
la corrección real (3 archivos) y las pruebas.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll →  90 superadas, 0 con error, 0 omitidas
Total: 666 pruebas, 0 fallidas, 0 warnings. Suite de Windows repetida 5 veces adicionales sin intermitencia.
```

**ZIP:** `artifacts\Kohana-0.9.5-beta-phase1.3B3.1-exit-hotfix-smoke-win-x64.zip` (detalle de tamaño
y SHA-256 en `artifacts\Kohana-Fase-1.3B3.1-Exit-Hotfix-Informe.md`).

**Smoke test manual pendiente.** El usuario debe confirmar sobre el portable de este hotfix que, al
elegir **Salir** desde la bandeja, `Kohana.exe` desaparece por completo, Alt + A deja de aplicar y un
nuevo `Kohana.exe` abre con normalidad como instancia primaria; y que la X con "Minimizar a bandeja"
sigue ocultando sin cerrar.

---

### Fase 1.3C — consolidación final de la arquitectura extraída (2026-07-24)

Turno nocturno, rama `nightshift/phase1-finalization` (basada en `release/kohana-1.0-rc` = e5c2233
+ el hotfix 1.3B3.1 ya completado). Cierra el paso 3 del ADR 0001 consolidando la arquitectura
lograda de 1.2 a 1.3B3.1 **sin añadir capacidades visibles**. Solo limpiezas respaldadas por
evidencia.

**Auditoría (evidencia).**
- `VoiceCoordinator`: los 16 miembros públicos son consumidos por `MainWindow` (≥1 uso cada uno) →
  **no hay API muerta**; el diseño de ámbitos de 1.3B3 ya era mínimo.
- `KohanaCompositionRoot`: `Provider`, `VoiceInputService`, `VoiceOutputService`, `WakeWordService`
  tienen **0 consumidores de producción**, pero **sí** consumidores de prueba
  (`KohanaCompositionRootTests` verifica tipos concretos, singletons y "no un cuarto motor"). Por
  tanto **no son API muerta**: se conservan públicos como superficie de verificación de composición
  (la razón por la que el root vive en `Nexo.Windows`), y se documenta así.
- `MainWindow`: los servicios `IAiChatService`/`IAudioMixerService`/`IScreenCaptureService` aún
  tenían fallback `?? new ...()` (residual desde 1.2), mientras el coordinador de voz ya era
  obligatorio desde 1.3B3 — inconsistencia de la composición.

**Implementación (cambios respaldados).**
1. **Servicios obligatorios en `MainWindow`.** Los tres fallbacks residuales `?? new ...()` se
   sustituyen por `?? throw new ArgumentNullException(...)`, igual que el coordinador. `MainWindow`
   ya **nunca** construye un motor; todos los servicios provienen de la raíz de composición única.
   Se eliminan así las últimas dependencias de tipos concretos de servicio en `MainWindow`.
2. **Pruebas obsoletas sustituidas.** `KohanaCompositionRootTests` tenía dos pruebas con premisa
   **pre-1.3B3** ("el root NO libera los tres motores de voz"), hoy falsa (el root SÍ los libera).
   Se sustituyen por `Dispose_ReleasesTheWholeVoiceSubsystemIdempotently`; el orden exacto de
   liberación ya lo guarda estructuralmente `CompositionRoot_OwnsAndDisposesTheThreeVoiceServicesInOrder`.
3. **Invariante reforzada.** `MainWindow_RequiresEveryInjectedService_WithoutBuildingAnyFallback`
   exige que los cuatro servicios lancen si faltan y que no exista ningún `?? new` ni construcción
   directa de motores.
4. **Comentarios de fase eliminados.** Las 9 anotaciones "Fase 1.3Bx / Subfase / diseño final de la
   fase …" en `MainWindow`, `App`, `VoiceCoordinator` y `KohanaCompositionRoot` se reescriben como
   documentación atemporal del diseño final. XML-docs de `KohanaCompositionRoot` actualizados.
5. **ADR 0001** cerrado con un addendum del estado final (salida completa + composición única +
   frontera de acceso a la voz).

**Invariantes preservados (verificados por pruebas):** composición única; `MainWindow` sin
`IServiceProvider` ni Service Locator; `Nexo.Core` sin `PackageReference`; acceso a la voz solo por
`VoiceCoordinator`; ownership y `Dispose` únicos en `KohanaCompositionRoot`; cierre idempotente y no
bloqueante (1.3B3.1); ámbitos `IAsyncDisposable` intactos.

**No se tocó:** UX, textos, motores, parámetros de Whisper/Vosk, Resource Governor, ni se inició
hardware detection, memoria, OCR o skills.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 576 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll →  88 superadas, 0 con error, 0 omitidas
Total: 664 pruebas, 0 fallidas, 0 warnings. Suite de Windows repetida 5 veces adicionales sin intermitencia.
```

**Smoke test manual pendiente.** La consolidación no cambia conducta observable; el smoke test manual
(idéntico al de 1.3B3.1) sigue pendiente del usuario. Detalle completo en
`artifacts\Kohana-Fase-1-Consolidation-Informe.md`.

---

### Checkpoint manual — Fase 1 y Design System Foundation aprobados (2026-07-24)

El usuario ejecutó y **aprobó** manualmente el smoke test completo sobre el portable de la fundación
visual, cerrando así la Fase 1 y aceptando el Design System Foundation 0.1. Tras la aprobación,
`release/kohana-1.0-rc` se promovió por **fast-forward** (sin merge commit, squash, rebase ni cherry-pick)
desde `nightshift/phase1-finalization`, quedando en `7b77116`.

| Campo | Valor |
|---|---|
| **ZIP probado** | `Kohana-0.9.5-beta-design-system-foundation-smoke-win-x64.zip` |
| **SHA-256** | `E8A8E77077AE1133CD20B38F7DB54D6CC6D157573E13C33AFB9FE28AC7AD2E43` |
| **Commit promovido** | `7b77116` (`docs: record Kohana design system foundation`) |
| **Release antes → después** | `5d368ec` → `7b77116` (fast-forward) |
| **Pruebas** | 671 (576 Core + 95 Windows), 0 fallidas, 0 warnings |

**Validación manual registrada (todo aprobado):**

- Arranque correcto.
- Mic mediante clic correcto; Whisper correcto.
- Wake word "Kohana" y "Oye Kohana" correctos; orden de corrido correcta.
- TTS correcto; cambio y persistencia del micrófono correctos; sensibilidad y aliases correctos.
- Modo Juego correcto.
- **Hotfix de salida (1.3B3.1) aprobado:** "Salir" desde bandeja termina completamente `Kohana.exe`;
  reapertura correcta como instancia primaria; instancia única correcta.
- **Diferencia ocultar/salir aprobada:** la X con "Minimizar a bandeja" oculta; **Alt + A** vuelve a
  mostrar Kohana; "Salir" cierra por completo.
- **Cierre durante escucha** correcto.
- **Apariencia revisada:** prácticamente idéntica; **ninguna regresión** funcional ni visual observada.

**Estado formal:**

- **Fase 1 (extracción del runtime, ADR 0001) formalmente CERRADA.** Sincronización, propiedad,
  ciclo de vida y cierre del subsistema de voz consolidados; composición única; `MainWindow` sin
  candados de voz, sin motores directos, sin `IServiceProvider`.
- **Design System Foundation 0.1 APROBADO** como **infraestructura visual** (tokens semánticos,
  agregador de tema, estilos base), preservando la apariencia actual.
- **Rediseño visual completo TODAVÍA PENDIENTE:** la fundación no es un rediseño; el sprint visual
  (migración de literales restantes, anillo de foco activo, modo claro/alto contraste, iconografía y
  logo, motion aplicado, accesibilidad) sigue pendiente.
- **Fase 2 TODAVÍA NO iniciada.**

La rama `nightshift/phase1-finalization` se conserva (no se elimina). Este checkpoint solo actualiza
la documentación; no cambia código de producción.

---

### Fase 2.1 — Hardware Capability Profile v1 (2026-07-24)

Primer sprint de la Fase 2. Construye una representación confiable y transparente de la capacidad
**estable** del equipo (CPU, RAM, GPU, batería, arquitectura), separada de las métricas **dinámicas**
que ya existían (`SystemSnapshot`). No selecciona ni cambia motores todavía — esa decisión es de la
Fase 2.2 (Adaptive Engine Registry).

**Modelos nuevos (`Nexo.Core.Hardware`, sin `PackageReference`, sin WMI/Registry/WinAPI):**

- `ProcessorCapability`, `MemoryCapability`, `GraphicsCapability` — datos crudos por categoría, cada
  campo opcional para poder representar "desconocido" sin recurrir a `0`.
- `HardwareCapabilitySnapshot` — captura completa: procesador, memoria, lista de GPUs + GPU
  preferida, presencia de batería, edición/versión de Windows, fecha de captura, si vino de caché.
- `HardwareCapabilityTier` (`Basic`, `Standard`, `Accelerated`, `HighPerformance`),
  `HardwareDataConfidence` (`Unknown`, `Estimated`, `Known`), `HardwareCapabilityReason` (mensaje
  legible en español).
- `HardwareCapabilityProfile` — salida de la política: perfil, resumen, razones positivas,
  limitaciones, datos desconocidos, confianza general; incluye el `Snapshot` completo para que la UI
  muestre los datos crudos sin duplicar campos.
- `IHardwareCapabilityService` — `GetCachedProfile()` (síncrono, sin E/S) y `RefreshAsync(CancellationToken)`
  (única vía que vuelve a detectar).

**Política de clasificación (`HardwareCapabilityPolicy`):** pura (`Evaluate(snapshot) → profile`), sin
estado, con umbrales centralizados como `const`. Cada categoría (RAM, procesadores lógicos, GPU/VRAM)
se traduce a un nivel 0–3 solo si el dato es conocido; el nivel final es
`floor(promedio de los niveles conocidos)`. Un dato desconocido se **excluye** del promedio en vez de
contar como `0`, así que RAM/GPU/núcleos físicos desconocidos nunca fuerzan `Basic` por sí solos. Con
cero categorías conocidas, el resultado es `Standard` con confianza `Unknown` (nunca `Basic` por
defecto). Umbrales: RAM 8/16/32 GiB, procesadores lógicos 5/9/17, VRAM dedicada 4/8 GiB.

**Detección en Windows (`Nexo.Windows.Hardware`):** `WindowsHardwareCapabilityService` orquesta cinco
fuentes inyectables (`IProcessorInfoSource`, `IMemoryInfoSource`, `IGraphicsInfoSource`,
`IBatteryInfoSource`, `IWindowsVersionInfoSource`), cada una con su propia captura de excepciones, de
modo que el fallo de una fuente nunca destruye el resto del snapshot. Fuentes reales:

- CPU: registro `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` (nombre, fabricante) +
  `Environment.ProcessorCount` (lógicos) + `GetLogicalProcessorInformationEx` por P/Invoke a
  `kernel32.dll` (núcleos físicos, contando entradas `RelationProcessorCore`).
- RAM: `GlobalMemoryStatusEx` (P/Invoke a `kernel32.dll`).
- GPU: registro `HKLM\SYSTEM\CurrentControlSet\Control\Video\{GUID}\0000`
  (`HardwareInformation.AdapterString`, `HardwareInformation.qwMemorySize` — VRAM dedicada real de 64
  bits, no el campo legado de 32 bits limitado a ~4 GB). Se conservan todas las GPUs detectadas; la
  preferida es la dedicada con más memoria conocida, documentado en `SelectPreferredGraphicsAdapter`.
- Batería: `GetSystemPowerStatus` (P/Invoke a `kernel32.dll`, bit `BatteryFlag`).
- Windows: registro `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` (`ProductName`,
  `DisplayVersion`/`ReleaseId`, `CurrentBuildNumber`).

**Ninguna dependencia nueva.** `Nexo.Core` sigue sin `PackageReference` (invariante verificado por
prueba). `Nexo.Windows` no agregó ningún paquete: todo se resuelve con `Microsoft.Win32.Registry` y
P/Invoke, ya disponibles en el TFM `net10.0-windows`.

**Caché y refresco:** el snapshot se cachea en memoria; `GetCachedProfile()` nunca hace E/S.
`RefreshAsync` corre en `Task.Run` y es cancelable — una cancelación no toca la caché existente. La
detección corre una vez al iniciar `MainWindow` (sin bloquear el hilo de UI) y de nuevo bajo demanda
desde el botón "Actualizar detección".

**Composición:** `IHardwareCapabilityService` se registra como instancia única en
`KohanaCompositionRoot` (mismo patrón que los otros seis servicios), se inyecta a `MainWindow` como
dependencia obligatoria (`?? throw`), y no requiere `Dispose` (no se inventó ciclo de vida). Ningún
`IServiceProvider` en `MainWindow`; `MainWindow` no construye el servicio.

**Interfaz:** `SystemView` gana la sección "Capacidad del equipo" (nivel, CPU, núcleos físicos y
lógicos, RAM, arquitectura, GPU preferida, memoria gráfica, batería, estado de completitud, razones,
datos desconocidos, botón de refresco) usando solo recursos existentes del Design System.
`DiagnosticsWindow` gana un bloque técnico con todas las GPUs detectadas, datos crudos, fecha de
captura, origen (caché/detección reciente) y confianza — sin exponer trazas de excepción. No se
añadieron controles de modos (Automático/Eco/Equilibrado/Máximo): eso es de la Fase 2.2.

**Pruebas nuevas:** 27 en total — 16 en `HardwareCapabilityPolicyTests` (los cuatro niveles, RAM/GPU/VRAM/
núcleos físicos desconocidos sin forzar `Basic`, límites exactos de umbral, razones deterministas,
misma entrada → misma clasificación) y 11 en `WindowsHardwareCapabilityServiceTests` (detección
completa, fallo aislado de CPU/GPU/batería, múltiples GPUs, caché, refresco, cancelación con caché
intacta, fallo total de todas las fuentes sin excepción). Se extendieron
`KohanaCompositionRootTests` y `CompositionInvariantTests` para cubrir el nuevo servicio singleton.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 592 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll → 108 superadas, 0 con error, 0 omitidas
Total: 700 pruebas (671 previas + 27 nuevas de Fase 2.1 + 2 de composición extendidas), 0 fallidas, 0 warnings.
```

**No se tocó:** voz, bandeja, Alt + A, cierre, instancia única, apariencia del Design System, ni se
seleccionó o cambió ningún motor.

**Smoke test manual pendiente.** El sprint no debe darse por cerrado hasta que el usuario confirme
manualmente que los datos mostrados en "Capacidad del equipo" corresponden a su equipo real. Detalle
completo del build, pruebas repetidas y ZIP en
`artifacts\Kohana-Fase-2.1-Hardware-Capability-Profile-Informe.md`.

---

### Checkpoint manual — Fase 2.1 Hardware Capability Profile aprobado (2026-07-24)

El usuario ejecutó y **aprobó** manualmente el smoke test del ZIP publicado de la Fase 2.1. Tras la
aprobación, `release/kohana-1.0-rc` se promovió por **fast-forward** (sin merge commit, squash,
rebase ni cherry-pick) desde `phase2/hardware-capability-profile-v1`, quedando en `2f77e22`.

| Campo | Valor |
|---|---|
| **ZIP probado** | `Kohana-0.9.5-beta-phase2.1-hardware-profile-smoke-win-x64.zip` |
| **SHA-256** | `9661543B3E4B5A4DED31CAF8C8A15A76D7BAE76376E4334B9609704028CF50E2` |
| **Commit promovido** | `2f77e22` (`docs: record phase 2.1 hardware capability profile`) |
| **Release antes → después** | `a8685dd` → `2f77e22` (fast-forward) |
| **Pruebas** | 700 (592 Core + 108 Windows), 0 fallidas, 0 warnings |

**Validación manual registrada (todo aprobado):**

- Arranque correcto.
- Nivel de capacidad detectado: **Acelerada**.
- CPU mostrada correctamente.
- Núcleos físicos correctos.
- Procesadores lógicos correctos.
- RAM total correcta.
- GPU preferida correcta.
- VRAM correcta.
- Sin GPUs duplicadas o incorrectas en el listado.
- "Actualizar detección" funcionó correctamente, con la interfaz fluida durante la actualización
  (sin bloqueo).
- Diagnóstico técnico revisado sin discrepancias reportadas.
- Alt + A correcto.
- Micrófono y Whisper correctos.
- Wake word correcto.
- TTS correcto.
- Ocultar, salir y reabrir correctos.
- Sin regresiones funcionales observadas.

**Batería:** no reportada en esta validación manual — el usuario no confirmó explícitamente el
estado mostrado para la línea de batería, por lo que no se registra un resultado (ni "presente" ni
"no detectada") para este campo.

**Estado formal:**

- **Hardware Capability Profile v1 APROBADO.**
- **Fase 2.2 (Adaptive Engine Registry) todavía NO iniciada.**
- Ningún modo de rendimiento (Automático/Eco/Equilibrado/Máximo) implementado todavía.
- Ninguna selección de motor se aplica todavía.

La rama `phase2/hardware-capability-profile-v1` se conserva (no se elimina). Este checkpoint solo
actualiza la documentación; no cambia código de producción, pruebas, proyectos ni recursos visuales.

---

### Fase 2.2 — Adaptive Engine Registry y modos de rendimiento v1 (2026-07-24)

Segundo sprint de la Fase 2. Construye un registro que sabe qué motores de voz/IA existen
realmente en el runtime de Kohana, qué tan disponibles/configurados/activos están, y qué
recomendaría cada modo de rendimiento (Automático/Ahorro/Equilibrado/Máximo) cruzando esa
información con el `HardwareCapabilityProfile` de la Fase 2.1. **No cambia ningún motor
automáticamente.** Whisper, Vosk, SAPI y Ollama siguen exactamente igual que antes de este sprint;
solo se agregó transparencia sobre lo que Kohana podría recomendar.

**Motores reales encontrados y registrados** (`WindowsAdaptiveEngineRegistry`):

- **Whisper** (`WhisperVoiceInputService`) — reconocimiento de voz, modelo `Base` fijo (hardcodeado
  en el código, nunca configurable), siempre en CPU (el paquete `Whisper.net.Runtime` referenciado
  no incluye soporte GPU — no se afirma uso de GPU).
- **Vosk** (`VoskWakeWordService`) — palabra de activación, modelo pequeño en español fijo.
- **SAPI de Windows** (`WindowsTextToSpeechService`) — síntesis de voz; no expone qué voz eligió ni
  si está hablando en este instante, así que esos campos quedan `Unknown` en vez de inventarse.
- **OpenAI, Ollama, LM Studio, compatible con OpenAI** — los cuatro proveedores reales que
  `AiChatRouterService` enruta hoy (confirmado leyendo su `Resolve`, no asumido).

**Motores explícitamente excluidos** (no existen en el runtime, no se registran aunque se
mencionen en documentación futura): openWakeWord, Silero VAD, Kokoro, Piper, `Windows.Media.Ocr`,
cualquier motor de visión distinto de la captura de pantalla existente. Verificado por `grep`
sobre `src/` — cero referencias reales a ninguno de estos.

**Modelo de dominio** (`Nexo.Core.AdaptiveEngine`, sin `PackageReference`, sin WMI/Registry/WinAPI):
`HardwarePerformanceMode` (Automatic/Eco/Balanced/Maximum), `EngineCategory` (SpeechToText/
WakeWord/TextToSpeech/LocalLanguageModel — sin categoría de Visión: no hay un motor visual
diferenciable de la captura de pantalla), `EngineIdentifier` (value type, no strings sueltos como
contrato), `EngineCostLevel` (Unknown/Low/Moderate/High — sin cifras exactas), `EngineRequirement`,
`EngineDescriptor`, `EngineRuntimeState` (tres `bool?` independientes: disponible/configurado/
activo — un motor puede ser disponible-pero-no-configurado, configurado-pero-inactivo, o
activo-pero-no-recomendado, simultáneamente), `EngineCompatibility`, `EngineRecommendation`,
`AdaptiveEnginePlan`, `AdaptiveEnginePolicy` (pura, `static`), `IAdaptiveEngineRegistry`.

**Política:** `AdaptiveEnginePolicy.Evaluate(hardwareProfile, mode, descriptors, runtimeStates,
evaluatedAt)` — pura, con la fecha como parámetro explícito (no `DateTimeOffset.Now` interno) para
que el mismo input produzca exactamente el mismo plan. Cada motor se clasifica compatible/
incompatible comparando su requisito mínimo contra un presupuesto de costo por tier (Basic→Low,
Standard→Moderate, Accelerated/HighPerformance→High); un costo `Unknown` nunca bloquea
compatibilidad. Entre los motores compatibles de una categoría, el modo elige: Ahorro → el de
menor costo; Máximo → el de mayor costo (nunca uno incompatible); Equilibrado → el más cercano a
un costo moderado; Automático → igual que Equilibrado si la confianza del hardware es `Known`, o
igual que Ahorro (conservador) si es `Unknown` — así "dato desconocido" nunca se confunde con
"máquina incapaz".

**Persistencia:** `ShellPreferences.HardwarePerformanceMode` (default `Automatic`), esquema
subido a v17 siguiendo el mismo patrón de migración versionada + `Enum.IsDefined` de
`WakeWordSensitivity`/`AiProvider`. Un valor corrupto o de una versión futura cae a `Automatic` en
vez de fallar. Cambiar el modo recalcula el plan y persiste de inmediato; no reinicia motores.

**Composición:** `IAdaptiveEngineRegistry` es el octavo singleton de `KohanaCompositionRoot`,
construido después de `VoiceCoordinator` (lee su estado ya expuesto — `IsVoiceInputReady`,
`IsWakeWordReady`, etc. — sin modificar su comportamiento ni su ciclo de vida). Inyectado a
`MainWindow` como dependencia obligatoria, sin `IServiceProvider`, sin construcción directa.

**Interfaz:** `SettingsView` gana "Modo de rendimiento" (cuatro `RadioButton` accesibles, con
`AutomationProperties.Name`, cada una con descripción breve) — con un título distinto a la sección
preexistente "Rendimiento adaptativo" del Resource Governor (modo Normal/Ocupado/Juego, un
concepto reactivo no relacionado, para no confundir ambos). `SystemView` gana "Plan adaptativo"
debajo de "Capacidad del equipo": motor configurado/activo/recomendado por categoría, motivos,
advertencias, y una etiqueta "Solo recomendación" siempre que el motor activo no coincida con el
recomendado. `DiagnosticsWindow` gana un bloque técnico con todos los motores registrados, sus
requisitos, disponibilidad, compatibilidad, estado y el timestamp/tier/modo del plan — sin exponer
claves de API ni rutas privadas.

**Pruebas nuevas:** 69 en total. 30 en `AdaptiveEnginePolicyTests` (los cuatro modos en los cuatro
tiers, costo de motor desconocido sin bloquear compatibilidad, confianza baja/desconocida forzando
conservadurismo, motor recomendado-pero-no-disponible, disponible-pero-incompatible, activo
distinto del recomendado, configurado-pero-inactivo, categoría sin motores, orden y razones
deterministas, misma entrada → mismo plan). 13 en `WindowsAdaptiveEngineRegistryTests` (Whisper/
Vosk/SAPI registrados una vez, identificadores únicos, proveedores remotos no marcados locales,
estado real desde `VoiceCoordinator`, Ollama configurado no implica activo). 11 invariantes
estructurales de interfaz (`AdaptiveEngineUiInvariantTests`) verificando los cuatro modos visibles,
nombres accesibles, la etiqueta "Solo recomendación", el bloque técnico de Diagnostics, y que
ningún manejador de UI toque un motor de voz u Ollama directamente. Se extendieron los tests de
composición para el octavo singleton. Migrar el esquema a v17 requirió actualizar 9 pruebas de
caracterización que verificaban el número de esquema "totalmente migrado" anterior (16 → 17).

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 629 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll → 140 superadas, 0 con error, 0 omitidas
Total: 769 pruebas (700 previas + 69 nuevas de Fase 2.2), 0 fallidas, 0 warnings.
```

**No se tocó:** VoiceCoordinator no cambió de comportamiento (solo se leyó su estado ya público),
ningún motor se descargó, instaló o sustituyó, ningún parámetro de voz actual cambió, Voice Lab no
se inició.

**Smoke test manual pendiente.** El sprint no debe darse por cerrado hasta que el usuario confirme
manualmente que los cuatro modos se muestran y seleccionan correctamente, que "Plan adaptativo"
refleja el hardware y los motores reales del equipo, y que cambiar de modo nunca cambia de motor
observable. Detalle completo en
`artifacts\Kohana-Fase-2.2-Adaptive-Engine-Registry-Informe.md`.

---

### Checkpoint manual — Fase 2.2 Adaptive Engine Registry aprobado (2026-07-24)

El usuario ejecutó y **aprobó** manualmente el smoke test del ZIP publicado de la Fase 2.2. Tras la
aprobación, `release/kohana-1.0-rc` se promovió por **fast-forward** (sin merge commit, squash,
rebase ni cherry-pick) desde `phase2/adaptive-engine-registry-v1`, quedando en `79bf2e0`.

| Campo | Valor |
|---|---|
| **ZIP probado** | `Kohana-0.9.5-beta-phase2.2-adaptive-engine-registry-smoke-win-x64.zip` |
| **SHA-256** | `D2F5553DC246D95EBD97984A4758AA636688D400B9CD85D6BE3641D03EE695EB` |
| **Commit promovido** | `79bf2e0` (`docs: record phase 2.2 adaptive engine registry`) |
| **Release antes → después** | `9ddb903` → `79bf2e0` (fast-forward) |
| **Pruebas** | 769 (629 Core + 140 Windows), 0 fallidas, 0 warnings |

**Validación manual registrada (todo aprobado):**

- Arranque correcto.
- Sección "Modo de rendimiento" correcta: Automático, Ahorro, Equilibrado y Máximo visibles y
  funcionales.
- Selección por teclado correcta; cambio entre modos fluido.
- Persistencia confirmada entre pantallas y después de cerrar y reabrir Kohana.
- "Plan adaptativo" visible, usando correctamente el nivel de hardware **Acelerada**.
- Entrada de voz, palabra de activación, TTS y modelo de lenguaje representados correctamente.
- Estados **configurado**, **activo** y **recomendado** correctamente diferenciados; etiqueta
  "Solo recomendación" correcta.
- Las recomendaciones cambian según el modo seleccionado.
- Probado con Ollama apagado: el estado se representó correctamente sin afirmar actividad falsa.
- Ningún motor futuro incorrecto visible; ningún motor duplicado; ningún dato privado visible.
- Cambiar de modo no reinicia ni sustituye ningún motor.
- Alt + A correcto; micrófono y Whisper correctos; wake word correcto; TTS correcto; ocultar,
  salir y reabrir correctos.
- Sin regresiones visuales ni funcionales observadas.

**Estado formal:**

- **Adaptive Engine Registry v1 APROBADO.**
- **Selección automática real de motores todavía NO implementada** — esta versión solo recomienda,
  nunca aplica.
- **Descargas y reinicios de motores todavía NO implementados.**
- **Siguiente etapa prevista: sprint de diseño visual de Kohana.**

La rama `phase2/adaptive-engine-registry-v1` se conserva (no se elimina). Este checkpoint solo
actualiza la documentación; no cambia código de producción ni pruebas.

---

### Diseño D1 — Sakura Shell, navegación y lenguaje visual (2026-07-25)

Primer sprint que produce un cambio visual evidente en Kohana. Hasta ahora la apariencia se había
preservado deliberadamente (Design System Foundation 0.1 era solo infraestructura de tokens). Este
sprint rediseña el marco principal — barra lateral, marca, navegación, encabezado, superficie de
contenido, estados y transiciones — sin tocar el contenido profundo de cada pantalla ni ninguna
función (voz, IA, modos de rendimiento, hardware, persistencia, hotkeys, bandeja, instancia única).

**Dirección visual — "Sakura Nocturna":** grafito oscuro, superficies elevadas, acento Sakura
apagado usado solo donde comunica algo (nunca como fondo dominante), geometría sobria y coherente,
nada de efectos pesados ni pétalos decorativos sin función.

**Cambios visibles principales:**

- Se eliminaron las dos elipses decorativas con `BlurEffect` y el degradado interno redundante del
  `ShellBorder` — ninguna comunicaba estado, ambas costaban composición GPU. El borde del shell
  pasó de un degradado de tres colores a un borde sólido semántico con sombra sutil.
- `MainWindow.xaml` terminó el sprint sin ningún color hexadecimal literal (verificado por
  prueba): todos migraron a `DynamicResource` sobre tokens existentes o un único token nuevo
  (`BrushSidebarSurface`, para diferenciar el fondo de la barra lateral del de la tarjeta de
  contenido).
- El estado de sección activa ahora se comunica con cuatro señales, no solo color: superficie
  elevada, indicador vertical tipo "tallo", ícono relleno en vez de solo trazo, y etiqueta con más
  peso de fuente.
- El foco de teclado usa el token semántico `BrushFocusRing` (existía desde la Fundación 0.1, sin
  consumidores hasta ahora) en vez de reutilizar `BrushAccent`.
- Las transiciones existentes (cambio de sección, expandir/contraer barra lateral) ahora usan
  tokens de movimiento (`MotionFast`/`MotionBase`/`MotionEaseOut`) en vez de literales, y respetan
  `SystemParameters.ClientAreaAnimation` además de la preferencia propia de Kohana.
- Se quitó el título de página duplicado que ocho de las nueve vistas renderizaban internamente
  (TasksView, FocusView, RoutinesView, AudioView, CaptureView, SystemView, SettingsView,
  AssistantView), ahora que el encabezado centralizado del shell es la única fuente del título.
  Subtítulos, estados dinámicos y botones de acción de cada vista quedaron intactos.

**Navegación:** las nueve entradas y su orden funcional no cambiaron (verificado por prueba contra
`ShellNavigationPolicy.KnownDestinations`); tampoco cambiaron rutas, comandos ni nombres
funcionales. Solo se rediseñaron los estilos visuales, ahora reutilizables en
`Themes/Controls.xaml` (`SakuraNavigationItemStyle`, `SakuraNavigationIconStyle`/
`SakuraNavigationIconFilledStyle`, `SakuraSidebarToggleStyle`) en vez de vivir localmente dentro de
`MainWindow.xaml`.

**Recursos nuevos:** `ColorSidebarSurface`/`BrushSidebarSurface` (Colors.xaml); `RadiusShell`,
`SidebarWidthCollapsed/Expanded`, `SidebarButtonWidthCollapsed/Expanded`, `NavigationIconSize`,
`NavigationIndicatorWidth` (Spacing.xaml); `SakuraNavigationIconStyle`,
`SakuraNavigationIconFilledStyle`, `SakuraNavigationItemStyle`, `SakuraSidebarToggleStyle`,
`SakuraPageTitleStyle`, `SakuraPageSubtitleStyle`, `SakuraShellCardStyle` (Controls.xaml). Ningún
diccionario de tema nuevo — todo se agregó a los 8 archivos existentes, así que
`DesignSystemResourceTests` (merge order, unicidad de claves, referencias válidas) sigue
cubriéndolos sin modificar esa prueba.

**No rediseñado en este sprint:** contenido interno de las 8 vistas (solo se quitó el título
duplicado), el símbolo floral completo (`KohanaFlowerMarkStyle`, ya existía, sigue sin usarse),
iconografía floral distintiva por módulo, modo claro/alto contraste, arrastre de ventana, icono de
instalador. Detalle completo, principios y plan de D2 en `docs/design/SAKURA_SHELL_V1.md`.

**Pruebas nuevas:** 24 en `SakuraShellStructureTests` — tokens/estilos nuevos existen, cero colores
hexadecimales literales en `MainWindow.xaml`, las nueve entradas de navegación y su orden funcional
se preservan, cada control de navegación tiene nombre accesible y tooltip, el estado activo cambia
más que solo color, el foco es visible vía `BrushFocusRing`, las animaciones del shell consultan
`SystemParameters.ClientAreaAnimation` y nunca exceden 300 ms ni usan `RepeatBehavior`, el mecanismo
de `ContentControl` de navegación sigue intacto, ningún método visual construye un servicio de
producción, y `VoiceCoordinator`/`AdaptiveEnginePolicy`/la ausencia de `PackageReference` en
`Nexo.Core`/la ausencia de `IServiceProvider` en `MainWindow` permanecen sin tocar.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 629 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll → 164 superadas, 0 con error, 0 omitidas
Total: 793 pruebas (769 previas + 24 nuevas de Diseño D1), 0 fallidas, 0 warnings.
```

**Limitación del smoke test automatizado:** al momento de esta validación seguía activa la sesión
de smoke test manual de Fase 2.2 del usuario (`Kohana.exe` corriendo desde
`artifacts\Kohana-0.9.5-beta-phase2.2-...\`). El mutex de instancia única de Kohana es de ámbito de
sesión, no de ruta de ejecutable, así que cualquier lanzamiento de prueba durante ese período habría
señalizado esa instancia y terminado de inmediato sin cargar el build nuevo. No se interrumpió esa
sesión; la validación automatizada de este sprint se apoya en el build limpio y la suite completa en
verde como señal de confianza, con el smoke test visual interactivo pendiente del usuario, como
exige explícitamente este sprint (no se afirma aprobación estética).

**Revisión visual humana pendiente.** Este sprint no debe darse por cerrado hasta que el usuario
revise visualmente el resultado. Detalle completo, capturas (si existen) y pasos sugeridos en
`artifacts\Kohana-Design-D1-Sakura-Shell-Informe.md`.

## Diseño D1.1 — Hotfix de crash al navegar

**Motivo:** el usuario probó el ZIP de Diseño D1
(`Kohana-0.9.5-beta-design-d1-sakura-shell-smoke-win-x64.zip`) y reportó un bloqueo total: Kohana
arranca bien en Inicio, pero se cierra de inmediato al seleccionar cualquiera de las otras ocho
secciones. Diseño D1 quedó NO aprobado por este defecto.

**Causa raíz (confirmada por evidencia, no por suposición):** el registro de eventos de Windows
provisto por el usuario (`artifacts\Kohana-D1-Navigation-Crash-EventLog.txt`, entrada ".NET
Runtime" Id 1026) contiene el stack trace administrado completo: un
`System.Windows.Markup.XamlParseException` — "No se puede encontrar el recurso con el nombre
'ColorAccentBorder'" — lanzado desde `Border.ArrangeOverride` durante el primer layout de un
control recién insertado. `BrushFocusRing` (junto con `BrushTextMuted` y `BrushError`, con el mismo
patrón) vivía en `Themes/Brushes.xaml` referenciando por `StaticResource` una clave `Color*`
definida en `Themes/Colors.xaml`, un archivo distinto — una referencia cruzada entre diccionarios
que WPF solo resuelve de forma fiable cuando ambas claves están en el mismo archivo. Antes de
Diseño D1 nada consumía `BrushFocusRing`, así que el fallo quedó latente; el primer consumidor real
fue el disparador `IsKeyboardFocused` de `SakuraNavigationItemStyle`/`SakuraSidebarToggleStyle`
introducido en D1, que lo expuso en cuanto el usuario navegaba y el control recién insertado en el
árbol visual recibía el foco — exactamente lo que ocurre en cada cambio de sección.

**Por qué las 24 pruebas de Diseño D1 no lo detectaron:** `DesignSystemResourceTests` verifica que
las claves de recurso existan *textualmente* en algún archivo de tema (regex/`XDocument`, sin cargar
WPF); nunca ejecuta la resolución real de recursos en tiempo de ejecución, así que esta clase de
error era estructuralmente invisible para ese tipo de prueba.

**Corrección:** se movieron `BrushTextMuted`, `BrushError` y `BrushFocusRing` a `Colors.xaml`, junto
a los `Color*` que referencian, para que la resolución sea siempre dentro del mismo archivo.
`Brushes.xaml` se conserva vacío (con comentario) en vez de eliminarse, porque `ThemeResources.xaml`
y `DesignSystemResourceTests` esperan que exista en el orden de fusión. No se tocó ninguna lógica de
navegación, animación, `VoiceCoordinator` ni `AdaptiveEnginePolicy`.

**Pruebas nuevas — `Nexo.App.Tests` (proyecto `UseWPF=true`, nuevo):** las pruebas estructurales de
D1 no ejecutan WPF real, así que no podían servir de regresión para este tipo de fallo. Se añadió un
proyecto de pruebas WPF en un hilo STA (`StaWpfFixture`) que reproduce el mecanismo exacto del
crash: muta `Application.Current.Resources` igual que `ApplyAccent`, fuerza el foco de teclado sobre
un botón con `SakuraNavigationItemStyle`/`SakuraSidebarToggleStyle` (11 pruebas en total, incluye
las 9 vistas reales de producción insertadas y con layout forzado en un host, con fakes mínimos para
`TaskManager`/`FocusManager`/`RoutineManager`/`IAudioMixerService`). Durante el desarrollo de estas
pruebas, una excepción sin observar dentro de un manejador `async void` (`AudioView.Loaded`) colgó
el fixture compartido indefinidamente; se corrigió instalando un manejador de
`DispatcherUnhandledException` en `StaWpfFixture` que captura y relanza esa clase de excepción de
forma determinista en vez de dejar que cuelgue el hilo STA — detalle completo en el informe.

**Validación interactiva real:** se publicó el build corregido
(`artifacts\Kohana-0.9.5-beta-design-d1.1-navigation-hotfix-smoke-win-x64`) y se ejecutó de verdad
mediante automatización de UI (System.Windows.Automation), no solo pruebas de compilación: las
nueve secciones en secuencia, vuelta a Inicio, barra lateral colapsada/expandida, ráfaga de clics
rápidos entre secciones distintas, clics repetidos sobre la sección activa, navegación por teclado
(Enter/Espacio), y ocultar/reabrir (cierre a bandeja + segundo lanzamiento reactivando la misma
instancia vía `SingleInstanceCoordinator`). Kohana permaneció abierto y responsivo en todo momento.
Evidencia completa en `artifacts\design-d1.1\`.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 629 superadas, 0 con error, 0 omitidas
  Nexo.Windows.Tests.dll → 164 superadas, 0 con error, 0 omitidas
  Nexo.App.Tests.dll     →  11 superadas, 0 con error, 0 omitidas (proyecto nuevo)
Total: 804 pruebas (793 previas + 11 nuevas de Diseño D1.1), 0 fallidas, 0 warnings.
Suite completa repetida 5 veces adicionales sin variación (0 fallidas, sin señales de flakiness).
```

**Regresión (breve, sin tocar voz/motor adaptativo):** el diff de la corrección toca únicamente
`Themes/Brushes.xaml` y `Themes/Colors.xaml` — cero líneas en `VoiceCoordinator`,
`AdaptiveEnginePolicy`, motores, atajos globales o `SingleInstanceCoordinator` — por lo que esos
sistemas no pueden haber regresado estructuralmente. La captura real de la sección Sistema tomada
durante la validación interactiva (`shell-sistema-after.png`) confirma en vivo que el estado de voz,
el Hardware Capability Profile (nivel "Acelerado", CPU/RAM/GPU detectados) y el plan del motor
adaptativo (modo Automático, recomendación de Whisper) siguen renderizando con datos reales. Ocultar
a la bandeja, reabrir vía instancia única y el mutex de instancia única se validaron en vivo en el
paso 8 de la validación interactiva (ver arriba). El menú "Salir completamente" de la bandeja del
sistema (`TrayIconController`) no se re-verificó con automatización de UI por la fragilidad de
simular clics sobre coordenadas de la bandeja; ese camino no fue tocado por este hotfix.

**Diseño D1.1 corrige el bloqueo de navegación de Diseño D1. Diseño D1 sigue sin promoverse a
release; Diseño D2 no ha comenzado.** Informe completo en
`artifacts\Kohana-Design-D1.1-Navigation-Hotfix-Informe.md`.

> **Nota posterior (2026-07-25):** el estado descrito en el párrafo anterior corresponde al momento
> en que se escribió, antes del smoke test manual del usuario. La aprobación y la integración
> posteriores se registran en el checkpoint siguiente; este texto se conserva sin reescribir para
> no falsear el historial.

---

### Checkpoint manual — Diseño D1 + D1.1 Sakura Shell aprobado (2026-07-25)

El usuario ejecutó y **aprobó** manualmente el smoke test del build corregido de Diseño D1.1.

| Campo | Valor |
|---|---|
| **Build probado** | `Kohana-0.9.5-beta-design-d1.1-navigation-hotfix-smoke-win-x64.zip` |
| **SHA-256** | `909e5758d3959245b7ca7df615e58ad917d3795a5b69a20c397f5ec1430206d8` |
| **Commit aprobado** | `6e50945` (`docs: record design D1.1 navigation hotfix`) |
| **Pruebas verificadas en el preflight** | 804 (629 Core + 164 Windows + 11 App), 0 fallidas, 0 omitidas, 0 warnings |

**Validación manual confirmada por el usuario (todo correcto):**

- Kohana inicia correctamente.
- Inicio se renderiza correctamente.
- Se puede seleccionar el resto de las secciones.
- **No ocurre ningún crash al navegar** — el defecto bloqueante de Diseño D1 queda resuelto.
- Las configuraciones recomendadas procedentes del Engine Registry aparecen correctamente.
- La información del motor recomendado/configurado se presenta en la aplicación.
- El usuario considera correcta la prueba funcional de D1.1.
- El usuario **autoriza integrar el trabajo aprobado en `release/kohana-1.0-rc`**.

**Estado formal:**

- **Diseño D1 + D1.1 APROBADO por el usuario e integrado en `release/kohana-1.0-rc`** mediante
  merge explícito `--no-ff` (no squash, no rebase, no cherry-pick), conservando la trazabilidad de
  los ocho commits de D1 y los tres de D1.1.
- La rama `design/sakura-shell-v1` se conserva (no se elimina).
- El hotfix D1.1 **ya no está pendiente de smoke manual**.

#### Defecto visual abierto — iconos de navegación seleccionados (asignado a Diseño D2)

Durante el mismo smoke manual el usuario observó un **defecto visual nuevo**, distinto del crash ya
corregido:

- Al seleccionar casi cualquier sección de la barra lateral, el contenedor obtiene correctamente su
  fondo/acento rosa, pero **el icono interior deja de conservar su forma de línea reconocible**.
- El glifo aparece como un pequeño bloque o cuadrado sólido magenta, dentro del cual apenas queda
  visible una marca pequeña.
- Ocurre en casi todos los elementos de navegación; el icono **no** seleccionado sí conserva una
  silueta clara.

**Clasificación:**

- **No produce crash.** No bloquea la navegación ni ninguna funcionalidad.
- **No es comportamiento intencional** — es un defecto real y debe corregirse.
- Queda **abierto como primer defecto de Diseño D2** (tarea D2.0), antes de cualquier otro trabajo
  de ese sprint.

Este defecto no invalida la aprobación funcional de D1.1: el usuario aprobó explícitamente la
corrección del crash y autorizó la integración conociendo este defecto visual pendiente.

---

## Diseño D2 — Sakura Command Center

**Rama:** `design/sakura-command-center-v2`, creada desde `release/kohana-1.0-rc` **ya integrada**
con D1 + D1.1 aprobados (`0e8cef4`). El desarrollo de D2 no ocurre en release.

**D2.0 — corrección del defecto de iconos seleccionados (primera tarea del sprint).**
`SakuraNavigationIconFilledStyle` aplicaba `Fill` + `StrokeThickness="0"` sobre geometrías que son
trazos abiertos, no siluetas rellenables. Medido renderizando con `RenderTargetBitmap`: la cobertura
de tinta pasaba de ~11 % en estado normal a 95.6 % (Sistema), 93.2 % (Hoy) y 79.7 % (Asistente) —
bloques sólidos— y a 0 % en Personalizar, que desaparecía por completo. Corregido expresando el
estado seleccionado con **grosor de trazo** (`SakuraNavigationIconSelectedStyle`, grosores
tokenizados en `Spacing.xaml`); tras la corrección todos quedan entre 5.9 % y 16.5 %, visibles y con
la misma silueta. El estado activo sigue sin depender solo del color: se sustituye una señal no
cromática por otra.

**Sakura Command Center.** Registro de comandos real en `Nexo.Core/Commands/CommandCenter/`
(descriptor con Id estable, disponibilidad evaluada en el momento y ejecución asíncrona que nunca
lanza hacia la UI; registro con Ids únicos; búsqueda que normaliza acentos y prioriza título). La
ventana (Ctrl + K, más un botón visible en el encabezado) solo busca, muestra y traslada teclado.
Convive con la paleta de prompts de IA en Ctrl + Espacio en vez de fusionarse: son cosas distintas.
Ctrl + K es window-level, no `RegisterHotKey`, para no quitarle el atajo a todo el sistema;
`Alt + A`, `Alt + Shift + A`, `Ctrl + Espacio` y `Ctrl + Shift + Espacio` siguen intactos.

**Componentes compartidos de workspace** en `Themes/Controls.xaml` (tarjeta pulsable, métrica, chip
de estado, estados vacío/no disponible/error/carga, barra de herramientas, separador, campo de
búsqueda). Ningún diccionario nuevo; solo `DynamicResource` entre archivos, nunca `StaticResource`
—la causa del crash de D1.1—.

**Persistencia.** No se creó almacén nuevo: `JsonSettingsStore` ya cumplía los requisitos. Se añadió
`ShellPreferences.ResetVisualPreferences()` para "Restaurar apariencia", que devuelve solo lo visual
y conserva tareas, rutinas, voz, IA, motores, integración con Windows y onboarding. Sus propias
pruebas detectaron un defecto antes de entregar: llamar a `Normalize()` arrastraba la escalera de
migración y reasignaba valores funcionales.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 666 superadas
  Nexo.Windows.Tests.dll → 164 superadas
  Nexo.App.Tests.dll     →  71 superadas
Total: 901 pruebas (804 previas + 97 nuevas), 0 fallidas, 0 omitidas, 0 warnings.
Suite completa repetida 5 veces adicionales sin variación ni flakiness.
```

**Limitaciones declaradas.** La **validación interactiva y las capturas no se realizaron**: hay una
instancia de Kohana del usuario en ejecución (PID 9696, el build de D1.1 de su propio smoke manual)
y el mutex de instancia única es de sesión, no de ruta, así que cualquier segundo lanzamiento cede y
termina. No se cerró ni se alteró esa instancia, y no se fabricó ninguna captura. Detalle en
`artifacts\design-d2\interactive-validation-blocked.txt`.

**El rediseño funcional de las nueve vistas (Inicio, Asistente, Hoy, Enfoque, Rutinas, Audio,
Captura) NO se hizo** — es la desviación principal del sprint y queda como trabajo de D3, sobre los
componentes compartidos que D2 deja definidos y probados.

**Diseño D2 pendiente de smoke test manual del usuario. No se declara aprobado.** Informe completo
en `artifacts\Kohana-Design-D2-Sakura-Command-Center-Informe.md`.

> **Nota posterior (2026-07-25):** el estado descrito arriba corresponde al momento en que se
> escribió, antes del smoke test manual del usuario. La aprobación se registra en el checkpoint
> siguiente; este texto se conserva sin reescribir para no falsear el historial.

---

### Checkpoint manual — Diseño D2 Sakura Command Center aprobado (2026-07-25)

El usuario ejecutó y **aprobó** manualmente el smoke test del build D2.

**Validación manual confirmada por el usuario (todo correcto):**

- Kohana inicia correctamente.
- Las nueve secciones abren sin crash.
- **Los iconos seleccionados conservan correctamente su forma** — el defecto de D2.0 queda
  resuelto.
- La barra lateral expandida y compacta funciona.
- **Ctrl + K abre el Sakura Command Center.**
- La búsqueda, flechas, Enter y Escape funcionan.
- La navegación mediante comandos funciona.
- Engine Registry y las recomendaciones del motor aparecen correctamente.
- Las preferencias visuales funcionan.
- No se detectaron cierres, congelamientos, textos cortados ni problemas visibles.
- El usuario **autoriza integrar el trabajo aprobado en `release/kohana-1.0-rc`**.

**Estado formal:**

- **Diseño D2 APROBADO por el usuario para integración.**
- Ya **no está pendiente de smoke manual**.
- La validación interactiva automatizada que había quedado bloqueada (instancia del usuario
  retenía el mutex de instancia única) se completó después de la aprobación, con capturas reales
  de una instancia propia de esta validación — ver
  `artifacts\design-d2\interactive-validation-after-user-approval.txt`.

Detalle completo de la integración en `release/kohana-1.0-rc` en
`artifacts\Kohana-Design-D2.1-Approval-And-Integration-Informe.md`.

---

## Diseño D3 — Sakura Daily Flow v1

**Rama:** `design/daily-flow-v1`, creada desde `release/kohana-1.0-rc` ya integrada con
D1 + D1.1 + D2 (`e2dce83`).

**Hallazgo central del descubrimiento:** Inicio, Hoy, Enfoque y Rutinas ya eran mucho más
funcionales de lo que el sprint asumía — CRUD real con persistencia en las tres, no vistas básicas
a construir desde cero. `NexoTask` ya tenía prioridad, vencimiento y recordatorio; `FocusTimer` ya
persistía con timestamps, no con una cuenta regresiva serializada frágil. El sprint fue de
**conexión y cierre de huecos puntuales**, documentados en `docs/design/SAKURA_DAILY_FLOW_V1.md`.

**Huecos cerrados:** `TaskManager.Reopen` (no existía); confirmación al eliminar una tarea (antes
borraba sin preguntar); asociación tarea↔enfoque (`FocusTimer.TaskId`/`FocusHistoryEntry.TaskId`,
anulables); `FocusManager.Finish` — termina la sesión antes de tiempo contando el tiempo real
transcurrido, distinto de `Cancel` (descarta sin historial); última ejecución y alternar
habilitada/deshabilitada en `RoutineManager` (`RoutineState` sube de esquema v1 a v2); tarjeta de
Rutinas y accesos rápidos en Inicio; corrección de un valor de relleno ("25 min" fijo en la tarjeta
de Enfoque sin ninguna sesión activa, ahora un estado vacío honesto o los minutos acumulados
reales).

**Coordinación:** `DailyFlowEventHub` (sin lógica de dominio) reenvía las señales de cambio ya
existentes a un punto único; `DailyFlowSummaryBuilder` extrae de `MainWindow` el cálculo del
resumen de Inicio a una función pura y comprobable sin WPF.

**Command Center:** se corrigió un defecto de nombres real (`"focus.cancel"` se titulaba
"Finalizar sesión de enfoque" pero llamaba a `Cancel()`); comandos nuevos `focus.pause/resume/
finish` y uno dinámico "Ejecutar `<rutina>`" por cada rutina habilitada, con el registro
reconstruido en cada apertura para no quedar desactualizado.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 689 superadas
  Nexo.Windows.Tests.dll → 164 superadas
  Nexo.App.Tests.dll     → 107 superadas
Total: 960 pruebas (901 previas + 59 nuevas), 0 fallidas, 0 omitidas, 0 warnings.
Suite completa repetida 3 veces adicionales sin variación ni flakiness.
```

**Validación interactiva:** completada sin bloqueos (no había ninguna instancia de Kohana en
ejecución). Recorrido completo con `System.Windows.Automation`: crear tarea → editar → Enfocarme →
iniciar sesión → Inicio refleja la sesión activa → pausar → continuar → finalizar → completar tarea
desde el aviso (nunca automático) → reabrir la tarea → Rutinas (sin ejecutar ninguna: todas tienen
efectos reales) → Command Center → sidebar → variante de acento → ocultar y reabrir con persistencia
confirmada leyendo `%LOCALAPPDATA%\Kohana\tasks.json` directamente. Kohana permaneció responsivo en
todo momento. 10 capturas reales en `artifacts\design-d3\screenshots\`.

**Rendimiento (una muestra, no una serie estadística):** arranque ~1.56 s, memoria en reposo
~290 MiB, cambio de sección ~25 ms, apertura del Command Center ~394 ms.

**Diseño D3 pendiente de smoke test manual del usuario. No se declara aprobado.** Informe completo
en `artifacts\Kohana-Design-D3-Sakura-Daily-Flow-Informe.md`.

> **Aprobación (Diseño D3.2, 2026-07-25):** el usuario probó D3 y confirmó Inicio, Hoy, el CRUD de
> tareas, la asociación tarea↔enfoque, pausar/continuar/finalizar/cancelar, Rutinas y Command
> Center funcionando, sin crash ni pérdida de datos. **Diseño D3 queda aprobado** e integrado en
> `release/kohana-1.0-rc` vía `merge: integrate approved Sakura Daily Flow D3`.

## Diseño D3.1 — Focus Continuity & Daily Flow Polish

**Rama:** `design/focus-continuity-v1`, creada desde `design/daily-flow-v1` (`0e45615`) — D3
confirmado por el usuario como probado y funcionando en ese momento, pero todavía no aprobado ni
integrado.

**Defecto corregido:** el reloj visual de Enfoque podía conservar temporalmente un valor anterior
al cambiar de sección y regresar, corrigiéndose solo en el siguiente tick del `DispatcherTimer` de
fondo (hasta 999 ms de retraso). Causa raíz: `MainWindow.NavigateTo` solo forzaba un refresco
inmediato para el destino `"Home"`; para cualquier otro destino (incluido `"Focus"`) el texto
visible esperaba al siguiente tick. Corregido reemplazando esa condición exclusiva por una llamada
incondicional a `CheckFocusTimer()` (el mismo patrón que ya usaba `HandleSystemResume()`), aplicada
también a `ShowFromBackground()`. Sin segundo `DispatcherTimer`, sin recrear `FocusView`, sin
guardar el contador cada segundo.

**Arquitectura nueva:** `FocusDisplayState`/`FocusDisplayStateBuilder` (Nexo.App/DailyFlow) — única
fuente pura del reloj/estado/etiqueta/comandos disponibles, usada ahora por FocusView, el nuevo
mini temporizador global y la tarjeta de Enfoque en Inicio (antes cada una calculaba por su
cuenta). `FocusMiniTimer` (control WPF) + `FocusContinuityCoordinator` (sin timer propio, se
refresca desde el mismo tick existente) — mini temporizador visible en el encabezado del shell en
cualquier sección salvo la propia Enfoque, sin botón de Cancelar de un clic. `FocusManager.
GetHistory()` + `FocusHistorySummaryBuilder` — actividad reciente y tarea con más tiempo hoy, solo
cuando es honestamente calculable, sin rachas ni puntajes inventados. `FocusOperationResult.
Completion` — aviso de fin de sesión generalizado a toda finalización real (natural o manual, con
tarea o sin ella), nunca para Cancelar.

**Command Center:** `focus.open` + `focus.start.15/25/45` (inicio real por preset, antes solo
navegaba) + `focus.history`; Cancelar/Pausar/Continuar ahora refrescan también el mini temporizador
y la tarjeta de Inicio de inmediato.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 696 superadas
  Nexo.Windows.Tests.dll → 164 superadas
  Nexo.App.Tests.dll     → 148 superadas
Total: 1008 pruebas (960 previas + 48 nuevas), 0 fallidas, 0 omitidas, 0 warnings.
Suite completa repetida 3 veces adicionales sin variación ni flakiness.
```

**Validación interactiva:** recorrido de 25 pasos con `System.Windows.Automation` contra el
publicado real: iniciar sesión asociada a una tarea → confirmar reloj → mini temporizador visible y
avanzando en Inicio y Hoy → pausar (estable) → continuar → regresar a Enfoque con valor correcto de
inmediato → ocultar a bandeja y reactivar por segundo lanzamiento (cede, valor correcto de
inmediato) → finalizar desde el mini temporizador → aviso de finalización con tarea asociada →
completarla → Command Center (presets visibles y disponibles) → iniciar 25 min → sidebar compacta →
cancelar (confirmado que no queda en el historial). Kohana permaneció responsivo en todo momento.
10 capturas reales en `artifacts\design-d3.1\screenshots\`.

**Diseño D3.1 pendiente de smoke test manual del usuario. No se declara aprobado.** Informe
completo en `artifacts\Kohana-Design-D3.1-Focus-Continuity-Informe.md`.

> **Aprobación (Diseño D3.2, 2026-07-25):** el usuario probó D3.1 y confirmó el refresco inmediato
> al regresar a Enfoque, el mini temporizador avanzando al cambiar de sección, la pausa estable, la
> reactivación correcta desde bandeja, e historial/resumen/presets funcionando, sin crash ni
> regresiones. **Diseño D3.1 queda aprobado** e integrado en `release/kohana-1.0-rc` vía
> `merge: integrate approved Focus Continuity D3.1`.

## Diseño D3.2 — Approved Release, Validation Sandbox & Product Technology Roadmap

**Alcance:** registrar la aprobación de D3 y D3.1 (ver secciones anteriores), integrarlos en
`release/kohana-1.0-rc`, aislar los datos de futuras validaciones interactivas del perfil real del
usuario, y formalizar la visión de producto, el roadmap tecnológico, la arquitectura de capacidades
y el modelo de confianza de Kohana. Kohana Lens, Flow, Sakura Pills, optimización adaptativa y
Computer Use quedan diseñados y planeados en este sprint, **no** implementados.

**Validation Data Sandbox:** `NexoDataPaths.RootDirectory` (`src/Nexo.Core/Diagnostics/
NexoDataPaths.cs`) resuelve ahora, en orden, un override explícito de proceso, la variable de
entorno `KOHANA_DATA_ROOT`, o la ruta de producción real sin cambios — todos los stores ya
derivaban de esa única propiedad, así que ningún store necesitó tocarse individualmente. 28 pruebas
nuevas (`NexoDataPathsTests`, `ValidationProfileIsolationTests`) cubren precedencia, rutas
inválidas, y aislamiento entre perfiles. `scripts/New-KohanaValidationProfile.ps1` crea perfiles
aislados bajo `artifacts/validation-profiles/` y lanza Kohana con la variable fijada solo para el
proceso hijo.

**Integración:** `merge: integrate approved Sakura Daily Flow D3` seguido de
`merge: integrate approved Focus Continuity D3.1`, ambos `--no-ff`, sin conflictos. Release pasó de
`e2dce83` a `72afa54`.

**Build y pruebas (Release), tras los merges:**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 715 superadas
  Nexo.Windows.Tests.dll → 173 superadas
  Nexo.App.Tests.dll     → 148 superadas
Total: 1036 pruebas (1008 previas + 28 nuevas), 0 fallidas, 0 omitidas, 0 warnings.
Suite completa repetida 3 veces adicionales tras los commits de documentación, sin flakiness.
```

**Validación del perfil aislado:** contra el checkpoint publicado, con un perfil de validación
real: tarea identificable creada, sesión de enfoque de 1 minuto iniciada y finalizada, acento
visual cambiado — los tres cambios verificados dentro del perfil aislado. Snapshot SHA-256 de los
12 archivos de `%LocalAppData%\Kohana` idéntico antes y después: 0 diferencias, aislamiento
confirmado con evidencia real, no fabricada.

**Documentación de producto:** `docs/product/SAKURA_PRODUCT_VISION.md`,
`docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md` (fases 0–9, solo la Fase 0 marcada implementada),
`docs/architecture/SAKURA_CAPABILITY_ARCHITECTURE.md` (12 capas con diagrama Mermaid),
`docs/security/SAKURA_TRUST_AND_AUTONOMY_MODEL.md`, `docs/roadmap/SAKURA_CAPABILITY_MATRIX.md`.
Próximo sprint grande recomendado: **D4 — Ambient Interaction Foundation**.

**Publicación:** `artifacts\Kohana-0.9.8-beta-d3-approved-checkpoint-win-x64\`, comprimido en
`Kohana-0.9.8-beta-d3-approved-checkpoint-win-x64.zip` (92 138 182 bytes), SHA-256
`82863a97436064b3ee6a7a8063ec180e62c139e35956b03631d1c1380e39b5d8`.

**Diseños D3 y D3.1 quedan aprobados por el usuario e integrados en `release/kohana-1.0-rc`.**
Diseño D3.2 (aislamiento, documentación de producto y roadmap) no se declara aprobado en nombre del
usuario — es trabajo de infraestructura y documentación, pendiente de que el usuario lo revise si
así lo decide. Informe completo en
`artifacts\Kohana-Design-D3.2-Release-And-Roadmap-Informe.md`. No se hizo push, no se abrió PR, no
se hizo merge a `main`.

---

## Diseño D4 — Ambient Interaction Foundation (D4.1 + D4.2)

**Rama:** `design/ambient-interaction-v1`, creada desde `release/kohana-1.0-rc` (`5885249`, ya con
D3.2 integrado). Implementa el comienzo de la Fase 1 del roadmap tecnológico
(`docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md`), sprint sugerido D4 en esa misma sección.

**Alcance de esta sesión:** dos pasos compilables y probados por separado, siguiendo la misma
disciplina de la Fase 1 de `STABLE_RELEASE_PLAN.md` (paso pequeño → compila → pruebas → commit).

### D4.1 — Modelo de dominio y almacenamiento

`Nexo.Core/Ambient/` — ciclo de vida puro de una solicitud ambiental (`AmbientRequestStatus`:
Escuchando/Pensando/Resultado/Cancelada/Error), `AmbientRequestManager` (misma forma que
`FocusManager`: `Begin/BeginThinking/CompleteWithResult/Fail/Cancel/Dismiss/Undo`, con archivado
automático al historial al iniciar una solicitud nueva sobre una anterior ya terminal),
`AmbientPermissionPolicy` (primitivas de permiso — niveles Ver/Guiar/Proponer del modelo de
confianza, `IsAllowed` falla cerrado ante cualquier nivel no reconocido explícitamente, para no
escalar autonomía en silencio si una fase futura añade niveles de ejecución al mismo enum sin
revisar esta política). `JsonAmbientRequestHistoryStore` en `Nexo.Windows/Ambient/` sigue el mismo
patrón atómico (`.tmp` + `File.Move`) y de preservación de archivo corrupto que `JsonFocusStore`.
Nueva ruta `NexoDataPaths.AmbientRequests` (`ambient-requests.json`).

23 pruebas nuevas (`AmbientRequestManagerTests`, `AmbientPermissionPolicyTests`), más una prueba y
un renombrado (`AllFourStores` → `AllStores`) en `ValidationProfileIsolationTests` para cubrir el
nuevo store bajo un perfil de validación aislado. 1059 pruebas totales, 0 fallidas, 0 warnings,
suite repetida 3 veces sin flakiness.

### D4.2 — Sakura Pill Host, Context Snapshot y disparador en el Command Center

`SakuraPillWindow` (`Nexo.App`) — ventana no activable: mismo mecanismo que `CapsuleWindow`
(`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` en `OnSourceInitialized`), pero a diferencia de esta (un
aviso transitorio que se autodescarta) admite interacción real — cancelar, descartar, deshacer,
hasta dos acciones rápidas por resultado, expandir/contraer — sin activarse ni robar el foco de la
ventana con la que el usuario esté trabajando. `AmbientRequestDisplayState`/`AmbientRequestDisplay-
StateBuilder` (`Nexo.App/Ambient/`) son función pura, sin WPF, mismo rol que `FocusDisplayState`/
`FocusDisplayStateBuilder` tienen para Enfoque. `SakuraPillCoordinator` cablea la ventana con el
manager, mismo rol que `FocusContinuityCoordinator`; `MainWindow.CheckAmbientRequest()` es el punto
único de refresco, llamado tras cada mutación, igual que `CheckFocusTimer()`.

**Context Snapshot real:** `IAmbientContextProvider`/`WindowsAmbientContextProvider` leen título y
proceso de un handle de ventana nativo (reutilizando `VisionPrivacyPolicy.IsSensitive` para no
exponer ventanas marcadas como sensibles), alimentados por `_lastExternalWindowHandle` — el mismo
campo que Vision ya usa para "la última ventana externa antes de que Kohana tomara el foco" — sin
inventar una segunda ruta de captura. Es deliberadamente más ligero que
`WindowsScreenCaptureService` (no enumera todas las ventanas, no captura píxeles): D4 nunca observa
contenido de pantalla, solo metadatos.

**Command Center:** nuevo comando `ambient.contextPeek` ("¿Qué ventana tengo activa?", categoría
`KohanaCommandCategory.Ambient` nueva) recorre el ciclo real Escuchando → Pensando →
Resultado/Error contra el Context Snapshot capturado; sin ningún procesamiento de IA todavía (eso
es Lens/Flow, Fases 2-3 del roadmap) — el resultado es honesto sobre lo que Kohana puede observar
hoy (título/proceso), incluida una acción rápida "Copiar" que copia el resultado al portapapeles.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 737 superadas
  Nexo.Windows.Tests.dll → 176 superadas
  Nexo.App.Tests.dll     → 155 superadas
Total: 1068 pruebas (1059 previas + 9 nuevas), 0 fallidas, 0 omitidas, 0 warnings.
Suite completa repetida 3 veces adicionales sin variación ni flakiness.
```

**Validación interactiva — intento honesto, no completado:** se escribió
`artifacts\design-d4.2\validate-ambient-pill.ps1` (no versionado, gitignored junto con el resto de
`artifacts/`) para automatizar, vía `System.Windows.Automation`, un perfil de validación aislado
(`KOHANA_DATA_ROOT` propio) que abriera el Command Center, buscara "ventana", ejecutara el comando
nuevo y verificara que el pill aparece sin robar el foco. **No se logró completar de forma
fiable en el entorno sandbox de esta sesión**: `SetForegroundWindow` y `GetForegroundWindow`
confirmaban que `MainWindow` tenía foco real en el momento del clic sintético
(`SetCursorPos`/`mouse_event`), pero la ventana del Command Center nunca apareció de forma
consistente en el árbol de UI Automation tras el clic — ni con `InvokePattern.Invoke()` ni con un
clic de mouse sintetizado sobre las coordenadas reales del botón. No se determinó con certeza si es
una limitación de inyección de entrada de este entorno concreto (posible sesión sin escritorio
interactivo real) u otra causa; no se investigó más allá de lo documentado aquí para no seguir
gastando tiempo en un problema de entorno ajeno al código de esta sesión. **No se declara D4
validado end-to-end de forma interactiva.** Lo que sí queda verificado con evidencia real: Kohana
arranca sin excepciones con los nuevos campos/servicios cableados en el constructor de
`MainWindow` (una clase de más de 4700 líneas) — `MainWindow` se localizó correctamente vía UI
Automation en cada intento, sin crash del proceso.

**Pendiente, no bloqueante:**
- Auditoría: las entradas de historial (`AmbientRequestHistoryEntry`, con `CanUndo`/`Undone`) ya
  registran qué pasó y cuándo, cumpliendo un "audit básico" honesto para esta fase — el Audit Log
  completo orientado al usuario (capa 11 de `SAKURA_CAPABILITY_ARCHITECTURE.md`) sigue siendo
  trabajo de la Fase 7, no se adelanta aquí.

### D4.4 — Historial de solicitudes visible

`AmbientRequestManager.GetHistory()` ya persistía las solicitudes archivadas desde D4.1 (usado
internamente para el archivado automático), pero no había ninguna superficie visible. Se agregó:
`AmbientRequestHistoryItem`/`AmbientRequestHistorySummaryBuilder` (función pura, más reciente
primero, honesta — una solicitud fallida muestra su mensaje de error, nunca un resultado
inventado), `AmbientHistoryWindow` (ventana normal, activable, a diferencia del pill) con un botón
"Deshacer" por entrada cuando su resultado declaró `CanUndo` y todavía no se deshizo — cierra el
hueco donde Deshacer solo funcionaba sobre la solicitud visible en el momento, nunca sobre el
historial. Nuevo comando del Command Center `ambient.history` ("Ver historial de solicitudes
ambientales").

8 pruebas nuevas, 1078 en total, 0 fallidas, 0 warnings, suite repetida 3 veces sin flakiness.
Pendiente de smoke test manual del usuario, igual que D4.1/D4.2.

### Smoke test manual del usuario (2026-07-28) y correcciones

El usuario probó D4.1/D4.2 manualmente (la validación interactiva que no se pudo completar por
automatización, ver arriba) y encontró dos defectos reales:

1. **El pill nunca se cerraba solo** — no tenía temporizador propio, a diferencia de
   `CapsuleWindow`. Corregido con un `DispatcherTimer` de auto-descarte de 8 segundos en
   `SakuraPillWindow`, que dispara `DismissRequested` (pasa por el manager, no un `Hide()` suelto,
   para no dejar el estado interno inconsistente) y se reinicia si el usuario interactúa
   (expandir, una acción rápida) para no cortarlo a media lectura.
2. **Volver a ejecutar el comando tras cambiar de ventana seguía mostrando la ventana anterior** —
   causa raíz: reutilizaba `MainWindow._lastExternalWindowHandle`, que solo se actualiza en puntos
   concretos ya existentes para Vision/Peek (`RememberForegroundWindow()`), nunca con un Alt+Tab
   normal ni al abrir el Command Center (que solo puede abrirse con Kohana ya enfocada). Corregido
   con `ForegroundWindowTracker` (`Nexo.Windows/Ambient/`), un listener propio de
   `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` que mantiene el último handle ajeno al proceso
   actualizado en tiempo real, sin tocar el mecanismo existente de Vision.

2 pruebas nuevas, 1070 en total, 0 fallidas, 0 warnings, suite repetida 3 veces sin flakiness.

**Aparte, en la misma sesión:** el usuario reportó de paso que el CI de GitHub estaba en rojo en
`main` (PR #26 ya mergeado). Se rastreó a un bug de zona horaria no relacionado con D4:
`DailyFlowSummaryBuilder` calculaba el saludo con `now.LocalDateTime.Hour` (reinterpreta el
`DateTimeOffset` de entrada en la zona horaria de la máquina que ejecuta el código) en vez de
`now.Hour` (la hora ya fijada en el propio valor, sin reconversión) — pasaba en un equipo en
UTC-6 pero fallaba en los runners de GitHub Actions (UTC). Corregido en `release/kohana-1.0-rc`
(`ea351ac`), empujado, y traído a `main` vía
[PR #27](https://github.com/EXOTARA/Sakura/pull/27) (mergeado, CI en verde confirmado). El mismo
commit se trajo también a `design/ambient-interaction-v1` por merge, para no arrastrar el bug.

> **Confirmación del usuario:** tras las dos correcciones del pill, el usuario volvió a probarlo y
> confirmó "se ve bien". D4.1 y D4.2 quedan validados interactivamente por el usuario.

> **Aprobación (2026-07-28):** el usuario probó también D4.4 (historial de solicitudes) y confirmó
> de nuevo "se ve bien". **Diseño D4 (D4.1 + D4.2 + D4.4) queda aprobado por el usuario** e
> integrado en `release/kohana-1.0-rc` vía `merge: integrate approved Ambient Interaction
> Foundation D4` (`7ce4635`). Build Release 0 warnings, 1078 pruebas, 0 fallidas, suite repetida 3
> veces sin flakiness tras el merge.

**No se hizo push a `release/kohana-1.0-rc` con el merge de D4, ni se abrió PR ni se hizo merge a
`main`.** (El hotfix de zona horaria sí se empujó y mergeó a `main` por separado, ver arriba — es
un cambio independiente de D4, ya integrado antes de este merge.)

---

## Diseño D5 — Kohana Lens

**Rama:** `design/kohana-lens-v1`, creada desde `release/kohana-1.0-rc` (`f279e18`, D4 ya integrado).
Implementa el comienzo de la Fase 2 del roadmap tecnológico
(`docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md`), siguiendo el orden sugerido por ese mismo documento:
"Lens: captura y OCR" primero, "Lens: guía visual y modos" después (todavía no iniciado).

### D5.1 — Migración de TFM a `net10.0-windows10.0.26100.0`

Ejecuta la decisión ya tomada en `ADR 0003`: el TFM `net10.0-windows` no da acceso a WinRT, lo que
bloqueaba `Windows.Media.Ocr`. Se cambió `Nexo.Windows`, `Nexo.App` y sus dos proyectos de pruebas;
`Nexo.Core` permanece en `net10.0` puro, sin cambios, tal como exige el mismo ADR. Cambio de TFM
puro, sin ningún cambio de comportamiento: compiló sin tocar código, la suite completa siguió en
verde, y un arranque manual de `Kohana.exe` recompilado confirmó que la app sigue iniciando con
normalidad en esta máquina (Windows 11 Pro build 26200, ya cumple el nuevo mínimo de 26100+).

### D5.2 — Servicio de OCR real sobre `Windows.Media.Ocr`

`IOcrService`/`OcrResult`/`OcrTextLine` en `Nexo.Core.Vision` + `WindowsOcrService` en
`Nexo.Windows.Vision`, desbloqueado por D5.1. Recibe los mismos bytes PNG que ya produce
`IScreenCaptureService` y devuelve el texto completo más una caja delimitadora por línea (unión de
las cajas de cada palabra — suficiente para un resaltado visual futuro sin necesitar precisión por
palabra todavía). Nativo de Windows: sin modelo que descargar, sin dependencia nueva, tal como
anticipaba el ADR. Falla honestamente (`IsSuccess = false` con un `Detail` explicativo) cuando el
equipo no tiene ningún paquete de idioma de reconocimiento instalado (función opcional de Windows,
no garantizada en toda instalación) — no se asume que el motor exista, se comprueba.

**Verificación real, no simulada:** se probó contra el motor real de Windows (no un doble):
renderizar una imagen con el texto "KOHANA KANBAN" y confirmar que se reconoce exactamente —
verificado manualmente con una salida de depuración temporal (`FullText=KOHANA KANBAN`) antes de
quitarla, para no dejar la prueba dependiendo de una intuición sobre qué rama del código se
ejecutó.

**Build y pruebas (Release):**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 737 superadas
  Nexo.Windows.Tests.dll → 181 superadas
  Nexo.App.Tests.dll     → 162 superadas
Total: 1080 pruebas (1078 previas + 2 nuevas — el resto son de D4), 0 fallidas, 0 omitidas,
0 warnings. Suite completa repetida 3 veces sin flakiness.
```

### D5.3 — Lectura de UI Automation (solo lectura)

`IUiAutomationReader`/`UiAutomationElement`/`UiAutomationSnapshot` en `Nexo.Core.Vision` +
`WindowsUiAutomationReader` en `Nexo.Windows.Vision`, primer uso en código de producción de UI
Automation sobre una ventana ajena (hasta ahora solo existía en arneses de prueba y como
`AutomationProperties` de los propios controles de Kohana). Complementa el OCR: donde el OCR ve
píxeles, esto ve la estructura semántica que la propia aplicación observada expone — solo lectura,
ninguna acción sobre la ventana leída. Recorrido acotado a propósito (`TreeWalker.ControlViewWalker`,
máximo 300 elementos, profundidad máxima 6): el árbol de una app ajena (un navegador, un IDE) puede
tener miles de nodos, y esto es contexto para Lens, no un inventario exhaustivo. Requirió una
`FrameworkReference` a `Microsoft.WindowsDesktop.App` en `Nexo.Windows` (sin `UseWPF`) — NuGet marcó
como redundantes dos `PackageReference` (`System.Diagnostics.PerformanceCounter`,
`System.Drawing.Common`) ya cubiertos por ese framework compartido, y se quitaron. Probado contra
una ventana WPF real (no un doble): un botón con nombre conocido aparece con límites válidos.

### D5.4 — Redacción de contenido sensible (texto e imagen)

`VisionPrivacyPolicy` ya excluía ventanas completas por proceso/título, pero una ventana que pasa
esa exclusión puede mostrar igual un dato sensible puntual (una contraseña escrita en un campo, un
número de tarjeta en un formulario) — el riesgo que el propio roadmap documentaba sin resolver
("requiere exclusiones y redacción antes de cualquier envío a un proveedor externo").
`SensitiveContentRedactor` (`Nexo.Core.Vision`) redacta solo el fragmento sensible, no la línea
completa: campos etiquetados como contraseña, números de tarjeta (candidato detectado y confirmado
con suma de Luhn real, para no marcar cualquier secuencia larga de dígitos), SSN, y tokens de
secretos (prefijos conocidos como `sk-`/`ghp_`/`AKIA` más una heurística genérica para tokens sin
prefijo que mezclan mayúsculas, minúsculas y dígitos). Sesgado a favor de redactar de más: un falso
positivo es una molestia menor, un secreto real sin redactar es una fuga.

**Extensión — redacción también en los píxeles:** redactar el texto no oculta la imagen: los mismos
píxeles sensibles seguirían viajando intactos dentro del `AiImageAttachment`. `ImageRedactor`
(`Nexo.Windows.Vision`) tapa con un rectángulo sólido (no difuminado — un desenfoque a veces se
puede revertir, un rectángulo negro no) las regiones que
`SensitiveContentRedactor.FindSensitiveLines` marcó como sensibles, usando las cajas delimitadoras
reales que ya devuelve el OCR. Verificado de punta a punta contra el motor real: renderizar
"Password: hunter2secreto", confirmar que el OCR lo lee, redactar la imagen, y volver a correr OCR
sobre la imagen ya redactada confirmando que el secreto desaparece mientras la línea vecina sigue
siendo legible.

### D5.5/D5.6 — Los tres modos, el indicador "Mirando" y la integración completa con la IA

Antes de conectar todo, se investigó cuánto del flujo "capturar pantalla → mandarla a la IA" ya
existía, para no duplicarlo: `AiImageAttachment`/`AiChatRequest.Images`/`AiRequestMode` y ambos
adaptadores de proveedor (Ollama nativo y compatible con OpenAI) **ya enviaban imágenes de punta a
punta** desde antes de este sprint — Lens no necesitó construir nada de eso, solo decidir cuándo
adquirir una imagen y qué hacer con el análisis.

`LensMode` (Soporte/Estudio/Desarrollo) + `LensContext`/`LensContextBuilder` (`Nexo.Core.Vision`,
función pura) arman, a partir del OCR y los elementos de UI Automation YA redactados, la pregunta
por defecto y el contexto de cada modo. Cada modo se traduce al `AiRequestMode` ya existente en vez
de inventar uno paralelo: Desarrollo siempre `VisionTechnicalDiagnostic`, Estudio siempre
`VisionGeneral`, Soporte decidido por `VisionIntentPolicy` a partir del texto de OCR (igual que ya
decidía para el chat normal).

Tres comandos nuevos del Command Center —`lens.soporte`, `lens.estudio`, `lens.desarrollo`— siguiendo
el mismo patrón que los presets de Enfoque (`focus.start.15/25/45`). Cada uno: captura la ventana
activa (usando `ForegroundWindowTracker` de D4, no el `_lastExternalWindowHandle` propio de Vision
—ese solo se actualiza en puntos concretos y hubiera quedado obsoleto para un disparo desde el
Command Center, el mismo problema que D4 ya tuvo y corrigió—), corre OCR y UI Automation, redacta
ambos (texto e imagen), arma el contexto del modo, y pregunta a la IA. **El resultado se muestra en
el mismo Sakura Pill Host de D4** (`AmbientRequestManager`) — Lens es, en esencia, otra fuente de
solicitudes ambientales, no una superficie nueva que construir.

**Indicador "Mirando":** el modelo de confianza exige que este estado tenga su propio indicador
visible (`docs/security/SAKURA_TRUST_AND_AUTONOMY_MODEL.md`) — antes de este sprint no existía nada
persistente, solo un aviso transitorio. Se agregó `LensIndicator` en el encabezado del shell (junto
al indicador existente de palabra de activación), visible mientras dura la captura y el análisis,
oculto el resto del tiempo.

**Build y pruebas (Release), acumulado D5.1-D5.6:**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 768 superadas
  Nexo.Windows.Tests.dll → 183 superadas
  Nexo.App.Tests.dll     → 164 superadas
Total: 1115 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado con un arranque manual que la app sigue iniciando con normalidad con los campos y el XAML
nuevos ya cableados.

> **Confirmación del usuario (D5.1-D5.6):** el usuario probó los tres modos de Lens manualmente y
> confirmó "funciona bien; el concepto está excelente". Dos observaciones de fricción registradas
> como retroalimentación, no como defectos: (1) el Command Center (Ctrl+K) solo funciona con
> Kohana ya enfocada — comportamiento conocido y documentado (D4), no un bug de Lens; (2) idea para
> el futuro — animaciones del pill y expansión automática con el contenido "escribiéndose" conforme
> la IA piensa (streaming) en vez de esperar la respuesta completa. Ninguna de las dos se atiende en
> este sprint; la segunda queda registrada como mejora de UX futura para el Sakura Pill Host.

### D5.7 — Resaltado visual sobre pantalla

Última pieza del criterio de terminado de la Fase 2. `LensHighlightMatcher` (`Nexo.Core.Vision`,
función pura) decide qué resaltar: no existe un mecanismo de citas estructuradas entre la IA y la
captura (eso exigiría cambiar el formato del prompt de forma más profunda), así que esta primera
versión usa una heurística honesta y simple — una línea de OCR o el nombre de un elemento de UI
Automation se resalta si aparece tal cual (sin distinguir mayúsculas ni acentos) dentro de la
respuesta de la IA. Es guía visual aproximada, no una prueba de que "esto es exactamente a lo que
se refiere la IA" — documentado así en el propio código.

`UiAutomationSnapshot` ahora también carga los límites reales de la ventana observada (UI
Automation ya los da gratis, son el `BoundingRectangle` del elemento raíz) para traducir las
coordenadas de OCR (relativas a la captura) a posición absoluta de pantalla; las coordenadas de UI
Automation ya son absolutas, así que pasan sin cambios. `VisionIntentPolicy.Normalize` se hizo
público para que el emparejador reutilice la misma normalización de mayúsculas/acentos en vez de
duplicarla.

`LensHighlightOverlay` (`Nexo.App`): mismo patrón no activable que `CapsuleWindow`/
`SakuraPillWindow`, más `WS_EX_TRANSPARENT` — esta ventana cubre el área completa de la ventana
observada, así que sin ese estilo bloquearía los clics reales dirigidos a la app de abajo. Se
autodescarta a los 6 segundos; nunca intercepta entrada, solo dibuja. Se muestra justo después de
un resultado exitoso de la IA en `ExecuteLensAsync`; se omite por completo si UI Automation no
devolvió límites válidos.

**Build y pruebas (Release), acumulado D5.1-D5.7:**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 778 superadas
  Nexo.Windows.Tests.dll → 183 superadas
  Nexo.App.Tests.dll     → 164 superadas
Total: 1125 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado con un arranque manual que la app sigue iniciando con normalidad con la superposición ya
cableada. **Con D5.1-D5.7, el criterio de terminado completo de la Fase 2 está cubierto.**

> **Aprobación (2026-07-28):** el usuario probó el resaltado visual (D5.7) y confirmó de nuevo "se
> ve bien". **Diseño D5 (D5.1-D5.7, Kohana Lens completo) queda aprobado por el usuario** e
> integrado en `release/kohana-1.0-rc` vía `merge: integrate approved Kohana Lens D5` (`1ab8871`).
> Build Release 0 warnings, 1125 pruebas, 0 fallidas, suite repetida 3 veces sin flakiness tras el
> merge. Fase 2 (Kohana Lens) pasa a **Implementada** en el roadmap.

**No se hizo push a `release/kohana-1.0-rc` con el merge de D5, ni se abrió PR ni se hizo merge a
`main`.**

---

## Diseño D6 — Kohana Flow (dictado global)

**Rama:** `design/kohana-flow-v1`, creada desde `release/kohana-1.0-rc` (`3180eec`, D5 ya integrado).
Implementa la Fase 3 del roadmap tecnológico, sprint sugerido "Flow: dictado global v1".

**Qué hace:** `Ctrl + Shift + D` en cualquier parte de Windows empieza a dictar; la misma
combinación otra vez transcribe y **escribe el texto en la aplicación que estuviera activa**.

### Decisión: interruptor, no "mantener presionado"

`RegisterHotKey` solo avisa de la pulsación, nunca del soltado. Un push-to-talk literal exigiría un
hook de teclado de bajo nivel (`WH_KEYBOARD_LL`) — la misma API que usan los registradores de
teclas, que dispara falsos positivos de antivirus (preocupación ya registrada del propietario) y
encaja mal con el empaquetado MSIX para la Store. Además, el valor que el propio roadmap le asigna a
Flow es *"escribir texto largo por voz"*, y sostener una tecla dos minutos es peor experiencia que
alternar. El botón de micrófono que ya existía en el Asistente también es un interruptor, así que
esto es consistente con lo que ya había, no una excepción.

### D6.1 — Normalizador de dictado

`Nexo.Core/Flow/`: `SpanishDictationNormalizer` convierte la transcripción CRUDA de Whisper en texto
insertable — puntuación hablada, muletillas, diccionario personal, atajos, mayúscula de oración — y
los tres modos que exige el criterio de terminado de la fase (texto/correo/código), cada uno con una
diferencia real de comportamiento, no una etiqueta cosmética.

Deliberadamente separado de `SpanishVoiceTranscriptNormalizer`: aquel normaliza para el motor de
COMANDOS y es destructivo por diseño (minúsculas, sin acentos, sin puntuación). Correcto para
"abre powershell", inservible para dictar un correo.

**Tres decisiones de criterio, todas cubiertas por pruebas:**
- La lista de muletillas solo contiene sonidos de duda sin significado ("eh", "mmm"…). Excluye a
  propósito "este", "o sea", "bueno", "pues": se usan como muletilla pero también son palabras
  reales, y borrar en silencio una palabra que la persona sí quiso decir es peor que dejar una
  muletilla.
- Los guiones NO se pegan a las palabras vecinas. Whisper escribe sus propios guiones como inciso en
  prosa ("esto - aquello") y pegarlos corrompería texto que ya estaba bien; un "kebab - case" mal
  separado es más raro y se ve de inmediato.
- La reconstrucción de dominios ("adler arroba gmail punto com" → `adler@gmail.com`) exige un TLD
  conocido después del punto, porque el punto de un dominio y el de fin de oración son el mismo
  carácter. Así "adler@ejemplo.com. luego te escribo" conserva su punto final en vez de convertirse
  en "…com.luego".

**Dos defectos reales que las pruebas atraparon antes del commit:** la capitalización convertía
`adler@gmail.com` en `adler@gmail. Com` (la comprobación de correo solo miraba hacia adelante y se
perdía una arroba que quedaba detrás), y los dominios de varios niveles necesitaban varias pasadas
porque `Regex.Replace` reanuda después de cada coincidencia.

### D6.2 — Inserción universal con guardia de foco

`IFlowTextInserter` + `WindowsFlowTextInserter`, sobre `SendInput` con `KEYEVENTF_UNICODE` (entrega
el carácter Unicode directamente, así que acentos y "ñ" funcionan sin depender de la distribución de
teclado). **Es el primer y único punto de todo Kohana que envía entrada a otro programa**; antes de
D6 no existía ninguno.

Tres negativas antes de enviar una sola tecla, cada una con su propio motivo para que la capa de
aplicación responda distinto:
1. Sin ventana destino recordada → no se escribe a ciegas.
2. **El foco cambió desde que empezó el dictado → no se escribe.** Es exactamente el riesgo que el
   roadmap señala para esta fase; dictar un mensaje privado dentro de la ventana equivocada es un
   daño real e irreversible.
3. Ventana marcada como sensible (gestor de contraseñas, diálogo de credenciales) → tampoco,
   reutilizando la misma `VisionPrivacyPolicy` que ya protege a Lens.

Todo el lote va en UNA sola llamada a `SendInput`, para que la secuencia sea atómica y las
pulsaciones reales del usuario no se intercalen en el texto dictado.

**Sobre las pruebas de esta clase:** solo se prueban los casos en que el insertor SE NIEGA a
escribir, a propósito. Una prueba del camino feliz llamaría a `SendInput` de verdad, y `SendInput`
escribe en la ventana que tenga el foco mientras corre la suite — en la máquina de desarrollo o en
CI. Una prueba que teclea dentro de una ventana ajena no es una prueba, es un efecto secundario. Las
negativas son además la mitad crítica de seguridad y sí son deterministas.

### D6.3 — Atajo global, orquestación e indicador "Dictando"

**Bloqueador que hubo que abrir primero:** la transcripción aplicaba el normalizador de comandos de
forma incondicional. Se añadió un `VoiceTranscriptionMode` OPCIONAL, propagado por
`IVoiceInputService`/`IVoiceInputScope`/`VoiceCoordinator`, con valor predeterminado `Command` para
que ningún llamador existente cambie de comportamiento; en `Dictation` se devuelve el texto de
Whisper intacto y se usa un prompt orientado a prosa en vez del orientado a órdenes.

El dictado tiene su propia rama de resultado y **nunca** pasa por `ProcessPromptAsync`: dictar "abre
Spotify" dentro de un correo debe escribir esas palabras, no abrir Spotify. Si no se pudo escribir,
el texto no se tira — se copia al portapapeles —, **salvo** cuando la ventana destino era sensible:
ahí lo dictado pudo ser una contraseña, y dejarla en el portapapeles (accesible a cualquier otro
programa) sería peor que perderla. Nuevo indicador "Dictando" en el encabezado, por la regla del
modelo de confianza de que un estado de escucha debe ser visible.

Preferencias al esquema v18 (`FlowEnabled`, `FlowMode`, listas de diccionario y atajos), siguiendo
el escalón existente y el precedente de v16: una migración añade valores por omisión pero nunca
borra los que ya estaban. Unas 23 pruebas de la suite fijaban el número de esquema anterior;
describen "Normalize migra al actual", que sigue siendo cierto, así que se actualizó el valor
esperado, no el comportamiento.

**Corrección de una prueba intermitente propia (de D5.4):** la prueba de retorno temprano de
`ImageRedactor` renderizaba un PNG que no necesitaba, y System.Drawing/GDI+ falla de vez en cuando
bajo la ejecución en paralelo de xUnit (~1 de cada 10 corridas de la suite completa, nunca aislada).
Se quitó el renderizado innecesario en vez de reintentar o silenciar el fallo. De paso se documentó
por qué `ImageRedactor` **no** captura los fallos de GDI+: devolver la imagen original entregaría
intactos justo los píxeles que se querían ocultar.

**Incidencia de proceso registrada:** el commit de D6.3 se creó sin tres archivos que su propio
mensaje describía, dejando un `HEAD` que fallaba 7 pruebas. Se detectó al verificar, se confirmó
guardando temporalmente los cambios y volviendo a correr la suite sobre el commit publicado, y se
enmendó el commit (no estaba empujado) para que mensaje y contenido coincidan.

**Build y pruebas (Release), acumulado D6.1-D6.3:**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 822 superadas
  Nexo.Windows.Tests.dll → 190 superadas
  Nexo.App.Tests.dll     → 164 superadas
Total: 1176 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness
(y 5 veces seguidas al verificar la corrección de la prueba intermitente).
```

Confirmado con un arranque manual que la app sigue iniciando con el atajo global registrado.

**Pendiente, no bloqueante:** las listas de diccionario y atajos se guardan y se leen, pero todavía
no tienen interfaz de edición — se editan a mano en `settings.json`. Se atiende en D7.

> **Aprobación (2026-07-30):** el usuario probó el dictado global a mano y confirmó "se ve bien".
> **Diseño D6 (D6.1-D6.3, Kohana Flow) queda aprobado** e integrado en `release/kohana-1.0-rc` vía
> `merge: integrate approved Kohana Flow D6`. Build Release 0 warnings, 1176 pruebas, 0 fallidas
> tras el merge. Fase 3 pasa a **Implementada** en el roadmap.

---

## Diseños D7, D8 y D9 — tres sprints encargados juntos

**Rama:** `design/kohana-sprints-d7-d9`, creada desde `release/kohana-1.0-rc` (`56e0d3a`).
El usuario pidió explícitamente hacer tres sprints en vez de uno.

### D7 — Píldora con streaming y ajustes de Flow

**Streaming (idea propuesta por el propio usuario tras probar Lens).** `AmbientRequestManager` gana
un estado `Streaming` —estado propio, no una variante de `Thinking`: quien presenta necesita
distinguir "todavía no hay nada que enseñar" de "esto que se ve aún no está completo"— más
`BeginStreaming`/`AppendStreamedText`/`CompleteStreamedResult`. Lens pasa de `SendAsync` a
`StreamAsync`, así que la respuesta aparece conforme el modelo la escribe.

Dos decisiones deliberadas: **no** se persiste en cada fragmento (una respuesta larga dispararía
cientos de escrituras del archivo de historial por una sola solicitud; se guarda cuando hay algo
definitivo), y si el proveedor se corta a mitad, **lo ya recibido se conserva y se muestra** en vez
de descartarse en silencio, avisando de que quedó incompleta.

La píldora suma una aparición suave (solo en el primer despliegue — animar cada refresco parpadearía
sin parar mientras el texto llega), un cuerpo desplazable con tope de 220 px para que una respuesta
larga no la estire fuera de pantalla, y seguimiento automático del final del texto. Nunca se
autodescarta mientras está recibiendo.

**Ajustes de Flow.** Interfaz para activar/desactivar el dictado, elegir estilo
(texto/correo/código) y editar el diccionario y los atajos que D6 dejó editables solo a mano. El
atajo global ahora se registra y libera en vivo, sin reiniciar. El parser ignora líneas mal escritas
en silencio para que una sola no rompa el resto, pero la interfaz **sí dice cuántas ignoró**: una
línea sin "=" que simplemente no hace nada parecería que la función está rota.

### D8 — Optimización adaptativa (Fase 4)

Siete comandos (jugar, programar, edición de video, videollamada, batería, general, restaurar) que
leen el `HardwareCapabilityProfile` REAL y proponen solo lo que ese hardware justifica.

La regla del roadmap gobierna todo el planificador: *"no es una lista genérica de tweaks de internet
— cada cambio debe justificarse con una medición real del equipo"*. Por eso **cuando falta el dato,
el cambio NO se propone**: pasa a `SkippedForMissingData` con el motivo. Sin saber si hay batería no
se toca el plan de energía; sin saber la RAM no se dice nada de memoria; sin saber la GPU nada de
gráficos. Pedir "optimizar para batería" en un equipo de escritorio no propone ahorro y explica por
qué. Los umbrales de memoria cambian según el escenario (12 GB sobran para una videollamada y se
quedan cortos para editar video) en vez de un número fijo, que sería justo el truco genérico que la
fase prohíbe.

Todo **propone** primero; aplicar es una confirmación aparte. Kohana solo aplica lo que puede
revertir con certeza — hoy únicamente el plan de energía, vía API documentada de Win32
(`PowerGet`/`PowerSetActiveScheme`), sin trucos de registro ni procesos lanzados, por la
preocupación ya registrada sobre falsos positivos de antivirus. Lo demás son consejos que ejecuta la
persona. **Aplicar poco y poder deshacerlo siempre es mejor que aplicar mucho sin garantía de vuelta
atrás**: el roadmap trata la reversión como requisito, no como aspiración.

Tres decisiones de orden que importan: el snapshot se escribe en disco ANTES de tocar nada (si algo
falla a media aplicación, la vuelta atrás ya existe); se persiste en disco y no en memoria (una
caída después de aplicar no debe llevarse el deshacer); si no se puede leer el plan de energía
actual **no se aplica nada**, porque no habría manera de volver.

### D9 — Memoria opt-in (Fase 6)

El criterio de terminado de la fase es literal: *"controles de exclusión y retención funcionando
ANTES de que exista cualquier almacenamiento de memoria de facto"*. Por eso `MemoryPolicy` vive
aparte del almacén y es la única puerta: no hay ningún camino que guarde saltándose los controles.

Cuatro razones para no recordar, en orden: memoria apagada (lo está por omisión), categoría concreta
no activada, coincidencia con una exclusión del usuario, o contenido sensible evidente —reutilizando
el mismo `SensitiveContentRedactor` de Lens en vez de duplicar heurísticas, para que una contraseña
dictada sin querer no acabe guardada aunque todo esté activado.

Tres categorías independientes (preferencias, conversación, hábitos): aceptar una no autoriza las
otras. Apagar el interruptor general apaga también las categorías, porque si no, reactivar la
memoria resucitaría en silencio permisos que la persona creía revocados. La retención se aplica
**también al leer**, no solo al escribir: bajar la retención de 90 a 7 días es una orden, no una
preferencia a futuro. Hay además un tope duro de entradas, porque una memoria sin límite es justo el
riesgo de "acumulación silenciosa" que el roadmap señala.

Almacenamiento cifrado en reposo con DPAPI (`CurrentUser`), la decisión ya registrada en la sesión
de roadmap: cifra de verdad sin obligar a inventar una contraseña. Consecuencia asumida: el archivo
no es portable entre equipos ni cuentas — para memoria personal, que no se pueda leer desde otra
cuenta es la propiedad buscada, no un defecto. Búsqueda literal primero, sin índice semántico, según
lo ya decidido.

"Ver lo que Kohana recuerda" y "olvidar todo" funcionan **aunque la memoria esté apagada**: revocar y
auditar deben poder hacerse siempre; si apagar bloqueara el borrado, lo ya guardado quedaría atrapado
justo cuando la persona quiere deshacerse de ello.

**Corrección detectada al escribir el escalón de migración:** el rung v19 quedó insertado ANTES del
v18, de modo que un archivo en v17 habría saltado la migración de las listas de Flow. Se detectó
revisando el orden y se reordenó para que la escalera siga siendo estrictamente ascendente.

**Build y pruebas (Release), acumulado D7-D9:**

```
dotnet build Nexo.slnx -c Release --no-incremental → Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    → 860 superadas
  Nexo.Windows.Tests.dll → 190 superadas
  Nexo.App.Tests.dll     → 166 superadas
Total: 1216 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado con arranques manuales que la app sigue iniciando tras cada sprint.

**Pendiente, no bloqueante:** la memoria tiene comandos y controles pero todavía no hay interfaz de
ajustes para activarla (se activa editando `settings.json`), ni escritura automática de recuerdos
desde la conversación — hoy la memoria existe, se protege y se consulta, pero solo se llena por
código. La optimización aplica un único ajuste real (plan de energía); ampliar el conjunto exige
poder revertir cada nuevo cambio con la misma certeza.

**No se hizo push, no se abrió PR, no se hizo merge a `release/kohana-1.0-rc` ni a `main`.**

---

## Diseños D10, D11 y D12 — segundo bloque de tres sprints seguidos

**Rama:** `design/kohana-sprints-d7-d9` (continúa la misma rama, sobre `3abf93a`).
El usuario volvió a pedir explícitamente tres sprints en una sola entrega. Los tres cierran
pendientes que las fases anteriores habían dejado nombrados por escrito, en vez de abrir frentes
nuevos: D10 y D11 completan lo que D9 y D8 dejaron a medias, y D12 arranca la Fase 5 por el nivel
más bajo del modelo de confianza.

### D10 — La memoria se llena, se usa y se configura (Fase 6)

D9 dejó la memoria protegida pero inerte: solo la llenaba el código y solo se activaba editando
`settings.json`.

`MemoryCandidateDetector` lee UNA frase y decide si contiene algo recordable. Dos límites
deliberados, los dos por la regla del roadmap de que la memoria no puede convertirse en vigilancia
permanente. **Solo reconoce frases literales**: no infiere del resto de la conversación, porque
deducir que alguien "parece preferir X" tras mencionarlo tres veces es exactamente la observación
silenciosa que la fase prohíbe, y además sería adivinar. Y **nunca produce la categoría `Habitos`**:
un hábito es conducta observada a lo largo del tiempo, no algo que se lea en una oración suelta. La
categoría existe y se respeta, pero solo puede llenarla algo que mida conducta de verdad, y eso
todavía no existe.

Dos caminos con reglas distintas. El **explícito** ("recuerda que ...") guarda: la persona acaba de
dar la orden, y preguntarle "¿seguro?" sería ruido; si la política lo rechaza, **se dice por qué**,
porque un "recuerda que ..." que no guarda nada y no explica nada parecería que funcionó. El
**observado** (una preferencia dicha de paso) no se guarda nunca solo: se propone y hace falta un
sí, y la propuesta caduca con la frase siguiente para que un "sí" dicho por otra cosa no la capture.
La política se consulta ANTES de proponer, para no preguntar por algo que una exclusión rechazaría
igualmente.

`MemoryContextBuilder` es lo que hace útil a la memoria — guardarla sin usarla no da continuidad
ninguna. Se trata como un envío, que es lo que es cuando el proveedor es remoto: solo categorías
activas EN ESE MOMENTO (desactivar una deja de usarla en el acto, aunque sus entradas sobrevivan;
borrarlas al mover un interruptor sería destruir datos, y para eso está "olvidar"), redacción
repetida a la salida, y tope de cantidad y tamaño. Si no cabe ni una entrada tampoco se manda el
encabezado: anunciar una memoria vacía es peor que no mandar nada.

Interfaz en Personalizar con los cuatro controles de la fase (interruptor general, tres categorías,
retención y exclusiones). Apagar el general desmarca también las categorías en la vista, igual que
hace `Normalize` con los datos: si no, reactivar la memoria parecería restaurar permisos que ya no
existen. "Ver lo que recuerda" y "olvidar todo" siguen activos con la memoria apagada.

**Defecto encontrado al probar:** `SensitiveContentRedactor` solo conocía la forma de formulario
("contraseña: 1234"), no la hablada ("mi contraseña es 1234"). La memoria y el dictado guardan
prosa, así que una contraseña dicha en voz alta pasaba de largo. Ampliado — sí redacta de más en
frases como "la clave es importante", que es el intercambio que esa clase ya declara aceptar.

### D11 — Optimización verificada, reversible y auditada (Fase 4)

D8 aplicaba una sola cosa y se fiaba de que hubiera funcionado.

**Verificar es releer.** Que `PowerSetActiveScheme` devuelva 0 significa que Windows aceptó la
llamada, no que el plan activo sea ahora el pedido: una directiva de grupo o una utilidad del
fabricante pueden reponer el suyo. Sin la relectura, Kohana podía anunciar "listo" sobre un cambio
que no ocurrió y ofrecer después deshacer algo que nunca se hizo. Los dos fallos se informan por
separado a propósito: "Windows lo rechazó" y "Windows lo aceptó pero el plan activo sigue siendo
otro" son problemas distintos.

**Segundo objetivo real: el consumo de la propia Kohana.** Cumple las dos condiciones que la fase
exige a la vez — es un cambio que se nota (los motores locales de voz e IA son lo más caro que corre
aquí) y vive en el archivo de preferencias, así que revertirlo es reescribir el valor anterior. Es
además el más honesto disponible: antes de pedirle a la persona que cierre sus programas, la
aplicación que se aparta primero es Kohana. Sigue exigiendo medición como todo lo demás: sin el
número de procesadores lógicos no se propone nada, y en un equipo holgado se omite con el motivo,
porque ahí bajarlo sería un gesto vacío que solo empeora sus propios motores. Programar queda fuera
a propósito: ahí Kohana es la herramienta que se está usando.

Con dos objetivos, el fallo parcial deja de ser un detalle, así que la orquestación se mudó de
`MainWindow` a `OptimizationCoordinator`, en Core, donde se prueba con dobles. Tres reglas: no se
toca nada si falta el valor anterior de algún objetivo (sin él, deshacer no existiría); el snapshot
llega al disco antes del primer cambio; y **si un paso falla, se deshacen los anteriores**, porque
un plan a medias deja el equipo en un estado que nadie pidió y que nadie sabría describir. Cuando la
propia reversión falla, se dice en voz alta en vez de fingir éxito. Un deshacer fallido conserva el
snapshot para poder reintentarlo.

Registro de auditoría en disco, de solo añadir, con tope de 50 entradas y recorte solo por
antigüedad: poder quitar UNA entrada convertiría el registro en una versión de los hechos. Sin
cifrar a propósito — no contiene datos personales, y que se pueda abrir con cualquier editor es
parte de que sea de la persona. Consultable desde un comando y desde el panel nuevo.

Sistema gana un panel de optimización con los mismos siete escenarios que ya existían como comandos
—mismo camino, la interfaz no aplica nada por su cuenta—. El botón de deshacer se desactiva cuando
no hay snapshot: ofrecer "deshacer" sin nada que deshacer haría dudar de si lo anterior se aplicó.

### D12 — Workspace autorizado y modo Guiar (Fase 5)

Primer sprint del acompañante de proyecto, y **no escribe nada**. El modelo de confianza es
explícito: *"ninguna capacidad nueva puede empezar en el nivel 6: cada una debe demostrarse en los
niveles 1–3 antes de solicitar el salto a ejecución"*. `IWorkspaceReader` no tiene ningún método de
escritura, y eso es el diseño, no un pendiente: mientras la capacidad viva en los niveles 1–3 no
debe existir siquiera la forma de llamar a una escritura. El nivel 4 exige antes lo que ese mismo
modelo pide y aquí no existe todavía: snapshot previo por archivo y el Audit Log orientado al
usuario.

`WorkspacePathPolicy` es la pieza de seguridad del sprint. La contención se comprueba sobre rutas ya
RESUELTAS, no sobre las cadenas que llegan: una comprobación textual la burla cualquier `..\..\`, y
también los enlaces simbólicos y las uniones de directorio de Windows, que apuntan fuera sin que la
ruta lo aparente. La comparación exige separador, así que `C:\...\Proyecto` no contiene a
`C:\...\Proyecto-privado`. Y las extensiones son una **lista de permitidos**: con una lista de
prohibidos, cada extensión nueva del mundo entraría por defecto.

Los archivos de secretos (`.env*`, `id_rsa`, `*.pem`, `*.pfx`, `secrets.json`...) se niegan **por
nombre, antes de abrirlos**: leer el contenido para decidir si contiene secretos ya sería haberlo
leído. Las carpetas de dependencias y de compilación se saltan, aunque un nombre excluido en la raíz
autorizada no bloquea nada — si alguien autoriza una carpeta llamada `build`, su proyecto se llama
build.

`WorkspaceSecretScanner` atrapa lo que queda, y es una clase aparte de `SensitiveContentRedactor` en
vez de un reemplazo: aquélla busca datos personales en texto de pantalla; ésta, asignaciones de
configuración, cadenas de conexión y bloques de clave privada con la forma que tienen en un archivo
de código. Se usan las dos. Conserva el nombre de la variable y tira el valor: saber que existe
`ApiKey` ayuda a explicar un proyecto, su valor no. Las asignaciones exigen un valor de 8 caracteres
o más para no redactar los marcadores vacíos del código de ejemplo, cuya redacción solo escondería
que el hueco está vacío.

Explicar un proyecto envía **estructura, no código**. Mandar un proyecto entero a un proveedor
remoto para que diga de qué va es desproporcionado; el árbol de archivos lo explica casi igual de
bien y sale del equipo una fracción. El contexto del proyecto se consume en UNA consulta y se apaga:
ésa es la garantía de que autorizar una carpeta no convierte cada pregunta posterior en un envío de
código.

Autorizar y revocar están al mismo nivel y en el mismo sitio: un permiso que cuesta más quitar que
dar no es un permiso, es una trampa. La confirmación dice qué se concede Y qué no. Preferencias al
esquema v20, y el escalón **revoca**: nadie hereda una carpeta autorizada al actualizar, por el
mismo motivo que la memoria en v19 — una migración no es un consentimiento.

De paso, la auditoría de D11 se añadió a la prueba de aislamiento de perfiles de validación: un
store que se escapara a la carpeta real mezclaría lo probado con lo que la persona hizo de verdad en
su equipo.

**Build y pruebas (Release), acumulado D10-D12:**

```
dotnet build Nexo.slnx -c Release --no-incremental -> Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    -> 959 superadas
  Nexo.Windows.Tests.dll -> 190 superadas
  Nexo.App.Tests.dll     -> 166 superadas
Total: 1315 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado que la app sigue arrancando tras los tres sprints (arranque real del ejecutable
publicado, 12 s en pie).

**Pendiente, no bloqueante:**

- **Sin validación manual del usuario.** Se comprobó que la app arranca, no que los paneles nuevos
  (memoria en Personalizar, optimización en Sistema) se vean y se usen bien. Los tres sprints
  quedan pendientes de la misma prueba a mano que aprobó D4, D5 y D6.
- La memoria sigue sin categoría `Habitos` llena: hace falta algo que mida conducta de verdad.
- La optimización aplica dos objetivos reales (plan de energía y consumo de Kohana). Ampliar el
  conjunto exige poder revertir cada nuevo cambio con la misma certeza.
- El workspace no tiene interfaz propia: se autoriza, se consulta y se revoca desde la paleta de
  comandos. Tampoco hay selección de nivel de autonomía en la interfaz (se queda en `Guiar`).
- La búsqueda en el proyecto (`IWorkspaceReader.Search`) está implementada y probada por contrato,
  pero todavía no tiene comando que la exponga.

**No se hizo push, no se abrió PR, no se hizo merge a `release/kohana-1.0-rc` ni a `main`.**

---

## Diseños D13, D14 y D15 — tercer bloque de tres sprints seguidos

**Rama:** `design/kohana-sprints-d7-d9` (misma rama, sobre `446c3a2`).
Tercera vez que el usuario pide tres sprints en una entrega. El arco tiene un orden deliberado: D13
construye la pieza que el modelo de confianza exige antes de poder escribir, D14 usa esa pieza para
abrir el nivel 4, y D15 empaqueta lo que ya existe sin añadir capacidad nueva.

### D13 — Audit Log orientado al usuario (capa 11)

El modelo de confianza es explícito en que los logs de diagnóstico por subsistema
(`command-center.log` y compañía) **no cumplen este propósito**: son técnicos, escritos para depurar,
no para que alguien entienda qué pasó con su equipo. Esto construye el que sí pide.

Cada entrada trae los cuatro datos que ese documento nombra literalmente —qué se hizo, cuándo, con
qué permiso y cómo revertirlo— **como campos separados**, no como prosa dentro de un mensaje. Si
"cómo revertirlo" formara parte de la frase, nada obligaría a rellenarlo y la primera acción
irreversible pasaría desapercibida. Las entradas sin vuelta atrás lo dicen en voz alta; callarlo
haría parecer que todo se puede deshacer.

El registro propio de la optimización (D11) desaparece: un registro por capacidad obliga a la
persona a saber de antemano en cuál mirar, y "¿qué ha hecho Kohana en mi equipo?" es una sola
pregunta. Lo ya escrito **se importa** en vez de abandonarse —una entrada de auditoría que
desaparece porque el formato cambió es justo lo que un registro no puede permitirse—, y la
importación ocurre una sola vez.

Las decisiones de permisos también se registran ahora: autorizar una carpeta, revocarla, cambiar su
nivel de autonomía, borrar la memoria. Conceder un permiso es la decisión de la que cuelgan todas
las demás, así que es lo primero que debería estar en el registro.

Sistema gana la tarjeta "Qué ha hecho Kohana", poblada al abrir: un panel vacío se leería como "no
hay registro" cuando lo que pasa es que nadie lo ha pedido. Personalizar gana la tarjeta del
proyecto —autorizar y revocar uno al lado del otro, más hasta dónde puede llegar Kohana—, y solo
deja elegir niveles que la política ofrece de verdad, para que la interfaz no pueda conceder lo que
el modelo de confianza aún no permite.

También cierra el pendiente de D12: la búsqueda en el proyecto ya tiene comando. La paleta no acepta
argumentos, así que la consulta se pide por conversación —una frase, cancelable— y se vuelve a
comprobar la autorización antes de buscar, porque revocar tiene que surtir efecto en el acto,
incluso a media conversación.

### D14 — Checkpoints y "ejecutar un paso" (Fase 5, nivel 4)

Primera vez que Kohana escribe en archivos de la persona, así que casi todo el sprint es sobre
cuándo se niega.

**El nivel 4 se abre porque las dos cosas que lo bloqueaban ya existen**, no porque haya pasado un
sprint: el checkpoint reversible por archivo y el Audit Log de D13. Los niveles 5 y 6 siguen
cerrados —encadenar pasos y automatizar una secuencia son problemas distintos de "hacer un cambio
confirmado", y ninguno se ha demostrado—. El nivel **por omisión no sube**: que escribir sea posible
no significa que deba estar encendido, y una actualización no toma esa decisión por nadie.

Orden en `WorkspaceEditCoordinator`, y cada paso sostiene algo: nivel de autonomía suficiente; la
MISMA `WorkspacePathPolicy` que gobierna la lectura (darle a la escritura sus propias reglas de
contención sería la forma más fácil de que una de las dos se quedara atrás); el checkpoint, que
además **se comprueba persistido antes de tocar el archivo**, porque un cambio sin vuelta atrás es
exactamente lo que el nivel 4 prohíbe; la escritura; la verificación releyendo; y si el archivo no
quedó como se pidió, se deshace —un archivo a medio escribir es peor que uno sin tocar—. La
reversión también se verifica: "lo dejé como estaba" es la frase que no puede decirse a la ligera,
así que si tampoco cuajó, se dice y se pide mirarlo.

**Deshacer se niega si el archivo cambió después de que Kohana lo escribiera.** Es el peor daño que
esta capacidad podría causar y el único que no sería un fallo sino una decisión de diseño: revertir
entonces no devolvería el archivo, destruiría lo que la persona hizo después. Por eso el checkpoint
guarda también lo que Kohana ESCRIBIÓ, no solo lo que había antes; suena redundante y es la pieza
que hace posible la comprobación.

`WorkspaceEditParser` es estricto a propósito: **el formato es exacto o no hay cambio**. Un parser
tolerante que adivina rutas o recompone bloques a medias acaba escribiendo en el archivo equivocado,
y ese error no lo ve nadie hasta que ya ocurrió. Dos propuestas en una respuesta significan que no
se aplica ninguna (el nivel 4 es literalmente un paso). Un bloque sin cerrar es una respuesta
truncada. Un bloque vacío vaciaría el archivo en silencio. Todo se rechaza; rechazar es barato.

La oferta de escribir se arma para UNA respuesta y se apaga en el `finally`, así que una respuesta
cortada, cancelada o fallida no puede producir un cambio, y una posterior que por casualidad
contuviera el formato tampoco. El permiso se vuelve a comprobar entre el comando y la respuesta. Los
intentos rechazados también se auditan: saber que Kohana intentó tocar un archivo y no pudo es de lo
que una auditoría existe para contar.

### D15 — Kohana Study y Kohana Dev (Fase 8)

El criterio de terminado de la fase es literal: al menos dos packs completos usando **exclusivamente**
capacidades ya implementadas en fases anteriores. Ningún pack inventa nada; si algo no existe, no
entra en un pack.

La regla que sostiene la fase es que cada pack hereda los permisos de las capacidades que combina y
no introduce excepciones. En consecuencia un pack **solo escribe preferencias**: nunca concede un
permiso. Encender la memoria, autorizar una carpeta o activar Vision siguen necesitando su propia
decisión; el pack se limita a decir que le harían falta y dónde se dan. Un pack que encendiera
permisos sería una forma de concederlos sin pedirlos, disfrazada de comodidad.

**Defecto real que atrapó una prueba:** llamar a `preferences.Normalize()` después de aplicar un
pack arrastra la escalera de migración por `SchemaVersion`, y sobre unas preferencias que aún no
hubieran pasado por ella eso **reactivaba Vision** — es decir, concedía un permiso que la persona
tenía apagado. Se quitó, por el mismo motivo ya registrado en `ResetVisualPreferences`: migrar no es
tarea de un pack, y los valores recién escritos ya son válidos porque el `Write` de cada ajuste solo
acepta valores reconocidos.

El riesgo que el roadmap señala para esta fase es la fragmentación ("si cada pack termina con su
propia lógica en vez de reutilizar capacidades comunes"). Por eso un pack aquí no es código, es una
LISTA: no puede hacer nada que Personalizar no pueda hacer ya, solo lo deja puesto de una vez. Los
ajustes se leen y se escriben como texto, que es lo que hace que el estado anterior quepa en un JSON
legible y que deshacer signifique "volver a escribir lo que había".

Un pack activo a la vez. Dos superpuestos dejarían un estado que ninguno de los dos describe, y
"¿qué tengo activado?" dejaría de tener respuesta. Cambiar de pack restaura primero los ajustes de
la persona; si no, el snapshot del segundo guardaría los del primero como si fueran suyos.

La vista previa enseña lo que cambiaría **y** lo que falta, con el mismo peso: enseñar solo lo que
gana la persona sería vender el pack, no explicarlo. Los ajustes que ya están como el pack los
quiere no se listan.

**Build y pruebas (Release), acumulado D13-D15:**

```
dotnet build Nexo.slnx -c Release --no-incremental -> Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    -> 1013 superadas
  Nexo.Windows.Tests.dll ->  206 superadas
  Nexo.App.Tests.dll     ->  166 superadas
Total: 1385 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado que la app sigue arrancando (arranque real del ejecutable publicado, 12 s en pie).

**Pendiente, no bloqueante:**

- **Sigue sin validación manual del usuario**, ahora acumulada desde D10. Se comprueba que la app
  arranca, no que los paneles nuevos se vean y se usen bien. **D14 escribe en archivos reales**: es
  el sprint que más pide una prueba a mano antes de integrarse.
- Los packs no tienen interfaz propia: se activan y desactivan desde la paleta de comandos.
- Solo hay dos packs de los seis que nombra el roadmap (faltan Support, Creator, Access y Meeting).
- El cambio en el proyecto reemplaza el archivo COMPLETO, no aplica parches. Es lo que permite
  verificar releyendo, pero obliga al modelo a reescribir archivos enteros y limita el tamaño.
- Los niveles 5 y 6 de autonomía siguen cerrados.
- La auditoría se lee, pero todavía no se puede deshacer una acción DESDE el panel: el
  `RevertToken` existe en la entrada y no tiene botón.

**No se hizo push, no se abrió PR, no se hizo merge a `release/kohana-1.0-rc` ni a `main`.**

---

## Diseños D16, D17 y D18 — cuarto bloque de tres sprints seguidos

**Rama:** `design/kohana-sprints-d7-d9` (misma rama, sobre `e7afebe`).
Cuarta entrega de tres sprints. El arco lo dicta el propio roadmap: la Fase 7 dice que Computer Use
*"requiere el Permission Broker y el Audit Log completos antes de habilitarse"*. El Audit Log llegó
en D13, así que D16 construye la otra mitad, D17 abre la fase por los niveles 1–3, y D18 abre el
nivel 4 cuando ya existe todo lo que hace falta para ejecutar sin dejar el equipo a medias.

### D16 — Permission Broker

Capacidades con permiso **independiente**, porque el modelo de confianza lo exige: *"cada capacidad
de la matriz tiene su propio permiso, independiente de las demás — habilitar Lens no habilita
Computer Use"*. Un enum por capacidad en vez de banderas de un permiso general, que es como se acaba
concediendo de más sin querer.

**El orden de las comprobaciones ES la política**, y por eso es lo que fijan las pruebas:

1. **Exclusión por aplicación**, primero, porque el modelo dice que vale *"incluso si la capacidad en
   general está habilitada"*: tiene que ganarle a Permitido.
2. **Bloqueado** → denegado. Denegar gana a preguntar: confirmar algo que de todas formas no va a
   ocurrir enseña a aceptar por costumbre.
3. **Las siete categorías de confirmación obligatoria**, que preguntan *sea cual sea el nivel*. Estar
   en Permitido no las salta; ése es todo el motivo de que existan.
4. **Permitido** → adelante.
5. **Cualquier otra cosa** → preguntar. Falla cerrado.

Los valores por omisión son la parte que más importa: Computer Use llega **Bloqueado** —el permiso
más alto del roadmap no se concede por instalar Kohana— y el resto llega **Preguntar**, no
Permitido. El escalón v21 los reconstruye así al actualizar, por el mismo motivo que v19 y v20: una
migración no es un consentimiento. Un `settings.json` escrito a mano tampoco se lo salta —los
duplicados se resuelven por el **más restrictivo**, porque resolverlos "por la última línea" dejaría
que añadir una línea ampliara permisos sin que nadie lo confirmara.

Ampliar un permiso vuelve a preguntar (política de mínimo privilegio del modelo); restringir, no.

Se cableó a dos caminos reales en vez de dejarlo como andamio: borrar la memoria pasa por el broker
en lugar de por su diálogo propio (borrado irreversible es una de las siete categorías, así que
pregunta aunque la memoria esté permitida), y los cambios en el proyecto comprueban denegación antes
de ofrecerse. Cada concesión, negativa y rechazo va al Audit Log: un "sí" que no deja rastro es
indistinguible de un permiso que nadie dio.

### D17 — Computer Use en los niveles 1–3

**La escalera de métodos es la política.** El roadmap fija el orden y explica por qué: *"automatizar
mouse/teclado es frágil e inseguro si se usa como primera opción en vez de último recurso — de ahí
el orden estricto"*. El enum se numera de forma que **menor es más seguro**, para que elegir sea
comparar números y no recordar una lista; un orden que hay que recordar es un orden que alguien se
salta.

No se baja de escalón mientras haya uno más arriba disponible: si existe la API oficial no se usa UI
Automation aunque "también funcione". Es toda la diferencia entre una automatización que sobrevive a
una actualización y una que no. Ratón y teclado simulados exigen haberlos habilitado a propósito:
que estén disponibles no basta, porque son el único método que no puede comprobar qué hizo.

**Disponibilidad y preferencia son interfaces separadas**, porque son preguntas distintas y
confundirlas es como se acaba prometiendo lo que no hay. La respuesta honesta hoy es: dos métodos.
El portapapeles y una lista corta de comandos de solo lectura. Los cuatro de arriba no se declaran
disponibles porque Kohana no sabe ejecutarlos, y declarar un método inexistente llevaría a elegirlo
y fallar después. UI Automation ya se usa para LEER en Lens, pero leer un control e invocarlo no son
la misma capacidad.

La lista de comandos es de permitidos, fija y sin ninguna entrada del usuario ni del modelo: en
cuanto un argumento venga de fuera deja de ser esta lista y pasa a ser ejecución arbitraria. **Dos
candidatos se descartaron al escribirla y el motivo quedó anotado en el archivo** para que no
vuelvan solos: `powercfg /batteryreport` ESCRIBE un archivo y rompe la invariante de "solo leen", y
`wmic` está en la lista de intérpretes de `ShellExecutionPolicy`, así que meterlo aquí contradiría
una política que Kohana ya aplica en otro sitio. Una lista de permitidos solo vale si se defiende
cuando estorba.

No se ejecuta nada. El plan se arma **aunque algo lo bloquee**, y entonces trae el motivo: un plan
que no llega a formarse deja a la persona sin saber qué haría falta para que sí, y explicarlo es
justo lo que "proponer" significa.

**Unificación de la escalera de autonomía.** Nació en D12 como `WorkspaceAutonomyLevel` y Computer
Use necesitaba los mismos seis niveles. Dos copias del mismo concepto son el riesgo de fragmentación
que el roadmap nombra: se separan en cuanto una crece, y entonces "nivel 4" significa cosas
distintas según a quién se pregunte. La escalera es del modelo de confianza, no de ninguna capacidad.

### D18 — Nivel 4: ejecutar una acción confirmada

El nivel 4 se abre porque existen las tres cosas que el modelo pide antes de ejecutar: el broker
(D16), el Audit Log (D13) y una reversión real para el único método que la admite —el portapapeles
guarda lo que había—. Los comandos de la lista no necesitan reversión porque no cambian nada, y eso
lo **defiende** `SafeShellCatalog`, no una promesa.

Mismo esqueleto que `WorkspaceEditCoordinator` de D14, a propósito: los dos resuelven el mismo
problema —ejecutar un paso confirmado sin quedarse a medias— y darle a cada capacidad el suyo es
como acaban divergiendo las garantías.

Lo específico de esta fase: **pedir un método menos seguro cuando hay uno mejor se rechaza**. Sin
eso, el orden estricto de D17 sería decorativo — bastaría con pedir el método cómodo para saltárselo.

El portapapeles se verifica releyendo, y deshacer **se niega si cambió después de que Kohana lo
pusiera**: misma regla que D14 con los archivos y por el mismo motivo, deshacer no puede destruir lo
que hizo la persona después. Los comandos se lanzan **sin shell**, con ejecutable y argumentos tal
cual vienen del catálogo: no se compone ninguna cadena, así que no hay nada que escapar.

Computer Use tiene **su propio nivel de autonomía**, no el del proyecto. Compartirlo haría que subir
el del proyecto concediera también éste, que es justo lo que "habilitar Lens no habilita Computer
Use" prohíbe. Ninguno de los dos valores por omisión sube: ejecutar es posible, estar encendido es
otra decisión, y el permiso sigue llegando Bloqueado — hacen falta **dos** decisiones, no una.
Escalón v22 para el campo nuevo, porque sin él un archivo anterior lo dejaría en 0, que no es ningún
nivel válido.

También cierra el pendiente de D13: el panel de auditoría ya tiene el botón de deshacer que sus
entradas sabían describir desde entonces. Un "cómo deshacerlo" que obliga a ir a buscar el comando
correcto es media promesa. El despacho es por capacidad: el registro dice QUÉ se puede deshacer, pero
quien sabe CÓMO sigue siendo la capacidad que lo hizo.

**Build y pruebas (Release), acumulado D16-D18:**

```
dotnet build Nexo.slnx -c Release --no-incremental -> Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    -> 1077 superadas
  Nexo.Windows.Tests.dll ->  206 superadas
  Nexo.App.Tests.dll     ->  166 superadas
Total: 1449 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado que la app sigue arrancando (arranque real del ejecutable publicado, 12 s en pie).

**Pendiente, no bloqueante:**

- **Sigue sin validación manual del usuario**, acumulada desde D10. D14 escribe archivos y D18
  ejecuta procesos: son los dos sprints que más piden una prueba a mano antes de integrarse.
- **Ningún método de los cuatro primeros está implementado.** Kohana elige bien entre lo que hay,
  pero lo que hay son dos métodos de los ocho. La escalera funciona; falta llenarla por arriba.
- Ratón y teclado simulados **no están implementados** y no hay interruptor para habilitarlos: la
  política los contempla y los rechaza, que es donde deben estar hasta que exista una razón.
- Las exclusiones por aplicación existen en el modelo y en el broker, pero **no tienen interfaz**:
  se editan a mano en `settings.json`.
- Los niveles 5 y 6 siguen cerrados para todas las capacidades.
- El panel de permisos no muestra el detalle de las exclusiones, solo cuántas hay.

**No se hizo push, no se abrió PR, no se hizo merge a `release/kohana-1.0-rc` ni a `main`.**

---

## Diseños D19, D20 y D21 — quinto bloque de tres sprints seguidos

**Rama:** `design/kohana-sprints-d7-d9` (misma rama, sobre `9ca424c`).
D19 sube un escalón de la escalera de métodos que la entrega anterior dejó casi vacía por arriba, y
D20/D21 abren la **Fase 9 — Productization**, la última fase que no se había empezado.

### D19 — UI Automation ejecutable (escalón 5) y exclusiones con interfaz

Kohana ya leía UI Automation desde D5.3, para Lens. Leer un control e invocarlo **no son la misma
capacidad**, así que el invocador tiene su propia interfaz, igual que el proyecto separó lector y
escritor. El comentario que `UiAutomationElement` lleva desde D5.3 —que nunca incluiría una acción
para invocarlo— sigue siendo cierto: la acción no vive en el elemento, vive detrás de su propia
interfaz y su propio permiso.

La regla de la política es **ante la duda, no se pulsa**. Sin coincidencia no se aproxima nada
(aproximar un nombre es pulsar un botón que nadie pidió). Con VARIAS coincidencias tampoco se pulsa:
una ventana con tres "Aceptar" no tiene un "el Aceptar", y elegir el primero del árbol es elegir al
azar con apariencia de criterio. Solo se invocan tipos de control que se pulsan. La ventana pasa por
la misma exclusión de ventanas sensibles que usa Lens.

El invocador **vuelve a resolver el control contra el árbol de AHORA**, no contra lo que se leyó al
proponer: entre proponer y confirmar puede aparecer un diálogo o reordenarse una lista, y pulsar por
una posición leída antes es cómo se acaba pulsando otra cosa. Y vuelve a comprobar la ambigüedad,
porque la lectura del lector está acotada en profundidad y anchura mientras que el árbol real no.

**Defecto real que destapó añadir el escalón.** La regla "no uses un método menos seguro habiendo
uno más seguro disponible" comparaba TODOS los métodos disponibles entre sí. En cuanto UI Automation
(5) pasó a estar disponible, el coordinador empezó a rechazar copiar al portapapeles (7) y ejecutar
un comando de diagnóstico (6) "porque hay algo más seguro" — cuando UI Automation no sabe hacer
ninguna de las dos. La comparación solo significa algo **entre métodos capaces de lo mismo**. La
corrección no fue relajar la regla sino aplicarla donde tiene sentido: orden estricto, dentro del
conjunto de métodos capaces de ESE objetivo (`ComputerUseGoal`). Lo atraparon dos pruebas
existentes al fallar.

También cierra el pendiente de D16: las exclusiones por aplicación se editan en Personalizar y no
solo en `settings.json`. Eran la parte del modelo de confianza que más se usa a diario, así que
dejarlas sin interfaz las volvía teóricas. Las líneas mal escritas se ignoran pero **se cuentan y se
dicen**: una exclusión que la persona cree puesta y no lo está es una protección que no existe.
Aplicar **reemplaza** en vez de acumular, porque si no, quitar una exclusión desde la interfaz sería
imposible.

### D20 — Copia verificada antes de actualizar, y desinstalar sin sorpresas

`KohanaDataInventory` existe porque tres cosas lo necesitaban y ninguna podía inventárselo: la copia
previa (¿qué hay que copiar?), la desinstalación (¿qué es la app y qué son tus datos?) y el informe
de privacidad de D21. Escrito una vez, para que nadie mantenga su propia lista y se quede corta justo
cuando importa. **Se mantiene a mano a propósito**: enumerar la carpeta estaría siempre al día pero
no sabría decir qué es cada archivo ni si es personal, y sin eso el inventario no sirve para lo único
que existe. Que añadir un archivo obligue a describirlo es la característica, no el coste.

El roadmap nombra el riesgo de esta fase sin rodeos: *"un actualizador mal diseñado puede romper
instalaciones existentes — requiere el mismo rigor de reversibilidad que la Fase 4"*. Así que se
aplica el mismo patrón: copiar ANTES, verificar releyendo, y **si algún archivo no queda verificado,
la actualización no se declara segura**. Eso último no es prudencia de más: el único momento en que
la copia importa es cuando algo salió mal, y ahí ya no hay ocasión de comprobarla. La verificación
compara SHA-256, no tamaños — dos archivos del mismo tamaño pueden ser distintos, y comprobar el
tamaño da por buena justo la corrupción que más se parece a un archivo sano. Un archivo que aún no
existe no es un fallo: quien nunca activó la memoria no tiene memoria que copiar.

La desinstalación separa "la app" de "tus datos" y enseña **las dos listas**. Limpia no significa
borrarlo todo, significa que nadie se lleva una sorpresa — y desinstalar un programa y descubrir
después que se llevó años de notas es la sorpresa más cara que puede dar un instalador. Al revés
también cuenta: dejar datos personales sin decirlo es dejar un rastro que la persona creía
eliminado. Una elección no reconocida conserva los datos, fallando hacia el lado que no destruye
nada.

**Acoplamiento corregido de paso:** al escribir la prueba de la copia se rompió una invariante que
`ValidationProfileIsolationTests` documenta —que ninguna otra prueba de ese ensamblado muta la raíz
de datos global—. El arreglo no fue serializar las pruebas sino dar al servicio una raíz explícita,
como ya la aceptan todos los stores del proyecto. La testabilidad era el síntoma; el acoplamiento
era el problema.

### D21 — Paquete de soporte exportable e informe de privacidad

El paquete se trata como lo que es: **un envío**. Sale del equipo y llega a otra persona, así que
vale la misma disciplina que el contexto de memoria (D10) o el del proyecto (D12).

Todo lo que entra se redacta con **las dos herramientas, no una**: el redactor de datos personales y
el escáner de secretos de código. Buscan formas distintas y un detalle de diagnóstico puede traer de
las dos. Las rutas pierden el nombre de la carpeta de usuario: es la fuga fácil de pasar por alto,
porque sin eso cada línea con una ruta revela cómo se llama la persona — no es el dato más sensible
del mundo, pero es un dato personal que se cuela sin que nadie lo haya decidido, y ésos son los que
más veces se escapan.

No entra contenido personal: ni memoria, ni conversaciones, ni código del proyecto. Del registro
entra QUÉ se hizo, que es lo útil para soporte, no sobre qué. Y el archivo **enumera lo que dejó
fuera** e invita a leerlo antes de mandarlo: un paquete que omite en silencio obliga a confiar; uno
que enumera sus omisiones se puede revisar, y quien lo manda debería poder revisarlo.

El informe de privacidad responde qué se guarda, dónde, si está cifrado y cómo borrarlo. Construido
sobre el inventario y no sobre una lista propia, que es el punto: si alguien añade un archivo y no lo
describe, no aparece — y esa omisión se ve al leer el informe en vez de quedarse escondida en el
código. Nunca enseña contenido: enseñar tus datos para demostrar que se guardan sería una
contradicción con patas.

**Build y pruebas (Release), acumulado D19-D21:**

```
dotnet build Nexo.slnx -c Release --no-incremental -> Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    -> 1146 superadas
  Nexo.Windows.Tests.dll ->  216 superadas
  Nexo.App.Tests.dll     ->  166 superadas
Total: 1528 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado que la app sigue arrancando (arranque real del ejecutable publicado, 12 s en pie).

**Pendiente, no bloqueante:**

- **Sigue sin validación manual del usuario**, acumulada desde D10. D14 escribe archivos, D18 lanza
  procesos y **D19 pulsa botones en otras aplicaciones**: son los tres que más piden probarse a mano.
- **La instalación y la actualización de verdad siguen siendo del instalador.** D20 garantiza que
  haya una copia verificada de la que volver; no automatiza actualizar ni desinstalar. El criterio de
  terminado de la Fase 9 pide las tres cosas verificadas de punta a punta, y eso exige probar el
  instalador en una máquina limpia.
- La escalera de métodos sigue con **tres de ocho** implementados. Los cuatro de arriba (API oficial,
  App Actions, MCP, integración nativa) no existen; ratón y teclado tampoco, y es donde debe estar.
- Los niveles 5 y 6 siguen cerrados para todas las capacidades.
- Faltan cuatro de los seis packs, y los packs siguen sin interfaz propia.
- La restauración de una copia exige reiniciar Kohana a mano: no se recargan las preferencias en
  caliente.

**No se hizo push, no se abrió PR, no se hizo merge a `release/kohana-1.0-rc` ni a `main`.**

---

## Diseños D22, D23 y D24 — sexto bloque de tres sprints seguidos

**Rama:** `design/kohana-sprints-d7-d9` (misma rama, sobre `810a2e5`).
D22 ataca de frente la deuda que lleva acumulándose desde D10 —la validación manual que no ha
ocurrido—, D23 cierra los seis packs de la Fase 8 y D24 abre el nivel 5 del modelo de confianza donde
puede cumplirse.

### D22 — Autocomprobación en el equipo de la persona

**Por qué, teniendo 1500 pruebas:** las pruebas corren en la máquina de desarrollo, sobre el código
de ese momento. Esto corre aquí, ahora, sobre el binario instalado. No las repite por gusto: repite
exactamente aquéllas que, si fallaran en un equipo concreto, significarían que **una promesa de
Kohana no se está cumpliendo en ESE equipo**.

Once comprobaciones puras (migraciones que no borran lo configurado, nada peligroso activado de
fábrica, las siete confirmaciones obligatorias imposibles de saltar, exclusiones que ganan a un
permiso, redacción de datos personales y de secretos de código, contención del proyecto, archivos de
credenciales rechazados, comandos que solo leen, orden de métodos, packs que no conceden permisos,
inventario completo) y cinco sobre disco (ajustes que van y vuelven, registro que escribe, memoria
**de verdad cifrada** —el archivo NO debe contener el texto—, copia que se verifica, carpeta de datos
escribible).

Las de disco corren en una carpeta temporal propia, nunca sobre datos reales: una autocomprobación
que ensucia lo que comprueba no sirve de nada. Y cada una aísla su fallo, porque un informe que se
detiene en el primero esconde los demás, que es justo lo contrario de lo que necesita quien está
intentando entender por qué algo no va.

**Lo que más importa del sprint es la última sección del informe: qué NO comprueba.** Un verde total
invita a concluir "está todo bien", y sería falso — nada de esto mira la interfaz, ni si el dictado
escribe en la ventana correcta, ni si la píldora roba el foco. Si el informe no lo dijera, la primera
persona que lo leyera sacaría la conclusión equivocada, y la culpa sería del informe. Acorta la
validación manual; no la sustituye.

De paso, la versión actual de la escalera de migración pasa a tener nombre
(`ShellPreferences.CurrentSchemaVersion`). Existía solo como número suelto repetido en el último
escalón y en una veintena de pruebas, y un número repetido es un número que se olvida de actualizar
en algún sitio; ahora un escalón nuevo que olvide subirla falla en la suite y no en el equipo de
alguien.

### D23 — Los seis packs y su panel (Fase 8)

D15 cumplió el criterio de la fase con dos; el roadmap nombra seis. **Support, Creator, Access y
Meeting** completan el conjunto.

Todos se construyen igual y desde la misma regla: un pack es una **lista** de ajustes que ya
existían, no código. Una prueba nueva lo obliga — los ajustes de cada pack tienen que ir y volver
sobre unas preferencias normales, así que un pack que se inventara algo fallaría ahí y no en
ejecución. Otra comprueba que **todos** son reversibles, no solo los dos primeros.

**Access** es el que merece nombrarse: es el pack que más cambia el día a día de quien lo necesita y
el que menos añade. Responder en voz alta, escuchar la palabra de activación, sin animaciones,
dictado encendido. Todo existía ya; solo estaba repartido en cuatro sitios de Personalizar, que es
exactamente el problema que la fase existe para resolver en alguien que no quiere aprenderse cómo se
llama cada capacidad.

**Meeting** es el único que solo APAGA cosas, y lo declara como su requisito: no pide ningún permiso
nuevo, así que se puede activar sin conceder nada. Decirlo es parte de explicarlo.

El panel lista los seis con su estado, y cada fila dice qué le **falta** al pack antes de activarlo:
enterarse después de que necesitaba un permiso que no diste es enterarse tarde.

### D24 — Nivel 5: colaborar con confirmaciones

`SequenceCoordinator` implementa las cuatro obligaciones que el modelo de confianza pone para la
recuperación tras fallo, y están numeradas en el código porque son la razón de que la clase exista:

1. **Detener** al primer fallo. No se sigue "a ver si los demás van".
2. **Constancia del punto exacto** en el Audit Log: qué paso, qué posición ocupaba y cuántos se
   habían aplicado.
3. **Ofrecer revertir** lo ya aplicado, en orden inverso.
4. **Nunca reintentar automáticamente.** No hay camino de reintento en esta clase, ni con espera ni
   sin ella.

Y la que da nombre al nivel: los puntos de riesgo se confirman **de uno en uno, a mitad de la
secuencia**, aunque la secuencia entera ya esté aprobada. Aprobar "haz estas cinco cosas" no es
aprobar la tercera si la tercera borra algo. La confirmación es un callback y no una bandera porque
la respuesta hay que **pedirla en ese momento**: preguntarlo todo al principio es aprobar por
adelantado, que es el nivel 6.

La reversión se **ofrece**, no se impone: revertir sin preguntar también sería decidir por la
persona, solo que hacia el otro lado. Y cuando algo no se pudo revertir, el resultado tiene nombre
propio (`DetenidaSinPoderRevertir`) en vez de mezclarse con el caso bueno — dar por buena una
reversión que no cuajó es el fallo que merece nombrarse aparte.

**El nivel 5 se abre para el acompañante de proyecto y NO para Computer Use**, y esa asimetría es el
argumento, no una precaución genérica: cada paso de proyecto es una edición con su copia previa por
archivo, así que la obligación 3 se puede cumplir; de los métodos implementados de Computer Use solo
el portapapeles sabe volver atrás, de modo que una secuencia que mezclara pulsar controles con
cualquier otra cosa no podría cumplirla. Abrir el nivel ahí sería prometer una recuperación que no
existe. **El nivel 6 sigue cerrado en todas partes.**

`ParseSequence` es un método aparte de `Parse` y no un parámetro: si fuera un parámetro, un descuido
en la llamada convertiría un paso confirmado en cinco. Un bloque mal formado invalida la secuencia
entera, porque saltarse el que no se entiende es peor en una secuencia — los pasos siguientes pueden
darlo por hecho.

**Build y pruebas (Release), acumulado D22-D24:**

```
dotnet build Nexo.slnx -c Release --no-incremental -> Compilación correcta. 0 Advertencia(s). 0 Errores.
dotnet test  Nexo.slnx -c Release --no-build
  Nexo.Core.Tests.dll    -> 1183 superadas
  Nexo.Windows.Tests.dll ->  216 superadas
  Nexo.App.Tests.dll     ->  166 superadas
Total: 1565 pruebas, 0 fallidas, 0 omitidas, 0 warnings. Suite repetida 3 veces sin flakiness.
```

Confirmado que la app sigue arrancando (arranque real del ejecutable publicado, 12 s en pie).

**Pendiente, no bloqueante:**

- **La validación manual sigue sin hacerse.** D22 la acorta —ahora hay un comando que comprueba la
  maquinaria en el equipo real— pero no la sustituye, y lo dice él mismo. Sigue haciendo falta que
  una persona use Kohana un rato: interfaz, dictado, píldora y voz.
- Instalar, actualizar y desinstalar de punta a punta en una máquina limpia sigue pendiente (Fase 9).
- La escalera de métodos sigue en tres de ocho.
- El **nivel 6** sigue cerrado en todas partes, y debería seguir así hasta que exista una razón mejor
  que "ya tocaba".
- Computer Use no encadena pasos, por diseño y no por olvido: sus métodos no saben deshacerse.
- El nivel 5 del proyecto pregunta en **cada** archivo, porque marcar unos como riesgo y otros no
  exigiría un criterio que Kohana no tiene.

**No se hizo push, no se abrió PR, no se hizo merge a `release/kohana-1.0-rc` ni a `main`.**
