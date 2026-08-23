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

$installFolder = Join-Path $env:LOCALAPPDATA "Programs\Sakura"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{5F9D061E-33C8-4F85-BE6E-8C3BAF240B85}_is1"
$report = New-Object System.Collections.Generic.List[string]

function Say([string]$text) {
    Write-Host $text
    $report.Add($text)
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
    Set-Content (Join-Path $WorkPath "informe.txt") -Value $report -Encoding UTF8
    return
}

$afterInstall = Inventory $installFolder
Say "   archivos tras instalar: $($afterInstall.Count)"

$recorded = if (Test-Path $uninstallKey) { (Get-ItemProperty $uninstallKey).DisplayVersion } else { "(sin entrada)" }
Say "   version en el registro: $recorded"
Say ""

# ---------------------------------------------------------------- 2. actualizar

$portable = Get-ChildItem $WorkPath -Filter "*portable.zip" | Select-Object -First 1
if (-not $portable) { throw "No encuentro ningún portable en $WorkPath." }

Say "2. Volcando $($portable.Name) sobre la instalacion"
Say "   (es lo que hace el ayudante del actualizador: sustituye archivos sin pasar por el instalador)"

$staged = Join-Path $WorkPath "nueva"
if (Test-Path $staged) { Remove-Item $staged -Recurse -Force }
Expand-Archive $portable.FullName -DestinationPath $staged -Force
Copy-Item (Join-Path $staged "*") $installFolder -Recurse -Force

$afterUpdate = Inventory $installFolder
$arrived = $afterUpdate | Where-Object { $afterInstall -notcontains $_ }

Say "   archivos tras actualizar: $($afterUpdate.Count)"
Say "   archivos NUEVOS que el instalador nunca anoto: $($arrived.Count)"
foreach ($file in ($arrived | Select-Object -First 15)) { Say "     + $file" }
if ($arrived.Count -gt 15) { Say "     ... y $($arrived.Count - 15) mas" }
Say ""

# ---------------------------------------------------------------- 3. desinstalar

$uninstaller = Get-ChildItem $installFolder -Filter "unins*.exe" | Select-Object -First 1
if (-not $uninstaller) {
    Say "3. FALLO: no encuentro el desinstalador en la carpeta."
    Set-Content (Join-Path $WorkPath "informe.txt") -Value $report -Encoding UTF8
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

$leftovers = Inventory $installFolder
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
    $orphans = $leftovers | Where-Object { $arrived -contains $_ }
    Say "De ellos, $($orphans.Count) son exactamente los que llegaron en la actualizacion."
}

$reportPath = Join-Path $WorkPath "informe.txt"
Set-Content $reportPath -Value $report -Encoding UTF8
Say ""
Say "Informe guardado en $reportPath (visible desde el equipo de casa en la carpeta montada)."
