# Sakura

Un asistente para Windows que vive en la bandeja del sistema y aparece cuando lo llamas.

Sakura escucha una palabra de activación, entiende órdenes normales ("abre PowerShell", "pon un
temporizador de veinte minutos", "¿qué ventana tengo abierta?") y hace lo que puede resolver sola,
en tu equipo, sin consultar a nadie. Cuando hace falta un modelo de lenguaje, lo usa; para todo lo
demás no lo necesita.

Lo que no es: un chat con esteroides. La conversación es una de sus superficies, no la única. Puede
mirar la ventana que tienes delante y explicarla, dictar texto en cualquier aplicación, ordenarte el
día y tocar cosas del sistema — siempre pidiendo permiso antes de lo que no se puede deshacer.

## Qué sabe hacer

Habla y escucha en local: la palabra de activación y el dictado corren en tu máquina, sin enviar
audio a ningún servidor.

Mira la pantalla cuando se lo pides. Sakura Lens captura la ventana activa, la lee con OCR y con la
información de accesibilidad de Windows, tapa lo que parezca sensible antes de usarlo, y te explica
qué estás viendo en modo soporte, estudio o desarrollo.

Dicta en cualquier parte. `Ctrl + Shift + D` empieza a dictar, la misma combinación termina y
escribe el texto donde tenías el cursor, sea Word, el navegador o una terminal.

Acompaña un proyecto. Le autorizas una carpeta y a partir de ahí explica, busca y modifica archivos,
siempre con copia previa y confirmación archivo por archivo.

Recuerda solo lo que le dejes. La memoria viene apagada, se activa por categorías separadas
(preferencias, contexto de conversación, hábitos de uso), se guarda cifrada y puedes leerla o
borrarla entera cuando quieras.

Cada capacidad tiene su permiso: bloqueado, preguntar o permitido. Las confirmaciones no se saltan
nunca, ni siquiera cuando tú mismo pediste la acción.

La lista completa, con cómo se activa cada cosa y ejemplos, está en la
[guía de capacidades](docs/product/SAKURA_CAPABILITIES_GUIDE.md).

## Instalar

En la [última versión](https://github.com/EXOTARA/Sakura/releases) hay dos formas de instalar: el
instalador, que deja Sakura en el menú Inicio y se puede desinstalar desde Windows, o el zip
portable, que se descomprime donde quieras y se ejecuta tal cual. No hace falta instalar nada más:
el .NET necesario va dentro de los dos.

Windows va a mostrar un aviso de SmartScreen diciendo que el programa no está firmado, porque no lo
está. Hay que darle a "Más información" → "Ejecutar de todas formas". Es un inconveniente real y
está en la lista de cosas por resolver: la vía es
[SignPath Foundation](https://signpath.org/), que firma gratis proyectos de código abierto, y lo que
se firmará y quién lo aprueba está escrito en la
[política de firma de código](docs/CODE_SIGNING_POLICY.md). Mientras tanto, cada versión publica su
`.sha256` al lado del archivo.

Sakura guarda sus datos en `%LocalAppData%\Sakura` y sus modelos de voz en la misma carpeta. Al
desinstalar puedes elegir si esa carpeta se va contigo o se queda.

## Estado

Sakura está en beta y se usa a diario, pero todavía no es 1.0. Lo que falta para serlo no son
funciones: es firma de código, una prueba de instalación completa en una máquina limpia, medir de
verdad la latencia de la voz, revisar la accesibilidad con un lector de pantalla, y unas semanas de
uso sostenido sin sorpresas.

Las actualizaciones ya llegan solas: Sakura busca una vez al día, avisa cuando hay algo nuevo y
tú decides si se instala.

## Privacidad

Todo lo que puede resolverse en local se resuelve en local. El audio del micrófono no sale del
equipo. Las capturas de pantalla se redactan antes de enviarse a un modelo, y solo se envían cuando
tú activas Lens o compartes una ventana a propósito.

Si conectas un proveedor de IA en la nube, las conversaciones que le mandes salen de tu equipo,
como es evidente. La clave se guarda en una variable de entorno tuya, no en el repositorio ni en
`settings.json`.

No hay telemetría, ni analítica, ni informes de errores. Lo único que sale de tu equipo sin que tú
lo pidas es la comprobación diaria de si hay versión nueva, y se puede apagar en Ajustes. La lista
completa, sin excepciones, está en la [política de privacidad](docs/PRIVACY.md).

## Cómo está hecho

Aplicación de escritorio en C# sobre .NET 10 y WPF, dividida en tres proyectos: `Nexo.Core` con la
lógica que se puede probar sin Windows delante, `Nexo.Windows` con todo lo que toca el sistema
operativo, y `Nexo.App` con la interfaz.

Por debajo usa Vosk para la palabra de activación, Whisper para transcribir, el OCR y la
automatización de interfaz que ya trae Windows para leer la pantalla, DPAPI para cifrar lo que
guarda, y DWM para el marco de las ventanas. Los modelos de IA en la nube son opcionales y
configurables; también funciona contra un Ollama local.

Las decisiones de diseño y el porqué de cada una están en el
[registro de implementación](docs/stable-release/IMPLEMENTATION_LOG.md), que es donde vive la
memoria larga del proyecto.

## Para desarrollar

Hace falta Windows 10 u 11 de 64 bits, el SDK de .NET 10 y, preferiblemente, PowerShell 7.

```powershell
dotnet restore .\Nexo.slnx
dotnet test .\Nexo.slnx -c Release
dotnet build .\Nexo.slnx -c Release
```

Para publicar una versión, `scripts\publish.ps1` genera el portable y `scripts\build-installer.ps1`
arma el instalador con Inno Setup 6. Los detalles están en [`docs/PUBLISHING.md`](docs/PUBLISHING.md)
y en [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md).

El trabajo va en ramas cortas contra `main`, con CI en verde antes de fusionar.

## Documentación

| Para… | Documento |
|---|---|
| Ver qué hace cada capacidad y cómo activarla | [Guía de capacidades](docs/product/SAKURA_CAPABILITIES_GUIDE.md) |
| Entender por qué cada cosa está hecha así | [Registro de implementación](docs/stable-release/IMPLEMENTATION_LOG.md) |
| Ver el estado real por fase | [Roadmap técnico](docs/roadmap/KOHANA_TECHNOLOGY_ROADMAP.md) |
| Permisos, autonomía y confirmaciones | [Modelo de confianza](docs/security/KOHANA_TRUST_AND_AUTONOMY_MODEL.md) |
| Lo que se sabe que falla o falta | [Limitaciones conocidas](docs/stable-release/KNOWN_LIMITATIONS.md) |
| Qué sale de tu equipo y qué no | [Política de privacidad](docs/PRIVACY.md) |
| Quién firma los binarios y con qué | [Política de firma de código](docs/CODE_SIGNING_POLICY.md) |
| Cómo informar de una vulnerabilidad | [Política de seguridad](SECURITY.md) |
| Qué se espera de quien participa | [Código de conducta](CODE_OF_CONDUCT.md) |

## Licencia

MIT — ver [`LICENSE`](LICENSE). Las bibliotecas y modelos de terceros conservan la suya y están
listados en [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

Los nombres internos `Nexo.App`, `Nexo.Core` y `Nexo.slnx` son de antes del cambio de nombre a
Sakura y siguen ahí a propósito: renombrarlos es un cambio grande y sin valor para nadie que use la
aplicación.
