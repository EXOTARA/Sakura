# Roadmap tecnológico de Sakura

> Diseño D3.2. Fases reales, no aspiracionales: una fase solo se marca "Implementada" si el código
> correspondiente existe y tiene pruebas en `release/kohana-1.0-rc` hoy. Infraestructura parcial se
> marca "Parcial", nunca "Implementada". Ver el detalle capacidad por capacidad en
> `docs/roadmap/SAKURA_CAPABILITY_MATRIX.md` y la arquitectura de capas en
> `docs/architecture/SAKURA_CAPABILITY_ARCHITECTURE.md`.

## Fase 0 — Stable Shell and Daily Core

**Estado: Implementada** (Diseños D1, D1.1, D2, D3, D3.1, D3.2).

- Objetivo: una base estable de escritorio — shell, navegación, voz, motores y flujo diario —
  antes de construir cualquier superficie ambiental encima.
- Valor: Sakura es usable y confiable hoy mismo, sin depender de fases futuras.
- Dependencias: ninguna (es la base).
- Tecnologías: WPF (.NET 10), Whisper (voz→texto), Vosk (palabra de activación), SAPI
  (texto→voz), `IHardwareCapabilityService`, `IAdaptiveEngineRegistry`.
- Permisos: ninguno más allá de lo que Windows concede a una app de escritorio estándar; micrófono
  bajo consentimiento del sistema operativo.
- Riesgos: ninguno nuevo — es la superficie más probada del proyecto (804 pruebas en D1.1, 1036+
  tras D3.2).
- No objetivos: nada ambiental, nada de visión de pantalla, nada de automatización de acciones.
- Criterio de terminado: cumplido — Hardware Capability Profile, Engine Registry inicial, voz base,
  Sakura Shell, navegación, personalización, Command Center, Daily Flow, Focus Continuity y
  perfiles aislados de validación (Diseño D3.2) están todos implementados y probados.
- Sprints que la componen: D1, D1.1, D2 (Sakura Command Center), D3 (Sakura Daily Flow), D3.1
  (Focus Continuity), D3.2 (este sprint: aprobación, integración, aislamiento, visión y roadmap).

## Fase 1 — Ambient Interaction Foundation

**Estado: Implementada** (Diseño D4 — D4.1 + D4.2 + D4.4 —, validado manualmente por el usuario e
integrado en `release/kohana-1.0-rc` — ver `docs/stable-release/IMPLEMENTATION_LOG.md`, sección
"Diseño D4").

- Objetivo: que Sakura pueda responder brevemente sin que el usuario tenga que abrir ni enfocar la
  ventana principal.
- Valor: reduce la fricción de cambiar de contexto para interacciones cortas — la primera pieza
  real de la visión "ambiental".
- Dependencias: Fase 0 completa (Command Center como origen de acciones, Engine Registry para
  saber qué motores están disponibles).
- Tecnologías: ventanas WPF no activables (`WS_EX_NOACTIVATE` / `ShowActivated=false`, ya usado en
  `HiddenWindowHost` de las pruebas y en el patrón de captura sin foco), un host de overlay nuevo.
- Permisos: ninguno nuevo más allá de los ya usados por voz y Command Center.
- Riesgos: una ventana que "no roba foco" mal implementada puede robar foco de todas formas en
  casos límite (multi-monitor, DPI mixto) — requiere validación interactiva real, no solo pruebas
  unitarias.
- No objetivos: nada de captura de pantalla, OCR ni control de otras aplicaciones (eso es Fase 2 en
  adelante).
- Criterio de terminado: Sakura Pill Host visible, ciclo de vida de una solicitud (Escuchando →
  Pensando → Resultado) con estados observables, resultado corto con expansión opcional, cancelar,
  deshacer cuando aplica, historial de solicitudes, primitivas de permisos, Context Snapshot de la
  ventana activa, integración inicial con Command Center.
- Sprint sugerido: **D4 — Ambient Interaction Foundation** (alcance detallado en la Sección 14).

## Fase 2 — Sakura Lens

**Estado: Implementada** (Diseño D5 — D5.1-D5.7, TFM, OCR real, UI Automation, redacción de texto e
imagen, indicador "Mirando", los tres modos —soporte/estudio/desarrollo— y resaltado visual—,
validado manualmente por el usuario e integrado en `release/kohana-1.0-rc` — ver
`docs/stable-release/IMPLEMENTATION_LOG.md`, sección "Diseño D5").

- Objetivo: que Sakura pueda observar y explicar lo que hay en pantalla (con autorización), no
  actuar sobre ello todavía.
- Valor: soporte técnico, estudio y desarrollo asistido por contexto visual real, sin depender de
  que el usuario describa lo que ve.
- Dependencias: Fase 1 (superficies ambientales para mostrar resultados sin robar foco).
- Tecnologías: UI Automation, captura de pantalla autorizada (ya existe `IScreenCaptureService`
  como base), OCR, un modelo visual (VLM), análisis de región.
- Permisos: captura de pantalla y ventana activa requieren consentimiento explícito y visible (ver
  estado "Mirando" en el modelo de confianza).
- Riesgos: exposición accidental de información sensible visible en pantalla; requiere exclusiones
  y redacción antes de cualquier envío a un proveedor externo.
- No objetivos: control automático de otras aplicaciones — la primera versión observa y guía.
- Criterio de terminado: modo soporte, modo estudio y modo desarrollo funcionando sobre la ventana
  activa, con resaltados y guía visual, sin ninguna acción automática sobre terceros.
- Sprints sugeridos: al menos dos — "Lens: captura y OCR" y "Lens: guía visual y modos".

## Fase 3 — Sakura Flow

**Estado: Implementada** (Diseño D6 — D6.1-D6.3: atajo global `Ctrl + Shift + D`, transcripción sin
normalizar para dictado, puntuación hablada, muletillas, diccionario, atajos, los tres modos
—texto/correo/código— e inserción universal con guardia de foco. Validado manualmente por el usuario
e integrado en `release/kohana-1.0-rc` — ver `docs/stable-release/IMPLEMENTATION_LOG.md`, sección
"Diseño D6". La interfaz para editar diccionario y atajos llega en D7).

- Objetivo: dictado global de alta calidad en cualquier aplicación de Windows.
- Valor: reemplaza el cambio de ventana para escribir texto largo por voz.
- Dependencias: Fase 1 (Voice Bar como superficie ambiental).
- Tecnologías: push-to-talk global, Whisper (ya integrado), puntuación y eliminación de muletillas,
  diccionario personalizado, snippets, inserción universal de texto.
- Permisos: acceso a micrófono global (fuera del foco de Sakura) y a la inserción de texto en la
  aplicación activa.
- Riesgos: inserción de texto en el lugar equivocado si el foco cambia durante el dictado.
- No objetivos: transformación de código o edición de proyectos (eso es Fase 5).
- Criterio de terminado: modo texto, correo y código funcionando con inserción universal confiable.
- Sprint sugerido: "Flow: dictado global v1".

## Fase 4 — Adaptive Computer Optimization

**Estado: Parcial** (Diseños D8 + D11 en `design/kohana-sprints-d7-d9`. El criterio de terminado está
cubierto en código: los siete escenarios proponen un plan basado en el hardware real, la aplicación
exige confirmación y snapshot previo, la reversión se **verifica releyendo** el estado, un fallo a
mitad deshace lo ya aplicado, y todo queda en un registro de auditoría consultable. Sakura aplica
dos objetivos reversibles con certeza —el plan de energía y su propio modo de rendimiento—; el resto
del plan son consejos que ejecuta la persona. Sigue marcada **Parcial** por la regla de este
documento: el código no está en `release/kohana-1.0-rc` todavía y falta la validación manual del
usuario).

- Objetivo: que el usuario pueda pedir "optimiza mi computadora para X" y Sakura proponga y aplique
  (con confirmación) cambios reversibles basados en el hardware real.
- Valor: convierte el ya existente `HardwareCapabilityProfile` y `AdaptiveEnginePolicy` —hoy usados
  solo para elegir motores propios— en una capacidad que beneficia a todo el equipo, no solo a
  Sakura.
- Dependencias: Fase 0 (Hardware Capability Profile, Engine Registry como base del Capability
  Router).
- Tecnologías: perfiles de optimización, plan de cambios con simulación previa, snapshots del
  estado anterior, reversión y restauración automática, auditoría.
- Permisos: cambios de configuración del sistema — nivel de riesgo alto, requiere confirmación
  explícita y snapshot previo obligatorio (ver modelo de confianza).
- Riesgos: un cambio de sistema mal revertido puede dejar el equipo en peor estado — el snapshot y
  la reversión no son opcionales, son requisito de diseño.
- No objetivos: no es una lista genérica de "tweaks" de internet — cada cambio debe justificarse
  con una medición real del equipo.
- Criterio de terminado: los siete comandos objetivo (jugar, programar, edición de video,
  videollamada, batería, general, restaurar) funcionando con plan, confirmación, aplicación y
  reversión completa verificada.
- Sprints sugeridos: "Optimización: snapshots y reversión", "Optimización: perfiles por escenario".

## Fase 5 — Project Companion

**Estado: Parcial** (Diseños D12 + D13 + D14 en `design/kohana-sprints-d7-d9`: carpeta de trabajo
autorizada con contención real de rutas, exclusión de archivos de secretos y de carpetas de
dependencias, detección de secretos en código antes de enviar nada, búsqueda dentro del proyecto, y
los niveles 1–5 del modelo de autonomía. **D14 abre la escritura**, con checkpoint reversible por
archivo comprobado antes de tocar nada, verificación releyendo, reversión de lo aplicado si un paso
falla, negativa a deshacer si la persona editó el archivo después, y registro en el Audit Log.
**D24 abre el nivel 5**: varios cambios encadenados, confirmando archivo por archivo, parada al
primer fallo con constancia del punto exacto y reversión de lo ya aplicado en orden inverso — se
abrió aquí y no en la Fase 7 porque aquí cada paso tiene su copia previa y la obligación de revertir
sí se puede cumplir. El nivel 6 sigue cerrado y el nivel por omisión sigue siendo `Guiar`. Sin
integrar a release y pendiente de validación manual, que aquí pesa más que en ningún otro sprint
porque se escriben archivos reales).

- Objetivo: que Sakura trabaje junto al usuario dentro de un proyecto de código autorizado —desde
  guiar hasta ejecutar cambios— con el nivel de autonomía que el usuario elija.
- Valor: acompaña tareas de desarrollo reales (archivos, terminal, Git, pruebas) sin sustituir al
  IDE.
- Dependencias: Fase 2 (relación entre pantalla, terminal y código) y el modelo de confianza
  completo (los cinco modos de autonomía: Guía, Observador, Copiloto, Colaborador, Agente).
- Tecnologías: workspace autorizado, búsqueda y edición de archivos, diffs, terminal, build,
  pruebas, Git, checkpoints, detección de secretos.
- Permisos: acceso de lectura/escritura a un workspace explícitamente autorizado, nunca a todo el
  disco.
- Riesgos: ejecución de comandos irreversibles (borrado, force-push) — requiere los mismos
  principios de snapshot/reversión que la Fase 4.
- No objetivos: no reemplaza revisión humana de cambios significativos por defecto.
- Criterio de terminado: los cinco modos operando con checkpoints y detección de secretos activa
  antes de cualquier acción de escritura.
- Sprints sugeridos: "Companion: workspace y modo Guía" (hecho en D12), "Companion: modo Agente y
  checkpoints" (los checkpoints y el nivel 4 son D14; el modo Agente —niveles 5 y 6— sigue
  pendiente).

> **Nota de vocabulario (D12).** Este documento nombra "cinco modos" (Guía, Observador, Copiloto,
> Colaborador, Agente) y el modelo de confianza nombra seis niveles numerados (Ver, Guiar, Proponer,
> Ejecutar un paso, Colaborar con confirmaciones, Automatizar una secuencia). Son la misma escalera
> con dos vocabularios. **La implementación sigue la del modelo de confianza**, que es la que fija el
> orden de habilitación y la regla de "ninguna capacidad empieza por arriba"; ver
> `WorkspaceAutonomyLevel`.

## Fase 6 — Context and Memory

**Estado: Parcial** (Diseños D9 + D10 en `design/kohana-sprints-d7-d9`: controles de exclusión y
retención funcionando antes que el almacenamiento, tres categorías independientes, cifrado DPAPI en
reposo y búsqueda literal; y ya se llena desde la conversación —solo por petición explícita, o por
propuesta con un sí de por medio—, se usa como contexto de las consultas y se configura desde
Personalizar. Falta llenar la categoría `Habitos`, que necesita algo que mida conducta real y no una
frase suelta. Sin integrar a release y pendiente de validación manual).

- Objetivo: que Sakura recuerde contexto relevante entre sesiones sin convertirse en vigilancia
  permanente.
- Valor: continuidad real (p. ej. retomar una tarea de ayer) sin que el usuario tenga que repetir
  contexto.
- Dependencias: Fases 1–5 como fuentes de contexto a recordar.
- Tecnologías: historial, memoria de proyecto, timeline visual opcional, búsqueda semántica.
- Permisos: retención de datos — requiere controles de retención y redacción de datos sensibles
  visibles y accesibles al usuario.
- Riesgos: acumulación silenciosa de datos sensibles si la retención no tiene límites claros.
- No objetivos: nunca activada por defecto.
- Criterio de terminado: controles de exclusión y retención funcionando antes de que exista
  cualquier almacenamiento de memoria de facto.
- Sprint sugerido: "Memoria: retención y exclusiones antes que almacenamiento".

## Fase 7 — Safe Computer Use

**Estado: Parcial** (Diseños D16 + D17 + D18 + D19 en `design/kohana-sprints-d7-d9`. Las dos
condiciones que esta fase ponía para habilitarse ya existen: el **Permission Broker** (D16) y el
**Audit Log** (D13). D17 implementa el orden estricto de métodos —nunca se baja de escalón habiendo
uno más seguro disponible, y ratón/teclado exigen habilitarse a propósito— y los niveles 1–3. D18
abre el **nivel 4**: una acción confirmada cada vez, verificada releyendo, reversible donde se puede
garantizar, y siempre auditada. D19 sube **UI Automation** al conjunto ejecutable, con negativa ante
ambigüedad, y corrige el alcance de la regla del "más seguro disponible", que ahora se aplica entre
métodos capaces del mismo objetivo. **Tres de los ocho métodos están implementados** —UI Automation,
una lista de comandos de solo lectura y el portapapeles—; los cuatro primeros no se declaran
disponibles porque Sakura no sabe ejecutarlos, y ratón/teclado no está implementado en absoluto. Sin
integrar a release y pendiente de validación manual).

- Objetivo: permitir que Sakura ejecute acciones reales sobre el equipo, siempre por el camino más
  seguro disponible primero.
- Valor: cierra el círculo entre "observar" (Lens) y "actuar" con el menor riesgo posible en cada
  paso.
- Dependencias: modelo de confianza completo (Fase 0), Fase 2 para saber qué hay en pantalla.
- Tecnologías, en orden de preferencia: API oficial → Windows App Actions → MCP → integración
  nativa → UI Automation → shell seguro → portapapeles → mouse/teclado simulados como último
  recurso.
- Permisos: el más alto del roadmap — requiere el Permission Broker y el Audit Log completos antes
  de habilitarse.
- Riesgos: automatizar mouse/teclado es frágil e inseguro si se usa como primera opción en vez de
  último recurso — de ahí el orden estricto.
- No objetivos: no se salta niveles del modelo de autonomía (Ver → Guiar → Proponer → Ejecutar un
  paso → Colaborar con confirmaciones → Automatizar una secuencia autorizada).
- Criterio de terminado: los seis niveles de autonomía disponibles y auditables para al menos una
  herramienta de cada categoría de la lista de preferencia.
- Sprints sugeridos: "Computer Use: niveles Ver/Guiar/Proponer" (hecho en D17), "Computer Use:
  ejecución y auditoría" (hecho en D18, en el nivel 4).

> **Distancia real al criterio de terminado (actualizado en D19).** El criterio pide los seis
> niveles *"para al menos una herramienta de cada categoría de la lista de preferencia"*, y ahí es
> donde queda lo gordo: hay herramienta para **tres** categorías de ocho (UI Automation, shell seguro
> y portapapeles), y los niveles llegan hasta el 4 de 6. Lo que sí está terminado es la parte que
> decide: el orden de preferencia, el broker, la auditoría y la reversión. D19 lo demostró sin
> quererlo — añadir UI Automation no exigió tocar la política ni el coordinador; bastó con
> declararlo disponible y la escalera lo eligió sola.

## Fase 8 — Skills Platform

**Estado: Parcial** (Diseños D15 + D23 en `design/kohana-sprints-d7-d9`: **los seis packs** que
nombra la fase —Study, Dev, Support, Creator, Access y Meeting—, hechos exclusivamente con
capacidades ya implementadas, con panel propio en Personalizar. Un pack solo escribe preferencias y
**nunca concede un permiso**: lo que necesita y no puede activar por su cuenta —memoria, Vision,
carpeta de proyecto— lo declara como requisito y dice dónde se da. Activar guarda el estado anterior
y desactivar lo devuelve; un pack activo a la vez, y todos reversibles con prueba que lo obliga. El
criterio de terminado de la fase —*"al menos dos packs completos usando exclusivamente capacidades
ya implementadas"*— está cumplido con margen. Sigue **Parcial** solo por la regla de este documento:
el código no está en `release/kohana-1.0-rc` y falta la validación manual).

- Objetivo: empaquetar combinaciones de capacidades anteriores en "packs" con propósito claro.
- Valor: un usuario no técnico puede activar "Sakura Study" sin entender qué capacidades incluye.
- Dependencias: Fases 1–7 (los packs combinan capacidades ya existentes, no inventan nuevas).
- Tecnologías: sistema de packs (Dev, Study, Support, Creator, Access, Meeting) sobre la misma
  arquitectura de capacidades.
- Permisos: cada pack hereda los permisos de las capacidades que combina — no introduce
  excepciones.
- Riesgos: fragmentación si cada pack termina con su propia lógica en vez de reutilizar capacidades
  comunes.
- No objetivos: no es un marketplace de terceros en esta fase.
- Criterio de terminado: al menos dos packs completos usando exclusivamente capacidades ya
  implementadas en fases anteriores.
- Sprints sugeridos: uno por pack, empezando por "Skills: Sakura Study" y "Skills: Sakura Dev"
  (ambos hechos en D15, en un solo sprint por ser listas de ajustes y no lógica propia).

## Fase 9 — Productization

**Estado: Parcial** (Diseños D20 + D21 en `design/kohana-sprints-d7-d9`. `SakuraDataInventory`
describe todo lo que Sakura guarda y de él cuelgan las tres piezas: copia **verificada** de los datos
antes de actualizar, con vuelta atrás y con la regla de que un archivo no verificado impide declarar
segura la actualización; plan de desinstalación que separa la app de tus datos y enseña las dos
listas; y paquete de soporte exportable, redactado con las dos herramientas y que enumera lo que
dejó fuera, más un informe de privacidad que dice qué se guarda, dónde, cifrado o no y cómo borrarlo.
**Lo que falta es lo que no depende del código de la app**: instalar, actualizar y desinstalar de
verdad siguen siendo del instalador, y el criterio de terminado pide las tres verificadas de punta a
punta en una máquina limpia. Sin integrar a release y pendiente de validación manual).

- Objetivo: llevar Sakura de "build interno validado por el usuario" a producto distribuible.
- Valor: onboarding, actualización y soporte reales para usuarios que no son parte del desarrollo.
- Dependencias: todas las anteriores en la medida en que definan qué se onboardea/actualiza.
- Tecnologías: onboarding (ya existe una versión inicial — `OnboardingWindow`), comprobación de
  hardware (ya existe `IHardwareCapabilityService`), selección y descarga de modelos, permisos,
  instalador, actualizador, recuperación, diagnóstico, exportación de soporte, distribución.
- Permisos: instalación y actualización requieren los permisos de sistema habituales de un
  instalador de Windows.
- Riesgos: un actualizador mal diseñado puede romper instalaciones existentes — requiere el mismo
  rigor de reversibilidad que la Fase 4.
- No objetivos: no incluye tiendas de terceros ni distribución fuera de los canales que el usuario
  apruebe.
- Criterio de terminado: instalación, actualización y desinstalación limpias verificadas de punta a
  punta, con diagnóstico exportable para soporte.
- Sprints sugeridos: "Productization: instalador y actualizador" (D20 hizo la mitad reversible: la
  copia verificada y el plan de desinstalación; el instalador en sí sigue pendiente),
  "Productization: diagnóstico y soporte" (hecho en D21).

> **Distancia real al criterio de terminado (D21).** El diagnóstico exportable está. Lo que falta es
> la otra mitad de la frase — *"instalación, actualización y desinstalación limpias verificadas de
> punta a punta"* —, y no se cierra escribiendo más código de la aplicación: exige instalar,
> actualizar y desinstalar en una máquina limpia y comprobar el resultado. Es trabajo de validación,
> no de implementación, y conviene no confundirlo con lo segundo.

---

## Fase recomendada a continuación (Sección 14 del encargo D3.2)

## D4 — Ambient Interaction Foundation

Sprint grande recomendado inmediatamente después de Diseño D3.2. Implementa la Fase 1 completa.

**Alcance previsto:**

- Sakura Pill Host.
- Ventanas no activables que no roban foco.
- Request lifecycle (ciclo de vida de una solicitud).
- Estados de request (Escuchando / Pensando / Resultado).
- Resultado corto y expandible.
- Una o dos acciones rápidas por resultado.
- Cancelar.
- Deshacer cuando aplique.
- Request History (historial de solicitudes).
- Audit básico.
- Permission primitives (primitivas de permisos, no el Permission Broker completo de la Fase 7).
- Context Snapshot de la ventana activa.
- Integración inicial con Command Center.

**Explícitamente fuera de alcance para D4:** Lens completa (Fase 2), Flow (Fase 3) y Computer Use
(Fase 7) — D4 sienta las bases ambientales; esas tres fases se construyen encima, no dentro de D4.

**Estado real (actualizado tras D4.1 + D4.2 + D4.4):** Sakura Pill Host, ventanas no activables,
ciclo de vida de solicitud, resultado corto/expandible, cancelar, deshacer (tanto en la solicitud
visible como en el historial), primitivas de permisos, Context Snapshot, historial de solicitudes
visible e integración inicial con el Command Center están implementados, probados, validados
manualmente por el usuario e integrados en `release/kohana-1.0-rc` — ver
`docs/stable-release/IMPLEMENTATION_LOG.md`, sección "Diseño D4".
