<#
.SYNOPSIS
    Diseño D89 — genera el paquete MSIX de Sakura para Microsoft Store.

.DESCRIPTION
    Toma una publicación autocontenida (la misma que usa el instalador), le añade el manifiesto y los
    logos, y la empaqueta con makeappx del SDK de Windows.

    El paquete sale SIN FIRMAR a propósito: la Store lo firma con su certificado al publicarlo, que es
    todo el motivo de ir por la Store. Para probarlo en local se registra la carpeta preparada con
    Add-AppxPackage -Register, que con el modo desarrollador de Windows no necesita firma.

    Name y Publisher tienen que ser los que da Partner Center al reservar el nombre; los valores por
    omisión solo sirven para probar en local.

.EXAMPLE
    .\scripts\build-msix.ps1 -PublishDirectory artifacts\publish\win-x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [string]$IdentityName = "EXO.Sakura.Local",
    [string]$IdentityPublisher = "CN=EXO",
    [string]$PublisherDisplayName = "EXO",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "artifacts" "msix"
}

$exe = Join-Path $PublishDirectory "Sakura.exe"
if (-not (Test-Path $exe)) {
    throw "No encontré Sakura.exe en $PublishDirectory. Publica antes con scripts\publish.ps1."
}

# La versión de un paquete son cuatro números y la Store exige que el cuarto sea 0.
$productVersion = (Get-Item $exe).VersionInfo.ProductVersion
if ($productVersion -notmatch '^(\d+)\.(\d+)\.(\d+)') {
    throw "La versión de Sakura.exe no se entiende: '$productVersion'."
}
$packageVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeappx) {
    throw "No encontré makeappx.exe. Hace falta el SDK de Windows 10/11."
}

$layout = Join-Path $OutputDirectory "layout"
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item $layout -ItemType Directory -Force | Out-Null

Write-Host "==> Copiando la publicación"
Copy-Item (Join-Path $PublishDirectory "*") $layout -Recurse -Force

Write-Host "==> Logos a partir del icono real de la aplicación"
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $layout "Assets"
New-Item $assets -ItemType Directory -Force | Out-Null
$icon = New-Object System.Drawing.Icon((Join-Path $root "src\Nexo.App\Assets\Sakura.ico"), 256, 256)
$source = $icon.ToBitmap()

function Save-Logo {
    param([string]$Name, [int]$Width, [int]$Height, [double]$Fill)
    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $side = [int]([Math]::Min($Width, $Height) * $Fill)
    $graphics.DrawImage($source, [int](($Width - $side) / 2), [int](($Height - $side) / 2), $side, $side)
    $graphics.Dispose()
    $bitmap.Save((Join-Path $assets $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

Save-Logo -Name "Square44x44Logo.png" -Width 44 -Height 44 -Fill 1.0
Save-Logo -Name "Square150x150Logo.png" -Width 150 -Height 150 -Fill 0.66
Save-Logo -Name "Wide310x150Logo.png" -Width 310 -Height 150 -Fill 0.66
Save-Logo -Name "StoreLogo.png" -Width 50 -Height 50 -Fill 1.0
$source.Dispose()
$icon.Dispose()

Write-Host "==> Manifiesto $IdentityName $packageVersion"
$manifest = Get-Content (Join-Path $root "packaging\msix\AppxManifest.template.xml") -Raw -Encoding UTF8
$tokens = @{
    "{{IDENTITY_NAME}}"          = $IdentityName
    "{{IDENTITY_PUBLISHER}}"     = $IdentityPublisher
    "{{PUBLISHER_DISPLAY_NAME}}" = $PublisherDisplayName
    "{{PACKAGE_VERSION}}"        = $packageVersion
}
foreach ($key in $tokens.Keys) { $manifest = $manifest.Replace($key, $tokens[$key]) }
if ($manifest -match '\{\{[A-Z_]+\}\}') { throw "Quedaron marcas sin rellenar en el manifiesto." }
Set-Content (Join-Path $layout "AppxManifest.xml") -Value $manifest -Encoding UTF8 -NoNewline

$package = Join-Path $OutputDirectory "Sakura-$packageVersion-x64.msix"
if (Test-Path $package) { Remove-Item $package -Force }

Write-Host "==> Empaquetando con $($makeappx.FullName)"
# makeappx escribe una línea por archivo (más de quinientas); solo se enseña si falla.
$makeappxOutput = & $makeappx.FullName pack /d $layout /p $package /o 2>&1
if ($LASTEXITCODE -ne 0) {
    $makeappxOutput | Select-Object -Last 30 | Out-Host
    throw "makeappx falló con código $LASTEXITCODE."
}

Write-Host ""
Write-Host "Paquete: $package"
Write-Host "Carpeta preparada (para Add-AppxPackage -Register): $layout"

[pscustomobject]@{ Package = $package; Layout = $layout; Version = $packageVersion }
