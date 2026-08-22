[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$RepositoryUrl = "https://github.com/EXOTARA/Sakura",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "SakuraVersion.ps1")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-SakuraVersion -RepositoryRoot $root
    Write-Host "==> Versión tomada de Directory.Build.props: $Version"
}

$solution = Join-Path $root "Nexo.slnx"
$project = Join-Path $root "src\Nexo.App\Nexo.App.csproj"
$artifactRoot = Join-Path $root "artifacts"
$publishDirectory = Join-Path $artifactRoot "publish\$Runtime"
$distributionDirectory = Join-Path $artifactRoot "dist"
$portableZip = Join-Path $distributionDirectory "Sakura-$Version-$Runtime-portable.zip"
$checksumFile = "$portableZip.sha256"

$running = Get-Process -Name "Sakura", "Kohana", "Nexo", "Nexo.App" -ErrorAction SilentlyContinue
if ($running) {
    $ids = ($running | Select-Object -ExpandProperty Id) -join ", "
    throw "Sakura sigue ejecutándose (PID: $ids). Usa 'Salir completamente' desde la bandeja antes de publicar."
}

Write-Host "==> Limpiando artefactos anteriores"
Remove-Item $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item $publishDirectory -ItemType Directory -Force | Out-Null
New-Item $distributionDirectory -ItemType Directory -Force | Out-Null
Remove-Item $portableZip, $checksumFile -Force -ErrorAction SilentlyContinue

Write-Host "==> Restaurando"
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "dotnet restore falló." }

Write-Host "==> Compilando"
dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build falló." }

if (-not $SkipTests) {
    Write-Host "==> Ejecutando pruebas"
    dotnet test $solution -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw "dotnet test falló." }
}

Write-Host "==> Restaurando dependencias para $Runtime"
dotnet restore $project -r $Runtime
if ($LASTEXITCODE -ne 0) { throw "La restauración para $Runtime falló." }

$numericVersion = Get-SakuraNumericVersion -Version $Version

$publishArguments = @(
    "publish",
    $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "--no-restore",
    "-o", $publishDirectory,
    "/p:Version=$Version",
    "/p:AssemblyVersion=$numericVersion",
    "/p:FileVersion=$numericVersion",
    "/p:PublishSingleFile=false",
    "/p:PublishTrimmed=false",
    "/p:PublishReadyToRun=false"
)

if (-not [string]::IsNullOrWhiteSpace($RepositoryUrl)) {
    $publishArguments += "/p:RepositoryUrl=$RepositoryUrl"
}

Write-Host "==> Publicando $Runtime"
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló." }

$executable = Join-Path $publishDirectory "Sakura.exe"
if (-not (Test-Path $executable)) {
    throw "La publicación terminó, pero no se encontró $executable."
}

@"
Sakura $Version
===============

Tu Windows, en flor.

Esta es la edición portable y autocontenida para Windows x64.

1. Extrae toda la carpeta antes de ejecutar.
2. Abre Sakura.exe.
3. Ollama y sus modelos no están incluidos.
4. Los datos personales se guardan en %LocalAppData%\Sakura.
5. Si existe una instalación anterior de Nexo, Sakura copia sus datos sin borrar el origen.
6. Windows puede mostrar una advertencia mientras la beta no tenga firma digital.

No muevas únicamente Sakura.exe: conserva todos los archivos de esta carpeta.
"@ | Set-Content (Join-Path $publishDirectory "LEEME.txt") -Encoding UTF8

Write-Host "==> Creando ZIP portable"
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $portableZip -Force

$hash = Get-FileHash $portableZip -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($portableZip))" |
    Set-Content $checksumFile -Encoding ASCII

Write-Host ""
Write-Host "Publicación lista:"
Write-Host "  Carpeta: $publishDirectory"
Write-Host "  ZIP:     $portableZip"
Write-Host "  SHA256:  $checksumFile"

[pscustomobject]@{
    PublishDirectory = $publishDirectory
    PortableZip = $portableZip
    ChecksumFile = $checksumFile
}
