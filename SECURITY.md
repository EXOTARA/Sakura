# Seguridad

*Security policy — an English summary follows at the end.*

## Qué versiones reciben arreglos

La última publicada. Sakura está en beta y no hay ramas de mantenimiento: lo que se arregla sale en
la versión siguiente.

| Versión | Soporte |
|---|---|
| 0.27.x-beta | Sí |
| Anteriores | No |

## Cómo informar de una vulnerabilidad

**No abras una incidencia pública.** Usa el aviso privado de GitHub:
[Security → Report a vulnerability](https://github.com/EXOTARA/Sakura/security/advisories/new).

Ayuda mucho incluir la versión, el sistema, qué hace falta para reproducirlo y qué consigue quien lo
explota. Si has escrito una prueba de concepto, adjúntala.

Respuesta esperable: unos días para acusar recibo, y una publicación con crédito a quien lo
encontró, salvo que prefiera lo contrario.

## Qué es una vulnerabilidad aquí, y qué no

Sakura corre con los permisos de quien la usa, en su equipo, y automatiza Windows a propósito: abrir
aplicaciones, mover ventanas, leer la pantalla, cambiar el volumen. Que pueda hacer eso no es un
fallo, es lo que es.

Sí lo es, entre otras cosas:

- Que una acción que el modelo de permisos marca como que necesita confirmación se ejecute sin ella
  (ver [modelo de confianza](docs/security/KOHANA_TRUST_AND_AUTONOMY_MODEL.md)).
- Que texto llegado de fuera —una página, un documento, una captura— consiga que Sakura ejecute algo
  como si se lo hubiera pedido su dueño.
- Que salgan del equipo datos que la [política de privacidad](docs/PRIVACY.md) dice que no salen.
- Que se pueda leer sin la sesión de Windows algo que se guarda cifrado con DPAPI.
- Que la actualización acepte un paquete que no venga de la página de versiones de este repositorio,
  o que no coincida con su hash.

## Firma de los binarios

Las versiones publicadas **todavía no están firmadas**; cada artefacto lleva su `.sha256` al lado y
esa es hoy la única verificación posible. El plan, quién aprueba una firma y qué se firma están en la
[política de firma de código](docs/CODE_SIGNING_POLICY.md).

---

## Security policy (English summary)

Only the latest published version is supported. Report vulnerabilities privately through
[GitHub Security Advisories](https://github.com/EXOTARA/Sakura/security/advisories/new), never in a
public issue; include version, environment, reproduction steps and impact. Expect acknowledgement
within days and public credit unless you prefer otherwise.

Sakura automates Windows on purpose and runs with the user's own permissions — that is not a
vulnerability. These are: bypassing a confirmation the permission model requires; untrusted content
(a page, a document, a screenshot) getting Sakura to act as if its owner had asked; data leaving the
machine that [the privacy policy](docs/PRIVACY.md) says does not; reading DPAPI-encrypted data
outside the Windows session; or the updater accepting a package that did not come from this
repository's releases or whose hash does not match.

Releases are **not signed yet** — each artifact ships a `.sha256`. See the
[code signing policy](docs/CODE_SIGNING_POLICY.md).
