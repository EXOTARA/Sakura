using Nexo.Core.Ambient;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D80 — la tarjeta «Ahora».
///
/// Lo que se fija aquí no es el texto, es la discreción: esta tarjeta vive en un panel que se abre
/// delante de quien esté al lado, así que lo que NO dice importa más que lo que dice.
/// </summary>
public sealed class NowGlancePolicyTests
{
    [Fact]
    public void ASensitiveWindow_IsNeverNamed()
    {
        // El gestor de contraseñas llega ya marcado, y con el título en blanco desde el proveedor.
        // Aun así se comprueba con título y proceso puestos: si algún día una fuente los trae
        // rellenos, esta tarjeta no puede ser la que los publique.
        var glance = NowGlancePolicy.Describe(
            new AmbientContextSnapshot("1Password — Bóveda personal", "1Password", IsSensitive: true));

        Assert.DoesNotContain("1Password", glance.Headline);
        Assert.DoesNotContain("1Password", glance.Detail);
        Assert.DoesNotContain("Bóveda", glance.Detail);
    }

    [Fact]
    public void TheApplicationIsTheHeadline_AndTheTitleIsTheDetail()
    {
        // El título cambia con cada pestaña; el programa es lo que describe en qué estás.
        var glance = NowGlancePolicy.Describe(
            new AmbientContextSnapshot("Presupuesto 2027.xlsx", "excel", IsSensitive: false));

        Assert.Equal("Excel", glance.Headline);
        Assert.Equal("Presupuesto 2027.xlsx", glance.Detail);
    }

    [Fact]
    public void WithNothingInFront_ItSaysSo_InsteadOfShowingSomethingStale()
    {
        var glance = NowGlancePolicy.Describe(null);

        Assert.Equal("Sakura", glance.Headline);
        Assert.False(string.IsNullOrWhiteSpace(glance.Detail));
    }

    [Fact]
    public void WithoutAProcessName_TheTitleTakesOver()
    {
        var glance = NowGlancePolicy.Describe(
            new AmbientContextSnapshot("Una ventana cualquiera", null, IsSensitive: false));

        Assert.Equal("Una ventana cualquiera", glance.Headline);
    }

    [Fact]
    public void WithNeither_ItAdmitsIt()
    {
        var glance = NowGlancePolicy.Describe(
            new AmbientContextSnapshot(null, null, IsSensitive: false));

        Assert.False(string.IsNullOrWhiteSpace(glance.Headline));
        Assert.False(string.IsNullOrWhiteSpace(glance.Detail));
    }

    [Fact]
    public void AVeryLongTitle_IsCutSoTheCardStaysAGlance()
    {
        var glance = NowGlancePolicy.Describe(
            new AmbientContextSnapshot(new string('a', 300), "chrome", IsSensitive: false));

        Assert.True(glance.Detail.Length <= NowGlancePolicy.MaximumDetailLength);
        Assert.EndsWith("…", glance.Detail);
    }
}
