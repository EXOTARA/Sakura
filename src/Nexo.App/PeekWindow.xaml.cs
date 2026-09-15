using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.App.Views.Controls;
using Nexo.Core.Metrics;
using Nexo.Core.Settings;

namespace Nexo.App;

public partial class PeekWindow : Window
{
    private readonly DispatcherTimer _hideTimer;
    private bool _isHiding;
    private SidebarPosition _position = SidebarPosition.Right;
    private bool _animationsEnabled = true;

    public PeekWindow()
    {
        InitializeComponent();
        Views.Controls.PopupKeyboardAccess.Attach(this, System.Windows.Input.Key.F11, () => HideImmediately());

        _hideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3.5)
        };
        // Con el ratón o el teclado encima se espera: quien la está leyendo no quiere que se vaya.
        _hideTimer.Tick += (_, _) => { if (!IsKeyboardFocusWithin && !IsMouseOver) HidePeekAnimated(); };
    }

    public void ShowSnapshot(SystemSnapshot snapshot, ShellPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(preferences);

        _position = preferences.Position;
        _animationsEnabled = preferences.AnimationsEnabled && SakuraMotion.AnimationsEnabled;
        ApplyPreferences(preferences);
        PositionWindow();

        _hideTimer.Stop();
        var wasShowing = IsVisible && !_isHiding;
        _isHiding = false;

        if (!IsVisible)
        {
            Show();
        }

        Topmost = true;

        if (!_animationsEnabled)
        {
            PeekBorder.BeginAnimation(OpacityProperty, null);
            PeekBorder.Opacity = 1;
            UpdateSnapshot(snapshot, animate: false);
            _hideTimer.Start();
            return;
        }

        // Si ya estaba a la vista, solo se ponen al día las cifras: volver a inflarla sería un
        // parpadeo cada vez que se pulsa el atajo.
        if (!wasShowing)
        {
            // El alto se fija mientras la burbuja crece: ajustando la ventana a su contenido, la
            // ventana encogía con el círculo del principio y la tarjeta aparecía de golpe al final.
            FixHeightToContent();
            BubbleMotion.Inflate(PeekBorder, Anchor, () => SizeToContent = SizeToContent.Height);
        }

        UpdateSnapshot(snapshot, animate: true, firstReveal: !wasShowing);
        _hideTimer.Start();
    }

    private HorizontalAlignment Anchor =>
        _position == SidebarPosition.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public void HideImmediately()
    {
        _hideTimer.Stop();
        _isHiding = false;
        if (IsVisible)
        {
            Hide();
        }

        SizeToContent = SizeToContent.Height;
    }

    private void ApplyPreferences(ShellPreferences preferences)
    {
        CpuMetricPanel.Visibility = preferences.ShowCpuInPeek ? Visibility.Visible : Visibility.Collapsed;
        MemoryMetricPanel.Visibility = preferences.ShowMemoryInPeek ? Visibility.Visible : Visibility.Collapsed;
        GpuMetricPanel.Visibility = preferences.ShowGpuInPeek ? Visibility.Visible : Visibility.Collapsed;
        DiskMetricPanel.Visibility = preferences.ShowDiskInPeek ? Visibility.Visible : Visibility.Collapsed;
        TopProcessPanel.Visibility = preferences.ShowTopProcessInPeek ? Visibility.Visible : Visibility.Collapsed;

        var visibleMetricCount = 0;
        visibleMetricCount += CpuMetricPanel.Visibility == Visibility.Visible ? 1 : 0;
        visibleMetricCount += MemoryMetricPanel.Visibility == Visibility.Visible ? 1 : 0;
        visibleMetricCount += GpuMetricPanel.Visibility == Visibility.Visible ? 1 : 0;
        visibleMetricCount += DiskMetricPanel.Visibility == Visibility.Visible ? 1 : 0;

        PeekMetricsGrid.Visibility = visibleMetricCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        PeekMetricsGrid.Columns = Math.Max(1, visibleMetricCount);

        var baseColor = (Color)ColorConverter.ConvertFromString("#11131A");
        var alpha = (byte)Math.Round(preferences.Opacity * 255);
        PeekBorder.Background = new SolidColorBrush(Color.FromArgb(
            alpha,
            baseColor.R,
            baseColor.G,
            baseColor.B));
    }

    private void UpdateSnapshot(SystemSnapshot snapshot, bool animate, bool firstReveal = false)
    {
        (IconRing Ring, TextBlock Text, double? Value)[] metrics =
        [
            (CpuRing, CpuValueText, snapshot.CpuUsagePercent),
            (MemoryRing, MemoryValueText, snapshot.MemoryUsagePercent),
            (GpuRing, GpuValueText, snapshot.GpuUsagePercent),
            (DiskRing, DiskValueText, snapshot.SystemDriveUsagePercent),
        ];

        var order = 0;
        foreach (var (ring, text, value) in metrics)
        {
            ring.Tag = text;
            if (value is not { } target)
            {
                ring.BeginAnimation(ShownPercentProperty, null);
                ring.Percent = null;
                text.Text = FormatPercentage(null);
                continue;
            }

            if (!animate)
            {
                ring.BeginAnimation(ShownPercentProperty, null);
                ring.SetValue(ShownPercentProperty, target);
                continue;
            }

            // La primera vez suben desde cero cuando la burbuja ya tiene su forma, una detrás de
            // otra; si ya se veía, van de la cifra de antes a la nueva.
            if (firstReveal)
            {
                ring.BeginAnimation(ShownPercentProperty, null);
                ring.SetValue(ShownPercentProperty, 0d);
            }

            var begin = firstReveal
                ? TimeSpan.FromMilliseconds(260 + 60 * order)
                : TimeSpan.Zero;
            ring.BeginAnimation(
                ShownPercentProperty,
                new DoubleAnimation(target, TimeSpan.FromMilliseconds(720))
                {
                    BeginTime = begin,
                    EasingFunction = SakuraMotion.DecelerateCurve
                });
            order++;
        }

        TopProcessNameText.Text = string.IsNullOrWhiteSpace(snapshot.TopProcessName)
            ? "Proceso no disponible"
            : snapshot.TopProcessName;
        TopProcessMemoryText.Text = snapshot.TopProcessWorkingSetBytes.HasValue
            ? FormatBytes(snapshot.TopProcessWorkingSetBytes.Value)
            : string.Empty;
    }

    /// <summary>
    /// La cifra que se ve mientras sube. Mueve a la vez el anillo y el número de debajo (guardado en
    /// <see cref="FrameworkElement.Tag"/>), así los dos llegan juntos.
    /// </summary>
    private static readonly DependencyProperty ShownPercentProperty = DependencyProperty.RegisterAttached(
        "ShownPercent", typeof(double), typeof(PeekWindow), new PropertyMetadata(0d, OnShownPercentChanged));

    private static void OnShownPercentChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not IconRing ring)
        {
            return;
        }

        var value = (double)e.NewValue;
        ring.Percent = value;
        if (ring.Tag is TextBlock text)
        {
            text.Text = FormatPercentage(value);
        }
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Top = workArea.Top + 18;
        Left = _position == SidebarPosition.Right
            ? workArea.Right - Width - 18
            : workArea.Left + 18;
    }

    private void HidePeekAnimated()
    {
        _hideTimer.Stop();

        if (!IsVisible || _isHiding)
        {
            return;
        }

        if (!_animationsEnabled)
        {
            Hide();
            return;
        }

        _isHiding = true;
        FixHeightToContent();
        BubbleMotion.Deflate(PeekBorder, Anchor, () =>
        {
            // Si se volvió a pedir mientras se recogía, ShowSnapshot ya la ha inflado de nuevo.
            if (!_isHiding)
            {
                return;
            }

            _isHiding = false;
            Hide();
            SizeToContent = SizeToContent.Height;
        });
    }

    private void FixHeightToContent()
    {
        // Ya fijado: una salida a medias lo dejó con el alto completo, que es el que sirve.
        if (SizeToContent == SizeToContent.Manual)
        {
            return;
        }

        UpdateLayout();
        var height = ActualHeight;
        SizeToContent = SizeToContent.Manual;
        Height = height;
    }

    private static string FormatPercentage(double? value)
    {
        return value.HasValue ? $"{value.Value:0}%" : "—";
    }

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        return bytes >= gigabyte
            ? $"{bytes / gigabyte:0.0} GB"
            : $"{bytes / megabyte:0} MB";
    }
}
