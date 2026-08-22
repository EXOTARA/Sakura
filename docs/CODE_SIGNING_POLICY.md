# Política de firma de código

*Code signing policy — an English summary follows at the end of this document.*

Esta página existe porque SignPath Foundation la exige para conceder un certificado gratuito a un
proyecto de código abierto, y porque quien descarga un ejecutable tiene derecho a saber quién lo
firmó, quién decidió firmarlo y qué se firma exactamente.

## Estado actual

**Sakura todavía no está firmada.** Las versiones publicadas hasta hoy —incluida la
[v0.27.0-beta](https://github.com/EXOTARA/Sakura/releases)— no llevan firma Authenticode, y por eso
Windows SmartScreen muestra un aviso al ejecutarlas. Mientras eso siga así, la única forma de
verificar una descarga es el archivo `.sha256` que acompaña a cada artefacto en la página de la
versión.

Esta política describe cómo se firmará y quién decide, y se publica antes de la firma a propósito:
es uno de los requisitos para solicitarla.

## Atribución

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

## Quién es quién

Sakura la mantiene una sola persona. Los tres papeles que SignPath distingue recaen hoy en ella, y
esta tabla se actualizará en cuanto eso deje de ser cierto.

| Papel | Qué decide | Quién |
|---|---|---|
| Autor / committer | Escribe y publica el código | Adler Rodríguez de la gala Soberanis ([@EXOTARA](https://github.com/EXOTARA)) |
| Revisor | Revisa las contribuciones externas antes de fusionarlas | Adler Rodríguez de la gala Soberanis |
| Aprobador | Decide si una versión concreta puede firmarse | Adler Rodríguez de la gala Soberanis |

Las contribuciones externas están cerradas por ahora (ver
[`STABLE_RELEASE_PLAN.md`](stable-release/STABLE_RELEASE_PLAN.md)). Si se abren, ninguna llegará a
un artefacto firmado sin pasar por una revisión y una aprobación explícitas.

La cuenta de GitHub del proyecto y la de SignPath tienen autenticación en dos factores activada.

## Qué se firma, y de dónde sale

Solo se firma lo que construye la integración continua a partir del código público de este
repositorio. No se firma nada compilado en un equipo personal.

El flujo es [`.github/workflows/release.yml`](../.github/workflows/release.yml), que se dispara al
publicar una etiqueta `v*` y produce, en este orden:

1. La publicación autocontenida (`Sakura.exe` y sus bibliotecas) mediante
   [`scripts/publish.ps1`](../scripts/publish.ps1).
2. El instalador de Inno Setup mediante [`scripts/build-installer.ps1`](../scripts/build-installer.ps1).
3. La verificación de [`scripts/verify-release.ps1`](../scripts/verify-release.ps1), que comprueba
   que los binarios publicados declaran nombre de producto, empresa y versión correctos, y que no
   se ha colado ningún dato personal ni ninguna clave en el paquete.
4. El `.sha256` de cada artefacto y su publicación en la página de la versión.

Cuando exista el certificado, la firma se intercalará entre los pasos 2 y 4: primero los binarios de
la aplicación, después el instalador que los contiene, y los hashes se calcularán al final, sobre
los archivos ya firmados. La versión que llegue a la página de descargas será byte a byte la que
firmó SignPath.

Todos los artefactos declaran nombre de producto y versión, y esos metadatos se comprueban en cada
publicación: no es una convención, es un paso del flujo que falla si no se cumple.

## Privacidad

La política de privacidad de Sakura está en [`docs/PRIVACY.md`](PRIVACY.md).

Resumen: Sakura funciona en local. No hay telemetría, ni analítica, ni cuentas. Lo que sale del
equipo sale porque el usuario lo pide o porque activó una función que lo necesita, y esa página
enumera cada caso, incluido el único que ocurre sin intervención: la comprobación diaria de
actualizaciones contra GitHub.

Las políticas de privacidad de los servicios de terceros que pueden intervenir —y que solo
intervienen si el usuario los configura— están enumeradas en esa misma página.

## Licencia

Sakura se publica bajo licencia MIT ([`LICENSE`](../LICENSE)), sin doble licencia comercial. Las
bibliotecas y modelos de terceros conservan la suya y están listados en
[`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md); todos son MIT, Apache-2.0 o BSD. El proyecto
no contiene ningún componente propietario.

## Cómo informar de un problema con un binario firmado

Si alguna vez aparece un ejecutable que dice ser Sakura y su firma no cuadra con lo que describe
esta página, ábrase una incidencia en
[github.com/EXOTARA/Sakura/issues](https://github.com/EXOTARA/Sakura/issues). Cualquier sospecha de
abuso del certificado se investigará y se comunicará a SignPath Foundation.

---

## Code signing policy (English summary)

**Sakura is not signed yet.** This policy is published as part of the application to SignPath
Foundation. Until a certificate exists, releases carry `.sha256` checksums and Windows SmartScreen
will warn about them.

- **Attribution:** Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
  [SignPath Foundation](https://signpath.org/).
- **Project:** [EXOTARA/Sakura](https://github.com/EXOTARA/Sakura) — a local-first personal
  assistant for Windows, written in C# on .NET 10 and WPF. Licensed MIT, no commercial
  dual-licensing, no proprietary components.
- **Team roles:** Adler Rodríguez de la gala Soberanis ([@EXOTARA](https://github.com/EXOTARA)) is
  the sole author, reviewer and approver. Two-factor authentication is enabled on the GitHub and
  SignPath accounts. External contributions are currently closed; if reopened, no contribution will
  reach a signed artifact without explicit review and approval.
- **Builds:** only artifacts produced by
  [GitHub Actions](../.github/workflows/release.yml) from this public repository are signed. Local
  builds are never signed. Every release verifies that product name, company and version metadata
  are present and correct before publishing.
- **Privacy policy:** [`docs/PRIVACY.md`](PRIVACY.md). No telemetry, no analytics, no accounts.
- **Abuse reports:** [github.com/EXOTARA/Sakura/issues](https://github.com/EXOTARA/Sakura/issues).
