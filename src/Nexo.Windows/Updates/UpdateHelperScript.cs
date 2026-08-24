using System.Text;
using Nexo.Core.Updates;

namespace Nexo.Windows.Updates;

/// <summary>
/// Diseño D63 — el ayudante que hace el intercambio, escrito como un guion de PowerShell.
///
/// Sakura no puede sustituirse a sí misma: Windows no deja mover la carpeta donde vive el
/// ejecutable que se está ejecutando. Así que alguien tiene que hacerlo **desde fuera**, cuando
/// Sakura ya no está.
///
/// Es un guion y no un segundo programa por tres razones, en orden de peso:
///
/// **Se puede leer.** Queda en disco, en texto, y cualquiera —incluido quien no programa— puede
/// abrirlo y ver exactamente qué carpetas toca. Un actualizador es la pieza con más poder de toda
/// la aplicación; que sea inspeccionable no es un detalle.
///
/// **No hay que actualizarlo.** Un segundo ejecutable tendría su propia versión y su propio
/// problema de cómo actualizarlo, que es la mordedura de la serpiente en su propia cola.
///
/// **Ya está en el equipo.** PowerShell viene con Windows; no hay nada que instalar ni firmar.
///
/// El guion se escribe **fuera de la carpeta de instalación** a propósito: si viviera dentro, se
/// estaría moviendo a sí mismo a mitad de trabajo.
/// </summary>
public static class UpdateHelperScript
{
    /// <summary>
    /// Cuánto espera a que Sakura termine antes de rendirse, en segundos.
    ///
    /// Se rinde en vez de forzar el cierre: matar el proceso podría interrumpir a Sakura mientras
    /// guarda ajustes o una conversación, y perder eso por instalar una versión nueva es un mal
    /// negocio. Si no se cierra, la actualización se queda para la próxima.
    ///
    /// **Eran treinta segundos y no bastaban.** Sakura no se cierra de golpe: antes para el runtime
    /// de IA que administra, que es un proceso aparte y se toma su tiempo. En la primera prueba
    /// real el ayudante llegó al final de la espera con Sakura aún viva, se retiró sin tocar nada
    /// —que es lo correcto— y el resultado visible fue que Sakura se cerró y no volvió.
    ///
    /// Tres minutos cubren un apagado lento sin dejar el ayudante colgado si algo se atasca de
    /// verdad. Esperar de más no cuesta nada: nadie está mirando, porque Sakura ya se cerró.
    /// </summary>
    private const int WaitForExitSeconds = 180;

    /// <summary>
    /// Genera el guion. Todo lo que decide qué carpetas se tocan viene ya resuelto y comprobado por
    /// <see cref="UpdateSwapPathPolicy"/>: aquí no se decide nada, solo se ejecuta.
    /// </summary>
    public static string Build(
        UpdateSwapPaths paths,
        int kohanaProcessId,
        string executableToLaunch,
        string packagePath = "")
    {
        if (!paths.IsSafe)
        {
            throw new ArgumentException(
                "No se genera el ayudante para unas rutas que la política rechazó.", nameof(paths));
        }

        var script = new StringBuilder();

        script.AppendLine("# Ayudante de actualización de Sakura. Lo genera Sakura y puedes leerlo entero:");
        script.AppendLine("# espera a que Sakura se cierre, aparta la carpeta actual, pone la nueva en su");
        script.AppendLine("# sitio y vuelve a abrir. Si algo falla, devuelve la anterior a donde estaba.");
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.AppendLine();

        // Diseño D67 — deja rastro. Los dos fallos en vivo hubo que deducirlos del estado de las
        // carpetas, porque el ayudante no decía nada: ni qué paso alcanzó, ni con qué error salió.
        // Un registro de cuatro líneas convierte media hora de deducción en una lectura.
        script.AppendLine("$registro = Join-Path $PSScriptRoot 'ultima-actualizacion.log'");
        script.AppendLine("function Apunta($t) {");
        script.AppendLine("    \"$(Get-Date -Format o)  $t\" | Add-Content -LiteralPath $registro -Encoding UTF8");
        script.AppendLine("}");
        script.AppendLine("Set-Content -LiteralPath $registro -Value '' -Encoding UTF8");
        script.AppendLine();

        script.AppendLine($"$install = {Quote(paths.Install)}");
        script.AppendLine($"$staged = {Quote(paths.Staged)}");
        script.AppendLine($"$previous = {Quote(paths.Previous)}");
        script.AppendLine($"$exe = {Quote(executableToLaunch)}");
        script.AppendLine($"$paquete = {Quote(packagePath)}");
        script.AppendLine($"$pid_ = {kohanaProcessId}");
        script.AppendLine();

        // Esperar por identificador y no por nombre: otra instancia de Sakura abierta a la vez no
        // tiene por qué bloquear esta, y matar «todo lo que se llame Sakura» es de las cosas que
        // parecen razonables hasta que cierran algo que no era.
        script.AppendLine("try {");
        script.AppendLine("    $proc = Get-Process -Id $pid_ -ErrorAction SilentlyContinue");
        script.AppendLine("    if ($proc) { $proc.WaitForExit(" + WaitForExitSeconds * 1000 + ") | Out-Null }");
        script.AppendLine("} catch { }");
        script.AppendLine();

        script.AppendLine("if (Get-Process -Id $pid_ -ErrorAction SilentlyContinue) {");
        script.AppendLine("    Apunta 'Sakura sigue abierta tras la espera. No se toca nada.'");
        script.AppendLine("    exit 2");
        script.AppendLine("}");
        script.AppendLine("Apunta 'Sakura cerrada. Empieza el intercambio.'");
        script.AppendLine();

        script.AppendLine("if (-not (Test-Path -LiteralPath $staged)) {");
        script.AppendLine("    Apunta 'No hay carpeta preparada que instalar.'");
        script.AppendLine("    exit 3");
        script.AppendLine("}");
        script.AppendLine();

        // L13 — el desinstalador viaja con la instalación, o deja de existir.
        //
        // El intercambio sustituye la carpeta **entera**: la actual se aparta y se borra, y la que
        // ocupa su sitio sale del zip portable. Ese zip es la salida de `dotnet publish` y no trae
        // `unins000.exe` ni `unins000.dat` — esos los escribe Inno al instalar, y solo existen en
        // una instalación hecha con el instalador.
        //
        // Sin esta copia, **cada actualización se llevaba por delante el desinstalador** y dejaba
        // en el registro una entrada que apuntaba a un archivo que ya no estaba. Desinstalar desde
        // «Aplicaciones instaladas» no dejaba restos: ni siquiera llegaba a arrancar.
        //
        // Se copia antes de mover nada, mientras las dos carpetas siguen donde estaban. Y no es un
        // fallo que no haya nada que copiar: quien usa el zip portable nunca tuvo desinstalador, y
        // su actualización debe seguir funcionando igual.
        script.AppendLine("$desinstalador = @(Get-ChildItem -LiteralPath $install -Filter 'unins*' -File -ErrorAction SilentlyContinue)");
        script.AppendLine("foreach ($u in $desinstalador) {");
        script.AppendLine("    Copy-Item -LiteralPath $u.FullName -Destination $staged -Force -ErrorAction SilentlyContinue");
        script.AppendLine("}");
        script.AppendLine("Apunta ('desinstalador conservado: ' + $desinstalador.Count + ' archivos')");
        script.AppendLine();

        // Un intento anterior pudo dejar una carpeta apartada. Se quita antes de empezar, porque si
        // no el movimiento falla y el ayudante se cree a mitad de un trabajo que no ha empezado.
        script.AppendLine("if (Test-Path -LiteralPath $previous) {");
        script.AppendLine("    Remove-Item -LiteralPath $previous -Recurse -Force -ErrorAction SilentlyContinue");
        script.AppendLine("}");
        script.AppendLine();

        script.AppendLine("$moved = $false");
        script.AppendLine("try {");
        script.AppendLine("    Move-Item -LiteralPath $install -Destination $previous");
        script.AppendLine("    $moved = $true");
        script.AppendLine("    Move-Item -LiteralPath $staged -Destination $install");
        script.AppendLine("}");
        // La vuelta atrás tiene que limpiar antes de devolver, y esto lo encontró una prueba
        // ejecutando el intercambio de verdad: al fallar la promoción, Windows deja una carpeta
        // **a medias** en el sitio de la instalación. La versión anterior de este guión solo
        // devolvía la apartada «si el sitio estaba libre», y el sitio no lo estaba: se quedaba la
        // carpeta incompleta puesta y la buena apartada en .old. Es exactamente el estado que todo
        // este diseño existe para evitar, y la guarda pensada para no pisar nada era justo lo que
        // lo causaba.
        script.AppendLine("catch {");
        script.AppendLine("    if ($moved) {");
        script.AppendLine("        # Lo que haya en el sitio ahora es la promoción a medias, no la");
        script.AppendLine("        # instalación: la buena está apartada. Se quita para hacerle hueco.");
        script.AppendLine("        if (Test-Path -LiteralPath $install) {");
        script.AppendLine("            Remove-Item -LiteralPath $install -Recurse -Force -ErrorAction SilentlyContinue");
        script.AppendLine("        }");
        script.AppendLine("        if (-not (Test-Path -LiteralPath $install)) {");
        script.AppendLine("            Move-Item -LiteralPath $previous -Destination $install -ErrorAction SilentlyContinue");
        script.AppendLine("        }");
        script.AppendLine("    }");
        script.AppendLine("    Apunta (\"FALLO: \" + $_.Exception.Message)");
        script.AppendLine("    Apunta (\"se habia apartado la actual: \" + $moved)");
        script.AppendLine("    exit 1");
        script.AppendLine("}");
        script.AppendLine("Apunta 'Intercambio hecho.'");
        script.AppendLine();

        // Lo viejo se borra solo cuando lo nuevo está en su sitio. Que no se pueda borrar es un
        // estorbo en disco, no una avería: no se deshace nada por eso.
        script.AppendLine("Remove-Item -LiteralPath $previous -Recurse -Force -ErrorAction SilentlyContinue");
        script.AppendLine();

        script.AppendLine("if (Test-Path -LiteralPath $exe) { Start-Process -FilePath $exe }");
        script.AppendLine();

        // El paquete descargado se borra al terminar. Son cien megas por actualización, y tras la
        // primera prueba real se quedaron en disco porque nadie los limpiaba.
        //
        // El guión **no** se borra a sí mismo, aunque la cabecera lo llegara a prometer: un guión no
        // puede borrarse mientras se ejecuta, y las maniobras para conseguirlo —lanzar otro proceso
        // que espere y lo borre— añaden una pieza frágil a la parte que menos puede permitirse una.
        // Ocupa dos kilobytes, se sobrescribe en la siguiente actualización, y dejarlo tiene una
        // ventaja: si algo sale mal, queda ahí para poder leerlo.
        // Se comprueba que hay ruta antes de borrar: Remove-Item con una cadena vacía es un error
        // de enlace de parámetros, que **no** lo silencia -ErrorAction, y el guión terminaba con
        // código de fallo después de haber hecho el intercambio bien. Un paso de limpieza no puede
        // convertir un éxito en un fallo aparente — es exactamente lo que despista al diagnosticar.
        script.AppendLine("if ($paquete -and (Test-Path -LiteralPath $paquete)) {");
        script.AppendLine("    Remove-Item -LiteralPath $paquete -Force -ErrorAction SilentlyContinue");
        script.AppendLine("}");
        script.AppendLine("Apunta 'Terminado.'");
        script.AppendLine("exit 0");

        return script.ToString();
    }

    /// <summary>
    /// Mete una ruta en el guion sin que pueda dejar de ser una ruta.
    ///
    /// Se usan comillas simples de PowerShell, que no interpretan nada dentro —ni <c>$</c> ni
    /// comillas invertidas—, y se duplica la comilla simple, que es como se escapa en PowerShell.
    /// Sin esto, una carpeta con un apóstrofo en el nombre partiría el guion por la mitad; y aunque
    /// las rutas vengan ya validadas, un guion que se rompe según cómo te llames es el tipo de
    /// fallo que solo le pasa a una persona y nadie sabe reproducir.
    /// </summary>
    private static string Quote(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
