using Nexo.App.DailyFlow;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.App.Tests;

/// <summary>
/// 2026-09-16 — qué enseña el Inicio nuevo. La pregunta que responde es «¿qué hago ahora?», así que
/// estas pruebas fijan qué gana la burbuja «Ahora» y qué no se cuenta.
/// </summary>
public sealed class HomeNowBuilderTests
{
    // Miércoles 16 de septiembre de 2026, 15:00.
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 15, 0, 0, TimeSpan.FromHours(-6));

    private static TaskManager Tasks(params NexoTask[] tasks)
    {
        var manager = new TaskManager(new FakeTaskStore(tasks));
        manager.Load();
        return manager;
    }

    private static FocusManager Focus(FocusState? state = null)
    {
        var manager = new FocusManager(new FakeFocusStore(state));
        manager.Load();
        return manager;
    }

    [Fact]
    public void AnImportantTask_WinsOverOneThatIsOnlyDueToday()
    {
        var model = HomeNowBuilder.Build(
            Tasks(
                new NexoTask { Title = "Comprar tinta", DueAt = Now.AddHours(2) },
                new NexoTask { Title = "Terminar el borrador", Priority = TaskPriority.High, DueAt = Now.AddDays(2) }),
            Focus(),
            null,
            Now);

        Assert.Equal(HomeNowKind.Task, model.Now.Kind);
        Assert.Equal("Terminar el borrador", model.Now.Title);
        Assert.Equal("Importante · vence el viernes a las 3:00 pm", model.Now.Detail);
    }

    [Fact]
    public void ALateTask_SaysItWasLeftPending_NotThatItIsOverdue()
    {
        var model = HomeNowBuilder.Build(
            Tasks(new NexoTask { Title = "Enviar el correo", DueAt = Now.AddDays(-2) }),
            Focus(),
            null,
            Now);

        Assert.Equal("Enviar el correo", model.Now.Title);
        Assert.Equal("Quedó pendiente", model.Now.Detail);
        Assert.DoesNotContain("vencid", model.Now.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithNothingImportant_TheBubbleInvitesToAddOne()
    {
        var model = HomeNowBuilder.Build(
            Tasks(new NexoTask { Title = "Algún día", DueAt = Now.AddDays(20) }),
            Focus(),
            null,
            Now);

        Assert.Equal(HomeNowKind.Empty, model.Now.Kind);
        Assert.Equal("Toca para planear el día", model.Now.Detail);
    }

    [Fact]
    public void ARunningSession_TakesTheBubble()
    {
        var task = new NexoTask { Title = "Terminar el borrador", Priority = TaskPriority.High };
        var focus = Focus();
        Assert.True(focus.Start(TimeSpan.FromMinutes(25), task.Title, FocusSessionKind.Focus, Now.AddMinutes(-5), task.Id).Success);

        var model = HomeNowBuilder.Build(Tasks(task), focus, null, Now);

        Assert.Equal(HomeNowKind.Focus, model.Now.Kind);
        Assert.Equal("Terminar el borrador", model.Now.Title);
        Assert.Contains("quedan 20:00", model.Now.Detail);
    }

    [Fact]
    public void TheDayCount_IsDoneTodayOutOfDonePlusStillPending()
    {
        var model = HomeNowBuilder.Build(
            Tasks(
                new NexoTask { Title = "Hecha hoy", CompletedAt = Now.AddHours(-1) },
                new NexoTask { Title = "Hecha ayer", CompletedAt = Now.AddDays(-1) },
                new NexoTask { Title = "Importante", Priority = TaskPriority.High },
                new NexoTask { Title = "Para hoy", DueAt = Now.AddHours(3) },
                new NexoTask { Title = "Otro día", DueAt = Now.AddDays(5) }),
            Focus(),
            null,
            Now);

        Assert.Equal(1, model.DoneToday);
        Assert.Equal(3, model.PlannedToday);
    }

    [Fact]
    public void Yesterday_IsTheLocalThread_InOrder()
    {
        var state = new FocusState
        {
            History =
            [
                new FocusHistoryEntry
                {
                    Label = "Estudio",
                    StartedAt = Now.AddDays(-1).AddHours(-4),
                    CompletedAt = Now.AddDays(-1).AddHours(-4).AddMinutes(25),
                    Duration = TimeSpan.FromMinutes(25)
                }
            ]
        };

        var model = HomeNowBuilder.Build(
            Tasks(new NexoTask { Title = "Leer capítulo 3", CompletedAt = Now.AddDays(-1).AddHours(-2) }),
            Focus(state),
            null,
            Now);

        Assert.NotNull(model.Yesterday);
        Assert.Equal("1 tarea · 25 min de enfoque", model.Yesterday.Headline);
        Assert.Equal(["Enfoque de 25 min", "Leer capítulo 3"], model.Yesterday.Items.Select(item => item.Text));
        Assert.Equal("11:00 am", model.Yesterday.Items[0].Time);
    }

    [Fact]
    public void TheMorningReview_ListsOnlyWhatWasLeftFromBefore()
    {
        var model = HomeNowBuilder.Build(
            Tasks(
                new NexoTask { Title = "Enviar el correo", DueAt = Now.AddDays(-1) },
                new NexoTask { Title = "Para hoy", DueAt = Now.AddHours(1) },
                new NexoTask { Title = "Hecha", DueAt = Now.AddDays(-2), CompletedAt = Now }),
            Focus(),
            null,
            Now,
            includeReview: true);

        var item = Assert.Single(model.Review!);
        Assert.Equal("Enviar el correo", item.Title);
        Assert.Equal("Era para ayer", item.Detail);
        Assert.Null(HomeNowBuilder.Build(Tasks(), Focus(), null, Now).Review);
    }

    [Fact]
    public void ANewInstall_IsInvitedInsteadOfShownZeros() =>
        Assert.Equal("Tu día empieza aquí", HomeNowBuilder.Build(Tasks(), Focus(), null, Now).Now.Title);

    [Fact]
    public void AnEmptyYesterday_IsNotOffered() =>
        Assert.Null(HomeNowBuilder.Build(Tasks(), Focus(), null, Now).Yesterday);

    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(60, "1 h")]
    [InlineData(70, "1 h 10 min")]
    public void Minutes_ReadNaturally(int minutes, string expected) =>
        Assert.Equal(expected, HomeNowBuilder.Minutes(minutes));

    [Fact]
    public void TheGreeting_CarriesTheNameOnlyWhenThereIsOne()
    {
        Assert.Equal("Buenas tardes", HomeNowBuilder.Build(Tasks(), Focus(), null, Now).Greeting);
        Assert.Equal("Buenas tardes, Adler", HomeNowBuilder.Build(Tasks(), Focus(), "Adler", Now).Greeting);
    }

    private sealed class FakeTaskStore(IEnumerable<NexoTask> tasks) : ITaskStore
    {
        private List<NexoTask> _tasks = tasks.Select(task => task.Copy()).ToList();

        public IReadOnlyList<NexoTask> Load() => _tasks.Select(task => task.Copy()).ToArray();

        public void Save(IReadOnlyCollection<NexoTask> tasks) => _tasks = tasks.Select(task => task.Copy()).ToList();
    }

    private sealed class FakeFocusStore(FocusState? state) : IFocusStore
    {
        private FocusState _state = state?.Copy() ?? new FocusState();

        public FocusState Load() => _state.Copy();

        public void Save(FocusState state) => _state = state.Copy();
    }
}
