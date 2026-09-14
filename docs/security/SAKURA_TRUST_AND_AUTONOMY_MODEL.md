# Modelo de confianza y autonomía de Sakura

> Diseño D3.2. Define cómo Sakura debe comunicar lo que está haciendo y cuánto puede hacer sin
> confirmación explícita, para todas las fases futuras del roadmap
> (`docs/roadmap/SAKURA_TECHNOLOGY_ROADMAP.md`). Este documento es normativo para diseño: toda
> capacidad nueva que observe, escuche o actúe debe poder ubicarse en los estados y niveles de
> autonomía descritos aquí antes de implementarse.

## Principio rector

**Sakura nunca debe pasar silenciosamente de Mirando a Actuando.** Observar contexto autorizado y
ejecutar una acción sobre el sistema, un archivo o una cuenta del usuario son cosas categóricamente
distintas; el paso de una a otra siempre debe ser visible y, salvo en los niveles de autonomía más
bajos, explícitamente confirmado.

## Estados visibles

| Estado | Significado | Indicador |
|---|---|---|
| Dormida | Sin escuchar, sin observar, sin actuar. Estado por defecto. | Ninguno / icono neutro |
| Escuchando | Micrófono activo (push-to-talk, palabra de activación armada, o dictado) | Indicador de voz ya existente en la Shell (`WakeWordIndicator`) |
| Mirando | Contexto visual autorizado en uso (captura de pantalla, ventana activa, OCR) | Planeado — requiere indicador propio en Fase 2 |
| Recordando | Leyendo o escribiendo memoria/contexto persistente | Planeado — requiere indicador propio en Fase 6 |
| Actuando | Ejecutando una acción con efecto fuera de la propia ventana de Sakura | Planeado — requiere indicador propio desde Fase 4/5/7 |

Hoy (Fase 0) Sakura solo implementa **Dormida** y **Escuchando**; los otros tres estados no tienen
indicador todavía porque las capacidades que representan (Fases 2, 4/5/6/7) no están implementadas.
Cuando se implementen, deben añadir su indicador visible como parte del mismo trabajo — un estado
sin indicador no es aceptable en este modelo.

## Niveles de autonomía (Fase 5 y 7)

De menor a mayor autonomía, en el orden en que deben habilitarse:

1. **Ver** — Sakura observa y describe, sin proponer acción.
2. **Guiar** — Sakura señala qué podría hacerse, el usuario lo ejecuta manualmente.
3. **Proponer** — Sakura redacta el plan de una acción concreta, sin ejecutarla.
4. **Ejecutar un paso** — Sakura ejecuta un único paso confirmado explícitamente.
5. **Colaborar con confirmaciones** — Sakura ejecuta varios pasos, confirmando en los puntos de
   riesgo.
6. **Automatizar una secuencia autorizada** — Sakura ejecuta una secuencia completa ya aprobada de
   antemano por el usuario, sin pedir confirmación en cada paso.

Ninguna capacidad nueva puede empezar en el nivel 6: cada una debe demostrarse en los niveles 1–3
antes de solicitar el salto a ejecución.

> **Estado real (Diseño D14).** El acompañante de proyecto (Fase 5) es la primera capacidad que
> llega al **nivel 4**, y llegó por este camino: D12 la implementó en los niveles 1–3 sin ningún
> método de escritura, D13 construyó el Audit Log que exige la sección "Auditoría" de este mismo
> documento, y D14 añadió el snapshot previo reversible que exige la sección "Reversión". El nivel 4
> se abrió cuando esas dos condiciones existieron, no antes. Los niveles 5 y 6 siguen cerrados para
> todas las capacidades. Ver `WorkspaceAutonomyPolicy`, que falla cerrado ante cualquier nivel que no
> reconozca explícitamente.
>
> **Actualización (Diseño D24).** El acompañante de proyecto llega al **nivel 5**. Se abrió cuando
> se pudo cumplir la sección "Recuperación tras fallo" de este documento entera, no antes:
> `SequenceCoordinator` detiene la secuencia al primer fallo, deja constancia del punto exacto,
> ofrece revertir lo ya aplicado y no reintenta nunca por su cuenta. Los puntos de riesgo se
> confirman uno a uno a mitad de secuencia, que es lo que distingue este nivel del 6.
>
> **Computer Use no llega al nivel 5, y es deliberado:** de sus métodos implementados solo el
> portapapeles sabe volver atrás, así que la obligación de "ofrecer revertir lo ya aplicado" no se
> podría cumplir. Abrir el nivel ahí sería prometer una recuperación inexistente. **El nivel 6 sigue
> cerrado para todas las capacidades.**
>
> **Actualización (Diseño D18).** Computer Use (Fase 7) es la segunda capacidad en llegar al nivel 4,
> por el mismo camino: D17 la implementó en los niveles 1–3 sin ejecutar nada, y el salto esperó a
> que existieran el Permission Broker (D16), el Audit Log (D13) y una reversión real. Tiene su
> **propio** nivel, independiente del de la Fase 5: compartirlos haría que subir uno concediera el
> otro, contra la regla de permisos independientes de este documento. La escalera de niveles vive
> ahora en `Nexo.Core.Permissions.AutonomyLevel`, una sola para todas las capacidades; cuál puede
> usarse lo decide la política de cada una.

## Permisos

- **Por aplicación**: qué aplicaciones pueden ser objeto de Context Sources o Action Runtime (p.
  ej. qué ventanas puede leer Lens, qué procesos puede tocar Computer Use).
- **Por capacidad**: cada capacidad de la matriz (`docs/roadmap/SAKURA_CAPABILITY_MATRIX.md`) tiene
  su propio permiso, independiente de las demás — habilitar Lens no habilita Computer Use.
- **Exclusiones**: el usuario puede excluir aplicaciones, carpetas o tipos de dato específicos de
  cualquier capacidad, incluso si la capacidad en general está habilitada.

## Confirmaciones obligatorias

Las siguientes categorías **siempre** requieren confirmación explícita, sin importar el nivel de
autonomía configurado:

- Datos sensibles (credenciales, tokens, información personal identificable expuesta en pantalla).
- Credenciales y contraseñas.
- Pagos o cualquier acción con efecto financiero.
- Borrados irreversibles (archivos, historial, configuración).
- Elevación administrativa (UAC o equivalente).
- Publicación externa (enviar algo fuera del equipo del usuario: correo, red social, API pública).
- Comandos de sistema con efecto amplio (reinicio, cambios de red, cambios de seguridad).

## Auditoría

Toda acción de nivel 4 en adelante (ver niveles de autonomía) debe quedar registrada en el Audit
Log (capa 11 de `docs/architecture/SAKURA_CAPABILITY_ARCHITECTURE.md`) con: qué se hizo, cuándo,
con qué permiso, y cómo revertirlo si aplica. Los logs de diagnóstico actuales por subsistema
(`command-center.log`, etc.) no cumplen este propósito por sí solos — son un punto de partida
técnico, no el Audit Log orientado al usuario que este modelo requiere.

## Reversión

Toda acción de las Fases 4, 5 y 7 que modifique estado persistente (configuración del sistema,
archivos de proyecto) debe generar un snapshot previo reversible antes de ejecutarse. Sin snapshot
previo, la acción no debe ofrecerse en niveles de autonomía 4 o superiores.

## Recuperación tras fallo

Si una acción falla a mitad de ejecución, Sakura debe: (1) detener la secuencia, (2) dejar
constancia en el Audit Log del punto exacto de fallo, (3) ofrecer revertir lo ya aplicado usando el
snapshot previo, y (4) nunca reintentar automáticamente una acción de riesgo alto sin confirmación
nueva.

## Límites de agentes

Ningún componente de Sakura puede lanzar un segundo proceso de Sakura con permisos distintos a los
del proceso principal, ni delegar una acción a un componente externo sin que esa delegación pase
por el mismo Permission Broker y quede en el mismo Audit Log que una acción hecha directamente por
Sakura.

## Política de mínimo privilegio

Cada capacidad solicita únicamente los permisos que necesita para su función actual, no los que
podría necesitar en una fase futura. Ampliar el alcance de una capacidad ya habilitada (p. ej.
Lens pasando de "leer ventana activa" a "leer todas las ventanas") requiere una nueva confirmación
explícita, no se hereda del permiso original.
