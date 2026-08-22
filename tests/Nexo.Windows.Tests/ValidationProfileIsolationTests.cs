using Nexo.Core.Ambient;
using Nexo.Core.Diagnostics;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;
using Nexo.Windows.Ambient;
using Nexo.Windows.Automation;
using Nexo.Windows.Focus;
using Nexo.Windows.Settings;
using Nexo.Windows.Storage;
using Nexo.Windows.Tasks;

namespace Nexo.Windows.Tests;

/// <summary>
/// Diseño D3.2 — confirma, con los stores reales (no fakes), que un override de
/// <see cref="NexoDataPaths"/> aísla de verdad la persistencia: las clases se construyen sin ruta
/// explícita (igual que en producción — <c>App.xaml.cs</c> tampoco les pasa una) para ejercer
/// exactamente el mismo camino que usaría un perfil de validación real.
///
/// **Comparte colección con <see cref="Storage.HeavyFolderConsolidationTests"/>.** Cuando se
/// escribió esta clase era la única del ensamblado que tocaba el override, y así lo decía este
/// comentario; la consolidación de carpetas pesadas (2026-08-20) añadió otra que también lo muta, y
/// xUnit paraleliza por colección. El resultado fue un fallo que aparecía una de cada varias
/// ejecuciones —<c>OverrideActive_DirectoryIsCreatedLazilyBySaveNotEagerly</c> encontraba su raíz
/// sin crear porque la otra clase había cambiado el override a mitad de prueba— y que en CI habría
/// parecido cualquier cosa menos lo que era. El agrupamiento explícito lo cierra.
/// </summary>
[Collection(WindowsDataPathsCollection.Name)]
public sealed class ValidationProfileIsolationTests : IDisposable
{
    private readonly List<string> _createdRoots = [];

    public void Dispose()
    {
        NexoDataPaths.UseExplicitDataRoot(null);
        foreach (var root in _createdRoots)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // La limpieza de una carpeta temporal no debe ocultar el resultado de la prueba.
            }
        }
    }

    private string UseNewIsolatedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "kohana-profile-tests-" + Guid.NewGuid().ToString("N"));
        _createdRoots.Add(root);
        NexoDataPaths.UseExplicitDataRoot(root);
        return root;
    }

    [Fact]
    public void JsonTaskStore_WithNoExplicitPath_WritesUnderTheOverriddenRoot()
    {
        var root = UseNewIsolatedRoot();
        var manager = new TaskManager(new JsonTaskStore());
        manager.Load();

        manager.Create("Tarea de perfil aislado");

        Assert.True(File.Exists(Path.Combine(root, "tasks.json")));
        Assert.Contains("Tarea de perfil aislado", File.ReadAllText(Path.Combine(root, "tasks.json")));
    }

    [Fact]
    public void JsonFocusStore_WithNoExplicitPath_WritesUnderTheOverriddenRoot()
    {
        var root = UseNewIsolatedRoot();
        var manager = new FocusManager(new JsonFocusStore());
        manager.Load();

        manager.Start(TimeSpan.FromMinutes(5), "Enfoque de prueba", FocusSessionKind.Focus, DateTimeOffset.Now);

        Assert.True(File.Exists(Path.Combine(root, "focus.json")));
    }

    [Fact]
    public void JsonRoutineStore_WithNoExplicitPath_WritesUnderTheOverriddenRoot()
    {
        var root = UseNewIsolatedRoot();
        var store = new JsonRoutineStore();

        store.Save(new Nexo.Core.Automation.RoutineState());

        Assert.True(File.Exists(Path.Combine(root, "routines.json")));
    }

    [Fact]
    public void JsonSettingsStore_WithNoExplicitPath_WritesUnderTheOverriddenRoot()
    {
        var root = UseNewIsolatedRoot();
        var store = new JsonSettingsStore();
        var preferences = store.Load();

        store.Save(preferences);

        Assert.True(File.Exists(Path.Combine(root, "settings.json")));
    }

    [Fact]
    public void JsonAmbientRequestHistoryStore_WithNoExplicitPath_WritesUnderTheOverriddenRoot()
    {
        var root = UseNewIsolatedRoot();
        var manager = new AmbientRequestManager(new JsonAmbientRequestHistoryStore());
        manager.Load();

        manager.Begin("solicitud de perfil aislado", context: null, DateTimeOffset.Now);

        Assert.True(File.Exists(Path.Combine(root, "ambient-requests.json")));
    }

    [Fact]
    public void AllStores_WithNoExplicitPath_ShareTheSameOverriddenRoot()
    {
        var root = UseNewIsolatedRoot();

        var taskManager = new TaskManager(new JsonTaskStore());
        taskManager.Load();
        taskManager.Create("Tarea");

        var focusManager = new FocusManager(new JsonFocusStore());
        focusManager.Load();
        focusManager.Start(TimeSpan.FromMinutes(5), "Enfoque", FocusSessionKind.Focus, DateTimeOffset.Now);

        new JsonRoutineStore().Save(new Nexo.Core.Automation.RoutineState());

        var settingsStore = new JsonSettingsStore();
        settingsStore.Save(settingsStore.Load());

        var ambientManager = new AmbientRequestManager(new JsonAmbientRequestHistoryStore());
        ambientManager.Load();
        ambientManager.Begin("solicitud", context: null, DateTimeOffset.Now);

        // Diseño D11/D13 — el Audit Log es un store más y tiene que quedar dentro del perfil
        // aislado: un registro que se escapara a la carpeta real mezclaría lo probado con lo que la
        // persona hizo de verdad en su equipo.
        new Nexo.Windows.Audit.JsonSakuraAuditLog().Append(
            new Nexo.Core.Audit.AuditEntry
            {
                At = DateTimeOffset.Now,
                Capability = Nexo.Core.Audit.AuditCapability.Optimizacion,
                Action = "Aplicado (Jugar)",
                Detail = "prueba"
            });

        Assert.True(File.Exists(Path.Combine(root, "tasks.json")));
        Assert.True(File.Exists(Path.Combine(root, "focus.json")));
        Assert.True(File.Exists(Path.Combine(root, "routines.json")));
        Assert.True(File.Exists(Path.Combine(root, "settings.json")));
        Assert.True(File.Exists(Path.Combine(root, "ambient-requests.json")));
        Assert.True(File.Exists(Path.Combine(root, "audit.json")));
    }

    [Fact]
    public void TwoProfiles_TasksSavedInOneNeverAppearInTheOther()
    {
        UseNewIsolatedRoot();
        var firstManager = new TaskManager(new JsonTaskStore());
        firstManager.Load();
        firstManager.Create("Tarea del perfil A");

        var secondRoot = UseNewIsolatedRoot();
        var secondManager = new TaskManager(new JsonTaskStore());
        secondManager.Load();

        // El segundo perfil es una carpeta nueva: no debe heredar nada del primero.
        Assert.Empty(secondManager.GetAll());
        Assert.False(File.Exists(Path.Combine(secondRoot, "tasks.json")));
    }

    [Fact]
    public void OverrideActive_DirectoryIsCreatedLazilyBySaveNotEagerly()
    {
        var root = UseNewIsolatedRoot();
        Assert.False(Directory.Exists(root));

        var manager = new TaskManager(new JsonTaskStore());
        manager.Load();
        manager.Create("Fuerza la creación perezosa del directorio");

        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void OverrideActive_LegacyMigratorGuardSkipsMigration_RealNexoFolderIsNeverRead()
    {
        UseNewIsolatedRoot();

        // Con el override activo, NexoDataPaths.LegacyRootDirectory colapsa a la misma raíz que
        // RootDirectory (origen == destino), así que el guard existente de LegacyDataMigrator
        // evita cualquier lectura del %LocalAppData%\Nexo real — sin necesidad de tocar
        // LegacyDataMigrator ni App.xaml.cs para lograrlo.
        var result = LegacyDataMigrator.Migrate(
            NexoDataPaths.LegacyRootDirectory,
            NexoDataPaths.RootDirectory);

        Assert.False(result.WasNeeded);
    }

    [Fact]
    public void NoOverride_LegacyMigratorStillSeesDistinctRealRoots()
    {
        // Confirmación de contraste: sin ningún override activo (comportamiento de producción),
        // el guard NO colapsa — origen y destino reales siguen siendo carpetas distintas.
        Assert.NotEqual(NexoDataPaths.LegacyRootDirectory, NexoDataPaths.RootDirectory);
    }
}
