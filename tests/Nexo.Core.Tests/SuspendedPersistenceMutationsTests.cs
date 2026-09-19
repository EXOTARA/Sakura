using System.Reflection;
using Nexo.Core.Focus;
using Nexo.Core.Storage;
using Nexo.Core.Tasks;

namespace Nexo.Core.Tests;

/// <summary>
/// Con el guardado suspendido (el archivo no se pudo leer), NINGUNA mutación pública de los gestores
/// puede llegar a escribir. La segunda mitad falla si alguien añade un método público sin decidir a
/// qué lado va: así una mutación nueva no se cuela sin cubrir.
/// </summary>
public sealed class SuspendedPersistenceMutationsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    private static readonly string[] TaskMutations =
    [
        "Create", "Update", "Complete", "Reopen", "Postpone", "MoveToDay", "Release", "Restore",
        "CompleteMatching", "Delete", "DeleteMatching", "CollectDueReminders"
    ];

    private static readonly string[] TaskNotMutations =
    [
        "Load", "GetAll", "BuildTodaySummary", "BuildPendingSummary", "get_LoadOutcome",
        "get_IsPersistenceSuspended", "add_WriteFailed", "remove_WriteFailed"
    ];

    private static readonly string[] FocusMutations =
    [
        "Start", "Pause", "Resume", "Cancel", "Finish", "CollectCompletion", "RecoverAfterRestart"
    ];

    private static readonly string[] FocusNotMutations =
    [
        "Load", "GetSnapshot", "GetHistory", "BuildStatus", "get_LoadOutcome",
        "get_IsPersistenceSuspended", "add_WriteFailed", "remove_WriteFailed"
    ];

    [Fact]
    public void EveryPublicMethodOfTheManagersIsClassified()
    {
        AssertClassified(typeof(TaskManager), TaskMutations, TaskNotMutations);
        AssertClassified(typeof(FocusManager), FocusMutations, FocusNotMutations);
    }

    private static void AssertClassified(Type type, string[] mutations, string[] notMutations)
    {
        var names = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Distinct();

        foreach (var name in names)
        {
            Assert.True(
                mutations.Contains(name) || notMutations.Contains(name),
                $"{type.Name}.{name} es público y no está clasificado: si escribe, añádelo a la lista " +
                "de mutaciones y a la prueba de guardado suspendido.");
        }
    }

    [Fact]
    public void TaskMutations_NeverSaveWhileSuspended()
    {
        var id = Guid.NewGuid();
        var store = new SuspendedTaskStore(new NexoTask { Id = id, Title = "Revisar matrices", DueAt = Now.AddDays(1) });
        var manager = new TaskManager(store);
        manager.Load();
        Assert.True(manager.IsPersistenceSuspended);

        manager.Create("Nueva");
        manager.Update(new NexoTask { Id = id, Title = "Cambiada" });
        manager.Complete(id);
        manager.Reopen(id);
        manager.Postpone(id, Now);
        manager.MoveToDay(id, DateOnly.FromDateTime(Now.DateTime), Now);
        manager.Release(id, Now);
        manager.Restore(id);
        manager.CompleteMatching("cambiada");
        manager.Delete(id);
        manager.DeleteMatching("nueva");
        manager.CollectDueReminders(Now.AddDays(2));

        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void FocusMutations_NeverSaveWhileSuspended()
    {
        var store = new SuspendedFocusStore();
        var manager = new FocusManager(store);
        manager.Load();
        Assert.True(manager.IsPersistenceSuspended);

        manager.Start(TimeSpan.FromMinutes(5), null, FocusSessionKind.Focus, Now);
        manager.Pause(Now.AddMinutes(1));
        manager.Resume(Now.AddMinutes(2));
        manager.Finish(Now.AddMinutes(3));
        manager.Start(TimeSpan.FromMinutes(5), null, FocusSessionKind.Focus, Now);
        manager.CollectCompletion(Now.AddHours(1));
        manager.Start(TimeSpan.FromMinutes(5), null, FocusSessionKind.Focus, Now);
        manager.RecoverAfterRestart(Now.AddHours(1));
        manager.Start(TimeSpan.FromMinutes(5), null, FocusSessionKind.Focus, Now);
        manager.Cancel();

        Assert.Equal(0, store.SaveCount);
    }

    private sealed class SuspendedTaskStore(NexoTask seed) : ITaskStore
    {
        public int SaveCount { get; private set; }

        public DataLoadOutcome LastLoad => DataLoadOutcome.Unreadable("bloqueado");

        public IReadOnlyList<NexoTask> Load() => [seed.Copy()];

        public void Save(IReadOnlyCollection<NexoTask> tasks) => SaveCount++;
    }

    private sealed class SuspendedFocusStore : IFocusStore
    {
        public int SaveCount { get; private set; }

        public DataLoadOutcome LastLoad => DataLoadOutcome.Corrupt("dañado", null);

        public FocusState Load() => new();

        public void Save(FocusState state) => SaveCount++;
    }
}
