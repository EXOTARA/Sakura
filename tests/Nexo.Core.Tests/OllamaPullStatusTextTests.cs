using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class OllamaPullStatusTextTests
{
    [Theory]
    [InlineData("pulling manifest", "Preparando la descarga…")]
    [InlineData("pulling 6a0746a1ec1a", "Descargando…")]
    [InlineData("verifying sha256 digest", "Comprobando que llegó entero…")]
    [InlineData("writing manifest", "Guardando…")]
    [InlineData("removing any unused layers", "Guardando…")]
    [InlineData("success", "Listo")]
    [InlineData("", "Descargando…")]
    [InlineData(null, "Descargando…")]
    public void KnownOllamaSteps_ReadInSpanish(string? status, string expected) =>
        Assert.Equal(expected, OllamaPullStatusText.Step(status));

    [Fact]
    public void AnUnknownStep_IsShownAsItCame() =>
        Assert.Equal("reticulating splines", OllamaPullStatusText.Step("reticulating splines"));

    [Fact]
    public void WithFigures_SaysHowMuchOfHowMuch()
    {
        var progress = new OllamaPullProgress("pulling 6a0746a1ec1a", 512L * 1024 * 1024, 3L * 1024 * 1024 * 1024);
        Assert.Equal("Descargando… · 512 MB de 3.0 GB", OllamaPullStatusText.Describe(progress).Replace(',', '.'));
    }

    [Fact]
    public void WithoutATotal_ShowsOnlyTheStep() =>
        Assert.Equal("Preparando la descarga…", OllamaPullStatusText.Describe(new OllamaPullProgress("pulling manifest", null, null)));
}
