# Sakura en Microsoft Store

Diseño D89 · 2026-09-14

## Por qué la Store

La descarga directa no está firmada, y Windows avisa con SmartScreen a quien la instala. La solicitud
de firma gratuita a SignPath Foundation se rechazó el 1 de septiembre de 2026. La Store resuelve eso
por otro camino:

- El registro de desarrollador individual es **gratis** desde 2025.
- **Microsoft firma el paquete** al publicarlo: quien instala desde la Store no ve el aviso de
  SmartScreen y no hace falta comprar ni renovar un certificado.
- Microsoft aloja la descarga y **actualiza la app** sola.

Fuentes: [registro gratuito para individuos](https://blogs.windows.com/windowsdeveloper/2025/09/10/free-developer-registration-for-individual-developers-on-microsoft-store/),
[opciones de firma de código](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).

La descarga directa desde GitHub sigue existiendo igual.

## Qué cambia dentro de un paquete MSIX

Fuente: [cómo se ejecutan las apps de escritorio empaquetadas](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes).

| Qué | En un paquete | Qué hace Sakura |
| --- | --- | --- |
| Carpeta del programa | Solo lectura | El actualizador propio no puede funcionar → **se quita** en la copia de la Store |
| Escrituras en `HKCU` | Van a una copia privada del paquete | La clave `Run` no llegaría a Windows → **tarea de inicio** del manifiesto |
| «Aplicaciones instaladas» | La gestiona Windows | D87 no se ejecuta |
| Archivos nuevos en `AppData` | Van a una carpeta privada del paquete, que se borra al desinstalar | Sin cambios de código; los archivos que ya existían se leen y modifican en su sitio |

## Políticas de la Store que afectan a Sakura

Fuente: [Microsoft Store Policies 7.19](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies).

| Política | Qué pide | Estado |
| --- | --- | --- |
| 10.2.3 | No ofrecer instalar software que no es tuyo | **Hecho:** la copia de la Store no instala Ollama; enlaza a su web |
| 10.2.4 | Avisar al principio de la descripción de cualquier dependencia externa | Pendiente de redactar la ficha (borrador abajo) |
| 10.2.7 | Desinstalación limpia | La da el propio paquete |
| 10.2.8 | Cambiar ajustes de Windows solo con métodos soportados y consentimiento | El plan de energía usa API documentada y pide confirmación. **A vigilar:** la revisión podría leer como «uso no soportado de las API de accesibilidad» que Sakura pulse botones de otras apps con UI Automation, aunque sea a petición y con confirmación |
| 10.5.1 | Política de privacidad (obligatoria para apps Win32) | **Hecho:** https://exotara.github.io/Sakura/privacidad/ |
| 10.7 | Localizar a los idiomas declarados | El paquete declara solo `es-MX` |
| 10.8.3 | Una cuenta individual no puede *exigir* información financiera, que incluye claves de API | Las claves de proveedores en la nube son opcionales; Sakura funciona con Ollama o sin IA. Dejarlo claro en la ficha |
| 11.11 | Cuestionario de edad (IARC) al enviar | Pendiente, lo rellena quien envía |
| 11.16 | IA generativa: declararla, y dar una forma de reportar contenido | **Hecho:** «Reportar esta respuesta» en cada respuesta, orden «Reportar una respuesta de IA» y plantilla de incidencia `contenido-ia.yml`. Falta declararlo en la ficha y en Partner Center |

## Qué hay en el repositorio

- `src/Nexo.Core/Distribution/` — `DistributionChannel` y `DistributionPolicy`: qué puede hacer cada
  canal, con pruebas.
- `src/Nexo.Windows/Distribution/WindowsDistributionChannel.cs` — detecta si el proceso corre
  empaquetado (`GetCurrentPackageFullName`) y si lo abrió la tarea de inicio.
- `packaging/msix/AppxManifest.template.xml` — el manifiesto: `runFullTrust`, micrófono y la tarea de
  inicio `SakuraStartup`.
- `scripts/build-msix.ps1` — publicación → logos desde `Sakura.ico` → manifiesto → `makeappx pack`.
  El paquete sale sin firmar a propósito: lo firma la Store.

## Comprobado en local (2026-09-14)

En el equipo de Adler, que ya tenía el modo desarrollador activado, se registró la carpeta preparada
con `Add-AppxPackage -Register` y se abrió como app empaquetada:

- `makeappx` valida el manifiesto y empaqueta 513 archivos (95 MB).
- Sakura se detecta como copia de la Store: Personalizar → Actualizaciones dice «Esta copia se
  instaló desde Microsoft Store…» y el botón de buscar actualizaciones no aparece.
- «Iniciar Sakura con Windows» enciende la tarea de inicio (estado 2) y la apaga (estado 0).
- Después se retiró el paquete y se volvió a abrir la instalación normal.

**No comprobado todavía:** el Kit de Certificación de Apps de Windows (WACK) sobre el paquete, el
micrófono y el dictado dentro del paquete, y la revisión real de la Store.

## Identidad en Partner Center (2026-09-14)

«Sakura» ya estaba reservado por otra app, así que el producto se llama **Sakura Assistant**. Dentro de
la aplicación sigue siendo Sakura.

| Campo | Valor |
| --- | --- |
| Package/Identity/Name | `EXOTARA.SakuraAssistant` |
| Package/Identity/Publisher | `CN=B069792F-C54F-4B21-8E04-959CF4734C01` |
| Package/Properties/PublisherDisplayName | `EXOTARA` |
| Package Family Name | `EXOTARA.SakuraAssistant_b32ke6sbb6xf8` |
| Store ID | `9NSR5FJFCR01` |

El Publisher se transcribió de una captura y se comprobó recalculando el hash del Package Family Name:
el SHA-256 de la cadena en UTF-16, primeros 8 bytes, codificados en base 32 con el alfabeto de
Crockford, da `b32ke6sbb6xf8`, igual que Partner Center.

La reserva caduca si no se envía la app en **tres meses**.

El flujo `release.yml` construye el paquete con esta identidad en cada versión y lo guarda como
artefacto `sakura-msix-<versión>`, no como archivo de la release.

## Lo que tuvo que hacer Adler (no se puede hacer por él)

1. **Crear la cuenta** en [Partner Center](https://developer.microsoft.com/en-us/store/register) como
   desarrollador individual. Pide verificar la identidad; es gratis.
2. **Reservar el nombre** de la app. «Sakura» puede estar ocupado; si lo está, hace falta una
   alternativa (por ejemplo «Sakura para Windows»).
3. Pasar dos datos que Partner Center muestra en *Identidad del producto*: **Package/Identity/Name** y
   **Package/Identity/Publisher**. No son contraseñas; van dentro del paquete.

Con eso se genera el paquete definitivo con `build-msix.ps1 -IdentityName ... -IdentityPublisher ...`
y se sube.

## Borrador del inicio de la ficha

> **Requiere Ollama para la IA local** (gratuito, de otros autores, se descarga desde ollama.com), o
> una clave de un proveedor de IA en la nube. Sin ninguno de los dos, Sakura funciona como asistente
> de órdenes, tareas, enfoque y dictado, sin respuestas de IA.
>
> Sakura usa **IA generativa**: las respuestas pueden ser incorrectas. Cada respuesta se puede
> reportar desde su menú.
