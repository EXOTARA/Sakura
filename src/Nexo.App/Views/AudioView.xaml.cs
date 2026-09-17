using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Nexo.Core.Audio;

namespace Nexo.App.Views;

public partial class AudioView : UserControl
{
    private readonly IAudioMixerService _audioService;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isApplyingSnapshot;
    private bool _refreshInProgress;
    private bool _isMasterMuted;

    public AudioView(IAudioMixerService audioService)
    {
        _audioService = audioService;
        InitializeComponent();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    public event EventHandler<AudioActionEventArgs>? ActionCompleted;

    public async Task RefreshAsync(bool force = false)
    {
        if (_refreshInProgress || (!force && IsMouseCaptureWithin))
        {
            return;
        }

        _refreshInProgress = true;
        try
        {
            var snapshot = await Task.Run(_audioService.ReadSnapshot);
            ApplySnapshot(snapshot);
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async void AudioView_Loaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Start();
        await RefreshAsync(force: true);
    }

    private void AudioView_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync(force: true);
    }

    private void ApplySnapshot(AudioMixerSnapshot snapshot)
    {
        _isApplyingSnapshot = true;
        try
        {
            if (!snapshot.IsAvailable)
            {
                ShowEmptyState(
                    "AUDIO NO DISPONIBLE",
                    snapshot.ErrorMessage ?? "No se encontró un dispositivo de salida.");
                DeviceNameText.Text = "Sin dispositivo de salida";
                MasterVolumeText.Text = "—";
                MasterVolumeSlider.IsEnabled = false;
                MasterMuteButton.IsEnabled = false;
                return;
            }

            DeviceNameText.Text = snapshot.DeviceName;
            MasterVolumeSlider.IsEnabled = true;
            MasterMuteButton.IsEnabled = true;
            MasterVolumeSlider.Value = snapshot.MasterVolumePercent;
            MasterVolumeText.Text = $"{snapshot.MasterVolumePercent:0}%";
            _isMasterMuted = snapshot.IsMasterMuted;
            MasterMuteButton.Content = snapshot.IsMasterMuted ? "Activar" : "Silenciar";

            SessionsPanel.Children.Clear();
            SessionCountText.Text = snapshot.Sessions.Count == 1
                ? "1 sesión"
                : $"{snapshot.Sessions.Count} sesiones";

            if (snapshot.Sessions.Count == 0)
            {
                ShowEmptyState(
                    "SIN SESIONES ACTIVAS",
                    "Reproduce audio en una aplicación y pulsa actualizar.");
                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            SessionsPanel.Visibility = Visibility.Visible;

            foreach (var session in snapshot.Sessions)
            {
                SessionsPanel.Children.Add(CreateSessionCard(session));
            }
        }
        finally
        {
            _isApplyingSnapshot = false;
        }
    }

    private Border CreateSessionCard(AudioSessionSnapshot session)
    {
        var volumeText = new TextBlock
        {
            Text = $"{session.VolumePercent:0}%",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("BrushTextSecondary")
        };

        var muteButton = new Button
        {
            Content = session.IsMuted ? "Activar" : "Silenciar",
            MinWidth = 64,
            Padding = new Thickness(9, 5, 9, 5),
            Style = (Style)FindResource("SecondaryButtonStyle")
        };

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = session.VolumePercent,
            Margin = new Thickness(0, 10, 0, 0),
            Style = (Style)FindResource("LiquidSliderStyle")
        };
        System.Windows.Automation.AutomationProperties.SetName(slider, $"Volumen de {session.DisplayName}");

        slider.ValueChanged += (_, args) =>
        {
            if (!_isApplyingSnapshot)
            {
                volumeText.Text = $"{args.NewValue:0}%";
            }
        };

        slider.PreviewMouseLeftButtonUp += (_, _) =>
            ApplySessionVolume(session.SessionId, slider.Value);
        slider.PreviewKeyUp += (_, _) =>
            ApplySessionVolume(session.SessionId, slider.Value);
        slider.MouseWheel += (_, _) =>
            ApplySessionVolume(session.SessionId, slider.Value);

        muteButton.Click += (_, _) =>
        {
            var result = _audioService.SetSessionMuted(session.SessionId, !session.IsMuted);
            RaiseAction(result);
            _ = RefreshAsync(force: true);
        };

        var titlePanel = new StackPanel();
        titlePanel.Children.Add(new TextBlock
        {
            Text = session.DisplayName,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        titlePanel.Children.Add(new TextBlock
        {
            // 2026-09-16 — «Sesión disponible» no decía nada: solo se avisa cuando suena algo.
            Text = session.IsActive ? "Sonando ahora" : string.Empty,
            Visibility = session.IsActive ? Visibility.Visible : Visibility.Collapsed,
            Foreground = (System.Windows.Media.Brush)FindResource("BrushAccent"),
            Margin = new Thickness(0, 3, 0, 0),
            Style = (Style)FindResource("MutedTextStyle")
        });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(titlePanel);

        Grid.SetColumn(volumeText, 1);
        volumeText.Margin = new Thickness(12, 0, 12, 0);
        header.Children.Add(volumeText);

        Grid.SetColumn(muteButton, 2);
        header.Children.Add(muteButton);

        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(slider);

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 10),
            Style = (Style)FindResource("SubtleCardStyle"),
            Child = content
        };
    }

    private void ApplySessionVolume(string sessionId, double percent)
    {
        if (_isApplyingSnapshot)
        {
            return;
        }

        var result = _audioService.SetSessionVolume(sessionId, percent);
        RaiseAction(result);
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isApplyingSnapshot)
        {
            MasterVolumeText.Text = $"{e.NewValue:0}%";
        }
    }

    private void MasterVolumeSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        CommitMasterVolume();
    }

    private void MasterVolumeSlider_KeyUp(object sender, KeyEventArgs e)
    {
        CommitMasterVolume();
    }

    private void CommitMasterVolume()
    {
        if (_isApplyingSnapshot)
        {
            return;
        }

        var result = _audioService.SetMasterVolume(MasterVolumeSlider.Value);
        RaiseAction(result);
    }

    private void MasterMuteButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _audioService.SetMasterMuted(!_isMasterMuted);
        RaiseAction(result);
        _ = RefreshAsync(force: true);
    }

    private void ShowEmptyState(string title, string detail)
    {
        SessionsPanel.Children.Clear();
        SessionsPanel.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        EmptyStateTitle.Text = title;
        EmptyStateDetail.Text = detail;
        SessionCountText.Text = "0 sesiones";
    }

    private void RaiseAction(AudioActionResult result)
    {
        ActionCompleted?.Invoke(this, new AudioActionEventArgs(result));
    }
}

public sealed class AudioActionEventArgs : EventArgs
{
    public AudioActionEventArgs(AudioActionResult result)
    {
        Result = result;
    }

    public AudioActionResult Result { get; }
}
