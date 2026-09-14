using Nexo.App.Updates;
using Nexo.Core.Distribution;
using Nexo.Core.Updates;
using Nexo.Windows.Updates;

namespace Nexo.App.Tests;

/// <summary>
/// L2, primera extracción — el flujo de actualizaciones que vivía en <c>MainWindow.xaml.cs</c>.
///
/// Estas pruebas se escribieron leyendo el código de la ventana ANTES de moverlo y fijan lo que hacía,
/// asimetrías incluidas. No hay forma de construir <c>MainWindow</c> en una prueba, así que la
/// caracterización se hizo contra el código leído y no contra la ventana en marcha: si alguna
/// asimetría resulta ser un fallo, se cambia a propósito y con su prueba, no de paso.
/// </summary>
public sealed class UpdateFlowCoordinatorTests
{
    private static readonly SakuraVersion Current = new(0, 29, 3, "beta");
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly UpdateManifest Newer = new(
        new SakuraVersion(0, 30, 0, "beta"),
        new Uri("https://github.com/EXOTARA/Sakura/releases/download/v0.30.0-beta/Sakura.zip"),
        new string('a', 64),
        1024,
        "Notas de la 0.30");

    [Fact]
    public async Task Background_InTheStoreCopy_OnlyExplainsThatTheStoreUpdates()
    {
        var f = new Fixture(DistributionChannel.MicrosoftStore);

        await f.Coordinator.CheckInBackgroundAsync(CancellationToken.None);

        Assert.Equal(["store"], f.Presenter.Calls);
        Assert.Equal(0, f.Source.Lookups);
    }

    [Fact]
    public async Task Background_WhenTurnedOff_DoesNothingAtAll()
    {
        var f = new Fixture { AutomaticCheck = false };

        await f.Coordinator.CheckInBackgroundAsync(CancellationToken.None);

        Assert.Empty(f.Presenter.Calls);
        Assert.Equal(0, f.Source.Lookups);
        Assert.Empty(f.Delays);
    }

    [Fact]
    public async Task Background_WaitsBeforeAsking_AndRecordsTheCheckEvenWhenNothingIsFound()
    {
        var f = new Fixture();
        f.Source.Result = UpdateLookup.Nothing("Ya tienes la última.");

        await f.Coordinator.CheckInBackgroundAsync(CancellationToken.None);

        Assert.Equal([UpdateFlowCoordinator.BackgroundStartDelay], f.Delays);
        Assert.Equal(Now, f.Preferences.LastCheckAt);
        Assert.Equal(1, f.Preferences.Saves);
        // Sin nada que ofrecer, en segundo plano no se toca la sección: no se pidió nada.
        Assert.Empty(f.Presenter.Calls);
    }

    [Fact]
    public async Task Background_OffersWithTheFolderItWillReplace_AndNotifies()
    {
        var f = new Fixture();
        f.Source.Result = UpdateLookup.Offer(Newer);

        await f.Coordinator.CheckInBackgroundAsync(CancellationToken.None);

        Assert.Same(Newer, f.Coordinator.OfferedUpdate);
        Assert.Equal(
            ["status:Hay una versión más nueva disponible.:False", $"offer:0.30.0-beta:{Fixture.Install}", "notify:0.30.0-beta"],
            f.Presenter.Calls);
    }

    [Fact]
    public async Task Background_ClosedDuringTheWait_DoesNotAsk()
    {
        var f = new Fixture { Closed = true };

        await f.Coordinator.CheckInBackgroundAsync(CancellationToken.None);

        Assert.Equal(0, f.Source.Lookups);
    }

    [Fact]
    public async Task Background_PassesTheSkippedVersion()
    {
        var f = new Fixture();
        f.Preferences.SkippedVersion = "0.30.0-beta";
        f.Source.Result = UpdateLookup.Nothing("nada");

        await f.Coordinator.CheckInBackgroundAsync(CancellationToken.None);

        Assert.Equal(new SakuraVersion(0, 30, 0, "beta"), f.Source.LastSkipped);
    }

    [Fact]
    public async Task CheckNow_NothingFound_ShowsTheSourcesMessage()
    {
        var f = new Fixture();
        f.Source.Result = UpdateLookup.Nothing("Ya tienes la última versión.");

        await f.Coordinator.CheckNowAsync(CancellationToken.None);

        Assert.Equal(
            ["status:Comprobando…:True", "offer:-:-", "status:Ya tienes la última versión.:False"],
            f.Presenter.Calls);
        Assert.Equal(1, f.Preferences.Saves);
    }

    [Fact]
    public async Task CheckNow_Found_OffersWithoutTheFolderLine()
    {
        // La asimetría heredada: la comprobación manual no enseña la carpeta que se va a reemplazar.
        var f = new Fixture();
        f.Source.Result = UpdateLookup.Offer(Newer);

        await f.Coordinator.CheckNowAsync(CancellationToken.None);

        Assert.Equal("offer:0.30.0-beta:-", f.Presenter.Calls[^1]);
        Assert.Same(Newer, f.Coordinator.OfferedUpdate);
    }

    [Fact]
    public async Task Install_WithNothingOffered_DoesNothing()
    {
        var f = new Fixture();

        await f.Coordinator.InstallOfferedAsync(CancellationToken.None);

        Assert.Empty(f.Presenter.Calls);
        Assert.False(f.ExitRequested);
    }

    [Fact]
    public async Task Install_WhenPreparationFails_ShowsWhy_AndOffersAgain()
    {
        var f = await Fixture.WithOfferAsync(Newer);
        f.Source.Prepared = (false, string.Empty, "La huella no coincide.");

        await f.Coordinator.InstallOfferedAsync(CancellationToken.None);

        Assert.Equal(
            ["status:Descargando…:True", "status:La huella no coincide.:False", "offer:0.30.0-beta:-"],
            f.Presenter.Calls);
        Assert.False(f.ExitRequested);
    }

    [Fact]
    public async Task Install_WhenTheHelperDoesNotStart_StaysOpen()
    {
        var f = await Fixture.WithOfferAsync(Newer);
        f.Source.Prepared = (true, "C:\\ayudante.ps1", string.Empty);
        f.Source.LaunchSucceeds = false;

        await f.Coordinator.InstallOfferedAsync(CancellationToken.None);

        Assert.Equal("status:No se pudo iniciar el instalador de la actualización.:False", f.Presenter.Calls[^1]);
        Assert.False(f.ExitRequested);
    }

    [Fact]
    public async Task Install_Ready_LaunchesTheHelperFirst_ThenAsksToExit()
    {
        var f = await Fixture.WithOfferAsync(Newer);
        f.Source.Prepared = (true, "C:\\ayudante.ps1", string.Empty);

        await f.Coordinator.InstallOfferedAsync(CancellationToken.None);

        Assert.Equal("C:\\ayudante.ps1", f.Source.LaunchedHelper);
        Assert.Equal("status:Sakura se va a cerrar para terminar de instalarse…:True", f.Presenter.Calls[^1]);
        Assert.True(f.ExitRequested);
        Assert.Equal(Fixture.Install, f.Source.LastInstallFolder);
        Assert.Equal(Fixture.Work, f.Source.LastWorkFolder);
    }

    [Fact]
    public async Task Skip_SavesThatExactVersion_AndClearsTheOffer()
    {
        var f = await Fixture.WithOfferAsync(Newer);

        f.Coordinator.SkipOffered();

        Assert.Equal("0.30.0-beta", f.Preferences.SkippedVersion);
        Assert.Null(f.Coordinator.OfferedUpdate);
        Assert.Equal(
            ["offer:-:-", "status:Se dejó pasar la 0.30.0-beta. Se avisará cuando salga otra.:False"],
            f.Presenter.Calls);
    }

    private sealed class Fixture
    {
        public const string Install = "C:\\Programs\\Sakura";
        public const string Work = "C:\\Data\\actualizaciones";

        private UpdateFlowCoordinator? _coordinator;

        public Fixture(DistributionChannel channel = DistributionChannel.Direct) => Channel = channel;

        public DistributionChannel Channel { get; }
        public bool AutomaticCheck { get; init; } = true;
        public bool Closed { get; init; }
        public bool ExitRequested { get; private set; }
        public List<TimeSpan> Delays { get; } = [];
        public FakeSource Source { get; } = new();
        public FakePresenter Presenter { get; } = new();
        public FakePreferences Preferences => _preferences ??= new FakePreferences(AutomaticCheck);
        private FakePreferences? _preferences;

        public UpdateFlowCoordinator Coordinator => _coordinator ??= new UpdateFlowCoordinator(
            Source,
            Presenter,
            Preferences,
            Channel,
            Current,
            Install + "\\",
            Work,
            () => ExitRequested = true,
            clock: () => Now,
            delay: (span, _) => { Delays.Add(span); return Task.CompletedTask; },
            isClosed: () => Closed);

        public static async Task<Fixture> WithOfferAsync(UpdateManifest manifest)
        {
            var f = new Fixture();
            f.Source.Result = UpdateLookup.Offer(manifest);
            await f.Coordinator.CheckNowAsync(CancellationToken.None);
            f.Presenter.Calls.Clear();
            f.Preferences.Saves = 0;
            return f;
        }
    }

    private sealed class FakeSource : IUpdateSource
    {
        public UpdateLookup Result { get; set; } = UpdateLookup.Nothing("nada");
        public (bool Ready, string HelperPath, string Problem) Prepared { get; set; } = (false, string.Empty, "sin preparar");
        public bool LaunchSucceeds { get; set; } = true;
        public int Lookups { get; private set; }
        public SakuraVersion? LastSkipped { get; private set; }
        public string? LaunchedHelper { get; private set; }
        public string? LastInstallFolder { get; private set; }
        public string? LastWorkFolder { get; private set; }

        public Task<UpdateLookup> LookForUpdateAsync(SakuraVersion current, SakuraVersion? skipped, CancellationToken cancellationToken)
        {
            Lookups++;
            LastSkipped = skipped;
            return Task.FromResult(Result);
        }

        public Task<(bool Ready, string HelperPath, string Problem)> PrepareAsync(
            UpdateManifest manifest, string installFolder, string workFolder, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            LastInstallFolder = installFolder.TrimEnd('\\');
            LastWorkFolder = workFolder;
            return Task.FromResult(Prepared);
        }

        public bool LaunchHelper(string helperPath)
        {
            LaunchedHelper = helperPath;
            return LaunchSucceeds;
        }
    }

    private sealed class FakePresenter : IUpdatePresenter
    {
        public List<string> Calls { get; } = [];

        public void SetStatus(string status, bool busy) => Calls.Add($"status:{status}:{busy}");
        public void ShowOffer(string? version, string? notes, string? targetFolder) => Calls.Add($"offer:{version ?? "-"}:{targetFolder ?? "-"}");
        public void SetProgress(double fraction) => Calls.Add($"progress:{fraction}");
        public void ShowStoreManaged() => Calls.Add("store");
        public void NotifyAvailable(SakuraVersion version) => Calls.Add($"notify:{version}");
    }

    private sealed class FakePreferences(bool automatic) : IUpdatePreferences
    {
        public DateTimeOffset? LastCheckAt { get; set; }
        public string SkippedVersion { get; set; } = string.Empty;
        public bool AutomaticCheckEnabled => automatic;
        public int Saves { get; set; }
        public void Save() => Saves++;
    }
}
