using System.Reflection;
using Nexo.App;
using Nexo.Core.Branding;

namespace Nexo.App.Tests;

/// <summary>
/// Los metadatos de ensamblado son lo que Windows enseña en las propiedades del archivo y lo que
/// SignPath Foundation exige que sea correcto en un binario firmado. Aquí se comprueban porque el
/// renombrado a Sakura (2026-08-20) barrió 279 archivos y dejó <c>Product=Kohana</c> intacto, y
/// porque <c>Nexo.App.csproj</c> fijaba su propia versión encima de <c>Directory.Build.props</c>:
/// el instalador 0.27.0-beta salió con ProductName "Kohana" y FileVersion 0.9.5.0 sin que ninguna
/// de las 2.158 pruebas se enterara.
/// </summary>
public sealed class AssemblyMetadataTests
{
    private static readonly Assembly Application = typeof(App).Assembly;

    [Fact]
    public void Product_metadata_uses_the_current_product_name()
    {
        var product = Application
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product;

        Assert.Equal(ProductIdentity.ProductName, product);
    }

    [Fact]
    public void Company_metadata_is_the_publisher_the_installer_declares()
    {
        var company = Application
            .GetCustomAttribute<AssemblyCompanyAttribute>()?
            .Company;

        Assert.Equal("EXOTARA", company);
    }

    [Fact]
    public void No_assembly_metadata_still_carries_a_previous_product_name()
    {
        var texts = new[]
        {
            Application.GetCustomAttribute<AssemblyProductAttribute>()?.Product,
            Application.GetCustomAttribute<AssemblyTitleAttribute>()?.Title,
            Application.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description,
        };

        foreach (var text in texts)
        {
            Assert.False(
                text?.Contains(ProductIdentity.PreviousProductName, StringComparison.OrdinalIgnoreCase),
                $"Un metadato de ensamblado sigue diciendo '{ProductIdentity.PreviousProductName}': {text}");
        }
    }

    /// <summary>
    /// La versión de archivo y la informativa vienen de propiedades distintas de MSBuild, así que
    /// pueden divergir en silencio — y divergieron. Esta prueba las ata.
    /// </summary>
    [Fact]
    public void File_version_matches_the_version_the_application_reports()
    {
        var fileVersion = Application
            .GetCustomAttribute<AssemblyFileVersionAttribute>()?
            .Version;

        Assert.NotNull(fileVersion);
        Assert.Equal($"{ReleaseMetadata.CurrentVersion.Split('-')[0]}.0", fileVersion);
    }
}
