using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.App.Views.Controls;
using Nexo.Core.Vision;

namespace Nexo.App;

/// <summary>
/// Diseño D5.7 (Fase 2 — Sakura Lens) — dibuja recuadros sobre las regiones que
/// <see cref="LensHighlightMatcher"/> identificó, posicionados en coordenadas reales de pantalla
/// sobre la ventana observada. Se autodescarta sola tras unos segundos: es guía visual pasajera,
/// no un estado que el usuario tenga que cerrar a mano.
/// </summary>
public partial class LensHighlightOverlay : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;

    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(6);

    private readonly DispatcherTimer _autoHideTimer;

    public LensHighlightOverlay()
    {
        InitializeComponent();

        _autoHideTimer = new DispatcherTimer { Interval = AutoHideDelay };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            FadeOut();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExNoActivate | WsExToolWindow | WsExTransparent);
    }

    public void ShowHighlights(
        int windowLeft,
        int windowTop,
        int windowWidth,
        int windowHeight,
        IReadOnlyList<LensHighlightRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        _autoHideTimer.Stop();
        HighlightCanvas.Children.Clear();

        if (regions.Count == 0 || windowWidth <= 0 || windowHeight <= 0)
        {
            Hide();
            return;
        }

        Left = windowLeft;
        Top = windowTop;
        Width = windowWidth;
        Height = windowHeight;

        // 2026-09-15 — los recuadros con el acento de Sakura en vez de dorado: un halo suave, el
        // trazo y la flor en la esquina, para que se sepa quién está señalando. Aparecen uno detrás
        // de otro encogiéndose hasta su sitio y, al terminar, se apagan en vez de desaparecer de golpe.
        var accent = (TryFindResource("BrushAccent") as SolidColorBrush)?.Color ?? Color.FromRgb(0xE8, 0x73, 0x9E);
        var index = 0;
        foreach (var region in regions)
        {
            var mark = BuildMark(region, accent);
            Canvas.SetLeft(mark, region.Left - windowLeft - HaloPadding);
            Canvas.SetTop(mark, region.Top - windowTop - HaloPadding);
            HighlightCanvas.Children.Add(mark);
            Reveal(mark, index++);
        }

        HighlightCanvas.BeginAnimation(OpacityProperty, null);
        HighlightCanvas.Opacity = 1;
        Show();
        _autoHideTimer.Start();
    }

    private const double HaloPadding = 10;

    private static FrameworkElement BuildMark(LensHighlightRegion region, Color accent)
    {
        var width = Math.Max(0, region.Width);
        var height = Math.Max(0, region.Height);
        var root = new Grid
        {
            Width = width + HaloPadding * 2,
            Height = height + HaloPadding * 2,
        };

        root.Children.Add(new Rectangle
        {
            Margin = new Thickness(HaloPadding - 5),
            RadiusX = 14,
            RadiusY = 14,
            Stroke = new SolidColorBrush(Color.FromArgb(0x40, accent.R, accent.G, accent.B)),
            StrokeThickness = 6
        });
        root.Children.Add(new Rectangle
        {
            Margin = new Thickness(HaloPadding),
            RadiusX = 9,
            RadiusY = 9,
            Stroke = new SolidColorBrush(accent),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(0x24, accent.R, accent.G, accent.B))
        });

        var badge = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0),
            Background = (Application.Current?.TryFindResource("BrushBackground") as Brush) ?? Brushes.Black,
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1.5),
            Child = new DecorativeMark
            {
                Width = 12,
                Height = 12,
                Focusable = false,
                Style = Application.Current?.TryFindResource("SakuraFlowerMarkStyle") as Style
            }
        };
        root.Children.Add(badge);
        return root;
    }

    private static void Reveal(FrameworkElement mark, int index) =>
        EntranceMotion.Pop(mark, TimeSpan.FromMilliseconds(70 * Math.Min(index, 6)), from: 1.14);

    private void FadeOut()
    {
        if (!IsVisible)
        {
            return;
        }

        if (!SakuraMotion.AnimationsEnabled)
        {
            Hide();
            return;
        }

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = SakuraMotion.AccelerateCurve };
        fade.Completed += (_, _) =>
        {
            // Unos recuadros nuevos pueden haber llegado mientras se apagaba: esos se quedan.
            if (_autoHideTimer.IsEnabled)
            {
                return;
            }

            Hide();
            HighlightCanvas.Children.Clear();
        };
        HighlightCanvas.BeginAnimation(OpacityProperty, fade);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);
}
