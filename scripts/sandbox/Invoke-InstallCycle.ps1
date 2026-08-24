<#
.SYNOPSIS
    Instalar → actualizar → desinstalar, dentro del Sandbox, y contar qué queda.

.DESCRIPTION
    Corre DENTRO de Windows Sandbox, no en el equipo de nadie. Lo lanza el `.wsb` que escribe
    `New-InstallCycleBundle.ps1`.

    Reproduce el mecanismo del que habla L13 sin necesitar red ni interacción:

      1. Instala la versión de partida con el instalador de verdad, en silencio.
      2. Hace el intercambio de carpetas **igual que el ayudante**: aparta la instalación, pone en
         su sitio el contenido del zip portable y borra la apartada.
      3. Desinstala con el desinstalador de verdad, en silencio.
      4. Mira qué quedó.

    **La primera versión de este guion simulaba el paso 2 con `Copy-Item` encima**, porque eso es lo
    que decía su propia documentación que hacía el ayudante. Era falso, y la medida que produjo
    describía un mecanismo que no existe. `UpdateHelperScript` mueve la carpeta entera: la actual se
    aparta y se borra, y la que ocupa su sitio sale del zip portable — que es la salida de
    `dotnet publish` y **no trae el desinstalador**, porque ese lo escribe Inno al instalar.

    La pregunta que contesta, entonces, son dos: **¿sigue habiendo desinstalador después de
    actualizar?** y **¿se lleva también lo que llegó en la actualización y él nunca anotó?**
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

Say "2. Intercambiando por $($portable.Name), como hace el ayudante"

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

# Una dependencia que llega con la version nueva y que el instalador nunca anoto. Con dos versiones
# reales distintas esto aparece solo —entre 0.28 y 0.29 fue WpfAnimatedGif.dll—, pero el instalador
# de partida tiene que llevar el barrido que se esta probando, asi que se construye aqui y las dos
# versiones son la misma. Este archivo ocupa el lugar de esa dependencia.
$forastero = "llegada-en-la-actualizacion.dll"
Set-Content (Join-Path $staged $forastero) -Value "una dependencia que el instalador nunca anoto" -Encoding UTF8

# El ayudante conserva el desinstalador antes de mover nada: no viene en el zip portable, y sin esta
# copia la actualizacion se lo llevaba por delante.
$conservados = @(Get-ChildItem -LiteralPath $installFolder -Filter "unins*" -File -ErrorAction SilentlyContinue)
foreach ($u in $conservados) { Copy-Item -LiteralPath $u.FullName -Destination $staged -Force }
Say "   desinstalador conservado: $($conservados.Count) archivos"

$apartada = "$installFolder.old"
if (Test-Path $apartada) { Remove-Item $apartada -Recurse -Force }
Move-Item -LiteralPath $installFolder -Destination $apartada
Move-Item -LiteralPath $staged -Destination $installFolder
Remove-Item $apartada -Recurse -Force -ErrorAction SilentlyContinue
Say "   intercambio hecho"

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
