<#
.SYNOPSIS
    ¿Se puede recuperar una instalación que ya se quedó sin desinstalador?

.DESCRIPTION
    Corre DENTRO de Windows Sandbox. Lo lanza un `.wsb` que monte esta carpeta en C:\ciclo.

    El arreglo de L13 impide que vuelva a pasar, pero **no repara las instalaciones que ya están
    rotas**: ahí el desinstalador se borró hace versiones y no hay nada que copiar. Este guion
    comprueba la única vía de recuperación que les queda, que es volver a pasar el instalador por
    encima.

    Funciona porque la entrada del registro guarda `Inno Setup: App Path`, y el instalador lo usa
    para reinstalar donde ya estaba. Esa entrada rota **es el hilo del que tirar**: borrarla —que
    fue la primera idea para «limpiar»— dejaría a la instalación sin ninguna forma de recuperarse,
    y al siguiente instalador plantando una segunda copia en su carpeta por defecto mientras la
    vieja se queda en disco para siempre.

      1. Instala con el instalador de verdad.
      2. **Rompe la instalación como lo hacía el fallo**: intercambia la carpeta sin llevarse el
         desinstalador. Es exactamente lo que hacía el ayudante antes del arreglo.
      3. Vuelve a pasar el instalador por encima. Esta es la reparación.
      4. Desinstala y mira si la carpeta queda limpia.

    Si el paso 4 deja cero archivos, una máquina ya rota se arregla sola con solo reinstalar.
#>
[CmdletBinding()]
param(
    [string]$WorkPath = "C:\ciclo"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$ProgressPreference = "SilentlyContinue"

$installFolder = Join-Path $env:LOCALAPPDATA "Programs\Sakura"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{5F9D061E-33C8-4F85-BE6E-8C3BAF240B85}_is1"
$reportPath = Join-Path $WorkPath "informe-reparacion.txt"

Set-Content $reportPath -Value "" -Encoding UTF8

function Say([string]$text) {
    $line = if ([string]::IsNullOrWhiteSpace($text)) { "" } else { "[$(Get-Date -Format HH:mm:ss)] $text" }
    Write-Host $line
    Add-Content -Path $reportPath -Value $line -Encoding UTF8
}

trap {
    Say ""
    Say "ERROR: $($_.Exception.Message)"
    Say "   en la linea $($_.InvocationInfo.ScriptLineNumber): $($_.InvocationInfo.Line.Trim())"
    Say "El ciclo se corto aqui."
    break
}

# Los archivos del desinstalador que hay ahora mismo en la carpeta.
#
# Quien llama envuelve en `@(...)`, siempre. Una funcion que devuelve una lista vacia en realidad no
# devuelve nada, y bajo StrictMode contar nada es un error terminante — eso se llevo la primera
# vuelta de este ciclo. El intento de arreglarlo devolviendo `,$lista` se llevo la segunda: la
# envoltura impide que la tuberia recorra la lista, asi que `Where-Object` recibia el array entero
# como un solo elemento y lo dejaba pasar. `@()` fuera funciona en los dos casos.
function Uninstaladores() {
    @(Get-ChildItem -LiteralPath $installFolder -Filter "unins*" -File -ErrorAction SilentlyContinue)
}

function RutaDelRegistro() {
    if (-not (Test-Path $uninstallKey)) { return "(sin entrada)" }
    $valor = (Get-ItemProperty $uninstallKey).UninstallString
    if (-not $valor) { return "(sin UninstallString)" }
    $valor.Trim('"')
}

Say "=== Recuperar una instalacion sin desinstalador ==="
Say ""

# ---------------------------------------------------------------- 1. instalar

$setup = Get-ChildItem $WorkPath -Filter "*Setup.exe" | Select-Object -First 1
if (-not $setup) { throw "No encuentro ningún instalador en $WorkPath." }

Say "1. Instalando $($setup.Name)"
$p = Start-Process $setup.FullName -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/NOICONS" -Wait -PassThru
Say "   codigo de salida: $($p.ExitCode)"
Say "   desinstaladores en la carpeta: $(@(Uninstaladores).Count)"
Say ""

# ------------------------------------------------- 2. romperla como lo hacia el fallo

$portable = Get-ChildItem $WorkPath -Filter "*portable.zip" | Select-Object -First 1
if (-not $portable) { throw "No encuentro ningún portable en $WorkPath." }

Say "2. Rompiendola: intercambio SIN llevarse el desinstalador (el fallo de L13)"

$staged = Join-Path $env:TEMP "sakura-rota"
if (Test-Path $staged) { Remove-Item $staged -Recurse -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($portable.FullName, $staged)

$apartada = "$installFolder.old"
if (Test-Path $apartada) { Remove-Item $apartada -Recurse -Force }
Move-Item -LiteralPath $installFolder -Destination $apartada
Move-Item -LiteralPath $staged -Destination $installFolder
Remove-Item $apartada -Recurse -Force -ErrorAction SilentlyContinue

Say "   desinstaladores en la carpeta: $(@(Uninstaladores).Count)"
Say "   el registro sigue ofreciendo: $(RutaDelRegistro)"
Say "   y ese archivo existe: $(Test-Path (RutaDelRegistro))"
Say ""

# ---------------------------------------------------------------- 3. reparar

Say "3. Reparando: el mismo instalador, otra vez por encima"
$p = Start-Process $setup.FullName -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/NOICONS" -Wait -PassThru
Say "   codigo de salida: $($p.ExitCode)"

$recuperados = @(Uninstaladores).Count
Say "   desinstaladores en la carpeta: $recuperados"
Say "   el registro ofrece: $(RutaDelRegistro)"
Say "   y ese archivo existe: $(Test-Path (RutaDelRegistro))"
Say "   archivos en la instalacion: $(@(Get-ChildItem $installFolder -Recurse -File -ErrorAction SilentlyContinue).Count)"
Say ""

# ---------------------------------------------------------------- 4. desinstalar

if ($recuperados -eq 0) {
    Say "4. No hay desinstalador que probar: la reparacion no lo devolvio."
    Say ""
    Say "=== Veredicto ==="
    Say "Una instalacion ya rota NO se recupera reinstalando. Hace falta otra via."
    return
}

# El .exe, no el primero de la lista: ordenados por nombre, `unins000.dat` va antes que
# `unins000.exe`, y el primer intento trato de ejecutar el registro de desinstalacion.
$uninstaller = @(Uninstaladores | Where-Object { $_.Extension -eq ".exe" })[0]
if (-not $uninstaller) { throw "Hay archivos unins* pero ninguno ejecutable." }
Say "4. Desinstalando con $($uninstaller.Name)"
$p = Start-Process $uninstaller.FullName -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART" -Wait -PassThru
Say "   codigo de salida: $($p.ExitCode)"
Start-Sleep -Seconds 6

$quedaron = @(Get-ChildItem $installFolder -Recurse -File -ErrorAction SilentlyContinue).Count
Say "   la carpeta existe todavia: $(Test-Path $installFolder)"
Say "   archivos que quedaron: $quedaron"
Say "   entrada del registro: $(if (Test-Path $uninstallKey) { 'QUEDA' } else { 'borrada' })"
Say ""

Say "=== Veredicto ==="
if ($quedaron -eq 0 -and -not (Test-Path $uninstallKey)) {
    Say "Una instalacion ya rota SE RECUPERA sola: basta con volver a pasar el instalador,"
    Say "que la encuentra por el rastro que dejo en el registro, devuelve el desinstalador"
    Say "y a partir de ahi desinstala limpio. No hace falta tocar nada desde la aplicacion."
}
else {
    Say "La reparacion devolvio el desinstalador, pero desinstalar dejo $quedaron archivos."
}
Say ""
Say "Informe guardado en $reportPath"
