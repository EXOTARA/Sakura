using Nexo.Core.Ai;
using Nexo.Core.Settings;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// Regresión del defecto que dejó un equipo con el modelo descargado y ninguna forma de usarlo.
///
/// Elegir "Ollama" en Ajustes escribía la dirección del Ollama externo (puerto 11434), pero el motor
/// que Sakura descarga e inicia por sí misma vive en el 11435. El supervisor solo arrancaba si la
/// dirección guardada era la administrada, así que nunca arrancaba: Sakura preguntaba a un puerto
/// donde no había nada y el mensaje que veía la persona era "no se pudo conectar con Ollama".
///
/// El arreglo separa los dos casos en proveedores distintos. Estas pruebas fijan que la separación
/// se mantiene y que quien ya lo tenía bien configurado no lo pierde al actualizar.
/// </summary>
public sealed class AiProviderMigrationTests
{
    [Fact]
    public void TheManagedProviderPointsAtTheManagedPort()
    {
        var preset = AiProviderDefaults.Get(AiProviderKind.SakuraLocal);

        Assert.Equal(OllamaRuntimeEndpoints.ManagedBaseUrl, preset.BaseUrl);
        Assert.True(OllamaRuntimeEndpoints.IsManagedBaseUrl(preset.BaseUrl));
    }

    [Fact]
    public void TheExternalProviderPointsAtTheExternalPort()
    {
        var preset = AiProviderDefaults.Get(AiProviderKind.Ollama);

        Assert.Equal(OllamaRuntimeEndpoints.ExternalBaseUrl, preset.BaseUrl);
        Assert.False(OllamaRuntimeEndpoints.IsManagedBaseUrl(preset.BaseUrl));
    }

    [Fact]
    public void ChoosingTheManagedProviderCanNeverEndUpOnTheExternalPort()
    {
        // El corazón del defecto: aquí se simula un archivo de ajustes que quedó apuntando al puerto
        // equivocado. Normalize tiene que corregirlo, no respetarlo.
        var preferences = new ShellPreferences
        {
            SchemaVersion = ShellPreferences.CurrentSchemaVersion,
            AiProvider = AiProviderKind.SakuraLocal,
            AiBaseUrl = "http://127.0.0.1:11434/v1"
        };

        preferences.Normalize();

        Assert.Equal(OllamaRuntimeEndpoints.ManagedBaseUrl, preferences.AiBaseUrl);
    }

    [Fact]
    public void AnOldFileAlreadyUsingTheManagedEngineMovesToTheNewProvider()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 22,
            AiProvider = AiProviderKind.Ollama,
            AiBaseUrl = OllamaRuntimeEndpoints.ManagedBaseUrl,
            AiModel = "qwen3.5:4b"
        };

        preferences.Normalize();

        Assert.Equal(AiProviderKind.SakuraLocal, preferences.AiProvider);
        Assert.Equal(OllamaRuntimeEndpoints.ManagedBaseUrl, preferences.AiBaseUrl);
        Assert.Equal("qwen3.5:4b", preferences.AiModel);
    }

    [Fact]
    public void AnOldFileUsingItsOwnOllamaStaysWhereItWas()
    {
        // Quien de verdad instaló Ollama por su cuenta no debe acabar en el motor administrado: la
        // migración corrige un nombre, no cambia la elección de nadie.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 22,
            AiProvider = AiProviderKind.Ollama,
            AiBaseUrl = OllamaRuntimeEndpoints.ExternalBaseUrl,
            AiModel = "llama3"
        };

        preferences.Normalize();

        Assert.Equal(AiProviderKind.Ollama, preferences.AiProvider);
        Assert.Equal(OllamaRuntimeEndpoints.ExternalBaseUrl, preferences.AiBaseUrl);
        Assert.Equal("llama3", preferences.AiModel);
    }

    [Theory]
    [InlineData(AiProviderKind.SakuraLocal)]
    [InlineData(AiProviderKind.Ollama)]
    public void BothOllamaProvidersRouteThroughTheOllamaProtocol(AiProviderKind provider)
    {
        // Los dos hablan el protocolo nativo de Ollama, no el de OpenAI. Si el enrutador comparara
        // solo contra AiProviderKind.Ollama, el motor administrado acabaría en el cliente equivocado.
        Assert.True(AiProviderDefaults.UsesOllamaProtocol(provider));
    }

    [Fact]
    public void OnlyTheManagedProviderIsAdministeredBySakura()
    {
        Assert.True(AiProviderDefaults.IsManagedBySakura(AiProviderKind.SakuraLocal));

        foreach (var other in Enum.GetValues<AiProviderKind>())
        {
            if (other == AiProviderKind.SakuraLocal)
            {
                continue;
            }

            Assert.False(
                AiProviderDefaults.IsManagedBySakura(other),
                $"Sakura no instala ni inicia {other}, así que no debe declararlo como administrado.");
        }
    }

    [Fact]
    public void EveryEnumValueIsOfferedInTheSelector()
    {
        // El selector se construye desde SelectableOrder. Un proveedor que exista en el enum pero no
        // en esa lista sería inalcanzable desde la interfaz sin que nada fallara.
        var offered = AiProviderDefaults.SelectableOrder.ToHashSet();

        foreach (var provider in Enum.GetValues<AiProviderKind>())
        {
            Assert.True(offered.Contains(provider), $"{provider} no aparece en el selector.");
        }
    }

    [Fact]
    public void EveryProviderThatNeedsAKeySaysWhereToGetOne()
    {
        // Pedir una clave sin decir dónde conseguirla es justo la barrera que este diseño quita.
        foreach (var provider in Enum.GetValues<AiProviderKind>())
        {
            var preset = AiProviderDefaults.Get(provider);
            if (!preset.RequiresApiKey)
            {
                continue;
            }

            Assert.False(
                string.IsNullOrWhiteSpace(preset.ApiKeyUrl),
                $"{provider} pide una clave pero no dice dónde conseguirla.");
            Assert.False(
                string.IsNullOrWhiteSpace(preset.ApiKeyEnvironmentVariable),
                $"{provider} pide una clave pero no nombra su variable de entorno.");
        }
    }

    [Fact]
    public void NoLocalProviderAsksForAKey()
    {
        foreach (var provider in Enum.GetValues<AiProviderKind>())
        {
            var preset = AiProviderDefaults.Get(provider);
            if (preset.Location != AiProviderLocation.Local)
            {
                continue;
            }

            Assert.False(
                preset.RequiresApiKey,
                $"{provider} corre en el equipo; pedir una clave no tendría sentido.");
        }
    }

    [Fact]
    public void EdgeRevealArrivesEnabledAfterUpgrading()
    {
        var preferences = new ShellPreferences { SchemaVersion = 23, EdgeRevealEnabled = false };

        preferences.Normalize();

        Assert.True(preferences.EdgeRevealEnabled);
        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
    }

    [Fact]
    public void AnAlreadyCurrentFileIsNotRewrittenByTheMigrations()
    {
        // Las migraciones arrancan desde la versión que declara el archivo. Un archivo ya al día no
        // debe pasar por ningún escalón: si lo hiciera, cada guardado pisaría lo que la persona
        // acabara de elegir.
        var preferences = new ShellPreferences
        {
            SchemaVersion = ShellPreferences.CurrentSchemaVersion,
            AiProvider = AiProviderKind.Groq,
            AiBaseUrl = "https://api.groq.com/openai/v1",
            AiModel = "llama-3.3-70b-versatile",
            EdgeRevealEnabled = false
        };

        preferences.Normalize();

        Assert.Equal(AiProviderKind.Groq, preferences.AiProvider);
        Assert.False(preferences.EdgeRevealEnabled);
    }

    [Theory]
    [InlineData("http://127.0.0.1:11435/v1", AiProviderKind.SakuraLocal)]
    [InlineData("http://127.0.0.1:11434/v1", AiProviderKind.Ollama)]
    public void OllamaPointingAtSakurasEngine_IsRepairedOnEveryLoad(string baseUrl, AiProviderKind expected)
    {
        // 2026-09-16 — la bienvenida guardaba «Ollama» con el motor de Sakura (11435) y la IA no
        // arrancaba nunca. Un archivo ya al día también se repara.
        var preferences = new ShellPreferences
        {
            SchemaVersion = ShellPreferences.CurrentSchemaVersion,
            AiProvider = AiProviderKind.Ollama,
            AiBaseUrl = baseUrl,
            AiModel = "qwen3.5:4b"
        };

        preferences.Normalize();

        Assert.Equal(expected, preferences.AiProvider);
        Assert.Equal(baseUrl, preferences.AiBaseUrl);
    }
}
