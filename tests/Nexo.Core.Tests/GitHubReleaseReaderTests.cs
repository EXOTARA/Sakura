using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D64 — leer una publicación de GitHub sin fiarse de ella.
///
/// Se usa GitHub porque ya es donde se publica: `publish.ps1` genera el zip y su `.sha256` al lado,
/// y una publicación los lleva como adjuntos. Lo que se prueba aquí es que una publicación
/// incompleta, un borrador o una respuesta que no es JSON no acaben en una instalación.
/// </summary>
public sealed class GitHubReleaseReaderTests
{
    private const string Complete = """
    {
      "tag_name": "v0.9.6-beta",
      "draft": false,
      "body": "Arreglos varios",
      "assets": [
        {
          "name": "Sakura-0.9.6-beta-win-x64-portable.zip",
          "size": 99700000,
          "browser_download_url": "https://github.com/EXOTARA/Nexo/releases/download/v0.9.6/Sakura.zip"
        },
        {
          "name": "Sakura-0.9.6-beta-win-x64-portable.zip.sha256",
          "size": 105,
          "browser_download_url": "https://github.com/EXOTARA/Nexo/releases/download/v0.9.6/Sakura.zip.sha256"
        }
      ]
    }
    """;

    [Fact]
    public void ACompleteReleaseIsRead()
    {
        var release = GitHubReleaseReader.Read(Complete);

        Assert.True(release.IsUsable, release.Problem);
        Assert.Equal("v0.9.6-beta", release.Version);
        Assert.EndsWith("Sakura.zip", release.PackageUrl, StringComparison.Ordinal);
        Assert.EndsWith("Sakura.zip.sha256", release.ChecksumUrl, StringComparison.Ordinal);
        Assert.Equal(99700000, release.PackageSize);
    }

    [Fact]
    public void TheChecksumIsNotMistakenForThePackage()
    {
        // «.zip.sha256» también termina en «.zip» si se mira mal el orden, y entonces Sakura
        // descargaría el archivo de la huella creyendo que es la aplicación.
        var release = GitHubReleaseReader.Read(Complete);

        Assert.DoesNotContain(".sha256", release.PackageUrl, StringComparison.Ordinal);
    }

    // Los cuatro adjuntos reales de una publicación (medido contra la API con v0.30.34-beta).
    private const string Installer = """{ "name": "Sakura-0.30.34-beta-Setup.exe", "size": 67695548, "browser_download_url": "https://x.invalid/Setup.exe" }""";
    private const string InstallerHash = """{ "name": "Sakura-0.30.34-beta-Setup.exe.sha256", "size": 100, "browser_download_url": "https://x.invalid/Setup.exe.sha256" }""";
    private const string Portable = """{ "name": "Sakura-0.30.34-beta-win-x64-portable.zip", "size": 100788001, "browser_download_url": "https://x.invalid/portable.zip" }""";
    private const string PortableHash = """{ "name": "Sakura-0.30.34-beta-win-x64-portable.zip.sha256", "size": 100, "browser_download_url": "https://x.invalid/portable.zip.sha256" }""";

    private static string ReleaseWith(params string[] assets) =>
        "{ \"tag_name\": \"v0.30.34-beta\", \"assets\": [" + string.Join(",", assets) + "] }";

    [Theory]
    [InlineData(0)] // el orden que devuelve GitHub hoy
    [InlineData(1)] // instalador al final
    [InlineData(2)] // huellas antes que archivos
    public void ThePackageIsPairedWithItsOwnChecksum_InAnyOrder(int order)
    {
        // Antes se tomaba «el último .zip» y «el último .sha256» por separado: acertaba solo porque
        // «Setup» ordena antes que «win». Con otro orden se bajaban cien megas y se rechazaban.
        string[] assets = order switch
        {
            0 => [Installer, InstallerHash, Portable, PortableHash],
            1 => [Portable, PortableHash, Installer, InstallerHash],
            _ => [PortableHash, InstallerHash, Portable, Installer],
        };

        var release = GitHubReleaseReader.Read(ReleaseWith(assets));

        Assert.True(release.IsUsable, release.Problem);
        Assert.Equal("https://x.invalid/portable.zip", release.PackageUrl);
        Assert.Equal("https://x.invalid/portable.zip.sha256", release.ChecksumUrl);
        Assert.Equal(100788001, release.PackageSize);
    }

    [Fact]
    public void ASmallerSecondZipDoesNotWin()
    {
        // Un adjunto extra (símbolos, por ejemplo) no puede acabar instalado en lugar de Sakura.
        const string symbols = """{ "name": "Sakura-0.30.34-beta-symbols.zip", "size": 5000000, "browser_download_url": "https://x.invalid/symbols.zip" }""";

        var release = GitHubReleaseReader.Read(ReleaseWith(Installer, InstallerHash, Portable, PortableHash, symbols));

        Assert.Equal("https://x.invalid/portable.zip", release.PackageUrl);
        Assert.Equal("https://x.invalid/portable.zip.sha256", release.ChecksumUrl);
    }

    [Fact]
    public void APackageWhoseOwnChecksumIsMissingIsRefused_EvenIfAnotherFileHasOne()
    {
        // La huella del instalador no vale para el zip: se rechaza en vez de comparar con la de otro.
        var release = GitHubReleaseReader.Read(ReleaseWith(Installer, InstallerHash, Portable));

        Assert.False(release.IsUsable);
        Assert.Equal("La publicación no trae la huella para comprobarla.", release.Problem);
    }

    [Fact]
    public void ADraftIsNotOffered()
    {
        // Un borrador ni siquiera se ha terminado de subir.
        var json = Complete.Replace("\"draft\": false", "\"draft\": true", StringComparison.Ordinal);

        Assert.False(GitHubReleaseReader.Read(json).IsUsable);
    }

    [Fact]
    public void AReleaseWithoutAChecksumIsRefused()
    {
        // Sin huella no hay forma de saber si lo descargado es lo publicado. Una publicación a la
        // que se le olvidó subir el .sha256 no es una de la que fiarse.
        var json = """
        {
          "tag_name": "v1.0.0",
          "assets": [
            { "name": "Sakura.zip", "size": 10, "browser_download_url": "https://x.invalid/Sakura.zip" }
          ]
        }
        """;

        var release = GitHubReleaseReader.Read(json);

        Assert.False(release.IsUsable);
        Assert.Contains("huella", release.Problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AReleaseWithoutAPackageIsRefused()
    {
        var json = """
        { "tag_name": "v1.0.0", "assets": [] }
        """;

        Assert.False(GitHubReleaseReader.Read(json).IsUsable);
    }

    [Fact]
    public void AReleaseWithoutAVersionIsRefused() =>
        Assert.False(GitHubReleaseReader.Read("""{ "assets": [] }""").IsUsable);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<html>429 Too Many Requests</html>")]
    [InlineData("no soy json")]
    [InlineData("[1,2,3]")]
    public void SomethingThatIsNotAReleaseIsRefused(string? body)
    {
        // Una página de error, una redirección o un límite de peticiones llegan así. Es un martes
        // normal, no una excepción.
        Assert.False(GitHubReleaseReader.Read(body).IsUsable);
    }

    // ---------- la huella ----------

    [Fact]
    public void TheChecksumFileFormatIsUnderstood()
    {
        // Es lo que escriben sha256sum y el script de publicación: «huella  nombre».
        const string content =
            "c4b2b97223b013944f30ad7f0ee529007a71251f64d920569a133e897bba2985  Sakura.zip";

        Assert.Equal(
            "c4b2b97223b013944f30ad7f0ee529007a71251f64d920569a133e897bba2985",
            GitHubReleaseReader.ReadChecksum(content));
    }

    [Fact]
    public void AChecksumOnItsOwnIsUnderstood() =>
        Assert.Equal(
            "c4b2b97223b013944f30ad7f0ee529007a71251f64d920569a133e897bba2985",
            GitHubReleaseReader.ReadChecksum(
                "  c4b2b97223b013944f30ad7f0ee529007a71251f64d920569a133e897bba2985\n"));

    [Fact]
    public void OnlyTheFirstLineCounts()
    {
        // Un archivo con varias huellas es de varios archivos; la primera es la del paquete que se
        // publicó junto a él.
        Assert.Equal("aaa", GitHubReleaseReader.ReadChecksum("aaa  uno.zip\nbbb  dos.zip"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyChecksumFileYieldsNothing(string? content) =>
        Assert.Empty(GitHubReleaseReader.ReadChecksum(content));

    [Fact]
    public void TheApiAddressAsksForTheListNotForLatest()
    {
        // «latest» en GitHub significa la última que NO es preliminar, y Sakura publica todas sus
        // versiones como preliminares: ese camino devolvía 404. Lo encontró la primera consulta de
        // verdad, no estas pruebas — ninguna podía, porque todas traen su JSON escrito a mano.
        var url = GitHubReleaseReader.ReleasesUrl("EXOTARA", "Nexo").ToString();

        Assert.StartsWith("https://api.github.com/repos/EXOTARA/Nexo/releases", url, StringComparison.Ordinal);
        Assert.DoesNotContain("/releases/latest", url, StringComparison.Ordinal);
    }

    [Fact]
    public void FromAListTheHighestVersionWins()
    {
        // Se compara por número y no por orden de llegada: una corrección publicada hoy sobre una
        // rama vieja es más reciente en el tiempo y más antigua en número.
        var json = "[" +
            Complete.Replace("v0.9.6-beta", "v0.9.4-beta", StringComparison.Ordinal) + "," +
            Complete + "," +
            Complete.Replace("v0.9.6-beta", "v0.9.5-beta", StringComparison.Ordinal) + "]";

        Assert.Equal("v0.9.6-beta", GitHubReleaseReader.Read(json).Version);
    }

    [Fact]
    public void AListWhereNothingIsUsableIsRefused()
    {
        var json = """[{ "tag_name": "v1.0.0", "assets": [] }]""";

        Assert.False(GitHubReleaseReader.Read(json).IsUsable);
    }
}
