using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Nexo.App.Motion;

namespace Nexo.App;

public partial class RecordingIndicatorWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    /// <summary>La ventana se ve en la pantalla pero no aparece en capturas ni grabaciones (Windows 10 2004+).</summary>
    private const uint WdaExcludeFromCapture = 0x11;

    public RecordingIndicatorWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExNoActivate | WsExToolWindow);
            SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
        };
    }

    public event EventHandler? StopRequested;

    /// <summary>Se enseña arriba en el centro del área de trabajo que contiene ese punto (en DIP).</summary>
    public void ShowOn(Rect workArea)
    {
        UpdateElapsed(TimeSpan.Zero);
        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + 4;

        if (SakuraMotion.AnimationsEnabled)
        {
            EntranceMotion.Pop(Body, TimeSpan.Zero, from: 0.85);
            var pulse = new DoubleAnimation(1, 0.65, TimeSpan.FromMilliseconds(700))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            DotScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            DotScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }
    }

    public void UpdateElapsed(TimeSpan elapsed) =>
        ElapsedText.Text = elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"mm\:ss");

    public void SetStopping()
    {
        StopButton.IsEnabled = false;
        StopButton.Content = "Guardando…";
    }

    public void HideIndicator()
    {
        DotScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        DotScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        StopButton.IsEnabled = true;
        StopButton.Content = "Parar";
        Hide();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int newLong);
}
