using Nexo.Core.Habits;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class HabitManagerTests
{
    private static readonly DateOnly Today = new(2026, 9, 16);

    private static HabitManager Manager()
    {
        var manager = new HabitManager(new MemoryStore());
        manager.Load();
        return manager;
    }

    [Fact]
    public void NotMarkingTodayYet_DoesNotBreakTheStreak() =>
        Assert.Equal(2, HabitManager.Streak([Today.AddDays(-1), Today.AddDays(-2)], Today));

    [Fact]
    public void MarkingToday_CountsIt() =>
        Assert.Equal(3, HabitManager.Streak([Today, Today.AddDays(-1), Today.AddDays(-2), Today.AddDays(-4)], Today));

    [Fact]
    public void AGap_EndsTheStreak() =>
        Assert.Equal(0, HabitManager.Streak([Today.AddDays(-2)], Today));

    [Fact]
    public void Toggle_MarksAndUnmarks()
    {
        var manager = Manager();
        Assert.True(manager.Add("Leer 10 páginas"));
        var id = manager.Today(Today).Single().Id;

        manager.Toggle(id, Today);
        Assert.True(manager.Today(Today).Single().DoneToday);

        manager.Toggle(id, Today);
        Assert.False(manager.Today(Today).Single().DoneToday);
    }

    [Fact]
    public void Add_RefusesEmptyRepeatedAndTooMany()
    {
        var manager = Manager();
        Assert.False(manager.Add("  "));
        Assert.True(manager.Add("Agua"));
        Assert.False(manager.Add("agua"));
        for (var i = 1; i < HabitManager.MaximumHabits; i++)
        {
            Assert.True(manager.Add($"Hábito {i}"));
        }

        Assert.False(manager.Add("Uno más"));
    }

    private sealed class MemoryStore : IHabitStore
    {
        private IReadOnlyCollection<Habit> _habits = [];

        public IReadOnlyList<Habit> Load() => _habits.Select(habit => habit.Copy()).ToList();

        public void Save(IReadOnlyCollection<Habit> habits) => _habits = habits;
    }
}
