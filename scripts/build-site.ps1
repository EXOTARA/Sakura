<#
.SYNOPSIS
    Genera la página de descargas rellenando los datos de una versión publicada.

.DESCRIPTION
    La plantilla de `site/` no lleva escritos ni la versión ni los enlaces ni los hashes: los trae
    este script desde la release de GitHub. La razón es simple — el día que se publique la 0.28,
    una página con los enlaces escritos a mano seguiría ofreciendo la 0.27 y nadie se enteraría,
    porque una página que apunta a un archivo viejo no falla, sólo miente.

    Los hashes no se calculan aquí: se leen de los archivos `.sha256` que la propia publicación
    subió junto a los binarios. Así la página enseña el mismo número que verificó el flujo, no uno
    recalculado por otro camino.

.EXAMPLE
    .\scripts\build-site.ps1
    Usa la última versión publicada.

.EXAMPLE
    .\scripts\build-site.ps1 -Tag v0.27.0-beta -OutputDirectory artifacts\site
#>
[CmdletBinding()]
param(
    [string]$Tag = "",
    [string]$Repository = "EXOTARA/Sakura",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $root "site"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    # Por segmentos y no "artifacts\site": este script también corre en el Linux de GitHub Actions,
    # donde la barra invertida no separa carpetas — crearía una carpeta llamada "artifacts\site".
    $OutputDirectory = Join-Path $root "artifacts" "site"
}

if (-not (Test-Path $sourceDirectory)) {
    throw "No encontré la plantilla del sitio en $sourceDirectory."
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "Hace falta la CLI de GitHub (gh) para leer los datos de la versión publicada."
}

# ---------------------------------------------------------------- datos de la versión

if ([string]::IsNullOrWhiteSpace($Tag)) {
    Write-Host "==> Buscando la última versión publicada de $Repository"

    # No se pregunta por `latest`: GitHub excluye de ahí las preliminares, y Sakura lleva en beta
    # desde que existe — `latest` responde "release not found" con seis versiones publicadas
    # delante. Es la misma trampa que ya documenta GitHubReleaseReader en la aplicación.
    $Tag = gh api "repos/$Repository/releases?per_page=20" `
        --jq 'map(select(.draft | not)) | first | .tag_name'

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Tag)) {
        throw "No pude averiguar cuál es la última versión de $Repository."
    }
}

Write-Host "==> Versión: $Tag"

$releaseJson = gh api "repos/$Repository/releases/tags/$Tag"
if ($LASTEXITCODE -ne 0) {
    throw "No pude leer la versión $Tag de $Repository."
}

$release = $releaseJson | ConvertFrom-Json
$assets = @($release.assets)

function Find-Asset {
    param([Parameter(Mandatory)][string]$Pattern)

    $match = $assets | Where-Object { $_.name -like $Pattern } | Select-Object -First 1
    if (-not $match) {
        throw "La versión $Tag no tiene ningún artefacto que coincida con '$Pattern'."
    }
    $match
}

$installer = Find-Asset -Pattern "*Setup.exe"
$portable = Find-Asset -Pattern "*portable.zip"
$installerChecksum = Find-Asset -Pattern "*Setup.exe.sha256"
$portableChecksum = Find-Asset -Pattern "*portable.zip.sha256"

function Get-PublishedHash {
    param([Parameter(Mandatory)]$Asset)

    # Los .sha256 tienen el formato "<hash>  <nombre de archivo>".
    $content = gh api $Asset.url -H "Accept: application/octet-stream"
    if ($LASTEXITCODE -ne 0) {
        throw "No pude descargar $($Asset.name)."
    }

    $first = ($content -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1)
    $hash = ($first -split "\s+")[0]

    if ($hash -notmatch '^[0-9a-fA-F]{64}$') {
        throw "El contenido de $($Asset.name) no parece un SHA-256: '$first'."
    }

    $hash.ToLowerInvariant()
}

function Format-Size {
    param([Parameter(Mandatory)][long]$Bytes)

    if ($Bytes -ge 1GB) { "{0:N1} GB" -f ($Bytes / 1GB) }
    elseif ($Bytes -ge 1MB) { "{0:N0} MB" -f ($Bytes / 1MB) }
    else { "{0:N0} KB" -f ($Bytes / 1KB) }
}

$culture = [System.Globalization.CultureInfo]::GetCultureInfo("es-MX")
# ConvertFrom-Json ya devuelve un DateTime cuando reconoce la fecha ISO, y volver a pasarla por
# Parse la convierte antes a texto con la cultura local — que Parse rechaza. Se aceptan las dos.
$published = if ($release.published_at -is [datetime]) {
    $release.published_at
}
else {
    [datetime]::Parse($release.published_at, [System.Globalization.CultureInfo]::InvariantCulture)
}

$published = $published.ToUniversalTime()

$replacements = @{
    "{{VERSION}}"          = $Tag.TrimStart("v")
    "{{RELEASE_URL}}"      = $release.html_url
    "{{RELEASE_DATE}}"     = $published.ToString("d 'de' MMMM 'de' yyyy", $culture)
    "{{INSTALLER_NAME}}"   = $installer.name
    "{{INSTALLER_URL}}"    = $installer.browser_download_url
    "{{INSTALLER_SIZE}}"   = Format-Size -Bytes $installer.size
    "{{INSTALLER_SHA256}}" = Get-PublishedHash -Asset $installerChecksum
    "{{ZIP_NAME}}"         = $portable.name
    "{{ZIP_URL}}"          = $portable.browser_download_url
    "{{ZIP_SIZE}}"         = Format-Size -Bytes $portable.size
    "{{ZIP_SHA256}}"       = Get-PublishedHash -Asset $portableChecksum
}

# ---------------------------------------------------------------- copiar y rellenar

Write-Host "==> Generando el sitio en $OutputDirectory"

if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null

Copy-Item (Join-Path $sourceDirectory "*") $OutputDirectory -Recurse -Force

# Sin esto, GitHub Pages pasa el sitio por Jekyll y se come cualquier carpeta que empiece por _.
New-Item (Join-Path $OutputDirectory ".nojekyll") -ItemType File -Force | Out-Null

$templates = Get-ChildItem $OutputDirectory -Recurse -File -Include *.html, *.css, *.js

foreach ($file in $templates) {
    $text = Get-Content $file.FullName -Raw -Encoding UTF8
    foreach ($key in $replacements.Keys) {
        $text = $text.Replace($key, $replacements[$key])
    }
    Set-Content $file.FullName -Value $text -Encoding UTF8 -NoNewline
}

# Ninguna marca puede sobrevivir: una que quede es un dato que la página estaría inventando.
$leftovers = Select-String -Path (Join-Path $OutputDirectory "*.html") -Pattern "\{\{[A-Z_]+\}\}" -AllMatches

if ($leftovers) {
    $names = ($leftovers.Matches.Value | Sort-Object -Unique) -join ", "
    throw "Quedaron marcas sin rellenar en el sitio: $names"
}

Write-Host ""
Write-Host "Sitio listo:"
Write-Host "  Carpeta:     $OutputDirectory"
Write-Host "  Versión:     $($replacements['{{VERSION}}'])"
Write-Host "  Instalador:  $($replacements['{{INSTALLER_NAME}}']) ($($replacements['{{INSTALLER_SIZE}}']))"
Write-Host "  Portable:    $($replacements['{{ZIP_NAME}}']) ($($replacements['{{ZIP_SIZE}}']))"

[pscustomobject]@{
    OutputDirectory = $OutputDirectory
    Version = $replacements["{{VERSION}}"]
    Tag = $Tag
}
