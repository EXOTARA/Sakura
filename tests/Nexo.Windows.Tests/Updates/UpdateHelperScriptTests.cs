using Nexo.Core.Updates;
using Nexo.Windows.Updates;

namespace Nexo.Windows.Tests.Updates;

/// <summary>
/// Diseño D63 — el guion que hace el intercambio.
///
/// Es la pieza con más poder de la aplicación: mueve la carpeta donde vive Sakura. Lo que se prueba
/// aquí es que no se le pueda dar otra forma que la prevista y que sepa deshacer.
/// </summary>
public sealed class UpdateHelperScriptTests
{
    private static UpdateSwapPaths SafePaths() =>
        UpdateSwapPathPolicy.Resolve(@"C:\Users\Alguien\AppData\Local\Programs\Sakura");

    private static string Build() =>
        UpdateHelperScript.Build(SafePaths(), 4242, @"C:\Users\Alguien\AppData\Local\Programs\Sakura\Kohana.exe");

    [Fact]
    public void ItRefusesToBuildForPathsThePolicyRejected()
    {
        // La última barrera: aunque alguien llame a esto directamente, no se genera un guion que
        // mueva una carpeta que la política ya dijo que no.
        var rejected = UpdateSwapPathPolicy.Resolve(@"C:\");

        Assert.Throws<ArgumentException>(() =>
            UpdateHelperScript.Build(rejected, 1, @"C:\x.exe"));
    }

    [Fact]
    public void ItWaitsForTheExactProcess_NotForAnythingNamedSakura()
    {
        // Matar «todo lo que se llame Sakura» parece razonable hasta que cierra algo que no era.
        var script = Build();

        Assert.Contains("$pid_ = 4242", script, StringComparison.Ordinal);
        Assert.Contains("Get-Process -Id $pid_", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop-Process", script, StringComparison.Ordinal);
    }

    [Fact]
    public void IfSakuraIsStillOpen_NothingIsTouched()
    {
        // Rendirse es la respuesta correcta: forzar el cierre podría interrumpirla guardando ajustes
        // o una conversación, y perder eso por instalar una versión nueva es un mal negocio.
        var script = Build();

        var stillOpen = script.IndexOf("if (Get-Process -Id $pid_", StringComparison.Ordinal);
        var firstMove = script.IndexOf("Move-Item", StringComparison.Ordinal);

        Assert.True(stillOpen >= 0 && firstMove > stillOpen);
    }

    [Fact]
    public void TheOldVersionIsPutBackIfThePromotionFails()
    {
        var script = Build();

        Assert.Contains("$moved = $true", script, StringComparison.Ordinal);
        Assert.Contains(
            "Move-Item -LiteralPath $previous -Destination $install",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void APartialPromotionIsClearedBeforePuttingTheOldOneBack()
    {
        // Esto lo encontró una prueba ejecutando el intercambio de verdad, no leyendo el guión.
        //
        // Cuando la promoción falla, Windows deja una carpeta **a medias** en el sitio de la
        // instalación. La primera versión solo devolvía la apartada «si el sitio estaba libre», y no
        // lo estaba: quedaba la carpeta incompleta puesta y la buena en .old. La guarda pensada para
        // no pisar nada era justo lo que rompía la vuelta atrás.
        var script = Build();

        var clear = script.IndexOf(
            "Remove-Item -LiteralPath $install -Recurse -Force", StringComparison.Ordinal);
        var restore = script.IndexOf(
            "Move-Item -LiteralPath $previous -Destination $install", StringComparison.Ordinal);

        Assert.True(clear >= 0, "La vuelta atrás tiene que quitar la promoción a medias.");
        Assert.True(restore > clear, "Hay que hacer hueco antes de devolver la versión anterior.");
    }

    [Fact]
    public void TheOldVersionIsOnlyDeletedAfterTheNewOneIsInPlace()
    {
        var script = Build();

        var promote = script.IndexOf(
            "Move-Item -LiteralPath $staged -Destination $install", StringComparison.Ordinal);
        var discard = script.LastIndexOf(
            "Remove-Item -LiteralPath $previous", StringComparison.Ordinal);

        Assert.True(promote >= 0);
        Assert.True(discard > promote);
    }

    [Fact]
    public void TheUninstallerIsCarriedIntoTheNewFolder()
    {
        // L13 — sin esto, actualizar borraba el desinstalador.
        //
        // El intercambio sustituye la carpeta entera, y la que entra sale del zip portable, que es
        // la salida de `dotnet publish`: no trae `unins000.exe` ni `unins000.dat`, porque esos los
        // escribe Inno al instalar. La entrada del registro sobrevivía apuntando a un archivo que
        // ya no existía.
        var script = Build();

        Assert.Contains("-Filter 'unins*'", script, StringComparison.Ordinal);
        Assert.Contains(
            "Copy-Item -LiteralPath $u.FullName -Destination $staged",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheUninstallerIsCopiedBeforeAnythingMoves()
    {
        // Se copia de la instalación a la carpeta preparada, así que tiene que ocurrir mientras las
        // dos siguen donde estaban. Después del primer movimiento, $install ya no es una carpeta.
        var script = Build();

        var copy = script.IndexOf("-Filter 'unins*'", StringComparison.Ordinal);
        var firstMove = script.IndexOf("Move-Item", StringComparison.Ordinal);

        Assert.True(copy >= 0, "El desinstalador tiene que conservarse.");
        Assert.True(firstMove > copy, "Copiarlo después de mover sería copiarlo de una carpeta que ya no está.");
    }

    [Fact]
    public void AnInstallWithNoUninstallerStillUpdates()
    {
        // Quien usa el zip portable nunca tuvo desinstalador. Que no haya nada que copiar no puede
        // ser un fallo: `@()` para que contar cero funcione, y sin `exit` por el camino.
        var script = Build();

        var copyBlock = script.IndexOf("$desinstalador = @(", StringComparison.Ordinal);
        var afterBlock = script.IndexOf("$moved = $false", StringComparison.Ordinal);

        Assert.True(copyBlock >= 0);
        Assert.DoesNotContain(
            "exit",
            script[copyBlock..afterBlock],
            StringComparison.Ordinal);
    }

    [Fact]
    public void APathWithAnApostropheCannotBreakTheScript()
    {
        // Una carpeta como «C:\Users\O'Brien\…» partiría el guion por la mitad sin escapar. Es el
        // tipo de fallo que solo le pasa a una persona y que nadie sabe reproducir.
        var paths = UpdateSwapPathPolicy.Resolve(@"C:\Users\O'Brien\AppData\Local\Programs\Sakura");
        Assert.True(paths.IsSafe, paths.Problem);

        var script = UpdateHelperScript.Build(paths, 7, @"C:\Users\O'Brien\k.exe");

        Assert.Contains("O''Brien", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPathIsQuoted()
    {
        // Sin comillas, un espacio en la ruta —«Program Files»— convierte un argumento en dos.
        var script = Build();

        Assert.Contains("$install = '", script, StringComparison.Ordinal);
        Assert.Contains("$staged = '", script, StringComparison.Ordinal);
        Assert.Contains("$previous = '", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ItStopsOnTheFirstError()
    {
        // Sin esto PowerShell sigue adelante tras un fallo, que en un intercambio de carpetas
        // significa continuar sobre un estado que ya no es el que se creía.
        Assert.Contains("$ErrorActionPreference = 'Stop'", Build(), StringComparison.Ordinal);
    }

    [Fact]
    public void ItExplainsItselfToWhoeverOpensIt()
    {
        // Es la pieza con más poder de la aplicación; que se pueda leer no es un detalle.
        Assert.Contains("# Ayudante de actualización de Sakura.", Build(), StringComparison.Ordinal);
    }
}
