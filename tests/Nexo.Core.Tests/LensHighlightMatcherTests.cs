using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class LensHighlightMatcherTests
{
    private static readonly IReadOnlyList<UiAutomationElement> NoElements = [];

    [Fact]
    public void FindMatches_WithBlankAnswer_ReturnsNoRegions()
    {
        var ocr = OcrResult.Success("Guardar", [new OcrTextLine("Guardar", 10, 20, 50, 15)]);

        var regions = LensHighlightMatcher.FindMatches("   ", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_WithFailedOcr_ReturnsNoRegions()
    {
        var regions = LensHighlightMatcher.FindMatches(
            "Pulsa Guardar", OcrResult.Failed("motivo"), NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_OcrLineMentionedInAnswer_IsHighlighted_WithWindowOffsetApplied()
    {
        var ocr = OcrResult.Success(
            "Guardar cambios", [new OcrTextLine("Guardar cambios", 10, 20, 80, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "Para terminar, pulsa el botón Guardar cambios.", ocr, NoElements, windowLeft: 100, windowTop: 200);

        var region = Assert.Single(regions);
        Assert.Equal(110, region.Left);
        Assert.Equal(220, region.Top);
        Assert.Equal(80, region.Width);
        Assert.Equal(15, region.Height);
    }

    [Fact]
    public void FindMatches_OcrLineNotMentioned_IsNotHighlighted()
    {
        var ocr = OcrResult.Success(
            "Cancelar", [new OcrTextLine("Cancelar", 0, 0, 40, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "Pulsa Guardar cambios para continuar.", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_IsCaseAndAccentInsensitive()
    {
        var ocr = OcrResult.Success(
            "Configuración", [new OcrTextLine("Configuración", 0, 0, 60, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "Ve a CONFIGURACION para cambiar esto.", ocr, NoElements, 0, 0);

        Assert.Single(regions);
    }

    [Fact]
    public void FindMatches_ShortOcrLine_IsNeverHighlightedEvenIfMentioned()
    {
        var ocr = OcrResult.Success("OK", [new OcrTextLine("OK", 0, 0, 20, 15)]);

        var regions = LensHighlightMatcher.FindMatches("Pulsa OK para confirmar.", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_UiElementMentioned_IsHighlighted_WithoutWindowOffset()
    {
        var ocr = OcrResult.Success(string.Empty, []);
        var elements = new[] { new UiAutomationElement("Enviar formulario", "ControlType.Button", 500, 600, 90, 30) };

        var regions = LensHighlightMatcher.FindMatches(
            "Da clic en Enviar formulario.", ocr, elements, windowLeft: 100, windowTop: 200);

        var region = Assert.Single(regions);
        // Las coordenadas de UI Automation ya son absolutas: no se les suma windowLeft/windowTop.
        Assert.Equal(500, region.Left);
        Assert.Equal(600, region.Top);
    }

    [Fact]
    public void FindMatches_RedactedPlaceholder_NeverMatchesEvenIfPresentInAnswer()
    {
        var ocr = OcrResult.Success(
            "[REDACTADO]", [new OcrTextLine("[REDACTADO]", 0, 0, 60, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "El campo dice [REDACTADO] y no debería mostrarse.", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_DuplicateRegions_AreDeduplicated()
    {
        var ocr = OcrResult.Success(
            "Guardar cambios",
            [
                new OcrTextLine("Guardar cambios", 10, 20, 80, 15),
                new OcrTextLine("Guardar cambios", 10, 20, 80, 15)
            ]);

        var regions = LensHighlightMatcher.FindMatches(
            "Pulsa Guardar cambios.", ocr, NoElements, 0, 0);

        Assert.Single(regions);
    }

    [Fact]
    public void FindMatches_ElementWithNullName_IsNeverHighlighted()
    {
        var ocr = OcrResult.Success(string.Empty, []);
        var elements = new[] { new UiAutomationElement(null, "ControlType.Pane", 0, 0, 50, 50) };

        var regions = LensHighlightMatcher.FindMatches("Cualquier respuesta.", ocr, elements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void Explaining_OnlyMarksControlsNamedInTheSteps_AtMostThree()
    {
        const string answer = """
            **Qué es**
            Ventana de Google con el logo y el botón Buscar.
            **Cómo resolverlo**
            Abre **Herramientas** y elige **Configuración**.
            **Pasos**
            1. Pulsa **Herramientas**.
            2. Pulsa «Configuración».
            3. Activa "Modo oscuro" y pulsa **Guardar**. No toques el botón Cerrar.
            """;
        UiAutomationElement[] elements =
        [
            new("Google", "ControlType.Image", 10, 10, 100, 40),
            new("Buscar", "ControlType.Button", 10, 60, 60, 20),
            new("Herramientas", "ControlType.MenuItem", 100, 60, 90, 20),
            new("Configuración", "ControlType.Hyperlink", 200, 60, 90, 20),
            new("Modo oscuro", "ControlType.CheckBox", 300, 60, 90, 20),
            new("Guardar", "ControlType.Button", 400, 60, 60, 20),
            new("Abre Herramientas y elige Configuración.", "ControlType.Text", 0, 100, 300, 20)
        ];

        var regions = LensHighlightMatcher.FindActionTargets(answer, elements);

        Assert.Equal(3, regions.Count);
        Assert.Equal(100, regions[0].Left);
        Assert.DoesNotContain(regions, region => region.Left == 10);
        Assert.DoesNotContain(regions, region => region.Left == 0);
    }

    [Fact]
    public void Explaining_WithNothingToDo_MarksNothing()
    {
        const string answer = """
            **Qué es**
            Google.
            **Qué está pasando**
            Nada raro.
            """;
        Assert.Empty(LensHighlightMatcher.FindActionTargets(answer, [new("Google", "ControlType.Button", 1, 1, 10, 10)]));
    }

    [Fact]
    public void Explaining_IgnoresUnquotedMentionsAndCaptionButtons()
    {
        // Lo que pasó en la primera prueba: «configuración del sistema» marcaba el engranaje del Bloc de notas.
        const string answer = """
            **Cómo resolverlo**
            Abre Windows Update en la configuración del sistema y pulsa **Cerrar** al terminar.
            **Pasos**
            1. Ve a **Resolver problemas**.
            """;
        UiAutomationElement[] elements =
        [
            new("Configuración", "ControlType.Button", 1400, 40, 30, 30),
            new("Cerrar", "ControlType.Button", 1420, 0, 40, 30),
            new("Minimizar", "ControlType.Button", 1300, 0, 40, 30)
        ];

        Assert.Empty(LensHighlightMatcher.FindActionTargets(answer, elements));
    }
}
