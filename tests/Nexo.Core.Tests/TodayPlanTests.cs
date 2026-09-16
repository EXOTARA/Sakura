using Nexo.Core.Tasks;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class TodayPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 15, 0, 0, TimeSpan.FromHours(-6));
    private static readonly DateOnly Today = new(2026, 9, 16);

    private static IReadOnlyList<string> Titles(IReadOnlyList<TodaySection> sections, TodaySectionKind kind) =>
        sections.Single(section => section.Kind == kind).Items.Select(item => item.Task.Title).ToList();

    [Fact]
    public void Today_SplitsImportantDayAndLater()
    {
        var sections = TodayPlan.Build(
            [
                new NexoTask { Title = "Importante sin fecha", Priority = TaskPriority.High },
                new NexoTask { Title = "Para hoy", DueAt = Now.AddHours(2) },
                new NexoTask { Title = "Atrasada", DueAt = Now.AddDays(-3) },
                new NexoTask { Title = "Sin fecha" },
                new NexoTask { Title = "Otro día", DueAt = Now.AddDays(4) },
                new NexoTask { Title = "Soltada", DueAt = Now, ArchivedAt = Now }
            ],
            Today,
            Now);

        Assert.Equal(["Importante sin fecha"], Titles(sections, TodaySectionKind.Important));
        Assert.Equal(["Atrasada", "Para hoy"], Titles(sections, TodaySectionKind.Day));
        Assert.Equal(["Otro día", "Sin fecha"], Titles(sections, TodaySectionKind.Later));
        Assert.True(sections.Single(s => s.Kind == TodaySectionKind.Day).Items[0].LeftPending);
    }

    [Fact]
    public void NoMoreThanThreeImportant()
    {
        var tasks = Enumerable.Range(1, 5)
            .Select(i => new NexoTask { Title = $"I{i}", Priority = TaskPriority.High, DueAt = Now.AddMinutes(i) })
            .ToList();

        var sections = TodayPlan.Build(tasks, Today, Now);

        Assert.Equal(3, Titles(sections, TodaySectionKind.Important).Count);
        Assert.Equal(2, Titles(sections, TodaySectionKind.Day).Count);
    }

    [Fact]
    public void AnotherDay_ShowsOnlyThatDay()
    {
        var sections = TodayPlan.Build(
            [
                new NexoTask { Title = "Viernes", DueAt = Now.AddDays(2) },
                new NexoTask { Title = "Hoy", DueAt = Now },
                new NexoTask { Title = "Atrasada", DueAt = Now.AddDays(-1) }
            ],
            Today.AddDays(2),
            Now);

        Assert.Equal(["Viernes"], Titles(sections, TodaySectionKind.Day));
        Assert.DoesNotContain(sections, section => section.Kind == TodaySectionKind.Later);
        Assert.Equal("Viernes 18 de septiembre", sections.Single(s => s.Kind == TodaySectionKind.Day).Title);
    }

    [Fact]
    public void BusyDays_AreThePendingOnes() =>
        Assert.Equal(
            [Today.AddDays(1)],
            TodayPlan.BusyDays(
            [
                new NexoTask { Title = "a", DueAt = Now.AddDays(1) },
                new NexoTask { Title = "b", DueAt = Now.AddDays(2), CompletedAt = Now },
                new NexoTask { Title = "c", DueAt = Now.AddDays(3), ArchivedAt = Now }
            ]));

    [Theory]
    [InlineData(-2, 0, "Quedó pendiente")]
    [InlineData(0, 17, "Hoy · 17:00")]
    [InlineData(1, 0, "Mañana")]
    public void Describe_ReadsCalmly(int days, int hour, string expected)
    {
        var due = new DateTimeOffset(Now.Date.AddDays(days).AddHours(hour), Now.Offset);
        Assert.Equal(expected, TodayPlan.Describe(new NexoTask { DueAt = due }, Now));
    }

    [Fact]
    public void Postpone_KeepsTheTime_AndReleaseHidesTheTask()
    {
        var manager = new TaskManager(new MemoryStore());
        manager.Load();
        var task = manager.Create("Llamar", dueAt: new DateTimeOffset(2026, 9, 16, 10, 30, 0, Now.Offset));

        manager.Postpone(task.Id, Now);
        Assert.Equal(new DateTimeOffset(2026, 9, 17, 10, 30, 0, Now.Offset), manager.GetAll().Single().DueAt);

        manager.Release(task.Id, Now);
        Assert.Empty(manager.GetAll());

        manager.Restore(task.Id);
        Assert.Single(manager.GetAll());
    }

    private sealed class MemoryStore : ITaskStore
    {
        private IReadOnlyCollection<NexoTask> _tasks = [];

        public IReadOnlyList<NexoTask> Load() => _tasks.ToList();

        public void Save(IReadOnlyCollection<NexoTask> tasks) => _tasks = tasks;
    }
}
