# Plan de versión estable — Kohana 1.0

Rama de trabajo: **`release/kohana-1.0-rc`**. No se modifica `main` directamente.

## Reglas de ejecución (aplican a toda fase)

Cada fase termina obligatoriamente con:
1. código que **compila** en Release;
2. **pruebas** ejecutadas y registradas;
3. **commit atómico**;
4. actualización de `IMPLEMENTATION_LOG.md`;
5. lista de **riesgos**;
6. **criterio explícito** para continuar.

Prohibiciones permanentes:
- No leer el repositorio completo en cada fase; leer solo archivos relacionados.
- No reescrituras masivas por estética o preferencia.
- No renombrar namespaces, proyectos o rutas sin plan de migración y justificación objetiva.
- No cambiar una prueba para que pase si la prueba describe el comportamiento correcto.
- No inventar métricas.

## Fase 0 — Baseline y documentos ✅ (esta sesión, sin código)

Crear `docs/stable-release/*` y `ADR/*`. Registrar decisiones A–K.
**Pendiente de completar desde Windows:** conteo de tests, warnings, tiempos, tamaños, hash.
Sin baseline medido no se puede declarar ninguna mejora posterior.

## Fase 1 — Extracción del Runtime + DI ⬅ **desbloquea todo lo demás**

Se ejecuta en **7 pasos**, cada uno compilable, con pruebas y commit propio.
Está **prohibido** hacerlo como reescritura masiva.

| Paso | Contenido | Criterio de salida |
|---|---|---|
| 1.1 | **Pruebas de caracterización** del comportamiento actual de `MainWindow` | Tests que capturan lo que hoy hace, antes de mover nada |
| 1.2 | **Composition root + DI** sin cambiar comportamiento | Servicios resueltos por contenedor; conducta idéntica |
| 1.3 | Extracción del **coordinador de voz** | `MainWindow` deja de orquestar voz |
| 1.4 | Extracción del **coordinador de navegación** | Vistas y shell separados de la lógica |
| 1.5 | Extracción de **tareas, enfoque y rutinas** | |
| 1.6 | Extracción de **IA y Vision** | |
| 1.7 | `MainWindow` queda como **vista y eventos mínimos** | < 500 líneas objetivo |

Contenedor: `Microsoft.Extensions.DependencyInjection` (MIT).
Riesgo: **alto** — es el archivo más grande. Mitigación: pasos pequeños, sin cambio de conducta,
caracterización primero.

## Fase 2 — Hardware Capability Profile + Adaptive Engine Registry

Dos sistemas **independientes**, prohibido fusionarlos:

- **A. Hardware Capability Profile** → *"¿Qué puede ejecutar bien esta computadora en general?"* (estático)
- **B. Resource Governor** → *"¿Qué puede ejecutar ahora mismo sin molestar al usuario?"* (dinámico, ya existe)

Detección: versión de Windows, arquitectura, CPU, núcleos lógicos, RAM, GPU integrada/dedicada, VRAM,
aceleradores, batería, alimentación, capacidades ONNX, aceleración disponible, espacio libre.

Etiquetas para el usuario: **Ligero · Equilibrado · Acelerado**.
Selección del usuario: **Automático · Ahorro · Equilibrado · Máximo rendimiento**.

Cada motor declara: tipo · estado (`Estable`/`FallbackHeredado`/`Candidato`/`Experimental`) ·
requisitos · consumo estimado · latencia estimada · soporte de streaming · local o remoto ·
licencia verificada · idiomas · disponibilidad · razón de selección.

**Prohibido descargar modelos automáticamente sin consentimiento claro.**

## Fase 3 — Kohana Voice Lab (medir antes de sustituir)

Banco reproducible + interfaces nuevas: `IVoiceActivityDetector`, `ISpeechToTextEngine`,
`ITextToSpeechEngine`, `IAudioEchoCancellationService`, `IVoicePipelineCoordinator`.
Se conservan y mejoran `IWakeWordService`, `IVoiceInputService`, `IVoiceOutputService`.

Métricas mínimas: latencia de detección · falso positivo · falso negativo · CPU · RAM ·
comportamiento con ruido · distancia · pronunciación mexicana · recuperación tras suspensión ·
funcionamiento durante juego · texto reconocido · motivo de aceptación/rechazo.

Cada frase de activación (`Oye Kohana`, `Kohana`, `Ey Kohana`, `Hey Kohana`) se mide **por separado**.

**En esta fase no se sustituye ningún motor.** Solo se generan los números que justifiquen (o no)
sustituirlos. No se guarda audio salvo modo dataset con consentimiento explícito (`PRODUCT_VISION` §H).

Para cada candidato (openWakeWord, Silero VAD, Piper, Kokoro, Chatterbox, Moonshine…):
verificar repositorio oficial → licencia actual → soporte Windows → compatibilidad .NET →
distribución de modelos → adaptador aislado → medir → comparar contra fallback → documentar → decidir.

## Fase 4 — Pipeline de voz según evidencia

Solo se promueve un motor si **gana medido** contra el fallback. Vosk y SAPI permanecen como fallback
seleccionable hasta que exista evidencia y un periodo de estabilidad.
Incluye: streaming por oración, barge-in, AEC, detención inmediata de la voz.

## Fase 5 — Agencia tipada

Planes con: acción · riesgo · datos utilizados · permiso · resultado esperado · reversibilidad ·
resultado real. Niveles `Bloqueado`/`Preguntar`/`Permitido` generalizados más allá de rutinas.
Separación visible: Conversación · Acción local · Plan · Confirmación · Resultado · Error recuperable.

Garantía dura: **"Abre Spotify" nunca llega al LLM.**
Prohibido: que el LLM genere y ejecute PowerShell arbitrario sin capacidad aislada, manifiesto,
validación y confirmación.

## Fase 6 — Memoria transparente (opt-in)

Estructura de archivos + **búsqueda literal primero**. Índice semántico solo tras benchmark y
verificación de licencia del modelo de embeddings. Diario automático desactivado por defecto.

## Fase 7 — Vision: OCR + UI Automation

Cambio de TFM a `net10.0-windows10.0.26100.0`. Pipeline de 8 pasos. UIA sobre coordenadas de píxel;
coordenadas solo como fallback marcado y limitado.

## Fase 8 — Skills oficiales + subagentes como perfiles

5 skills estables + 1 experimental (`PRODUCT_VISION` §K). Manifiesto con los 15 campos declarados.
Permisos **aplicados técnicamente**, no solo declarados. Subagentes como perfiles, no procesos pesados.

## Fase 9 — Actividad y privacidad + accesibilidad + endurecimiento

Pantalla "Actividad y privacidad". `AutomationProperties` en todo el XAML. Kill switch.
Secretos en almacenamiento seguro de Windows.

## Fase 10 — RC

Pruebas completas, instalador, portable, notas de versión, SHA-256, lista exacta de bloqueadores.

## Criterio de honestidad

Si una versión verdaderamente estable no cabe de forma responsable, **no se entrega una falsa 1.0**.
Se entrega RC + lista exacta de bloqueadores + plan de cierre. Ver `KNOWN_LIMITATIONS.md`.

## Adenda — sesión de planificación de roadmap (2026-07-28)

El propietario del producto pidió que el agente asuma el rol de líder técnico del proyecto:
decide tecnologías, investiga alternativas y ejecuta, sin pedirle a él decisiones de
implementación (solo de producto/visión). Registrado en detalle en la memoria del agente; resumen
aquí para que quede también en el repositorio.

**Proceso:** Kanban continuo, sin fechas fijas. Horizonte inicial de 3-6 meses tratado como
aspiración, no límite — se prefirió **alcance completo** (todas las fases de este documento, no un
subconjunto) sobre recortar para caber en una fecha. Autonomía: el propietario aprueba fases
grandes; el agente decide los detalles de implementación sin pausar a confirmar cada uno.
Checkpoints portables solo en hitos, no en cada subfase.

**Cambios/precisiones de alcance por fase, sobre lo ya escrito arriba:**

- **Fase 2:** la detección de hardware debe ser real (nunca asumir GPU integrada o dedicada; medir
  y adaptar). Se añade **"Optimizar dispositivo"**: asesor opt-in y reversible de ajustes de
  rendimiento de Windows (apps de arranque, efectos visuales, plan de energía), con lista
  transparente de qué cambia y por qué, usando solo APIs Win32 documentadas para no disparar falsos
  positivos de antivirus.
- **Fase 3:** además de los candidatos ya listados, evaluar concretamente **openWakeWord**
  (sustituto de Vosk basado en ONNX), **Piper TTS** (sustituto de SAPI5, sin binding .NET maduro
  todavía — medir antes de asumir) y **Silero VAD vía `VadSharp`** (sustituto de la heurística
  manual de fin de turno en `WindowsVoiceInputService`). Idiomas: plan real (no solo aspiracional)
  de cubrir español mexicano **e inglés**, cada frase calibrada por separado como ya exige ADR 0004.
- **Fase 5:** todas las categorías de acción importan por igual, pero red/internet e instalación de
  software merecen fricción/escrutinio extra en el modelo de permisos.
- **Fase 6:** decisión de tecnología de memoria (delegada al agente): archivos locales
  estructurados siguiendo el mismo patrón que `JsonTaskStore`/`JsonFocusStore`/`JsonRoutineStore`,
  cifrado en reposo con DPAPI de Windows (sin gestión de contraseña), búsqueda literal primero
  (ya exigido por este documento, no cambia). Recordar por igual: preferencias, contexto de
  conversación y hábitos de uso — las tres, opt-in.
- **Fase 8:** Tareas/Enfoque/Rutinas se conserva, pero el valor diferenciador reconocido es la
  automatización por voz con permisos aplicados (Rutinas), no la lista de tareas plana — al pulir
  esta fase, bajarle peso visual a lo segundo. Contribuciones de terceros: cerradas por ahora,
  revisar más adelante.
- **Fase 9:** revisar cuánta información de sistema expone la vista de diagnóstico (posible
  exceso de detalle sensible) — no bloqueante, revisar cuando se retome esta fase.
- **Fase 10:** ruta de firma sin costo — el repositorio (`EXOTARA/Sakura`) ya es público, el
  `LICENSE` (MIT) ya está, y desde el 2026-08-22 también están las dos páginas que **SignPath
  Foundation** exige (firma de código y privacidad) y los metadatos de producto/versión que verifica
  el flujo de publicación. Queda la solicitud, que la hace Adler. Con firma,
  se habilita auto-actualización real con **Velopack**, cumpliendo la condición que
  `PRODUCT_VISION` §I ya exigía ("automática solo cuando existan firma y rollback probado"). Se
  añade **Microsoft Store (MSIX)** como canal adicional a la distribución directa ya decidida; la
  build de Store puede tener menos capacidades por sandboxing si queda claramente marcado ante el
  usuario.
- **Nueva, sin fase numerada:** rediseño de interfaz (dirección visual tipo Apple — píldoras para
  respuestas, volumen, acciones) sobre el shell Hub/Peek/Capsule ya existente. Se hace después de
  cubrir las carencias funcionales, no antes.
- **Sostenibilidad:** el producto sigue siendo 100% gratuito, sin muro de pago ni funciones
  bloqueadas. Único añadido: enlace opcional de apoyo (GitHub Sponsors/Ko-fi) en Ajustes/Acerca de.
