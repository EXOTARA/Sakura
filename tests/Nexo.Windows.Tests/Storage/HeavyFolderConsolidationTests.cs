using System.IO;
using Nexo.Core.Diagnostics;
using Nexo.Windows.Storage;
using Xunit;

namespace Nexo.Windows.Tests.Storage;

/// <summary>
/// Diseño D70 — los modelos de voz viajan con el cambio de nombre.
///
/// La migración normal los excluye por tamaño, y esa exclusión salió cara: en el equipo de Adler
/// los tres giga y medio de modelos seguían en la carpeta de la primera etapa, leídos en vivo
/// desde ahí. Borrar esa carpeta —que es lo que cualquiera haría al ver cinco giga con un nombre
/// que ya no existe— habría dejado la aplicación sin voz sin que nada avisara.
///
/// Se mueven en vez de copiarse: dentro del mismo volumen, mover es renombrar.
/// </summary>
[Collection(WindowsDataPathsCollection.Name)]
public sealed class HeavyFolderConsolidationTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sakura-consolidacion-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        NexoDataPaths.UseExplicitDataRoot(null);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void WithAValidationProfileActive_NothingIsTouched()
    {
        // Un perfil aislado existe justamente para no tocar los archivos reales de nadie.
        Directory.CreateDirectory(_root);
        NexoDataPaths.UseExplicitDataRoot(_root);

        var moved = LegacyDataMigrator.ConsolidateHeavyFolders();

        Assert.Empty(moved);
    }
}
