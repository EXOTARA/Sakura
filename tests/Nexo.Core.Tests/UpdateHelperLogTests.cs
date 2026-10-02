using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

/// <summary>
/// Bloque de instalación y actualización (0.30.36) — el aviso de que la última actualización falló.
///
/// Los registros de ejemplo tienen la forma que produce el ayudante (ver <c>UpdateHelperScript</c>): si
/// el guion cambia lo que apunta, estas pruebas tienen que enterarse.
/// </summary>
public sealed class UpdateHelperLogTests
{
    private static readonly SakuraVersion Old = new(0, 30, 34, "beta");

    private const string Attempt = "2026-09-20T10:00:00.0000000+02:00  Version que se instala: 0.30.36-beta\n";

    private const string Success = """
        2026-09-20T10:00:00.0000000+02:00  Version que se instala: 0.30.36-beta
        2026-09-20T10:00:00.0000000+02:00  Sakura cerrada. Empieza el intercambio.
        2026-09-20T10:00:00.1000000+02:00  desinstalador conservado: 2 archivos
        2026-09-20T10:00:00.2000000+02:00  Intercambio hecho.
        2026-09-20T10:00:00.3000000+02:00  Sakura abierta de nuevo.
        2026-09-20T10:00:00.4000000+02:00  Terminado.
        """;

    [Fact]
    public void ASuccessfulUpdateSaysNothing() =>
        Assert.Null(UpdateHelperLog.Describe(Success, Old));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void AnEmptyOrMissingLogSaysNothing(string? log) =>
        Assert.Null(UpdateHelperLog.Describe(log, Old));

    [Fact]
    public void ALogWithAnUnexpectedShapeSaysNothing_InsteadOfInventingAFailure() =>
        Assert.Null(UpdateHelperLog.Describe(
            Attempt + "2026-09-20T10:00:00  Sakura cerrada. Empieza el intercambio.", Old));

    [Fact]
    public void IfSakuraWasStillOpen_ItSaysSo()
    {
        var message = UpdateHelperLog.Describe(
            Attempt + "2026-09-20T10:00:00.0000000+02:00  Sakura sigue abierta tras la espera. No se toca nada.",
            Old);

        Assert.Equal(
            "La última actualización no se aplicó porque Sakura seguía abierta. Vuelve a intentarlo.",
            message);
    }

    [Fact]
    public void IfThereWasNoPreparedFolder_ItSaysSo()
    {
        const string log = """
            2026-09-20T10:00:00.0000000+02:00  Version que se instala: 0.30.36-beta
            2026-09-20T10:00:00.0000000+02:00  Sakura cerrada. Empieza el intercambio.
            2026-09-20T10:00:00.1000000+02:00  No hay carpeta preparada que instalar.
            2026-09-20T10:00:00.2000000+02:00  Sakura abierta de nuevo.
            """;

        Assert.Equal(
            "La última actualización no llegó a prepararse. Vuelve a intentarlo.",
            UpdateHelperLog.Describe(log, Old));
    }

    [Fact]
    public void AFailedSwapIsReported_EvenThoughLinesFollowTheFailureLine()
    {
        // Tras el «FALLO:» el ayudante aún apunta líneas (la vuelta atrás y la reapertura de Sakura),
        // así que mirar solo la última línea no lo vería.
        const string log = """
            2026-09-20T10:00:00.0000000+02:00  Version que se instala: 0.30.36-beta
            2026-09-20T10:00:00.0000000+02:00  Sakura cerrada. Empieza el intercambio.
            2026-09-20T10:00:00.1000000+02:00  desinstalador conservado: 2 archivos
            2026-09-20T10:00:00.2000000+02:00  FALLO: Access to the path 'C:\Users\Alguien\AppData\Local\Programs\Sakura.new' is denied.
            2026-09-20T10:00:00.3000000+02:00  se habia apartado la actual: True
            2026-09-20T10:00:00.3500000+02:00  Instalacion anterior en su sitio.
            2026-09-20T10:00:00.4000000+02:00  Sakura abierta de nuevo.
            """;

        Assert.Equal(
            "La última actualización no se pudo aplicar y Sakura se quedó en la versión que tenías. Vuelve a intentarlo.",
            UpdateHelperLog.Describe(log, Old));
    }

    [Fact]
    public void TheSystemTextAfterFalloIsNeverShown()
    {
        // Rutas del usuario y mensajes de Windows: no van a lo que la persona lee.
        var message = UpdateHelperLog.Describe(
            Attempt + "2026-09-20T10:00:00  FALLO: Access to the path 'C:\\Users\\Alguien\\x' is denied.", Old);

        Assert.NotNull(message);
        Assert.DoesNotContain("Alguien", message, StringComparison.Ordinal);
        Assert.DoesNotContain("denied", message, StringComparison.Ordinal);
    }

    [Fact]
    public void IfTheOldInstallWasNotConfirmedInPlace_ItDoesNotPromiseSakuraStayedWhole()
    {
        var message = UpdateHelperLog.Describe(
            Attempt + "2026-09-20T10:00:00  FALLO: x\n2026-09-20T10:00:01  La instalacion anterior NO esta en su sitio.",
            Old);

        Assert.NotNull(message);
        Assert.DoesNotContain("se quedó en la versión que tenías", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.30.36-beta")]
    [InlineData("0.30.37-beta")]
    public void OnceTheRunningVersionReachesTheAttemptedOne_TheNoticeGoesAway(string running)
    {
        // Quien se actualizó después con el instalador o a mano no debe ver «vuelve a intentarlo».
        Assert.True(SakuraVersion.TryParse(running, out var version));

        Assert.Null(UpdateHelperLog.Describe(Attempt + "2026-09-20T10:00:00  FALLO: x", version));
    }

    [Fact]
    public void ALogWithoutTheAttemptedVersionSaysNothing() =>
        Assert.Null(UpdateHelperLog.Describe("2026-09-20T10:00:00  FALLO: x", Old));
}
