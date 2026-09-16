using Nexo.Core.Ai;
using Nexo.Core.Settings;
using Nexo.Windows.Settings;

namespace Nexo.Windows.Tests;

/// <summary>
/// El primer arranque real: no hay archivo de configuración, el usuario elige algo en el asistente
/// de bienvenida y se guarda. Nadie había probado esa secuencia completa contra el disco — solo las
/// migraciones por separado — y por ese hueco se colaba que <c>Normalize</c> tratara una
/// instalación nueva como un archivo prehistórico y borrara lo recién elegido.
/// </summary>
public sealed class JsonSettingsStoreFirstRunTests : IDisposable
{
    private readonly string _directory;

    public JsonSettingsStoreFirstRunTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(), "kohana-settings-firstrun-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Limpieza best-effort.
        }
    }

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void FirstRun_ChoicesMadeInOnboarding_SurviveBeingSaved()
    {
        var store = new JsonSettingsStore(SettingsPath);

        var preferences = store.Load();
        preferences.AiProvider = AiProviderKind.Ollama;
        preferences.AiBaseUrl = "http://127.0.0.1:11434/v1";
        preferences.AiModel = "qwen3.5:9b";
        preferences.HasCompletedOnboarding = true;
        store.Save(preferences);

        var reloaded = store.Load();

        Assert.Equal(AiProviderKind.Ollama, reloaded.AiProvider);
        Assert.Equal("qwen3.5:9b", reloaded.AiModel);
        Assert.True(reloaded.HasCompletedOnboarding);
    }

    [Fact]
    public void FirstRun_StartsAtTheCurrentSchema_SoNoMigrationRewritesIt()
    {
        var store = new JsonSettingsStore(SettingsPath);

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, store.Load().SchemaVersion);
    }

    [Fact]
    public void FirstRun_ShowsInicioAndHoy_AndLeavesPeekAndEdgeControlsOff()
    {
        var preferences = new JsonSettingsStore(SettingsPath).Load();

        Assert.True(preferences.ShowHomeModule);
        Assert.True(preferences.ShowTasksModule);
        Assert.False(preferences.PeekEnabled);
        Assert.False(preferences.EdgeQuickControlsEnabled);
    }

    [Fact]
    public void AnUpgrade_KeepsTheEdgeControls()
    {
        File.WriteAllText(SettingsPath, """{"SchemaVersion": 31, "HasCompletedOnboarding": true}""");

        Assert.True(new JsonSettingsStore(SettingsPath).Load().EdgeQuickControlsEnabled);
    }

    [Fact]
    public void AnOldFile_StillGetsItsMigrationsApplied()
    {
        File.WriteAllText(SettingsPath, """{"SchemaVersion": 1}""");

        var loaded = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.False(loaded.HasCompletedOnboarding);
    }
}
