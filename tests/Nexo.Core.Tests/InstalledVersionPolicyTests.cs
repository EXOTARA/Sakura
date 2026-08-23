using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D87 — L12: la lista de aplicaciones de Windows decía una versión y el ejecutable otra.
///
/// El caso que motivó esto está medido: en el equipo de Adler, el 2026-08-23, la entrada decía
/// «Sakura 0.27.0-beta» con la aplicación ya dos versiones por delante.
/// </summary>
public sealed class InstalledVersionPolicyTests
{
    [Fact]
    public void AStaleEntry_IsCorrected_InBothName_AndVersion()
    {
        // El nombre también lleva la versión, así que corregir solo el número dejaría la mitad de
        // la mentira en pie — y es la mitad que se ve en la lista.
        var correction = InstalledVersionPolicy.Reconcile("0.27.0-beta", "0.29.0-beta");

        Assert.NotNull(correction);
        Assert.Equal("0.29.0-beta", correction!.Value.DisplayVersion);
        Assert.Equal("Sakura 0.29.0-beta", correction.Value.DisplayName);
    }

    [Fact]
    public void AnEntryThatAlreadyAgrees_IsLeftAlone()
    {
        // Reescribir en cada arranque lo que ya está bien es tocar el registro por deporte.
        Assert.Null(InstalledVersionPolicy.Reconcile("0.29.0-beta", "0.29.0-beta"));
    }

    [Fact]
    public void WithNoEntryAtAll_NothingIsInvented()
    {
        // Portable: no aparece en «Aplicaciones instaladas» y no debe aparecer. Crear la entrada
        // prometería un desinstalador que no existe, para algo que se instala descomprimiendo.
        Assert.Null(InstalledVersionPolicy.Reconcile(null, "0.29.0-beta"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutKnowingWhatIsRunning_NothingIsWritten(string running)
    {
        // Cambiar un dato viejo por uno vacío es empeorarlo.
        Assert.Null(InstalledVersionPolicy.Reconcile("0.27.0-beta", running));
    }

    [Fact]
    public void SurroundingSpaceDoesNotCountAsADifference()
    {
        Assert.Null(InstalledVersionPolicy.Reconcile(" 0.29.0-beta ", "0.29.0-beta"));
    }
}
