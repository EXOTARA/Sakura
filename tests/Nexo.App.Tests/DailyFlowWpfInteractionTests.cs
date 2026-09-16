using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Nexo.App.DailyFlow;
using Nexo.App.Views;
using Nexo.Core.Automation;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D3 — comportamiento real en WPF de las cuatro vistas del flujo diario, con managers
/// reales sobre almacenes en memoria (mismo patrón que <c>Nexo.Core.Tests</c>). Ninguna prueba
/// aquí pulsa un botón "Eliminar": esos ya muestran <see cref="MessageBox"/> real (confirmación),
/// que bloquearía la prueba esperando una respuesta que nunca llega. Esa lógica de confirmación
/// se prueba indirectamente comprobando que el manejador existe y que el manager subyacente
/// (<c>TaskManager.Delete</c>/<c>RoutineManager.Delete</c>) ya está cubierto en
/// <c>Nexo.Core.Tests</c>.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class DailyFlowWpfInteractionTests
{
    private readonly StaWpfFixture _fixture;

    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 25, 10, 0, 0, TimeSpan.FromHours(-6));

    public DailyFlowWpfInteractionTests(StaWpfFixture fixture) => _fixture = fixture;

    private static OffscreenHost CreateOffscreenHost(FrameworkElement content) => new(content);

    private sealed class OffscreenHost(FrameworkElement content) : IDisposable
    {
        public Window Window { get; } = new()
        {
            Width = 420,
            Height = 700,
            Left = -6000,
            Top = -6000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = content
        };

        public void Show() => Window.Show();

        public void UpdateLayout() => Window.UpdateLayout();

        public void Dispose() => Window.Close();
    }

    // ---------- Hoy ----------

    [Fact]
    public void TasksView_OpenNewEditor_ShowsEditorAndFocusesTitle()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.OpenNewEditor();
            host.UpdateLayout();

            var editor = (FrameworkElement)view.FindName("EditorBorder")!;
            Assert.Equal(Visibility.Visible, editor.Visibility);
        });
    }

    [Fact]
    public void TasksView_ReopenButton_ReturnsACompletedTaskToPending()
    {
        _fixture.Invoke(() =>
        {
            var store = new FakeTaskStore();
            var manager = new TaskManager(store);
            manager.Load();
            var task = manager.Create("Tarea de prueba");
            manager.Complete(task.Id);

            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeFilterButton(view, TodaySectionKind.Done);
            host.UpdateLayout();
            var reopenButton = FindButtonByAutomationName(view, "Reabrir tarea");
            Assert.NotNull(reopenButton);

            reopenButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            host.UpdateLayout();

            Assert.False(manager.GetAll().Single().IsCompleted);
        });
    }

    [Fact]
    public void TasksView_FocusButton_RaisesFocusRequestedWithTheTaskIdentity()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            // Solo lo importante ofrece enfocarse, y se elige cuánto rato.
            var task = manager.Create("Escribir el informe", priority: TaskPriority.High);

            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            TaskFocusRequestedEventArgs? received = null;
            view.FocusRequested += (_, args) => received = args;

            var focusButton = FindButtonByAutomationName(view, "Enfocarme en esta tarea");
            Assert.NotNull(focusButton);
            Assert.Equal(Visibility.Visible, focusButton!.Visibility);
            focusButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            var fiveMinutes = (MenuItem)focusButton.ContextMenu!.Items[0];
            fiveMinutes.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            focusButton.ContextMenu.IsOpen = false;

            Assert.NotNull(received);
            Assert.Equal(task.Id, received!.TaskId);
            Assert.Equal(5, received.Minutes);
        });
    }

    [Fact]
    public void TasksView_IconButtons_AllHaveAccessibleNames()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            manager.Create("Tarea accesible");

            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeFilterButton(view, TodaySectionKind.Later);
            host.UpdateLayout();

            foreach (var name in new[] { "Marcar como completada", "Editar tarea", "Enfocarme en esta tarea", "Dejar para mañana", "Soltar tarea" })
            {
                Assert.NotNull(FindButtonByAutomationName(view, name));
            }
        });
    }

    [Fact]
    public void TasksView_TypingALine_CreatesATaskForToday()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var box = (TextBox)view.FindName("CaptureTextBox")!;
            box.Text = "Comprar tinta !";
            box.RaiseEvent(new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(box)!,
                0,
                System.Windows.Input.Key.Enter) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });

            var task = Assert.Single(manager.GetAll());
            Assert.Equal("Comprar tinta", task.Title);
            Assert.Equal(TaskPriority.High, task.Priority);
            Assert.Equal(DateTime.Today, task.DueAt!.Value.Date);
            Assert.Equal(string.Empty, box.Text);
        });
    }

    [Fact]
    public void TasksView_Release_HidesTheTaskAndCanBeUndone()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            manager.Create("Leer capítulo 3", dueAt: DateTimeOffset.Now);
            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            FindButtonByAutomationName(view, "Soltar tarea")!
                .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Empty(manager.GetAll());
            Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("UndoBar")!).Visibility);

            InvokeButtonByContent(view, "Deshacer");
            Assert.Single(manager.GetAll());
        });
    }

    [Fact]
    public void TasksView_AnEmptyDay_InvitesToWriteSomething()
    {
        _fixture.Invoke(() =>
        {
            var manager = new TaskManager(new FakeTaskStore());
            manager.Load();
            var view = new TasksView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("EmptyStatePanel")!).Visibility);
            Assert.Equal("Hoy está libre", ((TextBlock)view.FindName("EmptyStateTitle")!).Text);
        });
    }

    // ---------- Enfoque ----------

    [Fact]
    public void FocusView_StartingAPreset_ShowsARunningSession()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            view.Refresh(ReferenceNow);
            host.UpdateLayout();

            var state = (TextBlock)view.FindName("TimerStateText")!;
            Assert.Equal("EN CURSO", state.Text);
        });
    }

    [Fact]
    public void FocusView_PrepareTaskAssociation_ThenStarting_ShowsTheAssociatedTaskBadge()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var taskId = Guid.NewGuid();
            var view = new FocusView(manager, id => id == taskId ? "Preparar la demo" : null);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.PrepareTaskAssociation(taskId, "Preparar la demo");
            var custom = (TextBox)view.FindName("CustomMinutesTextBox")!;
            custom.Text = "20";
            InvokeButtonByContent(view, "Iniciar");
            host.UpdateLayout();

            var badge = (FrameworkElement)view.FindName("AssociatedTaskBadge")!;
            var badgeText = (TextBlock)view.FindName("AssociatedTaskText")!;
            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Contains("Preparar la demo", badgeText.Text);
        });
    }

    [Fact]
    public void FocusView_FinishButton_RecordsHistory_DistinctFromCancel()
    {
        _fixture.Invoke(() =>
        {
            var store = new FakeFocusStore();
            var manager = new FocusManager(store);
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(30), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);

            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeButtonByName(view, "FinishButton");
            host.UpdateLayout();

            Assert.Null(manager.GetSnapshot(ReferenceNow).ActiveTimer);
        });
    }

    [Fact]
    public void FocusView_CompletionNotice_DismissHidesItWithoutCompletingAnything()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var taskId = Guid.NewGuid();
            var view = new FocusView(manager, _ => "Tarea pendiente");
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.ShowSessionCompletionNotice(new FocusCompletion(
                "Sesión de enfoque", FocusSessionKind.Focus, TimeSpan.FromMinutes(25), ReferenceNow, taskId));
            host.UpdateLayout();

            var noticeBorder = (FrameworkElement)view.FindName("SessionCompletionNoticeBorder")!;
            Assert.Equal(Visibility.Visible, noticeBorder.Visibility);

            var dismissButton = FindButtonByAutomationName(view, "Cerrar aviso de sesión completada");
            Assert.NotNull(dismissButton);
            dismissButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            host.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, noticeBorder.Visibility);
        });
    }

    [Fact]
    public void FocusView_CompletionNotice_CompleteButton_RaisesTheRequestWithoutActingOnItsOwn()
    {
        // La vista nunca completa la tarea por su cuenta: solo pide, quien construya la vista
        // decide qué hacer (aquí, MainWindow llama a TaskManager.Complete).
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var taskId = Guid.NewGuid();
            var view = new FocusView(manager, _ => "Escribir el informe");
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.ShowSessionCompletionNotice(new FocusCompletion(
                "Sesión de enfoque", FocusSessionKind.Focus, TimeSpan.FromMinutes(25), ReferenceNow, taskId));
            host.UpdateLayout();

            TaskFocusRequestedEventArgs? received = null;
            view.CompleteAssociatedTaskRequested += (_, args) => received = args;

            var completeButton = FindButtonByAutomationName(view, "Marcar tarea asociada como completada");
            Assert.NotNull(completeButton);
            completeButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.NotNull(received);
            Assert.Equal(taskId, received!.TaskId);
        });
    }

    [Fact]
    public void FocusView_CompletionNotice_WithoutTask_HidesCompleteButtonButStillOffersStartAnother()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            view.ShowSessionCompletionNotice(new FocusCompletion(
                "Sesión de enfoque", FocusSessionKind.Focus, TimeSpan.FromMinutes(10), ReferenceNow));
            host.UpdateLayout();

            var noticeBorder = (FrameworkElement)view.FindName("SessionCompletionNoticeBorder")!;
            Assert.Equal(Visibility.Visible, noticeBorder.Visibility);

            var completeButton = (FrameworkElement)view.FindName("CompleteAssociatedTaskButton")!;
            Assert.Equal(Visibility.Collapsed, completeButton.Visibility);

            var startAnotherButton = FindButtonByAutomationName(view, "Iniciar otra sesión de enfoque");
            Assert.NotNull(startAnotherButton);
        });
    }

    // ---------- Enfoque: historial y resumen ----------

    [Fact]
    public void FocusView_WithoutAnyHistory_ShowsAnHonestEmptyState()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var emptyText = (FrameworkElement)view.FindName("RecentSessionsEmptyText")!;
            var topTaskText = (FrameworkElement)view.FindName("TopTaskText")!;
            Assert.Equal(Visibility.Visible, emptyText.Visibility);
            Assert.Equal(Visibility.Collapsed, topTaskText.Visibility);
        });
    }

    [Fact]
    public void FocusView_AfterFinishing_ShowsTheSessionInRecentActivity()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(30), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeButtonByName(view, "FinishButton");
            host.UpdateLayout();

            var emptyText = (FrameworkElement)view.FindName("RecentSessionsEmptyText")!;
            Assert.Equal(Visibility.Collapsed, emptyText.Visibility);

            var items = (ItemsControl)view.FindName("RecentSessionsItemsControl")!;
            Assert.Single((System.Collections.IEnumerable)items.ItemsSource);
        });
    }

    [Fact]
    public void FocusView_CancelledSessions_NeverAppearInRecentActivity()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(30), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            var view = new FocusView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeButtonByName(view, "CancelButton");
            host.UpdateLayout();

            var emptyText = (FrameworkElement)view.FindName("RecentSessionsEmptyText")!;
            Assert.Equal(Visibility.Visible, emptyText.Visibility);
        });
    }

    [Fact]
    public void FocusView_WithATaskFocusedTodayForAWhile_ShowsItAsTheTopTask()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var taskId = Guid.NewGuid();
            manager.Start(TimeSpan.FromMinutes(30), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow, taskId);
            var view = new FocusView(manager, id => id == taskId ? "Escribir el informe" : null);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            InvokeButtonByName(view, "FinishButton");
            host.UpdateLayout();

            var topTaskText = (TextBlock)view.FindName("TopTaskText")!;
            Assert.Equal(Visibility.Visible, topTaskText.Visibility);
            Assert.Contains("Escribir el informe", topTaskText.Text);
        });
    }

    // ---------- Enfoque: mini temporizador global ----------

    [Fact]
    public void FocusMiniTimer_Apply_NoSession_CollapsesTheControl()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();

            control.Apply(NoSessionState());
            host.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, control.Visibility);
        });
    }

    [Fact]
    public void FocusMiniTimer_Apply_ActiveSession_ShowsClockAndLabelAndIsVisible()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();

            control.Apply(SessionState(isPaused: false, label: "Escribir el informe", clockText: "12:34"));
            host.UpdateLayout();

            Assert.Equal(Visibility.Visible, control.Visibility);
            var labelText = (TextBlock)control.FindName("LabelText")!;
            var clockText = (TextBlock)control.FindName("ClockText")!;
            var pausedBadge = (FrameworkElement)control.FindName("PausedBadge")!;
            Assert.Equal("Escribir el informe", labelText.Text);
            Assert.Equal("12:34", clockText.Text);
            Assert.Equal(Visibility.Collapsed, pausedBadge.Visibility);
        });
    }

    [Fact]
    public void FocusMiniTimer_Apply_PausedSession_ShowsThePausedBadge()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();

            control.Apply(SessionState(isPaused: true, label: "Enfoque", clockText: "05:00"));
            host.UpdateLayout();

            var pausedBadge = (FrameworkElement)control.FindName("PausedBadge")!;
            Assert.Equal(Visibility.Visible, pausedBadge.Visibility);
        });
    }

    [Fact]
    public void FocusMiniTimer_OpenButton_RaisesOpenRequested()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();
            control.Apply(SessionState(isPaused: false, label: "Enfoque", clockText: "10:00"));
            host.UpdateLayout();

            var raised = false;
            control.OpenRequested += (_, _) => raised = true;
            InvokeButtonByName(control, "OpenButton");

            Assert.True(raised);
        });
    }

    [Fact]
    public void FocusMiniTimer_PauseResumeButton_RaisesPauseWhenRunning()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();
            control.Apply(SessionState(isPaused: false, label: "Enfoque", clockText: "10:00"));
            host.UpdateLayout();

            var pauseRaised = false;
            var resumeRaised = false;
            control.PauseRequested += (_, _) => pauseRaised = true;
            control.ResumeRequested += (_, _) => resumeRaised = true;
            InvokeButtonByName(control, "PauseResumeButton");

            Assert.True(pauseRaised);
            Assert.False(resumeRaised);
        });
    }

    [Fact]
    public void FocusMiniTimer_PauseResumeButton_RaisesResumeWhenPaused()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();
            control.Apply(SessionState(isPaused: true, label: "Enfoque", clockText: "10:00"));
            host.UpdateLayout();

            var pauseRaised = false;
            var resumeRaised = false;
            control.PauseRequested += (_, _) => pauseRaised = true;
            control.ResumeRequested += (_, _) => resumeRaised = true;
            InvokeButtonByName(control, "PauseResumeButton");

            Assert.True(resumeRaised);
            Assert.False(pauseRaised);
        });
    }

    [Fact]
    public void FocusMiniTimer_FinishButton_RaisesFinishRequested()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();
            control.Apply(SessionState(isPaused: false, label: "Enfoque", clockText: "10:00"));
            host.UpdateLayout();

            var raised = false;
            control.FinishRequested += (_, _) => raised = true;
            InvokeButtonByName(control, "FinishButton");

            Assert.True(raised);
        });
    }

    [Fact]
    public void FocusMiniTimer_NeverExposesAOneClickCancelControl()
    {
        // Diseño D3.1: Cancelar no debe ser una acción de un solo clic en el mini temporizador. La
        // forma más simple de garantizarlo es que el control ni siquiera tenga un botón para eso;
        // Cancelar sigue disponible en FocusView y el Command Center.
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();
            host.UpdateLayout();
            control.Apply(SessionState(isPaused: false, label: "Enfoque", clockText: "10:00"));
            host.UpdateLayout();

            var hasCancelButton = FindDescendants<Button>(control).Any(button =>
                (AutomationProperties.GetName(button)?.Contains("ancelar", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (button.Content as string)?.Contains("ancelar", StringComparison.OrdinalIgnoreCase) == true);

            Assert.False(hasCancelButton);
        });
    }

    [Fact]
    public void FocusMiniTimer_SurvivesMeasureArrangeUpdateLayout_WithoutThrowing()
    {
        _fixture.Invoke(() =>
        {
            var control = new FocusMiniTimer();
            using var host = CreateOffscreenHost(control);
            host.Show();

            control.Apply(SessionState(isPaused: false, label: "Enfoque", clockText: "10:00"));
            control.Measure(new Size(300, 60));
            control.Arrange(new Rect(0, 0, 300, 60));
            host.UpdateLayout();

            Assert.True(control.IsMeasureValid);
            Assert.True(control.IsArrangeValid);
        });
    }

    [Fact]
    public void FocusContinuityCoordinator_Refresh_NoSession_CollapsesTheMiniTimer()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            var miniTimer = new FocusMiniTimer();
            using var host = CreateOffscreenHost(miniTimer);
            host.Show();
            host.UpdateLayout();

            var coordinator = new FocusContinuityCoordinator(manager, miniTimer, _ => null, () => { });
            coordinator.Refresh(ReferenceNow);
            host.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, miniTimer.Visibility);
        });
    }

    [Fact]
    public void FocusContinuityCoordinator_Refresh_ActiveSession_ShowsTheRealRemainingClock()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            var miniTimer = new FocusMiniTimer();
            using var host = CreateOffscreenHost(miniTimer);
            host.Show();
            host.UpdateLayout();

            var coordinator = new FocusContinuityCoordinator(manager, miniTimer, _ => null, () => { });
            coordinator.Refresh(ReferenceNow.AddMinutes(5));
            host.UpdateLayout();

            Assert.Equal(Visibility.Visible, miniTimer.Visibility);
            var clockText = (TextBlock)miniTimer.FindName("ClockText")!;
            Assert.Equal("20:00", clockText.Text);
        });
    }

    [Fact]
    public void FocusContinuityCoordinator_PauseRequestedFromTheControl_PausesTheRealFocusManager()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            var miniTimer = new FocusMiniTimer();
            using var host = CreateOffscreenHost(miniTimer);
            host.Show();
            host.UpdateLayout();
            var coordinator = new FocusContinuityCoordinator(manager, miniTimer, _ => null, () => { });
            coordinator.Refresh(ReferenceNow);
            host.UpdateLayout();

            InvokeButtonByName(miniTimer, "PauseResumeButton");

            Assert.Equal(FocusTimerStatus.Paused, manager.GetSnapshot(ReferenceNow).ActiveTimer?.Status);
        });
    }

    [Fact]
    public void FocusContinuityCoordinator_ResumeRequestedFromTheControl_ResumesTheRealFocusManager()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            manager.Pause(ReferenceNow);
            var miniTimer = new FocusMiniTimer();
            using var host = CreateOffscreenHost(miniTimer);
            host.Show();
            host.UpdateLayout();
            var coordinator = new FocusContinuityCoordinator(manager, miniTimer, _ => null, () => { });
            coordinator.Refresh(ReferenceNow);
            host.UpdateLayout();

            InvokeButtonByName(miniTimer, "PauseResumeButton");

            Assert.Equal(FocusTimerStatus.Running, manager.GetSnapshot(ReferenceNow).ActiveTimer?.Status);
        });
    }

    [Fact]
    public void FocusContinuityCoordinator_OpenRequestedFromTheControl_InvokesTheOpenFocusCallback()
    {
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            var miniTimer = new FocusMiniTimer();
            using var host = CreateOffscreenHost(miniTimer);
            host.Show();
            host.UpdateLayout();
            var openRequested = false;
            var coordinator = new FocusContinuityCoordinator(manager, miniTimer, _ => null, () => openRequested = true);
            coordinator.Refresh(ReferenceNow);
            host.UpdateLayout();

            InvokeButtonByName(miniTimer, "OpenButton");

            Assert.True(openRequested);
        });
    }

    [Fact]
    public void FocusContinuityCoordinator_FinishRequestedFromTheControl_BubblesTheEvent_WithoutFinishingItself()
    {
        // El coordinador no decide cómo finalizar (eso implica capturar la tarea asociada y
        // ofrecer completarla, lógica que ya vive en MainWindow.FinishActiveFocusSession): solo
        // reenvía la intención de quien construya el mini temporizador.
        _fixture.Invoke(() =>
        {
            var manager = new FocusManager(new FakeFocusStore());
            manager.Load();
            manager.Start(TimeSpan.FromMinutes(25), "Sesión de enfoque", FocusSessionKind.Focus, ReferenceNow);
            var miniTimer = new FocusMiniTimer();
            using var host = CreateOffscreenHost(miniTimer);
            host.Show();
            host.UpdateLayout();
            var coordinator = new FocusContinuityCoordinator(manager, miniTimer, _ => null, () => { });
            coordinator.Refresh(ReferenceNow);
            host.UpdateLayout();

            var finishRequested = false;
            coordinator.FinishRequested += (_, _) => finishRequested = true;
            InvokeButtonByName(miniTimer, "FinishButton");

            Assert.True(finishRequested);
            Assert.NotNull(manager.GetSnapshot(ReferenceNow).ActiveTimer);
        });
    }

    private static FocusDisplayState NoSessionState() => new(
        HasSession: false,
        IsPaused: false,
        ClockText: "00:00",
        StatusText: "No hay una sesión de enfoque activa",
        AssociatedLabel: null,
        ProgressPercent: 0,
        CanPause: false,
        CanResume: false,
        CanFinish: false,
        CanStart: true,
        FocusMinutesToday: 0,
        CompletedSessionsToday: 0);

    private static FocusDisplayState SessionState(bool isPaused, string label, string clockText) => new(
        HasSession: true,
        IsPaused: isPaused,
        ClockText: clockText,
        StatusText: isPaused ? "En pausa" : "En curso",
        AssociatedLabel: label,
        ProgressPercent: 25,
        CanPause: !isPaused,
        CanResume: isPaused,
        CanFinish: true,
        CanStart: false,
        FocusMinutesToday: 0,
        CompletedSessionsToday: 0);

    // ---------- Rutinas ----------

    [Fact]
    public void RoutinesView_ToggleButton_FlipsEnabledState()
    {
        _fixture.Invoke(() =>
        {
            var manager = new RoutineManager(new FakeRoutineStore());
            manager.Load();
            var routine = manager.GetAll().First();
            Assert.True(routine.IsEnabled);

            var view = new RoutinesView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var toggleName = $"Desactivar {routine.Name}";
            var toggleButton = FindButtonByAutomationName(view, toggleName);
            Assert.NotNull(toggleButton);
            toggleButton!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            host.UpdateLayout();

            Assert.False(manager.GetAll().Single(candidate => candidate.Id == routine.Id).IsEnabled);
        });
    }

    [Fact]
    public void RoutinesView_ShowsNeverExecutedUntilItRuns()
    {
        _fixture.Invoke(() =>
        {
            var manager = new RoutineManager(new FakeRoutineStore());
            manager.Load();
            var view = new RoutinesView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var list = (ItemsControl)view.FindName("RoutinesItemsControl")!;
            var texts = list.Items.Cast<object>()
                .Select(item => (string)item.GetType().GetProperty("LastExecutionText")!.GetValue(item)!)
                .ToArray();

            Assert.All(texts, text => Assert.Equal("Nunca ejecutada", text));
        });
    }

    [Fact]
    public void RoutinesView_AfterRecordingAnExecution_ShowsTheLastRun()
    {
        _fixture.Invoke(() =>
        {
            var manager = new RoutineManager(new FakeRoutineStore());
            manager.Load();
            var routine = manager.GetAll().First();
            manager.RecordExecution(routine.Id, ReferenceNow, succeeded: true);

            var view = new RoutinesView(manager);
            using var host = CreateOffscreenHost(view);
            host.Show();
            host.UpdateLayout();

            var list = (ItemsControl)view.FindName("RoutinesItemsControl")!;
            var item = list.Items.Cast<object>()
                .First(candidate => (Guid)candidate.GetType().GetProperty("Id")!.GetValue(candidate)! == routine.Id);
            var text = (string)item.GetType().GetProperty("LastExecutionText")!.GetValue(item)!;

            Assert.Contains("Última vez", text);
        });
    }

    // ---------- Inicio ----------

    private static HomeTodayModel TaskModel() =>
        new(
            "Buenas tardes",
            "miércoles, 16 de septiembre",
            new HomeNow(HomeNowKind.Task, "Terminar el borrador", "Importante · hoy a las 18:00", Guid.NewGuid()),
            DoneToday: 2,
            PlannedToday: 3,
            FocusMinutesToday: 45,
            Yesterday: new HomeYesterday(1, 25, [new HomeYesterdayItem("10:55", "Enfoque de 25 min")]));

    [Fact]
    public void HomeView_Refresh_ShowsTheNextThingAndTheDay()
    {
        _fixture.Invoke(() =>
        {
            var view = new HomeView();
            using var host = CreateOffscreenHost(view);
            host.Show();
            view.Refresh(TaskModel());
            host.UpdateLayout();

            Assert.Equal("Terminar el borrador", ((TextBlock)view.FindName("NowTitleText")!).Text);
            Assert.Equal("2 de 3", ((TextBlock)view.FindName("DoneText")!).Text);
            Assert.Equal("45 min", ((TextBlock)view.FindName("FocusText")!).Text);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)view.FindName("YesterdayBlock")!).Visibility);
        });
    }

    [Fact]
    public void HomeView_TheNowBubble_SplitsIntoItsTwoChoices()
    {
        _fixture.Invoke(() =>
        {
            var view = new HomeView();
            using var host = CreateOffscreenHost(view);
            host.Show();
            view.Refresh(TaskModel());
            host.UpdateLayout();

            var actions = (FrameworkElement)view.FindName("NowActions")!;
            Assert.Equal(Visibility.Collapsed, actions.Visibility);

            var bubble = FindButtonByAutomationName(view, "Ahora: Terminar el borrador. Toca para decidir");
            Assert.NotNull(bubble);
            bubble!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.Equal(Visibility.Visible, actions.Visibility);

            // Una tarea que no es importante se marca «Hecha»; «Mañana» la mueve.
            TaskFocusRequestedEventArgs? completed = null;
            TaskFocusRequestedEventArgs? postponed = null;
            view.CompleteTaskRequested += (_, e) => completed = e;
            view.PostponeTaskRequested += (_, e) => postponed = e;

            var primary = (Button)view.FindName("NowPrimaryButton")!;
            Assert.Equal("Hecha", primary.Content);
            primary.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            ((Button)view.FindName("NowSecondaryButton")!).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.Equal("Terminar el borrador", completed?.TaskTitle);
            Assert.Equal("Terminar el borrador", postponed?.TaskTitle);
        });
    }

    [Fact]
    public void HomeView_WithNothingImportant_TheBubbleAsksForATask()
    {
        _fixture.Invoke(() =>
        {
            var view = new HomeView();
            using var host = CreateOffscreenHost(view);
            host.Show();
            view.Refresh(TaskModel() with
            {
                Now = new HomeNow(HomeNowKind.Empty, "Nada importante para hoy", "Toca para añadir una tarea"),
                Yesterday = null
            });
            host.UpdateLayout();

            var asked = false;
            view.NewTaskRequested += (_, _) => asked = true;
            ((Button)view.FindName("NowBubble")!).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.True(asked);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)view.FindName("NowActions")!).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)view.FindName("YesterdayBlock")!).Visibility);
        });
    }

    [Fact]
    public void HomeView_KeepsOnlyTheLastThreeThingsThatHappened()
    {
        _fixture.Invoke(() =>
        {
            var view = new HomeView();
            foreach (var title in new[] { "Uno", "Dos", "Tres", "Cuatro" })
            {
                view.AddRecentAction(title, "detalle");
            }

            var trail = (ItemsControl)view.FindName("TrailItems")!;
            Assert.Equal(3, trail.Items.Count);
            Assert.Equal("Cuatro", ((HomeRecentAction)trail.Items[0]).Title);
        });
    }

    // ---------- Ayudantes ----------

    private static void InvokeFilterButton(TasksView view, TodaySectionKind section)
    {
        var button = FindDescendants<Button>(view).First(candidate => candidate.Tag is TodaySectionKind kind && kind == section);
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static void InvokeButtonByName(FrameworkElement root, string name)
    {
        var button = (Button)root.FindName(name)!;
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static void InvokeButtonByContent(DependencyObject root, string content)
    {
        var button = FindDescendants<Button>(root).First(candidate => (candidate.Content as string) == content);
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static Button? FindButtonByAutomationName(DependencyObject root, string automationName) =>
        FindDescendants<Button>(root).FirstOrDefault(button =>
            AutomationProperties.GetName(button) == automationName);

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class FakeTaskStore : ITaskStore
    {
        private List<NexoTask> _tasks = [];

        public IReadOnlyList<NexoTask> Load() => _tasks.Select(task => task.Copy()).ToArray();

        public void Save(IReadOnlyCollection<NexoTask> tasks) => _tasks = tasks.Select(task => task.Copy()).ToList();
    }

    private sealed class FakeFocusStore : IFocusStore
    {
        private FocusState _state = new();

        public FocusState Load() => _state.Copy();

        public void Save(FocusState state) => _state = state.Copy();
    }

    private sealed class FakeRoutineStore : IRoutineStore
    {
        private RoutineState? _state;

        public RoutineState Load() => _state?.Copy() ?? new RoutineState();

        public void Save(RoutineState state) => _state = state.Copy();
    }
}
