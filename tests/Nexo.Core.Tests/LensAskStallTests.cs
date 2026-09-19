using System.IO;
using Nexo.Core.Ambient;

namespace Nexo.Core.Tests;

/// <summary>
/// El diálogo de «Preguntar» es modal y el reloj de atasco sigue corriendo mientras espera. Si
/// ExecuteLensAsync ya hubiera abierto la solicitud ambiental, una persona lenta haría caducar la
/// solicitud y la respuesta de Lens nunca llegaría a la píldora (BeginStreaming rechazado).
///
/// La premisa cambió al arreglarlo: el manager sigue rechazando el streaming tras caducar (primera
/// prueba, documenta por qué importa), y lo que se fija ahora es que el permiso se pide ANTES de
/// abrir la solicitud (segunda prueba).
/// </summary>
public sealed class LensAskStallTests
{
    [Fact]
    public void AfterStallLimit_StreamingCanNoLongerStart_SoTheRequestMustNotBeOpenWhileAsking()
    {
        var manager = new AmbientRequestManager(new MemoryAmbientRequestHistoryStore());
        manager.Load();
        var t0 = DateTimeOffset.Parse("2026-09-18T10:00:00+00:00");
        manager.Begin("Sakura Lens", context: null, t0);
        manager.BeginThinking(t0);

        Assert.True(manager.FailIfStalled(t0 + AmbientRequestManager.ThinkingStallLimit + TimeSpan.FromSeconds(5)).Success);
        Assert.False(manager.BeginStreaming(t0 + TimeSpan.FromMinutes(4)).Success);
    }

    [Fact]
    public void ExecuteLens_AsksForPermission_BeforeOpeningTheAmbientRequest()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Nexo.App")))
        {
            dir = dir.Parent;
        }

        var source = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Nexo.App", "MainWindow.xaml.cs"));
        var method = source.IndexOf("Task<CommandExecutionResult> ExecuteLensAsync(", StringComparison.Ordinal);
        Assert.True(method >= 0);

        var ask = source.IndexOf("TryGetLensPermission(", method, StringComparison.Ordinal);
        var begin = source.IndexOf("_ambientRequestManager.Begin(", method, StringComparison.Ordinal);

        Assert.True(ask >= 0 && begin >= 0);
        Assert.True(ask < begin, "El permiso debe pedirse antes de abrir la solicitud ambiental.");
    }

    private sealed class MemoryAmbientRequestHistoryStore : IAmbientRequestHistoryStore
    {
        private AmbientRequestState _state = new();

        public AmbientRequestState Load() => _state.Copy();

        public void Save(AmbientRequestState state) => _state = state.Copy();
    }
}
