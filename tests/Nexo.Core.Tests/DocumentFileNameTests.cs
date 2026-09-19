using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

/// <summary>
/// Un título que escribió un modelo tiene que acabar siempre en un nombre válido: rechazarlo tiraba
/// el documento entero, ya hecho, por un nombre que nadie eligió.
/// </summary>
public sealed class DocumentFileNameTests
{
    private const string Fallback = "Respuesta de Sakura";

    private static string Name(string? title, string extension = ".docx") =>
        DocumentFileName.Sanitize(title, extension, Fallback);

    [Theory]
    [InlineData("Introducción: el problema", "Introducción el problema.docx")]
    [InlineData("¿Qué es la fotosíntesis?", "¿Qué es la fotosíntesis.docx")]
    [InlineData("Ventajas/Desventajas", "Ventajas Desventajas.docx")]
    [InlineData("Resumen \"ejecutivo\"", "Resumen ejecutivo.docx")]
    [InlineData("5 * 3 = 15", "5 3 = 15.docx")]
    [InlineData("Informe <borrador>", "Informe borrador.docx")]
    [InlineData("Ventajas y desventajas", "Ventajas y desventajas.docx")]
    [InlineData("Ecuación E = mc²", "Ecuación E = mc².docx")]
    public void TitlesThatWindowsRejectedBefore_NowProduceAValidName(string title, string expected)
    {
        var name = Name(title);

        Assert.Equal(expected, name);
        Assert.True(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }

    [Fact]
    public void ALongTitle_IsCutAtAWholeWord()
    {
        var title = "Trabajo de Química Orgánica: síntesis de compuestos aromáticos a partir de " +
                    "derivados del benceno y su caracterización espectroscópica";

        var name = Name(title);
        var stem = name[..^".docx".Length];

        Assert.True(stem.Length <= 96);
        Assert.False(stem.EndsWith(' '));

        // Termina en una palabra que existe entera en el título, no a mitad de una.
        var lastWord = stem[(stem.LastIndexOf(' ') + 1)..];
        Assert.Contains(lastWord, title.Replace(':', ' ').Split(' '));
    }

    [Fact]
    public void AnUnbrokenVeryLongWord_IsStillCut()
    {
        var name = Name(new string('a', 300));

        Assert.Equal(96 + ".docx".Length, name.Length);
    }

    [Theory]
    [InlineData("CON:")]
    [InlineData("NUL")]
    [InlineData("com1")]
    [InlineData("LPT9.txt")]
    public void ReservedNames_NeverEndUpAsABareDevice(string title)
    {
        // Medido: escribir en <carpeta>\NUL no da error y no deja archivo.
        var name = Name(title);

        Assert.Contains("(documento)", name);
        Assert.False(name.StartsWith("CON.", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("???")]
    [InlineData("...")]
    [InlineData(":::")]
    public void ATitleWithNothingUsable_FallsBack(string? title) =>
        Assert.Equal(Fallback + ".docx", Name(title));

    [Theory]
    [InlineData("../../../etc passwd")]
    [InlineData(@"..\..\Windows\System32")]
    [InlineData("a/../b")]
    public void NoPathSeparatorSurvives(string title)
    {
        var name = Name(title);

        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
        Assert.False(name.StartsWith('.'));
    }

    [Theory]
    [InlineData("informe.docx")]
    [InlineData("Informe.DOCX")]
    public void TheExtensionIsNotDuplicated(string title) =>
        Assert.Equal(1, Count(Name(title), ".docx"));

    [Fact]
    public void NeverReturnsAnEmptyStem()
    {
        // Incluso con un fallback inútil.
        var name = DocumentFileName.Sanitize("???", ".xlsx", "***");

        Assert.NotEqual(".xlsx", name);
        Assert.EndsWith(".xlsx", name);
    }

    private static int Count(string text, string part, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var count = 0;
        for (var index = text.IndexOf(part, comparison); index >= 0; index = text.IndexOf(part, index + part.Length, comparison))
        {
            count++;
        }

        return count;
    }
}
