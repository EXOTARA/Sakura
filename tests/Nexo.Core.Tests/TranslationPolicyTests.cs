using System.Globalization;
using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D86 — traducir un trozo de pantalla.
///
/// Se fijan las dos decisiones que no son de presentación: cuándo NO hay que preguntarle nada a un
/// modelo, y que lo que venía tapado por sensible siga tapado cuando salga del equipo.
/// </summary>
public sealed class TranslationPolicyTests
{
    private static OcrResult Read(params string[] lines) =>
        OcrResult.Success(
            string.Join("\n", lines),
            lines.Select(line => new OcrTextLine(line, 0, 0, 10, 10)).ToArray());

    [Fact]
    public void WithoutAModel_ItSaysSo_InsteadOfFailingLater()
    {
        var request = TranslationPolicy.Build(Read("Hello"), "es", hasProvider: false);

        Assert.False(request.CanTranslate);
        Assert.Contains("Ollama", request.Detail);
    }

    [Fact]
    public void WithNoTextInTheRegion_NobodyIsAsked()
    {
        // Mandar una imagen en blanco a un modelo gasta una llamada para recibir una disculpa.
        var request = TranslationPolicy.Build(Read(), "es", hasProvider: true);

        Assert.False(request.CanTranslate);
        Assert.Contains("No encontré texto", request.Detail);
    }

    [Fact]
    public void ASingleStrayCharacter_IsNotASentence()
    {
        var request = TranslationPolicy.Build(Read("¬"), "es", hasProvider: true);

        Assert.False(request.CanTranslate);
    }

    [Fact]
    public void TheTextTravelsWithItsLineBreaks()
    {
        var request = TranslationPolicy.Build(
            Read("Something went wrong", "Try again later"), "es", hasProvider: true);

        Assert.True(request.CanTranslate);
        Assert.Contains("Something went wrong\nTry again later", request.Prompt);
    }

    [Fact]
    public void ItAsksForATranslationAndNothingElse()
    {
        // Un modelo conversacional, si no se le ata, opina sobre el error en vez de traducirlo.
        var request = TranslationPolicy.Build(Read("Access denied"), "es", hasProvider: true);

        Assert.Contains("SOLO la traducción", request.Prompt);
        Assert.Contains("sin comentar el contenido", request.Prompt);
    }

    [Fact]
    public void WhatWasRedactedStaysRedacted()
    {
        // El texto llega ya tapado por SensitiveContentRedactor. Esta política no lo destapa ni
        // pide el original: si alguien arrastra el recuadro sobre un gestor de contraseñas, lo que
        // sale del equipo son las marcas.
        var redacted = SensitiveContentRedactor.Redact(Read("password: hunter2"));
        var request = TranslationPolicy.Build(redacted, "en", hasProvider: true);

        Assert.DoesNotContain("hunter2", request.Prompt);
    }

    [Theory]
    [InlineData("es", "español")]
    [InlineData("en", "English")]
    [InlineData("fr", "français")]
    public void TheTargetLanguageIsNamed_NotCoded_AndNotInTheMachinesLanguage(
        string code,
        string expected)
    {
        // El nombre tiene que ser el mismo en cualquier equipo. La primera versión usaba
        // DisplayName, que está traducido al idioma de Windows: en el runner en inglés de la CI
        // salía «Traduce al spanish». NativeName no depende de la máquina.
        var request = TranslationPolicy.Build(Read("Hello"), code, hasProvider: true);

        Assert.Contains(expected, request.Prompt);
    }

    [Fact]
    public void AnUnrecognisedLanguageCode_StillProducesAnOrder()
    {
        // .NET no lanza con un código inventado: fabrica una cultura y devuelve algo. No es un caso
        // que pueda darse hoy —el idioma sale de la cultura de Windows— pero el día que alguien lo
        // escriba a mano, lo que no puede pasar es que esto reviente o mande una orden vacía.
        var request = TranslationPolicy.Build(Read("Hello"), "xx-YY", hasProvider: true);

        Assert.True(request.CanTranslate);
        Assert.Contains("Traduce al", request.Prompt);
        Assert.Contains("Hello", request.Prompt);
    }

    [Fact]
    public void WithoutAChosenLanguage_WindowsDecides()
    {
        Assert.Equal(
            "es",
            TranslationPolicy.DefaultTargetLanguage(CultureInfo.GetCultureInfo("es-MX")));
    }
}
