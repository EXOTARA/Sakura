using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfAnimatedGif;
using Nexo.App.Motion;
using Nexo.App.Views.Controls;
using Nexo.Core.Ambient;
using Nexo.Core.Media;
using Nexo.Core.Metrics;
using Nexo.Core.Shell;
using Nexo.Core.Time;
using Nexo.Core.Voice;

namespace Nexo.App.Views;

/// <summary>
/// Diseño D44 — el contenido del cajón: Panel, Media y Rendimiento bajo la tira de pestañas.
///
/// Vive en su propia vista y no dentro de Sistema porque en la referencia no es una sección de una
/// pantalla, es un cajón que baja del borde de arriba sobre lo que estés haciendo. Sistema se queda
/// con el diagnóstico, que es lo que sí se va a leer sentado.
/// </summary>
public partial class DashboardView : UserControl
{
    private readonly ObservableCollection<CalendarCell> _calendarCells = [];

    private DateOnly _calendarMonth = DateOnly.FromDateTime(DateTime.Now);
    private MediaSnapshot _media = MediaSnapshot.Nothing;
    private DashboardTab _activeTab = DashboardTab.Panel;

    /// <summary>
    /// Diseño D55 — la silueta de pétalos gira despacio mientras suena.
    ///
    /// Una vuelta cada cuarenta segundos. El número importa: la forma tiene ocho pétalos, así que
    /// un giro completo pasa ocho crestas por el mismo punto — a esta velocidad eso es una cada
    /// cinco segundos, que se percibe como que la pieza está viva sin llegar a pedir atención. Más
    /// rápido y una silueta ondulada girando marea; más lento y no se distingue de estar quieta.
    ///
    /// Lo que gira es la FORMA, no la fotografía. La imagen se queda derecha y la flor es su
    /// máscara: es la diferencia entre un disco dando vueltas —que marea y además pone la carátula
    /// del revés cada veinte segundos— y un marco vivo alrededor de una imagen que se puede seguir
    /// mirando.
    ///
    /// El centro de giro se declara en 94,94 y no con RenderTransformOrigin porque la misma
    /// transformación sirve para dos cosas distintas: la máscara de la imagen y el fondo. Una
    /// geometría de recorte no tiene «origen relativo»; tiene coordenadas, y las suyas van de 0 a
    /// 188.
    /// </summary>
    private static readonly TimeSpan CoverTurn = TimeSpan.FromSeconds(40);

    private readonly RotateTransform _coverSpin = new() { CenterX = 94, CenterY = 94 };

    /// <summary>
    /// El giro de la carátula del panel. Es otro transform y no el mismo porque el centro cambia
    /// con el tamaño —188 allí, 120 aquí— y una rotación alrededor del centro equivocado no gira:
    /// tambalea. Los dos se animan a la vez y con la misma vuelta, así que van sincronizados.
    /// </summary>
    private readonly RotateTransform _panelCoverSpin = new() { CenterX = 60, CenterY = 60 };
    private bool _coverSpinning;

    public DashboardView()
    {
        InitializeComponent();
        CalendarDayItems.ItemsSource = _calendarCells;

        DashboardTabs.SetTabs(
        [
            new DashboardTabDefinition("Panel", FindResource("IconTabPanel") as Geometry),
            new DashboardTabDefinition("Media", FindResource("IconTabMedia") as Geometry),
            new DashboardTabDefinition("Rendimiento", FindResource("IconTabPerformance") as Geometry),
            new DashboardTabDefinition("Voz", FindResource("IconSakuraMic") as Geometry)
        ]);
        DashboardTabs.SelectionChanged += (_, index) => ShowTab((DashboardTab)index, animate: true);

        // Diseno D49 - las dos caratulas se recortan con el contorno de flor. La forma se pide por
        // tamano: a 188 ondula, a 46 la formula la devuelve como circulo, porque una ondulacion de
        // dos pixeles y medio no se lee como borde vivo, solo ensucia el canto.
        var largeCover = PetalGeometry.Create(188);
        MediaCoverBackdrop.Data = largeCover;

        // El fondo gira como elemento; la imagen no gira, gira su recorte. El resultado en
        // pantalla es el mismo contorno en los dos, que es lo que hace falta para que no se vea
        // doble.
        MediaCoverBackdrop.RenderTransform = _coverSpin;

        // La geometría cacheada está congelada —se comparte entre las dos carátulas— y a una
        // congelada no se le puede poner transformación. El clon es de esta máscara y solo de ella.
        var mask = largeCover.Clone();
        mask.Transform = _coverSpin;
        MediaCoverShape.Clip = mask;

        // Diseno D76 - la caratula del panel pasa de 46 a 120 al volverse columna alta. A 46 la
        // formula devolvia un circulo a proposito; a 120 si ondula, y la portada se reconoce como
        // la marca en vez de como un circulo cualquiera.
        var panelCover = PetalGeometry.Create(120);
        PanelMediaBackdrop.Data = panelCover;
        PanelMediaCoverShape.Data = panelCover;

        // Diseno D82 - el fondo gira como elemento; la imagen no gira, gira su recorte. Es lo mismo
        // que hace la caratula grande, y por lo mismo: girando la imagen se veria doble contorno.
        PanelMediaBackdrop.RenderTransform = _panelCoverSpin;

        var panelMask = panelCover.Clone();
        panelMask.Transform = _panelCoverSpin;
        PanelMediaCoverShape.Clip = panelMask;

        BuildCalendarHeader();
        RefreshCalendar();
        RefreshClock();
        RefreshMediaUi();
    }

    /// <summary>Se pidió elegir (o cambiar) la imagen del hueco libre del Panel.</summary>
    public event EventHandler? PanelImagePickRequested;

    /// <summary>
    /// Diseño D58 — enseña la imagen de la persona, o la invitación si no hay ninguna.
    ///
    /// Una ruta que ya no existe se trata como «no hay imagen» y no como un error: el archivo pudo
    /// moverse o borrarse meses después de elegirlo, y un aviso cada vez que se abre el cajón por
    /// algo que la persona hizo a propósito en otro sitio sería un castigo, no una ayuda. Vuelve a
    /// salir el hueco vacío, que ya dice qué hacer.
    /// </summary>
    /// <summary>
    /// Diseño D85 — el hueco acepta también un GIF animado.
    ///
    /// Un GIF no es «una imagen más»: sus fotogramas suelen ser PARCIALES —solo el trozo que
    /// cambia— y cada uno trae un modo de descarte que dice qué hacer con el anterior. Enseñarlos
    /// tal cual, que es lo que sale de decodificarlos a mano sin componer, deja rastros y parpadeos
    /// en buena parte de los GIF reales. Por eso esto lo hace WpfAnimatedGif (Apache-2.0) y no
    /// código propio: la parte difícil de un GIF no es leerlo, es componerlo.
    ///
    /// La animación se para con «menos movimiento» del sistema. Un hueco decorativo es justo lo que
    /// esa opción viene a apagar.
    /// </summary>
    public void SetPanelImage(string? path)
    {
        var animated = IsAnimatedGif(path) && SakuraMotion.AnimationsEnabled;

        // Se limpian SIEMPRE los dos caminos antes de poner nada: cambiar de un GIF a una imagen
        // fija dejaría el temporizador del anterior corriendo sobre un control que ya enseña otra
        // cosa, y lo que se ve entonces es una imagen que parpadea sin motivo.
        ImageBehavior.SetAnimatedSource(PanelUserImage, null);
        PanelUserImage.Source = null;

        var image = TryLoadPanelImage(path, decode: !animated);

        if (image is not null && animated)
        {
            ImageBehavior.SetRepeatBehavior(PanelUserImage, RepeatBehavior.Forever);
            ImageBehavior.SetAnimatedSource(PanelUserImage, image);
        }
        else
        {
            PanelUserImage.Source = image;
        }

        PanelUserImage.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        PanelImageEmptyHint.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;

        PanelImageButton.ToolTip = image is null
            ? "Elige una imagen o un GIF para este hueco"
            : "Cambiar la imagen · clic derecho para quitarla";
    }

    private static bool IsAnimatedGif(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        System.IO.Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase);

    /// <param name="decode">
    /// Reducir al vuelo mientras se decodifica ahorra memoria en una foto de catorce megapíxeles,
    /// pero a un GIF hay que dejarlo a su tamaño: la reducción se aplicaría al primer fotograma y
    /// el resto llegarían con otras medidas.
    /// </param>
    private static BitmapImage? TryLoadPanelImage(string? path, bool decode = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();

            // OnLoad para no dejar el archivo tomado: es un archivo de la persona y bloquearlo le
            // impediría moverlo o borrarlo mientras Sakura esté abierta.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.UriSource = new Uri(path, UriKind.Absolute);

            if (decode)
            {
                image.DecodePixelHeight = 264;
            }

            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (
            exception is NotSupportedException or UriFormatException or IOException or
                UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            // Un archivo que ya no es una imagen válida se comporta como si no estuviera.
            return null;
        }
    }

    private void PanelImageButton_Click(object sender, RoutedEventArgs e) =>
        PanelImagePickRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler? MediaPlayPauseRequested;

    public event EventHandler? MediaNextRequested;

    public event EventHandler? MediaPreviousRequested;

    public DashboardTab ActiveTab => _activeTab;

    /// <summary>
    /// Abre una pestaña concreta, dejando la tira y el panel de acuerdo.
    ///
    /// Existe porque hasta ahora la única forma de cambiar de pestaña era pulsarla: bien para
    /// alguien delante de la pantalla, imposible para cualquier otra cosa —una orden de voz que
    /// quiera llevar a Voz, o un retrato que quiera dibujarla.
    /// </summary>
    public void SelectTab(DashboardTab tab)
    {
        DashboardTabs.Select((int)tab);
        ShowTab(tab, animate: false);
    }

    /// <summary>
    /// Elige la pestaña con la que abrir el cajón. La regla vive en
    /// <see cref="DashboardTabPolicy"/> y no aquí: es una decisión de producto —no enseñar un
    /// reproductor vacío— y se puede probar sin abrir una ventana.
    /// </summary>
    public void PrepareForReveal()
    {
        var tab = DashboardTabPolicy.Resolve(_activeTab, _media.HasSession);
        DashboardTabs.Select((int)tab);
        ShowTab(tab, animate: false);
        RefreshClock();
    }

    /// <summary>
    /// Diseño D58 — cuánto se desplaza la pestaña que entra.
    ///
    /// Más que el desplazamiento vertical anterior (12) porque ahora el recorrido tiene que leerse
    /// como una dirección y no solo como un empujón: con doce píxeles en horizontal no se distingue
    /// venir de la derecha de venir de la izquierda.
    /// </summary>
    private const double TabEnterOffset = 32;

    private void ShowTab(DashboardTab tab, bool animate)
    {
        // La dirección se calcula antes de mover _activeTab, que es de dónde venimos.
        var direction = DashboardTabPolicy.EnterDirection(_activeTab, tab);
        _activeTab = tab;

        // Se entra desde el lado contrario al que se va: al avanzar a la derecha, la página nueva
        // llega desde la derecha, igual que la hoja de un libro.
        var offset = direction * TabEnterOffset;

        Show(PanelPane, PanelPaneTranslate, tab == DashboardTab.Panel, animate, offset);
        Show(MediaPane, MediaPaneTranslate, tab == DashboardTab.Media, animate, offset);
        Show(PerformancePane, PerformancePaneTranslate, tab == DashboardTab.Performance, animate, offset);
        Show(VoicePane, VoicePaneTranslate, tab == DashboardTab.Voice, animate, offset);

        static void Show(
            UIElement pane,
            TranslateTransform transform,
            bool visible,
            bool animate,
            double offset)
        {
            if (!visible)
            {
                pane.Visibility = Visibility.Collapsed;
                return;
            }

            pane.Visibility = Visibility.Visible;

            // Sin dirección —volver a pulsar la pestaña activa, o abrir el cajón de cero— no hay
            // hacia dónde ir, así que se conserva el gesto vertical de antes.
            if (animate && offset != 0)
            {
                pane.EnterFrom(transform, fromOffset: offset);
            }
            else if (animate)
            {
                pane.EnterFrom(transform, fromOffset: 12, vertical: true);
            }
            else
            {
                pane.Opacity = 1;
                transform.X = 0;
                transform.Y = 0;
            }
        }
    }

    public void RefreshClock()
    {
        var now = DateTime.Now;

        PanelClockHourText.Text = now.ToString("hh", CultureInfo.CurrentCulture);
        PanelClockMinuteText.Text = now.ToString("mm", CultureInfo.CurrentCulture);

        // En español Windows escribe el meridiano como «p. m.»; en mayúsculas queda «P. M.», que
        // bajo un reloj de dos cifras se lee como una abreviatura rara. Se dejan solo las letras.
        PanelClockMeridiemText.Text = new string(now
            .ToString("tt", CultureInfo.CurrentCulture)
            .ToUpperInvariant()
            .Where(char.IsLetter)
            .ToArray());

        var today = DateOnly.FromDateTime(now);
        if (_calendarCells.Count > 0 && !_calendarCells.Any(cell => cell.IsToday && cell.Date == today))
        {
            RefreshCalendar();
        }
    }

    public void UpdateSession(string? profileName, TimeSpan? uptime)
    {
        PanelSessionTitleText.Text = string.IsNullOrWhiteSpace(profileName) ? "Sakura" : profileName;
        PanelUptimeText.Text = uptime is { } value
            ? $"Equipo encendido {DescribeUptime(value)}"
            : "Equipo encendido";
    }

    private static string DescribeUptime(TimeSpan uptime)
    {
        if (uptime.TotalMinutes < 1)
        {
            return "hace menos de un minuto";
        }

        if (uptime.TotalHours < 1)
        {
            var minutes = (int)uptime.TotalMinutes;
            return minutes == 1 ? "hace 1 minuto" : $"hace {minutes} minutos";
        }

        if (uptime.TotalDays < 1)
        {
            var hours = (int)uptime.TotalHours;
            var minutes = uptime.Minutes;
            var hourText = hours == 1 ? "1 hora" : $"{hours} horas";
            return minutes == 0 ? $"hace {hourText}" : $"hace {hourText} y {minutes} min";
        }

        var days = (int)uptime.TotalDays;
        return days == 1 ? "hace 1 día" : $"hace {days} días";
    }

    /// <summary>
    /// Diseño D61 — el resumen diario ya no se dibuja en el Panel.
    ///
    /// D53 lo puso ahí como tres fichas. Adler pidió después que el Panel quede exactamente como
    /// el de su Caelestia, y ese no tiene esa fila. Se conserva el método vacío porque quien lo
    /// llama sigue calculando el resumen para Inicio y para el shell: quitar también la llamada
    /// esparciría el cambio de una pestaña por media aplicación.
    /// </summary>
    /// <summary>
    /// Diseño D80 — qué enseña la tarjeta «Ahora».
    ///
    /// La vista no decide nada: recibe el vistazo ya resuelto por <c>NowGlancePolicy</c> y lo pinta.
    /// La regla de que una ventana sensible no se nombra vive en Core, con pruebas, porque es una
    /// decisión de producto y no una de presentación.
    /// </summary>
    public void UpdateNow(NowGlance glance)
    {
        PanelNowHeadlineText.Text = glance.Headline;
        PanelNowDetailText.Text = glance.Detail;

        // El título completo en el tooltip: la tarjeta es un vistazo y recorta, pero quien quiera
        // leerlo entero no debería tener que ir a buscar la ventana. Salvo que no haya nada que
        // añadir, en cuyo caso un tooltip que repite lo que ya se ve solo estorba.
        PanelNowDetailText.ToolTip =
            glance.Detail.EndsWith('…') ? glance.Detail : null;
    }

    /// <summary>
    /// Diseño D81 — las líneas de la pestaña Voz, ya resueltas por <c>VoiceGlancePolicy</c>.
    ///
    /// La vista solo traduce a pinceles lo que la política marcó: qué está mal lo decide Core, con
    /// pruebas, porque «Sakura no te oye» tiene tres causas concretas y cuál de ellas es no es una
    /// cuestión de presentación.
    /// </summary>
    // Un objeto anónimo anuncia sus propiedades internas en vez del estado que se ve.
    public sealed record AccessibleVoiceRow(string Label, string Value, Brush Foreground, Visibility MarkVisibility)
    {
        public override string ToString() => $"{Label}: {Value}";
    }

    public void UpdateVoice(IReadOnlyList<VoiceGlanceRow> rows)
    {
        var normal = (Brush)FindResource("BrushTextPrimary");
        var attention = (Brush)FindResource("BrushWarning");

        VoiceRowItems.ItemsSource = rows
            .Select(row => new AccessibleVoiceRow(row.Label, row.Value,
                row.NeedsAttention ? attention : normal,
                row.NeedsAttention ? Visibility.Visible : Visibility.Collapsed))
            .ToArray();
    }

    public void UpdateDailySummary(
        string? taskValue,
        string? taskDetail,
        string? focusValue,
        string? focusDetail,
        string? routineValue,
        string? routineDetail)
    {
        _ = taskValue;
        _ = taskDetail;
        _ = focusValue;
        _ = focusDetail;
        _ = routineValue;
        _ = routineDetail;
    }

    // ---------- Calendario ----------

    private void BuildCalendarHeader()
    {
        var culture = CultureInfo.CurrentCulture;
        CalendarWeekdayItems.ItemsSource = MonthGrid
            .WeekdayOrder(culture.DateTimeFormat.FirstDayOfWeek)
            .Select(day =>
            {
                var name = culture.DateTimeFormat.GetShortestDayName(day);
                return name.Length > 0 ? char.ToUpper(name[0], culture) + name[1..] : name;
            })
            .ToArray();
    }

    private void RefreshCalendar()
    {
        var culture = CultureInfo.CurrentCulture;
        var today = DateOnly.FromDateTime(DateTime.Now);

        var monthName = culture.DateTimeFormat.GetMonthName(_calendarMonth.Month);
        CalendarMonthText.Text =
            $"{char.ToUpper(monthName[0], culture)}{monthName[1..]} {_calendarMonth.Year}";

        var inMonth = (Brush)FindResource("BrushTextPrimary");
        var outMonth = (Brush)FindResource("BrushTextTertiary");
        var todayBackground = (Brush)FindResource("BrushAccent");

        _calendarCells.Clear();
        foreach (var day in MonthGrid.Build(_calendarMonth, today, culture.DateTimeFormat.FirstDayOfWeek))
        {
            _calendarCells.Add(new CalendarCell(
                Date: day.Date,
                Day: day.Date.Day.ToString(CultureInfo.CurrentCulture),
                IsToday: day.IsToday,
                Foreground: day.IsToday ? (Brush)FindResource("BrushBackground") : day.InMonth ? inMonth : outMonth,
                Background: day.IsToday ? todayBackground : Brushes.Transparent,
                Weight: day.IsToday || day.InMonth ? FontWeights.SemiBold : FontWeights.Normal));
        }
    }

    private void CalendarPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        _calendarMonth = _calendarMonth.AddMonths(-1);
        RefreshCalendar();
    }

    private void CalendarNextButton_Click(object sender, RoutedEventArgs e)
    {
        _calendarMonth = _calendarMonth.AddMonths(1);
        RefreshCalendar();
    }

    private sealed record CalendarCell(
        DateOnly Date,
        string Day,
        bool IsToday,
        Brush Foreground,
        Brush Background,
        FontWeight Weight)
    {
        public override string ToString() => Date.ToString("D", CultureInfo.CurrentCulture) + (IsToday ? ", hoy" : "");
    }

    // ---------- Métricas ----------

    public void UpdateSnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        CpuCapsule.Percent = snapshot.CpuUsagePercent;
        CpuCapsule.Detail = "Uso ahora mismo";

        GpuCapsule.Percent = snapshot.GpuUsagePercent;
        GpuCapsule.Detail = snapshot.DedicatedGpuMemoryBytes.HasValue
            ? $"VRAM: {FormatBytes(snapshot.DedicatedGpuMemoryBytes.Value)}"
            : "VRAM no disponible";

        MemoryCapsule.Percent = snapshot.MemoryUsagePercent;
        MemoryCapsule.Detail = snapshot.TotalMemoryBytes > 0
            ? $"{FormatBytes((long)snapshot.UsedMemoryBytes)}{Environment.NewLine}de {FormatBytes((long)snapshot.TotalMemoryBytes)}"
            : "En uso";

        DiskCapsule.Percent = snapshot.SystemDriveUsagePercent;

        // Los mismos tres datos, resumidos en el panel. Salen de esta misma lectura y no de otra
        // propia: dos consultas separadas por medio segundo se contradicen entre sí, y ver el 40%
        // arriba y el 43% abajo en la misma ventana hace dudar de las dos.
        PanelCpuRing.Percent = snapshot.CpuUsagePercent;
        PanelMemoryRing.Percent = snapshot.MemoryUsagePercent;
        PanelDiskRing.Percent = snapshot.SystemDriveUsagePercent;
    }

    public void UpdateHardwareNames(string? processorName, string? graphicsName)
    {
        CpuCapsule.Subtitle = string.IsNullOrWhiteSpace(processorName) ? "Procesador" : processorName;
        GpuCapsule.Subtitle = string.IsNullOrWhiteSpace(graphicsName) ? "Gráfica" : graphicsName;
    }

    public void UpdateThroughput(
        double downloadBytesPerSecond,
        double uploadBytesPerSecond,
        long? driveUsedBytes,
        long? driveTotalBytes,
        string? driveName)
    {
        NetworkCapsule.Subtitle = "Ahora mismo";
        NetworkCapsule.Detail =
            $"↓ {FormatRate(downloadBytesPerSecond)}{Environment.NewLine}↑ {FormatRate(uploadBytesPerSecond)}";

        // El medidor de la red se queda vacío a propósito: un porcentaje necesita un máximo, y la
        // velocidad de una conexión no lo tiene.
        NetworkCapsule.Percent = null;

        if (driveUsedBytes is { } used && driveTotalBytes is { } total && total > 0)
        {
            DiskCapsule.Subtitle = string.IsNullOrWhiteSpace(driveName) ? "Unidad del sistema" : driveName;
            DiskCapsule.Detail = $"{FormatBytes(used)}{Environment.NewLine}de {FormatBytes(total)}";
        }
        else
        {
            DiskCapsule.Subtitle = "Unidad del sistema";
            DiskCapsule.Detail = "Espacio no disponible";
        }
    }

    // ---------- Media ----------

    public void UpdateMedia(MediaSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var trackChanged = !snapshot.SameTrackAs(_media);
        _media = snapshot;

        if (trackChanged)
        {
            var cover = CreateCover(snapshot.Thumbnail);
            SetCover(MediaCoverShape, MediaCoverBrush, cover);
            SetCover(PanelMediaCoverShape, PanelMediaCoverBrush, cover);
            PanelMediaPlaceholderIcon.Visibility = cover is null ? Visibility.Visible : Visibility.Collapsed;
        }

        RefreshMediaUi();
    }

    public bool HasMediaSession => _media.HasSession;

    /// <summary>
    /// Cierto solo cuando el anillo y la onda tienen algo que decir: la pestaña Media a la vista y
    /// música sonando. Quien mueve los fotogramas lo consulta para no gastar treinta repintados por
    /// segundo dibujando el silencio detrás de otra pestaña.
    /// </summary>
    public bool WantsAnimationFrames =>
        _activeTab == DashboardTab.Media && _media.HasSession && _media.IsPlaying;

    /// <summary>Avanza un fotograma del anillo, de la onda y del tiempo transcurrido.</summary>
    public void RenderAudioFrame(IReadOnlyList<double> spectrumLevels, double wavePhase)
    {
        ArgumentNullException.ThrowIfNull(spectrumLevels);

        CoverRing.SetLevels(spectrumLevels);
        PanelCoverRing.SetLevels(spectrumLevels);
        MediaProgressBar.Phase = wavePhase;
        RefreshMediaPosition();
    }

    /// <summary>
    /// Diseño D48 — el tiempo y la barra, recalculados desde el reloj.
    ///
    /// Se hace en cada fotograma y no solo cuando llega una lectura nueva porque el reproductor
    /// informa su posición cada cinco o seis segundos: pintar solo con esas lecturas es lo que
    /// dejaba la barra clavada y luego pegando un salto. La lectura sigue siendo la única fuente
    /// de verdad; lo que se hace aquí es contar el tiempo que ha pasado desde ella.
    /// </summary>
    private void RefreshMediaPosition()
    {
        var position = MediaProgress.Resolve(
            _media.Position,
            _media.PositionUpdatedAt,
            DateTimeOffset.UtcNow,
            _media.IsPlaying,
            _media.Duration);

        MediaPositionText.Text = MediaProgress.Format(position);

        var fraction = MediaProgress.Fraction(position, _media.Duration);
        MediaProgressBar.Progress = fraction ?? 0;
        MediaProgressBar.IsWaving = _media.IsPlaying;
    }

    private void RefreshMediaUi()
    {
        var playing = _media.HasSession;

        MediaEmptyPanel.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        MediaNowPlayingPanel.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;

        var playIcon = (Geometry)FindResource(_media.IsPlaying ? "IconMediaPause" : "IconMediaPlay");
        MediaPlayPauseIcon.Data = playIcon;
        PanelMediaPlayIcon.Data = playIcon;

        ApplyCoverSpin(playing && _media.IsPlaying);

        PanelMediaPlayButton.IsEnabled = playing && _media.CanPlayPause;
        PanelMediaPreviousButton.IsEnabled = playing && _media.CanGoPrevious;
        PanelMediaNextButton.IsEnabled = playing && _media.CanGoNext;
        MediaPlayPauseButton.IsEnabled = _media.CanPlayPause;
        MediaNextButton.IsEnabled = _media.CanGoNext;
        MediaPreviousButton.IsEnabled = _media.CanGoPrevious;

        if (!playing)
        {
            PanelMediaTitleText.Text = "Nada sonando";
            PanelMediaArtistText.Text = "Sin reproducción";
            return;
        }

        MediaTitleText.Text = _media.Title.Length > 0 ? _media.Title : "Sin título";
        MediaArtistText.Text = _media.Artist.Length > 0 ? _media.Artist : "Artista desconocido";
        MediaAlbumText.Text = _media.Album;
        MediaAlbumText.Visibility = _media.Album.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        MediaSourceText.Text = _media.SourceApp.Length > 0 ? _media.SourceApp : "Windows";

        PanelMediaTitleText.Text = MediaTitleText.Text;
        PanelMediaArtistText.Text = MediaArtistText.Text;

        MediaDurationText.Text = MediaProgress.Format(_media.Duration);

        // Una emisión en directo no tiene final: enseñar «0:00» a la derecha haría pensar que la
        // duración no se pudo leer, cuando lo cierto es que no existe.
        MediaDurationText.Visibility = _media.Duration is { } total && total > TimeSpan.Zero
            ? Visibility.Visible
            : Visibility.Collapsed;

        RefreshMediaPosition();
    }

    /// <summary>
    /// Arranca o detiene el giro. Al detenerse se conserva el ángulo en el que iba: reiniciarlo a
    /// cero haría que la carátula pegara un salto al pausar, que es justo el momento en el que uno
    /// está mirándola.
    /// </summary>
    private void ApplyCoverSpin(bool spinning)
    {
        if (spinning == _coverSpinning)
        {
            return;
        }

        _coverSpinning = spinning;

        var angle = _coverSpin.Angle;

        foreach (var spin in new[] { _coverSpin, _panelCoverSpin })
        {
            spin.BeginAnimation(RotateTransform.AngleProperty, null);
            spin.Angle = angle;
        }

        if (!spinning || !SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = angle,
            To = angle + 360,
            Duration = new Duration(CoverTurn),
            RepeatBehavior = RepeatBehavior.Forever
        };

        // La MISMA animación en los dos: congelada y compartida, van exactamente al mismo ángulo.
        animation.Freeze();
        _coverSpin.BeginAnimation(RotateTransform.AngleProperty, animation);
        _panelCoverSpin.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private static BitmapImage? CreateCover(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();

            // Sin OnLoad, WPF deja el flujo abierto y la imagen se cae al liberarlo; DecodePixelWidth
            // evita guardar en memoria una carátula de 1400 píxeles para pintarla a 188.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.DecodePixelWidth = 376;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void SetCover(Shape target, ImageBrush brush, BitmapImage? cover)
    {
        brush.ImageSource = cover;
        target.Visibility = cover is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void MediaPlayPauseButton_Click(object sender, RoutedEventArgs e) =>
        MediaPlayPauseRequested?.Invoke(this, EventArgs.Empty);

    private void MediaNextButton_Click(object sender, RoutedEventArgs e) =>
        MediaNextRequested?.Invoke(this, EventArgs.Empty);

    private void MediaPreviousButton_Click(object sender, RoutedEventArgs e) =>
        MediaPreviousRequested?.Invoke(this, EventArgs.Empty);

    private static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "0 KB/s";
        }

        return bytesPerSecond >= 1024 * 1024
            ? $"{bytesPerSecond / (1024 * 1024):0.0} MB/s"
            : $"{bytesPerSecond / 1024:0} KB/s";
    }

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        return bytes >= gigabyte ? $"{bytes / gigabyte:0.0} GB" : $"{bytes / megabyte:0} MB";
    }
}
