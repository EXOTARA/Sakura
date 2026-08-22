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
    [string]$OutputDirectory = "",
    [string]$SiteRoot = "https://exotara.github.io/Sakura/"
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

# ConvertFrom-Json ya devuelve un DateTime cuando reconoce la fecha ISO, y volver a pasarla por
# Parse la convierte antes a texto con la cultura local — que Parse rechaza. Se aceptan las dos.
$published = if ($release.published_at -is [datetime]) {
    $release.published_at
}
else {
    [datetime]::Parse($release.published_at, [System.Globalization.CultureInfo]::InvariantCulture)
}

$published = $published.ToUniversalTime()

# Datos de la versión. Son los mismos en los dos idiomas salvo la fecha, que se escribe en cada uno.
$release_tokens = @{
    "{{VERSION}}"          = $Tag.TrimStart("v")
    "{{RELEASE_URL}}"      = $release.html_url
    "{{INSTALLER_NAME}}"   = $installer.name
    "{{INSTALLER_URL}}"    = $installer.browser_download_url
    "{{INSTALLER_SIZE}}"   = Format-Size -Bytes $installer.size
    "{{INSTALLER_SHA256}}" = Get-PublishedHash -Asset $installerChecksum
    "{{ZIP_NAME}}"         = $portable.name
    "{{ZIP_URL}}"          = $portable.browser_download_url
    "{{ZIP_SIZE}}"         = Format-Size -Bytes $portable.size
    "{{ZIP_SHA256}}"       = Get-PublishedHash -Asset $portableChecksum
}

# ---------------------------------------------------------------- textos

$stringsPath = Join-Path $sourceDirectory "strings.json"
if (-not (Test-Path $stringsPath)) {
    throw "No encontré los textos en $stringsPath."
}

$strings = Get-Content $stringsPath -Raw -Encoding UTF8 | ConvertFrom-Json

# Un idioma traducido a medias no se publica. Cualquier clave que exista en uno y falte en el otro
# para el flujo aquí, y no en la cara de quien abra la página y encuentre media frase en español.
$languages = @("es", "en")
$keysByLanguage = @{}

foreach ($language in $languages) {
    if (-not $strings.PSObject.Properties.Name.Contains($language)) {
        throw "strings.json no tiene el idioma '$language'."
    }
    $keysByLanguage[$language] = @($strings.$language.PSObject.Properties.Name)
}

$missing = foreach ($language in $languages) {
    $others = $languages | Where-Object { $_ -ne $language }
    foreach ($other in $others) {
        foreach ($key in $keysByLanguage[$other]) {
            if ($keysByLanguage[$language] -notcontains $key) {
                "'$key' está en '$other' y falta en '$language'"
            }
        }
    }
}

if ($missing) {
    throw "Los textos no están completos:`n  $(($missing | Sort-Object -Unique) -join "`n  ")"
}

# Claves que nadie usa: no rompen la página, pero son texto que alguien mantiene para nada.
$templateText = Get-Content (Join-Path $sourceDirectory "index.html") -Raw -Encoding UTF8
$usedKeys = [regex]::Matches($templateText, '\{\{t:([^}]+)\}\}') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

$unused = $keysByLanguage["es"] | Where-Object { $_ -notlike "_*" -and $usedKeys -notcontains $_ }
if ($unused) {
    Write-Warning ("Textos que la plantilla no usa: {0}" -f (($unused | Sort-Object) -join ", "))
}

$orphans = $usedKeys | Where-Object { $keysByLanguage["es"] -notcontains $_ }
if ($orphans) {
    throw "La plantilla pide textos que no existen: $(($orphans | Sort-Object) -join ", ")"
}

# ---------------------------------------------------------------- generar

Write-Host "==> Generando el sitio en $OutputDirectory"

if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null

# Lo compartido va en la raíz una sola vez; /en/ lo alcanza con ../ vía {{BASE}}.
foreach ($shared in @("styles.css", "app.js")) {
    Copy-Item (Join-Path $sourceDirectory $shared) $OutputDirectory -Force
}
Copy-Item (Join-Path $sourceDirectory "assets") $OutputDirectory -Recurse -Force

# Sin esto, GitHub Pages pasa el sitio por Jekyll y se come cualquier carpeta que empiece por _.
New-Item (Join-Path $OutputDirectory ".nojekyll") -ItemType File -Force | Out-Null

$cultures = @{ es = "es-MX"; en = "en-US" }
$dateFormats = @{ es = "d 'de' MMMM 'de' yyyy"; en = "d MMMM yyyy" }
# El español va en la raíz porque es el idioma en el que está escrito el producto.
$paths = @{ es = ""; en = "en/" }
$bases = @{ es = ""; en = "../" }

foreach ($language in $languages) {
    # @() alrededor del filtro: con dos idiomas devuelve una sola cadena, y [0] sobre una cadena
    # da su primer carácter, no la cadena.
    $other = @($languages | Where-Object { $_ -ne $language })[0]
    $culture = [System.Globalization.CultureInfo]::GetCultureInfo($cultures[$language])

    $text = $templateText

    foreach ($property in $strings.$language.PSObject.Properties) {
        $text = $text.Replace("{{t:$($property.Name)}}", $property.Value)
    }

    $tokens = $release_tokens.Clone()
    $tokens["{{RELEASE_DATE}}"]  = $published.ToString($dateFormats[$language], $culture)
    $tokens["{{BASE}}"]          = $bases[$language]
    $tokens["{{ALT_HREF}}"]      = if ($language -eq "es") { "en/" } else { "../" }
    $tokens["{{ALT_CODE}}"]      = $other.ToUpperInvariant()
    $tokens["{{SITE_ROOT}}"]     = $SiteRoot
    $tokens["{{CANONICAL}}"]     = $SiteRoot + $paths[$language]
    $tokens["{{ALT_CANONICAL}}"] = $SiteRoot + $paths[$other]

    foreach ($key in $tokens.Keys) {
        $text = $text.Replace($key, $tokens[$key])
    }

    $target = Join-Path $OutputDirectory $paths[$language]
    if (-not (Test-Path $target)) {
        New-Item $target -ItemType Directory -Force | Out-Null
    }

    $file = Join-Path $target "index.html"
    Set-Content $file -Value $text -Encoding UTF8 -NoNewline

    # Ninguna marca puede sobrevivir: una que quede es un dato que la página estaría inventando.
    $leftovers = [regex]::Matches($text, '\{\{[A-Za-z_:.]+\}\}')
    if ($leftovers.Count -gt 0) {
        $names = ($leftovers.Value | Sort-Object -Unique) -join ", "
        throw "Quedaron marcas sin rellenar en '$language': $names"
    }

    Write-Host ("  {0,-3} -> {1}" -f $language, $file)
}

Write-Host ""
Write-Host "Sitio listo:"
Write-Host "  Carpeta:     $OutputDirectory"
Write-Host "  Idiomas:     $($languages -join ', ')"
Write-Host "  Versión:     $($release_tokens['{{VERSION}}'])"
Write-Host "  Instalador:  $($release_tokens['{{INSTALLER_NAME}}']) ($($release_tokens['{{INSTALLER_SIZE}}']))"
Write-Host "  Portable:    $($release_tokens['{{ZIP_NAME}}']) ($($release_tokens['{{ZIP_SIZE}}']))"

[pscustomobject]@{
    OutputDirectory = $OutputDirectory
    Version = $release_tokens["{{VERSION}}"]
    Tag = $Tag
}
