# Fuente única de la versión: Directory.Build.props.
#
# Existe porque durante meses hubo tres versiones distintas a la vez: la de `Directory.Build.props`,
# la que `Nexo.App.csproj` machacaba encima, y la que estos scripts traían como valor por omisión.
# El binario instalado el 2026-08-21 lo demostraba: ProductVersion 0.27.0-beta y FileVersion
# 0.9.5.0 en el mismo archivo. Un valor por omisión de versión en un script de publicación es una
# trampa: acierta el día que se escribe y miente todos los demás.

function Get-SakuraVersion {
    [CmdletBinding()]
    param(
        [string]$RepositoryRoot
    )

    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Split-Path -Parent $PSScriptRoot
    }

    $propsPath = Join-Path $RepositoryRoot "Directory.Build.props"
    if (-not (Test-Path $propsPath)) {
        throw "No encontré Directory.Build.props en $RepositoryRoot."
    }

    [xml]$props = Get-Content $propsPath -Raw
    $group = $props.Project.PropertyGroup | Select-Object -First 1

    $prefix = $group.VersionPrefix
    $suffix = $group.VersionSuffix

    if ([string]::IsNullOrWhiteSpace($prefix)) {
        throw "Directory.Build.props no declara VersionPrefix."
    }

    if ([string]::IsNullOrWhiteSpace($suffix)) { $prefix } else { "$prefix-$suffix" }
}

function Get-SakuraNumericVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Version
    )

    if ($Version -match '^(?<number>\d+\.\d+\.\d+)') {
        "$($Matches.number).0"
    }
    else {
        throw "La versión '$Version' no tiene el formato X.Y.Z[-sufijo]."
    }
}
