<#
.SYNOPSIS
    Instalar → actualizar → desinstalar, dentro del Sandbox, y contar qué queda.

.DESCRIPTION
    Corre DENTRO de Windows Sandbox, no en el equipo de nadie. Lo lanza el `.wsb` que escribe
    `New-InstallCycleBundle.ps1`.

    Reproduce el mecanismo del que habla L13 sin necesitar red ni interacción:

      1. Instala la versión de partida con el instalador de verdad, en silencio.
      2. Vuelca encima los archivos de la versión siguiente — que es EXACTAMENTE lo que hace el
         ayudante del actualizador: sustituye el contenido de la carpeta sin pasar por el
         instalador, así que los archivos nuevos no quedan anotados en su registro.
      3. Desinstala con el desinstalador de verdad, en silencio.
      4. Mira qué quedó.

    La pregunta que contesta es una y concreta: **¿el desinstalador deja atrás los archivos que
    llegaron en la actualización?** L13 sospecha que sí y admite que nadie lo miró.
#>
[CmdletBinding()]
param(
    [string]$WorkPath = "C:\ciclo"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# La barra de progreso de PowerShell cuesta mas tiempo que el trabajo cuando son miles de archivos.
$ProgressPreference = "SilentlyContinue"

$installFolder = Join-Path $env:LOCALAPPDATA "Programs\Sakura"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{5F9D061E-33C8-4F85-BE6E-8C3BAF240B85}_is1"
$reportPath = Join-Path $WorkPath "informe.txt"

# El informe se escribe frase a frase, no al final. La primera vez que esto se corrio de verdad se
# quedo doce minutos en silencio, y desde el equipo de casa no habia forma de distinguir «va lento»
# de «se colgo»: la ventana del Sandbox no se deja capturar desde fuera, y el informe todavia no
# existia. Cada linea lleva la hora, para que un atasco se vea en el hueco entre dos.
Set-Content $reportPath -Value "" -Encoding UTF8

function Say([string]$text) {
    $line = if ([string]::IsNullOrWhiteSpace($text)) { "" } else { "[$(Get-Date -Format HH:mm:ss)] $text" }
    Write-Host $line
    Add-Content -Path $reportPath -Value $line -Encoding UTF8
}

# Un error terminante mataba el guion sin dejar rastro: la consola de dentro se queda con el texto
# rojo, pero esa ventana no se puede capturar desde fuera, asi que el informe se cortaba a media
# frase y no decia por que. Ahora el error entra en el informe como una frase mas.
trap {
    Say ""
    Say "ERROR: $($_.Exception.Message)"
    Say "   en la linea $($_.InvocationInfo.ScriptLineNumber): $($_.InvocationInfo.Line.Trim())"
    Say ""
    Say "El ciclo se corto aqui."
    break
}

function Inventory([string]$path) {
    if (-not (Test-Path $path)) { return @() }
    Get-ChildItem $path -Recurse -File -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName.Substring($path.Length).TrimStart('\') } |
        Sort-Object
}

Say "=== Ciclo instalar -> actualizar -> desinstalar ==="
Say "Fecha: $(Get-Date -Format o)"
Say ""

# ---------------------------------------------------------------- 1. instalar

$setup = Get-ChildItem $WorkPath -Filter "*Setup.exe" | Select-Object -First 1
if (-not $setup) { throw "No encuentro ningún instalador en $WorkPath." }

Say "1. Instalando $($setup.Name)"
$process = Start-Process $setup.FullName `
    -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/NOICONS" `
    -Wait -PassThru
Say "   codigo de salida: $($process.ExitCode)"

if (-not (Test-Path $installFolder)) {
    Say "   FALLO: no existe $installFolder. El instalador no dejo nada donde se esperaba."
    return
}

$afterInstall = @(Inventory $installFolder)
Say "   archivos tras instalar: $($afterInstall.Count)"

$recorded = if (Test-Path $uninstallKey) { (Get-ItemProperty $uninstallKey).DisplayVersion } else { "(sin entrada)" }
Say "   version en el registro: $recorded"
Say ""

# ---------------------------------------------------------------- 2. actualizar

$portable = Get-ChildItem $WorkPath -Filter "*portable.zip" | Select-Object -First 1
if (-not $portable) { throw "No encuentro ningún portable en $WorkPath." }

Say "2. Volcando $($portable.Name) sobre la instalacion"
Say "   (es lo que hace el ayudante del actualizador: sustituye archivos sin pasar por el instalador)"

# El zip se descomprime en el disco de la maquina virtual, no en la carpeta montada. Montada es
# comoda para mirar desde casa, pero cada archivo cruza el puente del Sandbox: descomprimir ahi
# significa escribir 250 MB al otro lado y volver a leerlos enteros para copiarlos. Dos travesias
# que no hacen falta.
$staged = Join-Path $env:TEMP "sakura-nueva"
if (Test-Path $staged) { Remove-Item $staged -Recurse -Force }

Say "   descomprimiendo en $staged"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($portable.FullName, $staged)
Say "   descomprimidos: $((Get-ChildItem $staged -Recurse -File).Count) archivos"

Say "   copiando sobre $installFolder"
Copy-Item (Join-Path $staged "*") $installFolder -Recurse -Force
Say "   copia terminada"

$afterUpdate = @(Inventory $installFolder)
$arrived = @($afterUpdate | Where-Object { $afterInstall -notcontains $_ })

Say "   archivos tras actualizar: $($afterUpdate.Count)"
Say "   archivos NUEVOS que el instalador nunca anoto: $($arrived.Count)"
foreach ($file in ($arrived | Select-Object -First 15)) { Say "     + $file" }
if ($arrived.Count -gt 15) { Say "     ... y $($arrived.Count - 15) mas" }
Say ""

# ---------------------------------------------------------------- 3. desinstalar

$uninstaller = Get-ChildItem $installFolder -Filter "unins*.exe" | Select-Object -First 1
if (-not $uninstaller) {
    Say "3. FALLO: no encuentro el desinstalador en la carpeta."
    return
}

Say "3. Desinstalando con $($uninstaller.Name)"
$process = Start-Process $uninstaller.FullName `
    -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" `
    -Wait -PassThru
Say "   codigo de salida: $($process.ExitCode)"

# El desinstalador de Inno se copia a %TEMP% y sigue trabajando un momento tras devolver el control.
Start-Sleep -Seconds 6
Say ""

# ---------------------------------------------------------------- 4. qué quedó

Say "4. Lo que quedo"

$leftovers = @(Inventory $installFolder)
Say "   la carpeta existe todavia: $(Test-Path $installFolder)"
Say "   archivos que quedaron: $($leftovers.Count)"

foreach ($file in ($leftovers | Select-Object -First 40)) { Say "     - $file" }
if ($leftovers.Count -gt 40) { Say "     ... y $($leftovers.Count - 40) mas" }

$keyLeft = Test-Path $uninstallKey
Say "   entrada de desinstalacion en el registro: $(if ($keyLeft) { 'QUEDA' } else { 'borrada' })"

$dataFolder = Join-Path $env:LOCALAPPDATA "Sakura"
Say "   carpeta de datos ($dataFolder): $(if (Test-Path $dataFolder) { 'queda' } else { 'no existe' })"
Say ""

# ---------------------------------------------------------------- veredicto

Say "=== Veredicto sobre L13 ==="
if ($leftovers.Count -eq 0 -and -not $keyLeft) {
    Say "L13 NO se reproduce: el desinstalador dejo la carpeta vacia aunque los archivos"
    Say "llegaran por una actualizacion. La limitacion se puede cerrar."
}
else {
    Say "L13 SE REPRODUCE: quedaron $($leftovers.Count) archivos tras desinstalar."
    $orphans = @($leftovers | Where-Object { $arrived -contains $_ })
    Say "De ellos, $($orphans.Count) son exactamente los que llegaron en la actualizacion."
}

Say ""
Say "Informe guardado en $reportPath (visible desde el equipo de casa en la carpeta montada)."
