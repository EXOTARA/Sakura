using System.IO;
using Nexo.Core.Distribution;
using Nexo.Core.Updates;
using Nexo.Windows.Updates;

namespace Nexo.App.Updates;

/// <summary>De dónde salen las versiones y cómo se preparan. En producción, <see cref="WindowsUpdateService"/>.</summary>
public interface IUpdateSource
{
    Task<UpdateLookup> LookForUpdateAsync(SakuraVersion current, SakuraVersion? skipped, CancellationToken cancellationToken);

    Task<(bool Ready, string HelperPath, string Problem)> PrepareAsync(
        UpdateManifest manifest,
        string installFolder,
        string workFolder,
        IProgress<double>? progress,
        CancellationToken cancellationToken);

    bool LaunchHelper(string helperPath);
}

/// <summary>Dónde se enseña lo que pasa. En producción, la sección Actualizaciones de Personalizar y la cápsula.</summary>
public interface IUpdatePresenter
{
    void SetStatus(string status, bool busy);

    void ShowOffer(string? version, string? notes, string? targetFolder);

    void SetProgress(double fraction);

    void ShowStoreManaged();

    void NotifyAvailable(SakuraVersion version);
}

/// <summary>Las tres preferencias que toca el flujo, y cómo guardarlas.</summary>
public interface IUpdatePreferences
{
    DateTimeOffset? LastCheckAt { get; set; }

    string SkippedVersion { get; set; }

    bool AutomaticCheckEnabled { get; }

    void Save();
}

/// <summary>
/// L2, primera extracción — el flujo de actualizaciones, sacado de <c>MainWindow.xaml.cs</c>.
///
/// Vivía repartido en cuatro métodos de la ventana, entre la voz y el atajo de Ctrl + K, y solo se
/// podía comprobar abriendo Sakura con una versión publicada delante. Aquí no decide nada que no
/// decidiera antes —las reglas siguen en <see cref="UpdateCheckPolicy"/> y
/// <see cref="DistributionPolicy"/>—; lo que cambia es que el orden de los pasos se puede probar sin
/// ventana, sin red y sin esperar 45 segundos.
///
/// Se movió tal cual. Las pruebas de <c>UpdateFlowCoordinatorTests</c> fijan el comportamiento que
/// tenía el código de la ventana antes de moverlo, incluidas sus asimetrías: la comprobación en
/// segundo plano enseña la carpeta que se va a reemplazar y la manual no.
/// </summary>
public sealed class UpdateFlowCoordinator
{
    /// <summary>
    /// Al arrancar, Sakura está levantando la voz, las métricas y el runtime de IA; una petición de red
    /// en ese momento solo hace el arranque más lento a cambio de una respuesta sin prisa.
    /// </summary>
    public static readonly TimeSpan BackgroundStartDelay = TimeSpan.FromSeconds(45);

    private readonly IUpdateSource _source;
    private readonly IUpdatePresenter _presenter;
    private readonly IUpdatePreferences _preferences;
    private readonly DistributionChannel _channel;
    private readonly SakuraVersion _current;
    private readonly string _installFolder;
    private readonly string _workFolder;
    private readonly Action _requestExit;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<bool> _isClosed;

    public UpdateFlowCoordinator(
        IUpdateSource source,
        IUpdatePresenter presenter,
        IUpdatePreferences preferences,
        DistributionChannel channel,
        SakuraVersion current,
        string installFolder,
        string workFolder,
        Action requestExit,
        Func<DateTimeOffset>? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<bool>? isClosed = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _channel = channel;
        _current = current;
        _installFolder = installFolder;
        _workFolder = workFolder;
        _requestExit = requestExit ?? throw new ArgumentNullException(nameof(requestExit));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _delay = delay ?? Task.Delay;
        _isClosed = isClosed ?? (() => false);
    }

    /// <summary>La versión ofrecida ahora mismo, o <see langword="null"/> si no hay ninguna.</summary>
    public UpdateManifest? OfferedUpdate { get; private set; }

    /// <summary>
    /// Diseño D68 — mira sin que nadie lo pida, como mucho una vez al día. Mira, pero no instala.
    /// </summary>
    public async Task CheckInBackgroundAsync(CancellationToken cancellationToken)
    {
        // Diseño D89 — la copia de Microsoft Store no busca ni instala versiones: eso lo hace la Store.
        if (!DistributionPolicy.UsesOwnUpdater(_channel))
        {
            _presenter.ShowStoreManaged();
            return;
        }

        if (!UpdateCheckPolicy.ShouldCheck(_preferences.LastCheckAt, _clock(), _preferences.AutomaticCheckEnabled))
        {
            return;
        }

        try
        {
            await _delay(BackgroundStartDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_isClosed())
        {
            return;
        }

        var lookup = await _source.LookForUpdateAsync(_current, Skipped(), cancellationToken);

        // La fecha se apunta haya salido lo que haya salido. Si no, un equipo sin conexión reintentaría
        // en cada arranque, que es justo lo que este límite viene a evitar.
        _preferences.LastCheckAt = _clock();
        _preferences.Save();

        if (!lookup.Found || _isClosed())
        {
            return;
        }

        OfferedUpdate = lookup.Manifest;
        _presenter.SetStatus("Hay una versión más nueva disponible.", busy: false);
        _presenter.ShowOffer(
            lookup.Manifest!.Version.ToString(),
            lookup.Manifest.Notes,
            Path.TrimEndingDirectorySeparator(_installFolder));
        _presenter.NotifyAvailable(lookup.Manifest.Version);
    }

    /// <summary>El botón «Buscar actualizaciones».</summary>
    public async Task CheckNowAsync(CancellationToken cancellationToken)
    {
        if (!DistributionPolicy.UsesOwnUpdater(_channel))
        {
            _presenter.ShowStoreManaged();
            return;
        }

        _presenter.SetStatus("Comprobando…", busy: true);
        _presenter.ShowOffer(null, null, null);
        OfferedUpdate = null;

        var lookup = await _source.LookForUpdateAsync(_current, Skipped(), cancellationToken);

        _preferences.LastCheckAt = _clock();
        _preferences.Save();

        if (!lookup.Found)
        {
            _presenter.SetStatus(lookup.Message, busy: false);
            return;
        }

        OfferedUpdate = lookup.Manifest;
        _presenter.SetStatus("Hay una versión más nueva disponible.", busy: false);
        _presenter.ShowOffer(lookup.Manifest!.Version.ToString(), lookup.Manifest.Notes, null);
    }

    /// <summary>
    /// Descarga, prepara y entrega el relevo al ayudante. Sakura se cierra **después** de lanzarlo: el
    /// guion espera a que este proceso termine, así que si se cerrara antes nadie lo lanzaría.
    /// </summary>
    public async Task InstallOfferedAsync(CancellationToken cancellationToken)
    {
        if (OfferedUpdate is not { } manifest)
        {
            return;
        }

        _presenter.SetStatus("Descargando…", busy: true);

        var progress = new Progress<double>(_presenter.SetProgress);
        var prepared = await _source.PrepareAsync(manifest, _installFolder, _workFolder, progress, cancellationToken);

        if (!prepared.Ready)
        {
            _presenter.SetStatus(prepared.Problem, busy: false);
            _presenter.ShowOffer(manifest.Version.ToString(), manifest.Notes, null);
            return;
        }

        if (!_source.LaunchHelper(prepared.HelperPath))
        {
            _presenter.SetStatus("No se pudo iniciar el instalador de la actualización.", busy: false);
            return;
        }

        _presenter.SetStatus("Sakura se va a cerrar para terminar de instalarse…", busy: true);

        // Por la puerta de siempre y no con Shutdown a pelo: la salida normal para antes el runtime de
        // IA administrado, y saltársela dejó en la primera prueba real un proceso hijo vivo que hacía
        // que el ayudante no viera morir a Sakura y se retirase sin tocar nada.
        _requestExit();
    }

    /// <summary>«Ahora no»: se guarda esa versión concreta, no un «no molestar». La siguiente sí se ofrece.</summary>
    public void SkipOffered()
    {
        if (OfferedUpdate is not { } manifest)
        {
            return;
        }

        _preferences.SkippedVersion = manifest.Version.ToString();
        _preferences.Save();

        OfferedUpdate = null;
        _presenter.ShowOffer(null, null, null);
        _presenter.SetStatus($"Se dejó pasar la {manifest.Version}. Se avisará cuando salga otra.", busy: false);
    }

    private SakuraVersion? Skipped() =>
        SakuraVersion.TryParse(_preferences.SkippedVersion, out var parsed) ? parsed : null;
}

/// <summary>La fuente de producción: el servicio de siempre, detrás de la interfaz.</summary>
public sealed class WindowsUpdateSource(WindowsUpdateService service) : IUpdateSource
{
    public Task<UpdateLookup> LookForUpdateAsync(SakuraVersion current, SakuraVersion? skipped, CancellationToken cancellationToken) =>
        service.LookForUpdateAsync(current, skipped, cancellationToken);

    public Task<(bool Ready, string HelperPath, string Problem)> PrepareAsync(
        UpdateManifest manifest,
        string installFolder,
        string workFolder,
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        service.PrepareAsync(manifest, installFolder, workFolder, progress, cancellationToken);

    public bool LaunchHelper(string helperPath) => WindowsUpdateService.LaunchHelper(helperPath);
}
