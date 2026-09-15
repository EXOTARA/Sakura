using Nexo.Core.Ai;
using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class LensContextBuilderTests
{
    private static readonly OcrResult EmptyOcr = OcrResult.Success(string.Empty, []);
    private static readonly IReadOnlyList<UiAutomationElement> NoElements = [];

    [Fact]
    public void Desarrollo_AlwaysResolvesToTechnicalDiagnostic()
    {
        var context = LensContextBuilder.Build(
            LensMode.Desarrollo, "Bloc de notas", EmptyOcr, NoElements);

        Assert.Equal(AiRequestMode.VisionTechnicalDiagnostic, context.RequestMode);
    }

    [Fact]
    public void Estudio_AlwaysResolvesToGeneral_EvenWithTechnicalOcrText()
    {
        var ocrWithError = OcrResult.Success("System.NullReferenceException at line 42", []);

        var context = LensContextBuilder.Build(
            LensMode.Estudio, "Visual Studio", ocrWithError, NoElements);

        Assert.Equal(AiRequestMode.VisionGeneral, context.RequestMode);
    }

    [Fact]
    public void Soporte_ResolvesToTechnicalDiagnostic_WhenOcrTextLooksTechnical()
    {
        var ocrWithError = OcrResult.Success("Error de compilación: CS0103", []);

        var context = LensContextBuilder.Build(
            LensMode.Soporte, "Visual Studio", ocrWithError, NoElements);

        Assert.Equal(AiRequestMode.VisionTechnicalDiagnostic, context.RequestMode);
    }

    [Fact]
    public void Soporte_ResolvesToGeneral_WhenOcrTextIsNotTechnical()
    {
        var ocrWithoutError = OcrResult.Success("Bienvenido a tu correo", []);

        var context = LensContextBuilder.Build(
            LensMode.Soporte, "Outlook", ocrWithoutError, NoElements);

        Assert.Equal(AiRequestMode.VisionGeneral, context.RequestMode);
    }

    [Theory]
    [InlineData(LensMode.Soporte, "¿Qué problema hay en esta ventana y cómo lo resuelvo?")]
    [InlineData(LensMode.Estudio, "¿Qué es esto y cómo funciona? Explícamelo paso a paso.")]
    [InlineData(LensMode.Desarrollo, "Analiza el código o error visible aquí y dime qué corregir.")]
    public void Build_UsesTheExpectedDefaultPromptPerMode(LensMode mode, string expectedPrompt)
    {
        var context = LensContextBuilder.Build(mode, "Alguna ventana", EmptyOcr, NoElements);

        Assert.Equal(expectedPrompt, context.Prompt);
    }

    [Fact]
    public void SystemContext_IncludesWindowTitleAndOcrText()
    {
        var ocr = OcrResult.Success("Texto visible en pantalla", []);

        var context = LensContextBuilder.Build(LensMode.Estudio, "Mi Ventana", ocr, NoElements);

        Assert.Contains("Mi Ventana", context.SystemContext);
        Assert.Contains("Texto visible en pantalla", context.SystemContext);
    }

    [Fact]
    public void SystemContext_IncludesOnlyNamedElements()
    {
        var elements = new UiAutomationElement[]
        {
            new("Guardar", "ControlType.Button", 0, 0, 10, 10),
            new(null, "ControlType.Pane", 0, 0, 10, 10)
        };

        var context = LensContextBuilder.Build(LensMode.Estudio, "Ventana", EmptyOcr, elements);

        Assert.Contains("Guardar", context.SystemContext);
        Assert.Contains("Button", context.SystemContext);
    }

    [Fact]
    public void SystemContext_WithEmptyWindowTitle_UsesAnHonestPlaceholder()
    {
        var context = LensContextBuilder.Build(LensMode.Estudio, string.Empty, EmptyOcr, NoElements);

        Assert.Contains("(sin título)", context.SystemContext);
    }

    [Fact]
    public void SystemContext_TruncatesVeryLongOcrText()
    {
        var longText = new string('a', 5000);
        var ocr = OcrResult.Success(longText, []);

        var context = LensContextBuilder.Build(LensMode.Estudio, "Ventana", ocr, NoElements);

        Assert.DoesNotContain(new string('a', 5000), context.SystemContext);
        Assert.Contains("…", context.SystemContext);
    }

    [Fact]
    public void Explain_AsksForTheFourSectionsAndNotToInventProblems()
    {
        var context = LensContextBuilder.Build(LensMode.Explicar, "Configuración", EmptyOcr, NoElements);

        foreach (var section in new[] { "**Qué es**", "**Qué está pasando**", "**Cómo resolverlo**", "**Pasos**" })
        {
            Assert.Contains(section, context.Prompt);
        }

        Assert.Contains("no inventes", context.Prompt);
        Assert.Contains("Modo Sakura Lens: Explicar", context.SystemContext);
    }
}
