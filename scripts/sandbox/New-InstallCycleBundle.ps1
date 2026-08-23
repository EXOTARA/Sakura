<#
.SYNOPSIS
    Prepara, en el equipo de casa, todo lo que el ciclo de instalación necesita dentro del Sandbox.

.DESCRIPTION
    L13 dice que desinstalar después de actualizar puede dejar archivos, y admite que **nunca se
    comprobó**: se dedujo leyendo cómo funcionan el instalador y el actualizador. Esto lo comprueba.

    Windows Sandbox es el sitio correcto para hacerlo: es un Windows desechable que se borra entero
    al cerrarlo, viene con Windows 11 Pro y no ensucia el equipo de nadie. Probar un instalador en la
    máquina que usas a diario contamina justamente lo que quieres medir — ya hay carpetas, ya hay
    entradas de registro, y no sabes cuáles puso quién.

    Este guion NO prueba nada: solo baja los dos instaladores y escribe el archivo `.wsb` que abre el
    Sandbox con la carpeta montada dentro. La prueba la corre `Invoke-InstallCycle.ps1`, ya dentro.

.EXAMPLE
    .\scripts\sandbox\New-InstallCycleBundle.ps1 -From v0.28.0-beta -To v0.29.0-beta
#>
[CmdletBinding()]
param(
    # La versión que se instala con el instalador.
    [Parameter(Mandatory)]
    [string]$From,

    # La versión a la que se «actualiza» sustituyendo archivos, que es lo que hace el ayudante.
    [Parameter(Mandatory)]
    [string]$To,

    [string]$Repository = "EXOTARA/Sakura",
    [string]$StagingPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if ([string]::IsNullOrWhiteSpace($StagingPath)) {
    $StagingPath = Join-Path $root "artifacts" "sandbox"
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "Hace falta la CLI de GitHub (gh) para bajar los artefactos."
}

Write-Host "==> Preparando $StagingPath"
if (Test-Path $StagingPath) {
    Remove-Item $StagingPath -Recurse -Force
}
New-Item $StagingPath -ItemType Directory -Force | Out-Null

# El instalador de la versión de partida y el portable de la de destino. El portable es lo correcto
# para simular la actualización: contiene exactamente el mismo conjunto de archivos que el ayudante
# vuelca sobre la carpeta de instalación.
Write-Host "==> Bajando el instalador de $From"
gh release download $From --repo $Repository --pattern "*Setup.exe" --dir $StagingPath
if ($LASTEXITCODE -ne 0) { throw "No pude bajar el instalador de $From." }

Write-Host "==> Bajando el portable de $To"
gh release download $To --repo $Repository --pattern "*portable.zip" --dir $StagingPath
if ($LASTEXITCODE -ne 0) { throw "No pude bajar el portable de $To." }

Copy-Item (Join-Path $PSScriptRoot "Invoke-InstallCycle.ps1") $StagingPath -Force

# El .wsb se escribe aquí y no se versiona con una ruta fija: la carpeta montada depende de dónde
# esté el repositorio, y una ruta ajena dentro de un archivo compartido no le sirve a nadie.
$configuration = @"
<Configuration>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$StagingPath</HostFolder>
      <SandboxFolder>C:\ciclo</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell -ExecutionPolicy Bypass -NoExit -File C:\ciclo\Invoke-InstallCycle.ps1</Command>
  </LogonCommand>
</Configuration>
"@

$wsb = Join-Path $StagingPath "Sakura-ciclo.wsb"
Set-Content $wsb -Value $configuration -Encoding UTF8

Write-Host ""
Write-Host "Listo. Para correr el ciclo:"
Write-Host "  1. Windows Sandbox tiene que estar activado (una vez, y pide reiniciar):"
Write-Host "     Enable-WindowsOptionalFeature -Online -FeatureName 'Containers-DisposableClientVM' -All"
Write-Host "  2. Abre:  $wsb"
Write-Host ""
Write-Host "El Sandbox arranca, corre el ciclo solo y deja el informe en la misma carpeta."

[pscustomobject]@{
    Staging = $StagingPath
    Configuration = $wsb
}
