using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.App.DailyFlow;
using Nexo.App.Motion;

namespace Nexo.App.Views;

/// <summary>
/// 2026-09-16 — el Inicio nuevo. Qué enseña lo decide <see cref="HomeNowBuilder"/>; aquí solo se
/// dibuja y se mueve.
///
/// Movimiento: lo que aparece solo entra escalonado y sin rebote; el rebote se reserva para cuando
/// la persona toca algo —la burbuja «Ahora» que se parte en dos—, que es cuando el gesto lo pide.
/// </summary>
public partial class HomeView : UserControl
{
    private const int MaximumTrail = 3;
    private const double ActionsHeight = 50;

    private readonly ObservableCollection<HomeRecentAction> _recentActions = [];
    private HomeNow _now = new(HomeNowKind.Empty, string.Empty, string.Empty);
    private bool _actionsOpen;
    private bool _yesterdayOpen;

    public HomeView()
    {
        InitializeComponent();
        TrailItems.ItemsSource = _recentActions;
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                PlayEntrance();
            }
        };
    }

    /// <summary>Abrir la paleta de comandos.</summary>
    public event EventHandler? CommandRequested;

    /// <summary>Ir a Hoy.</summary>
    public event EventHandler? TasksRequested;

    /// <summary>Ir a Enfoque.</summary>
    public event EventHandler? FocusRequested;

    /// <summary>Crear una tarea: la burbuja «Ahora» sin nada importante.</summary>
    public event EventHandler? NewTaskRequested;

    /// <summary>Empezar a enfocarse en la tarea de «Ahora».</summary>
    public event EventHandler<TaskFocusRequestedEventArgs>? StartTaskFocusRequested;

    /// <summary>Dejar para mañana la tarea de «Ahora».</summary>
    public event EventHandler<TaskFocusRequestedEventArgs>? PostponeTaskRequested;

    public event EventHandler? PauseFocusRequested;

    public event EventHandler? ResumeFocusRequested;

    public event EventHandler? FinishFocusRequested;

    public void Refresh(HomeTodayModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        GreetingText.Text = model.Greeting;
        DateText.Text = model.DateLine;
        ApplyNow(model.Now);

        DoneText.Text = model.PlannedToday > 0 ? $"{model.DoneToday} de {model.PlannedToday}" : model.DoneToday.ToString();
        FocusText.Text = HomeNowBuilder.Minutes(model.FocusMinutesToday);
        AutomationProperties.SetName(DoneBubble, $"Hechas hoy: {DoneText.Text}. Ir a Hoy");
        AutomationProperties.SetName(FocusBubble, $"Enfoque de hoy: {FocusText.Text}");

        // Dos ceros no dicen nada: en un día sin movimiento (o recién instalada) las cifras se esconden.
        CountsBlock.Visibility = model.DoneToday == 0 && model.PlannedToday == 0 && model.FocusMinutesToday == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (model.Yesterday is { } yesterday)
        {
            YesterdayBlock.Visibility = Visibility.Visible;
            YesterdayHeadlineText.Text = yesterday.Headline;
            YesterdayItems.ItemsSource = yesterday.Items;
        }
        else
        {
            YesterdayBlock.Visibility = Visibility.Collapsed;
            SetYesterdayOpen(false, animate: false);
        }
    }

    public void AddRecentAction(string title, string detail)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        _recentActions.Insert(0, new HomeRecentAction(title.Trim(), detail?.Trim() ?? string.Empty, DateTime.Now.ToString("HH:mm")));
        while (_recentActions.Count > MaximumTrail)
        {
            _recentActions.RemoveAt(_recentActions.Count - 1);
        }

        TrailBlock.Visibility = Visibility.Visible;
    }

    /// <summary>Los bloques entran de arriba abajo, uno detrás de otro, sin rebote.</summary>
    public void PlayEntrance()
    {
        if (!SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        FrameworkElement[] blocks = [HeaderBlock, NowBlock, CountsBlock, YesterdayBlock, TrailBlock];
        var index = 0;
        foreach (var block in blocks)
        {
            if (block.Visibility == Visibility.Visible)
            {
                EntranceMotion.Rise(block, SakuraMotion.StaggerAt(index++));
            }
        }

        UpdateGlow();
    }

    private void ApplyNow(HomeNow now)
    {
        var changed = now.Kind != _now.Kind || now.TaskId != _now.TaskId;
        _now = now;

        NowTitleText.Text = now.Title;
        NowDetailText.Text = now.Detail;

        switch (now.Kind)
        {
            case HomeNowKind.Task:
                NowLabelText.Text = "AHORA · TOCA PARA DECIDIR";
                NowPrimaryButton.Content = "Empezar";
                NowSecondaryButton.Content = "Mañana";
                AutomationProperties.SetName(NowBubble, $"Ahora: {now.Title}. Toca para decidir");
                AutomationProperties.SetName(NowPrimaryButton, $"Empezar a enfocarme en {now.Title}");
                AutomationProperties.SetName(NowSecondaryButton, $"Dejar {now.Title} para mañana");
                break;
            case HomeNowKind.Focus:
                NowLabelText.Text = "ENFOCÁNDOTE";
                NowPrimaryButton.Content = now.IsPaused ? "Continuar" : "Pausar";
                NowSecondaryButton.Content = "Terminar";
                AutomationProperties.SetName(NowBubble, $"Sesión de enfoque: {now.Title}. Toca para las opciones");
                AutomationProperties.SetName(NowPrimaryButton, now.IsPaused ? "Continuar la sesión" : "Pausar la sesión");
                AutomationProperties.SetName(NowSecondaryButton, "Terminar la sesión");
                break;
            default:
                NowLabelText.Text = "AHORA";
                AutomationProperties.SetName(NowBubble, now.Detail);
                break;
        }

        // Lo que había abierto era para otra cosa: se cierra antes de enseñar lo nuevo.
        if (changed || now.Kind == HomeNowKind.Empty)
        {
            SetActionsOpen(false, animate: false);
        }

        UpdateGlow();
    }

    /// <summary>
    /// El brillo de la burbuja sale del acento en uso, que puede venir del fondo de pantalla: por eso
    /// se construye aquí y no en el XAML.
    /// </summary>
    private void UpdateGlow()
    {
        var accent = (TryFindResource("BrushAccent") as SolidColorBrush)?.Color ?? Color.FromRgb(0xE3, 0x9A, 0xB4);
        var strong = _now.Kind == HomeNowKind.Empty ? (byte)0x22 : (byte)0x55;

        NowBubble.Tag = new RadialGradientBrush
        {
            Center = new Point(0, 0),
            GradientOrigin = new Point(0, 0),
            RadiusX = 1.2,
            RadiusY = 1.4,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(strong, accent.R, accent.G, accent.B), 0),
                new GradientStop(Color.FromArgb(0x10, accent.R, accent.G, accent.B), 0.55),
                new GradientStop(Color.FromArgb(0x00, accent.R, accent.G, accent.B), 1)
            }
        };

        YesterdayBubble.Tag = new SolidColorBrush(Color.FromArgb(0x1C, accent.R, accent.G, accent.B));
        ThreadTop.Color = accent;
    }

    private void NowBubble_Click(object sender, RoutedEventArgs e)
    {
        if (_now.Kind == HomeNowKind.Empty)
        {
            NewTaskRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        SetActionsOpen(!_actionsOpen, animate: true);
    }

    /// <summary>
    /// La burbuja se parte en dos: el hueco de abajo se abre con un muelle y los dos botones se inflan
    /// desde ella, el segundo un instante después. Al cerrarse, vuelven por el mismo camino.
    /// </summary>
    private void SetActionsOpen(bool open, bool animate)
    {
        _actionsOpen = open;
        AutomationProperties.SetItemStatus(NowBubble, open ? "Opciones abiertas" : "Opciones cerradas");

        if (!animate || !SakuraMotion.AnimationsEnabled)
        {
            NowActions.BeginAnimation(HeightProperty, null);
            NowActions.Height = open ? ActionsHeight : 0;
            NowActions.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            NowPrimaryButton.RenderTransform = new ScaleTransform(1, 1);
            NowSecondaryButton.RenderTransform = new ScaleTransform(1, 1);
            return;
        }

        if (open)
        {
            NowActions.Visibility = Visibility.Visible;
            NowActions.Animate(HeightProperty, ActionsHeight, SakuraMotion.Emphasized, SakuraMotion.SpringCurve);
            Inflate(NowPrimaryButton, TimeSpan.FromMilliseconds(40));
            Inflate(NowSecondaryButton, TimeSpan.FromMilliseconds(90));
            return;
        }

        NowActions.Animate(
            HeightProperty,
            0,
            SakuraMotion.Exit,
            SakuraMotion.AccelerateCurve,
            completed: () =>
            {
                if (!_actionsOpen)
                {
                    NowActions.Visibility = Visibility.Collapsed;
                }
            });
    }

    private static void Inflate(FrameworkElement button, TimeSpan begin)
    {
        var scale = new ScaleTransform(0.6, 0.6);
        button.RenderTransform = scale;
        button.Opacity = 0;
        button.Animate(OpacityProperty, 1, SakuraMotion.Fast, SakuraMotion.DecelerateCurve, begin);
        scale.AnimateTransform(ScaleTransform.ScaleXProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve, begin);
        scale.AnimateTransform(ScaleTransform.ScaleYProperty, 1, SakuraMotion.Emphasized, SakuraMotion.SpringCurve, begin);
    }

    private void NowPrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_now.Kind)
        {
            case HomeNowKind.Task when _now.TaskId is { } taskId:
                StartTaskFocusRequested?.Invoke(this, new TaskFocusRequestedEventArgs(taskId, _now.Title));
                break;
            case HomeNowKind.Focus when _now.IsPaused:
                ResumeFocusRequested?.Invoke(this, EventArgs.Empty);
                break;
            case HomeNowKind.Focus:
                PauseFocusRequested?.Invoke(this, EventArgs.Empty);
                break;
        }

        SetActionsOpen(false, animate: true);
    }

    private void NowSecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_now.Kind)
        {
            case HomeNowKind.Task when _now.TaskId is { } taskId:
                PostponeTaskRequested?.Invoke(this, new TaskFocusRequestedEventArgs(taskId, _now.Title));
                break;
            case HomeNowKind.Focus:
                FinishFocusRequested?.Invoke(this, EventArgs.Empty);
                break;
        }

        SetActionsOpen(false, animate: true);
    }

    private void YesterdayBubble_Click(object sender, RoutedEventArgs e) =>
        SetYesterdayOpen(!_yesterdayOpen, animate: true);

    private void SetYesterdayOpen(bool open, bool animate)
    {
        _yesterdayOpen = open;
        YesterdayPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetItemStatus(YesterdayBubble, open ? "Resumen abierto" : "Resumen cerrado");

        if (animate)
        {
            YesterdayChevronRotation.AnimateTransform(RotateTransform.AngleProperty, open ? 90 : 0, SakuraMotion.Base, SakuraMotion.StandardCurve);
            if (open)
            {
                EntranceMotion.Rise(YesterdayPanel, TimeSpan.Zero, offset: 6);
            }
        }
        else
        {
            YesterdayChevronRotation.BeginAnimation(RotateTransform.AngleProperty, null);
            YesterdayChevronRotation.Angle = open ? 90 : 0;
        }
    }

    private void CommandButton_Click(object sender, RoutedEventArgs e) =>
        CommandRequested?.Invoke(this, EventArgs.Empty);

    private void DoneBubble_Click(object sender, RoutedEventArgs e) =>
        TasksRequested?.Invoke(this, EventArgs.Empty);

    private void FocusBubble_Click(object sender, RoutedEventArgs e) =>
        FocusRequested?.Invoke(this, EventArgs.Empty);
}

public sealed record HomeDashboardViewModel(
    string Greeting,
    string GreetingDetail,
    string TaskValue,
    string TaskDetail,
    string FocusValue,
    string FocusDetail,
    bool FocusHasActiveSession,
    bool FocusIsPaused,
    string RoutineValue,
    string RoutineDetail,
    string ContextTitle,
    string ContextDetail);

public sealed record HomeRecentAction(
    string Title,
    string Detail,
    string Time);
