using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Media.Animation;
using Nexo.App.Motion;

namespace Nexo.App.Views;

public partial class CaptureView : UserControl
{
    private static readonly int[] DelayOptions = [3, 5, 10];

    public CaptureView()
    {
        InitializeComponent();

        foreach (var seconds in DelayOptions)
        {
            var chip = new Button
            {
                Content = $"{seconds} s",
                Margin = new Thickness(0, 0, 6, 0),
                Style = (Style)FindResource("SoftChipButtonStyle"),
                Tag = seconds
            };
            AutomationProperties.SetName(chip, $"Capturar dentro de {seconds} segundos");
            chip.Click += (_, e) =>
            {
                // El chip está dentro de la tarjeta: sin esto, la tarjeta también se daría por pulsada.
                e.Handled = true;
                DelayedCaptureRequested?.Invoke(this, seconds);
            };
            DelayChoices.Children.Add(chip);
        }
    }

    public event EventHandler? CaptureRequested;

    /// <summary>2026-09-15 — capturar una zona de la pantalla.</summary>
    public event EventHandler? RegionCaptureRequested;

    /// <summary>2026-09-15 — capturar la pantalla del ratón pasados estos segundos.</summary>
    public event EventHandler<int>? DelayedCaptureRequested;

    /// <summary>2026-09-15 — empezar o parar la grabación de la pantalla.</summary>
    public event EventHandler? RecordingToggleRequested;

    public bool RecordSystemAudio => RecordSystemAudioCheckBox.IsChecked == true;

    public bool RecordMicrophone => RecordMicrophoneCheckBox.IsChecked == true;

    private void CaptureButton_Click(object sender, RoutedEventArgs e) =>
        CaptureRequested?.Invoke(this, EventArgs.Empty);

    private void RegionCaptureButton_Click(object sender, RoutedEventArgs e) =>
        RegionCaptureRequested?.Invoke(this, EventArgs.Empty);

    private void DelayedCaptureButton_Click(object sender, RoutedEventArgs e) =>
        DelayedCaptureRequested?.Invoke(this, DelayOptions[0]);

    private void RecordButton_Click(object sender, RoutedEventArgs e) =>
        RecordingToggleRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Refleja si se está grabando: el botón pasa a «Parar», se enseña el tiempo y el punto rojo late.
    /// Las opciones de sonido no se pueden cambiar a mitad de una grabación.
    /// </summary>
    public void SetRecordingState(bool recording, TimeSpan elapsed, string? detail = null)
    {
        RecordButton.Content = recording ? "Parar" : "Grabar";
        AutomationProperties.SetName(RecordButton, recording ? "Parar la grabación" : "Empezar a grabar la pantalla");
        RecordingTitle.Text = recording ? $"Grabando · {elapsed:mm\\:ss}" : "Grabar la pantalla";
        RecordingDetail.Text = detail ?? (recording
            ? "Para con este botón o con el mismo atajo."
            : "Se guarda en Vídeos › Sakura");
        RecordingOptions.IsEnabled = !recording;

        var pulsing = RecordDotScale.HasAnimatedProperties;
        if (recording && !pulsing && SakuraMotion.AnimationsEnabled)
        {
            var pulse = new DoubleAnimation(1, 0.7, TimeSpan.FromMilliseconds(700))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            RecordDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            RecordDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }
        else if (!recording && pulsing)
        {
            RecordDotScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            RecordDotScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }
}
