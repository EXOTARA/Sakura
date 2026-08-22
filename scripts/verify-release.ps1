[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "SakuraVersion.ps1")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-SakuraVersion -RepositoryRoot $root
}

$publishDirectory = Join-Path $root "artifacts\publish\$Runtime"

$required = @(
    "Sakura.exe",
    "Sakura.dll",
    "Sakura.deps.json",
    "Sakura.runtimeconfig.json",
    "Nexo.Core.dll",
    "Nexo.Windows.dll"
)

$missing = $required | Where-Object {
    -not (Test-Path (Join-Path $publishDirectory $_))
}

if ($missing) {
    throw "Faltan archivos de publicación: $($missing -join ', ')"
}

$forbidden = Get-ChildItem $publishDirectory -Recurse -File | Where-Object {
    $_.Name -in @(
        "settings.json",
        "tasks.json",
        "focus.json",
        "routines.json",
        "conversation-history.json"
    ) -or
    $_.Extension -in @(".key", ".pfx")
}

if ($forbidden) {
    throw "La publicación contiene datos que no deben distribuirse: $($forbidden.FullName -join ', ')"
}

# Metadatos del binario.
#
# SignPath Foundation exige que los binarios firmados lleven nombre de producto y versión
# correctos, y este proyecto ya se equivocó en los dos a la vez: la versión 0.27.0-beta salió con
# ProductName "Kohana" (el renombrado de 2026-08-20 barrió 279 archivos pero no los metadatos de
# ensamblado) y FileVersion 0.9.5.0 (Nexo.App.csproj machacaba lo que decía Directory.Build.props).
# Nada de eso lo ve una prueba unitaria: vive en el recurso VERSIONINFO del archivo publicado.
$numericVersion = Get-SakuraNumericVersion -Version $Version

$metadataExpectations = @{
    "Sakura.exe"       = "Sakura"
    "Nexo.Core.dll"    = "Sakura"
    "Nexo.Windows.dll" = "Sakura"
}

$metadataProblems = foreach ($file in $metadataExpectations.Keys) {
    $info = (Get-Item (Join-Path $publishDirectory $file)).VersionInfo

    if ($info.ProductName -ne $metadataExpectations[$file]) {
        "{0}: ProductName es '{1}' y debe ser '{2}'." -f $file, $info.ProductName, $metadataExpectations[$file]
    }

    if ($info.CompanyName -ne "EXOTARA") {
        "{0}: CompanyName es '{1}' y debe ser 'EXOTARA'." -f $file, $info.CompanyName
    }

    if ($info.FileVersion -ne $numericVersion) {
        "{0}: FileVersion es '{1}' y debe ser '{2}'." -f $file, $info.FileVersion, $numericVersion
    }

    # ProductVersion lleva '+' y el commit cuando SourceLink está activo; basta con que empiece igual.
    if (-not $info.ProductVersion.StartsWith($Version)) {
        "{0}: ProductVersion es '{1}' y debe empezar por '{2}'." -f $file, $info.ProductVersion, $Version
    }
}

if ($metadataProblems) {
    throw "Metadatos de versión incorrectos:`n  $($metadataProblems -join "`n  ")"
}

Write-Host ("Metadatos verificados: Sakura {0} (archivo {1})." -f $Version, $numericVersion)

$size = (Get-ChildItem $publishDirectory -Recurse -File |
    Measure-Object Length -Sum).Sum

Write-Host "Publicación de Sakura verificada."
Write-Host ("Archivos: {0}" -f (Get-ChildItem $publishDirectory -Recurse -File).Count)
Write-Host ("Tamaño:   {0:N1} MB" -f ($size / 1MB))
