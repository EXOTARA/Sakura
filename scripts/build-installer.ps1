[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [string]$RepositoryUrl = "https://github.com/EXOTARA/Sakura",
    [switch]$SkipPublish,
    [string]$InnoSetupPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "SakuraVersion.ps1")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-SakuraVersion -RepositoryRoot $root
    Write-Host "==> Versión tomada de Directory.Build.props: $Version"
}

$publishDirectory = Join-Path $root "artifacts\publish\$Runtime"
$outputDirectory = Join-Path $root "artifacts\installer"
$installerScript = Join-Path $root "installer\Sakura.iss"

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "publish.ps1") `
        -Version $Version `
        -Runtime $Runtime `
        -RepositoryUrl $RepositoryUrl
}

if (-not (Test-Path (Join-Path $publishDirectory "Sakura.exe"))) {
    throw "No existe una publicación válida de Sakura en $publishDirectory."
}

if ([string]::IsNullOrWhiteSpace($InnoSetupPath)) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    $InnoSetupPath = $candidates |
        Where-Object { $_ -and (Test-Path $_) } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoSetupPath) -or -not (Test-Path $InnoSetupPath)) {
    throw "No encontré Inno Setup 6. Instálalo o usa -InnoSetupPath con la ruta de ISCC.exe."
}

$numericVersion = Get-SakuraNumericVersion -Version $Version

New-Item $outputDirectory -ItemType Directory -Force | Out-Null
Remove-Item (Join-Path $outputDirectory "Sakura-*-Setup.exe") -Force -ErrorAction SilentlyContinue

Write-Host "==> Creando instalador de Sakura"
& $InnoSetupPath `
    "/DMyAppVersion=$Version" `
    "/DMyNumericVersion=$numericVersion" `
    "/DSourceDir=$publishDirectory" `
    "/DOutputDir=$outputDirectory" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup no pudo crear el instalador."
}

$installer = Get-ChildItem $outputDirectory -Filter "Sakura-*-Setup.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $installer) {
    throw "Inno Setup terminó, pero no encontré el instalador generado."
}

$hash = Get-FileHash $installer.FullName -Algorithm SHA256
$checksumFile = "$($installer.FullName).sha256"
"$($hash.Hash.ToLowerInvariant())  $($installer.Name)" |
    Set-Content $checksumFile -Encoding ASCII

Write-Host ""
Write-Host "Instalador listo:"
Write-Host "  EXE:    $($installer.FullName)"
Write-Host "  SHA256: $checksumFile"

[pscustomobject]@{
    Installer = $installer.FullName
    ChecksumFile = $checksumFile
}
