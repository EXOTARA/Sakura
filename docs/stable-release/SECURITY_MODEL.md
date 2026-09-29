# Modelo de seguridad — Kohana 1.0

## Principio rector

**Mínimo privilegio por defecto.** Los permisos se amplían conscientemente, nunca al revés.
Los permisos se **aplican técnicamente**; un manifiesto declarativo no es un control de seguridad.

## Matriz de permisos (normativa — decisión F)

### Nivel `Permitido` — sin confirmación adicional
Aplica **solo** cuando la petición viene directamente del usuario (voz, paleta o UI).

| Acción | Reversible | Nota |
|---|---|---|
| Abrir aplicaciones | Sí | Un programa ordinario, con o sin argumentos (`code .`). Scripts, instaladores, accesos directos, rutas de red, URL, o binarios que ejecutan cosas (`forfiles`, `msiexec`, `schtasks`, `reg`…), o un shell con argumentos → `Preguntar` (0.30.38). Los argumentos también pasan por las comprobaciones de extensión, UNC y esquema |
| Abrir carpetas conocidas | Sí | Lista blanca; rutas arbitrarias → `Preguntar` |
| Mostrar configuraciones | Sí | |
| Cambiar volumen general | Sí | |
| Controlar audio por aplicación | Sí | |
| Crear tareas | Sí | |
| Iniciar enfoque | Sí | |
| Ejecutar rutinas **previamente aprobadas** | Sí | La aprobación ocurrió al crearla |
| Captura bajo demanda | Sí | Sujeta a `PRIVACY_BOUNDARIES` |
| Consultar estado del sistema | Sí | |
| **Abrir PowerShell** | Sí | **Abrir la terminal ≠ ejecutar en ella.** Vale para la orden directa y para abrirlo sin argumentos como paso de rutina (`OpenApplication`); con argumentos (`-Command …`) pide confirmación |

### Nivel `Preguntar` — siempre requiere confirmación explícita

| Acción | Motivo |
|---|---|
| **Ejecutar un comando arbitrario dentro de PowerShell** | Ejecución arbitraria |
| Escribir, modificar o borrar archivos | Pérdida de datos |
| Ejecutar scripts o comandos arbitrarios | Ejecución arbitraria |
| Instalar software | Cambio persistente del sistema |
| Enviar información a internet | Salida de datos |
| Publicar, enviar o comprar | Efecto externo irreversible |
| Usar credenciales | Exposición de secretos |
| Cualquier acción irreversible | Sin rollback |
| Skill que solicite `network:true` | Exfiltración potencial |
| Elevar privilegios | Escalada |

> **La distinción PowerShell es normativa y debe estar cubierta por una prueba de regresión.**
> **Estado real del código:** `AutomationActionType.OpenTerminal` sigue siendo `Sensitive` (como paso
> de rutina pide confirmación); la orden directa «abre PowerShell» no pasa por esa ruta y no pregunta.
> El objetivo de pasarlo a `Reversible` no se ha aplicado. `ExecuteShellCommand` **no existe**: no hay
> acción de ejecutar comandos. `OpenTerminal` abre `powershell.exe -NoExit` con la carpeta como
> directorio de trabajo y no reenvía argumentos del llamador.

### Nivel `Bloqueado`
Todo lo no declarado explícitamente. El default de un tipo de acción desconocido es **bloquear**,
nunca permitir.

## Defensa por capas

1. **Mínimo privilegio** — cada capacidad declara lo que necesita; nada hereda acceso global.
2. **Identidad del actor** — se distingue: usuario · modelo · skill · automatización programada.
   Una instrucción originada en contenido leído **nunca** tiene autoridad de usuario.
3. **Autoridad para aprobar** — se verifica que quien aprueba *tenga* autoridad para aprobar.
   (Falla raíz de ClawJacked/CVE-2026-33579: se comprobaba el acceso, no la autoridad.)
4. **Permisos efectivos** — aplicados en el ejecutor, no confiados al manifiesto.
5. **Aislamiento** — skills con capacidad sensible se ejecutan acotadas.
6. **Validación de entradas** — todo texto proveniente de OCR, web, archivos o mensajes es **dato**,
   nunca instrucción.
7. **Planes previos** — acciones compuestas se muestran antes de ejecutarse.
8. **Confirmaciones** — específicas, no genéricas; describen la acción real.
9. **Límites** — tiempo, costo y alcance por subagente y por skill.
10. **Cancelación** — todo trabajo largo es cancelable.
11. **Logs y auditoría comprensible** — legibles por humano, no solo para depurar.
12. **Secretos** en almacenamiento seguro de Windows (DPAPI/Credential Manager), nunca en
    `settings.json` ni en el repositorio.
13. **Red visible** — el usuario ve cuándo algo sale del equipo.
14. **Reversibilidad** cuando sea posible.
15. **Kill switch** — corta voz, IA, Vision y automatizaciones de inmediato.

## Modelo de amenaza (lo que se defiende explícitamente)

| Amenaza | Mitigación en Kohana |
|---|---|
| **Inyección de instrucciones** vía OCR, archivo o web | Contenido observado = dato. Nunca eleva a orden. Acciones tipadas, no shell libre. |
| **Skill maliciosa** | Sin marketplace en 1.0. Skills oficiales firmadas. Permisos aplicados técnicamente. |
| **Escalada por auto-aprobación** | Verificación de autoridad separada de verificación de acceso. |
| **Exfiltración silenciosa** | Toda salida a red es `Preguntar` + visible en "Actividad y privacidad". |
| **Captura sensible** | `VisionPrivacyPolicy` bloquea gestores de contraseñas y ventanas sensibles. |
| **Superficie de mensajería** | Eliminada por diseño: no hay canal WhatsApp/Telegram/Discord. |
| **Actualización maliciosa** | Sin actualización silenciosa mientras no haya firma: la persona ve versión, notas y la carpeta que se reemplazará, y confirma. Se comprueba el SHA-256 del paquete contra el `.sha256` de la misma publicación de GitHub, pero **eso solo detecta corrupción o una descarga cortada**: quien controlara la cuenta de GitHub podría cambiar paquete y huella a la vez. La huella no se enseña en pantalla. Sin firma, no hay defensa contra ese caso. |

## Estado de firma

**No existe certificado Authenticode.** Sakura comprueba el SHA-256 de lo descargado contra el
`.sha256` que viene en la misma publicación: protege contra corrupción, no contra una cuenta de
GitHub comprometida (paquete y huella cambiarían juntos). La huella se comprueba pero no se enseña.
Hasta que haya firma: notificar, mostrar versión, notas y carpeta, y exigir confirmación de la persona.
**Prohibida la actualización silenciosa.**
