using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
        _hideTimer.Tick += (_, _) => { if (!IsKeyboardFocusWithin) HidePeekAnimated(); };
    }

    public void ShowSnapshot(SystemSnapshot snapshot, ShellPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(preferences);

        _position = preferences.Position;
        _animationsEnabled = preferences.AnimationsEnabled;
        ApplyPreferences(preferences);
        UpdateSnapshot(snapshot);
        PositionWindow();

        _hideTimer.Stop();
        _isHiding = false;

        PeekBorder.BeginAnimation(OpacityProperty, null);
        PeekTranslate.BeginAnimation(TranslateTransform.XProperty, null);

        if (!IsVisible)
        {
            Show();
        }

        Topmost = true;

        if (!_animationsEnabled)
        {
            PeekTranslate.X = 0;
            PeekBorder.Opacity = 1;
            _hideTimer.Start();
            return;
        }

        var offset = _position == SidebarPosition.Right ? 24 : -24;
        PeekTranslate.X = offset;
        PeekBorder.Opacity = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(150);

        PeekTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0, duration) { EasingFunction = easing });

        PeekBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration) { EasingFunction = easing });

        _hideTimer.Start();
    }

    public void HideImmediately()
    {
        _hideTimer.Stop();
        _isHiding = false;
        if (IsVisible)
        {
            Hide();
        }
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

    private void UpdateSnapshot(SystemSnapshot snapshot)
    {
        CpuValueText.Text = FormatPercentage(snapshot.CpuUsagePercent);
        MemoryValueText.Text = FormatPercentage(snapshot.MemoryUsagePercent);
        GpuValueText.Text = FormatPercentage(snapshot.GpuUsagePercent);
        DiskValueText.Text = FormatPercentage(snapshot.SystemDriveUsagePercent);
        TopProcessNameText.Text = string.IsNullOrWhiteSpace(snapshot.TopProcessName)
            ? "Proceso no disponible"
            : snapshot.TopProcessName;
        TopProcessMemoryText.Text = snapshot.TopProcessWorkingSetBytes.HasValue
            ? FormatBytes(snapshot.TopProcessWorkingSetBytes.Value)
            : string.Empty;
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
        var offset = _position == SidebarPosition.Right ? 18 : -18;
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(120);

        var opacityAnimation = new DoubleAnimation(0, duration)
        {
            EasingFunction = easing
        };
        opacityAnimation.Completed += (_, _) =>
        {
            Hide();
            _isHiding = false;
        };

        PeekTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(offset, duration) { EasingFunction = easing });
        PeekBorder.BeginAnimation(OpacityProperty, opacityAnimation);
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
