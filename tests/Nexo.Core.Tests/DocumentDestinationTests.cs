using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D59 — «déjalo en mi escritorio», no una ruta.
///
/// Adler lo dijo tal cual: estar administrando permisos, carpetas y rutas del estilo
/// <c>Usuario/user/profile1/appdata/downloads</c> es muy complicado para quien no sabe. Estas
/// pruebas fijan las dos mitades: qué frases nombran un sitio, y qué nombres de archivo se aceptan
/// antes de tocar el disco.
/// </summary>
public sealed class DocumentDestinationTests
{
    [Theory]
    [InlineData("escritorio", DocumentFolder.Desktop)]
    [InlineData("déjalo en mi escritorio", DocumentFolder.Desktop)]
    [InlineData("Desktop", DocumentFolder.Desktop)]
    [InlineData("en descargas", DocumentFolder.Downloads)]
    [InlineData("mis documentos", DocumentFolder.Documents)]
    public void SpokenPlaces_AreRecognised(string spoken, DocumentFolder expected) =>
        Assert.Equal(expected, DocumentDestination.TryReadFolder(spoken));

    [Fact]
    public void AnUnknownPlace_IsNotGuessed()
    {
        // Null no es lo mismo que prohibido: quien llama decide si preguntar o usar el escritorio.
        // Adivinar una carpeta a partir de una frase que no la nombra es cómo un documento acaba
        // donde nadie lo busca.
        Assert.Null(DocumentDestination.TryReadFolder("en la carpeta de siempre"));
        Assert.Null(DocumentDestination.TryReadFolder(null));
        Assert.Null(DocumentDestination.TryReadFolder("   "));
    }

    [Fact]
    public void AGoodNameGetsItsExtension()
    {
        var target = DocumentDestination.Resolve(DocumentFolder.Desktop, "Primer alunizaje", ".docx");

        Assert.True(target.IsAllowed);
        Assert.Equal("Primer alunizaje.docx", target.FileName);
        Assert.Equal(DocumentFolder.Desktop, target.Folder);
    }

    [Fact]
    public void TheExtensionIsNotDoubled()
    {
        // Quien dicta «informe punto docx» no está pidiendo «informe.docx.docx».
        Assert.Equal(
            "informe.docx",
            DocumentDestination.Resolve(DocumentFolder.Desktop, "informe.docx", ".docx").FileName);
    }

    [Theory]
    [InlineData("carpeta/archivo")]
    [InlineData("..\\arriba")]
    [InlineData("dos:puntos")]
    [InlineData("pregunta?")]
    public void NamesThatWindowsRejects_AreRefusedBeforeTouchingTheDisk(string name)
    {
        // Un error de E/S al final de un trabajo largo es la peor forma de enterarse de que el
        // título llevaba dos puntos. Y la barra es además cómo se sale de la carpeta pedida.
        Assert.False(DocumentDestination.Resolve(DocumentFolder.Desktop, name, ".docx").IsAllowed);
    }

    [Fact]
    public void ReservedNames_AreRefused() =>
        Assert.False(DocumentDestination.Resolve(DocumentFolder.Desktop, "CON", ".docx").IsAllowed);

    [Fact]
    public void ANameStartingWithADot_IsRefused()
    {
        // Quedaría oculto en Windows, y un documento que se pide y no aparece se lee como que no se
        // creó.
        Assert.False(DocumentDestination.Resolve(DocumentFolder.Desktop, ".notas", ".docx").IsAllowed);
    }

    [Fact]
    public void TrailingDotsAndSpaces_AreTrimmed_NotRefused()
    {
        // Windows se los come al crear el archivo, así que el nombre guardado no sería el pedido.
        // Recortarlos da lo que la persona quería; rechazarlo sería quisquilloso.
        Assert.Equal(
            "informe.docx",
            DocumentDestination.Resolve(DocumentFolder.Desktop, "informe. ", ".docx").FileName);
    }

    [Fact]
    public void AnEmptyName_IsRefused()
    {
        Assert.False(DocumentDestination.Resolve(DocumentFolder.Desktop, "", ".docx").IsAllowed);
        Assert.False(DocumentDestination.Resolve(DocumentFolder.Desktop, "   ", ".docx").IsAllowed);
        Assert.False(DocumentDestination.Resolve(DocumentFolder.Desktop, "...", ".docx").IsAllowed);
    }

    [Fact]
    public void AVeryLongName_IsRefused() =>
        Assert.False(DocumentDestination.Resolve(
            DocumentFolder.Desktop, new string('a', 200), ".docx").IsAllowed);

    [Fact]
    public void RefusalsExplainThemselves()
    {
        // El mensaje va a leerlo alguien que no sabe qué es un carácter reservado.
        var refusal = DocumentDestination.Resolve(DocumentFolder.Desktop, "a/b", ".docx");

        Assert.False(refusal.IsAllowed);
        Assert.NotEmpty(refusal.Message);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("¿Qué es la fotosíntesis?")]
    [InlineData("CON")]
    [InlineData("...")]
    [InlineData("")]
    [InlineData(null)]
    public void ResolveFromTitle_NeverRefuses(string? title)
    {
        // La otra puerta: el título lo escribió un modelo y nadie lo va a corregir.
        var target = DocumentDestination.ResolveFromTitle(DocumentFolder.Desktop, title, ".docx");

        Assert.True(target.IsAllowed);
        Assert.EndsWith(".docx", target.FileName);
        Assert.Equal(DocumentFolder.Desktop, target.Folder);
    }

    [Fact]
    public void ResolveFromTitle_CleansTheColon() =>
        Assert.Equal(
            "Introducción el problema.docx",
            DocumentDestination.ResolveFromTitle(
                DocumentFolder.Desktop, "Introducción: el problema", ".docx").FileName);

    [Fact]
    public void PlacesAreDescribedInWords_NotPaths() =>
        Assert.Equal("el escritorio", DocumentDestination.Describe(DocumentFolder.Desktop));
}
