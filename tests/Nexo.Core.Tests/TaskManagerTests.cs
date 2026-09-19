using Nexo.Core.Storage;
using Nexo.Core.Tasks;

namespace Nexo.Core.Tests;

public sealed class TaskManagerTests
{
    [Fact]
    public void Create_PersistsTask()
    {
        var store = new MemoryTaskStore();
        var manager = new TaskManager(store);
        manager.Load();

        var created = manager.Create("Revisar proyecto");

        Assert.Equal("Revisar proyecto", created.Title);
        Assert.Single(store.Tasks);
    }

    [Fact]
    public void CompleteMatching_IgnoresAccentsAndCase()
    {
        var store = new MemoryTaskStore([
            new NexoTask { Title = "Revisión de matrices" }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var result = manager.CompleteMatching("revision de matrices");

        Assert.True(result.Success);
        Assert.True(manager.GetAll().Single().IsCompleted);
    }

    [Fact]
    public void DeleteMatching_RemovesTask()
    {
        var store = new MemoryTaskStore([
            new NexoTask { Title = "Comprar alcohol" }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var result = manager.DeleteMatching("comprar alcohol");

        Assert.True(result.Success);
        Assert.Empty(manager.GetAll());
    }

    [Fact]
    public void CollectDueReminders_DeliversOnlyOnce()
    {
        var now = DateTimeOffset.Now;
        var store = new MemoryTaskStore([
            new NexoTask
            {
                Title = "Entregar tarea",
                DueAt = now.AddMinutes(-1),
                ReminderEnabled = true
            }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var first = manager.CollectDueReminders(now);
        var second = manager.CollectDueReminders(now.AddMinutes(1));

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public void CollectDueReminders_SkipsCompletedTasks()
    {
        var now = DateTimeOffset.Now;
        var store = new MemoryTaskStore([
            new NexoTask
            {
                Title = "Tarea hecha",
                DueAt = now.AddMinutes(-1),
                ReminderEnabled = true,
                CompletedAt = now.AddMinutes(-2)
            }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        Assert.Empty(manager.CollectDueReminders(now));
    }

    [Fact]
    public void BuildTodaySummary_ReportsTodayAndOverdueTasks()
    {
        var now = new DateTimeOffset(
            2026, 7, 19, 12, 0, 0,
            TimeSpan.FromHours(-6));
        var store = new MemoryTaskStore([
            new NexoTask { Title = "Tarea de hoy", DueAt = now.AddHours(1) },
            new NexoTask { Title = "Tarea vencida", DueAt = now.AddDays(-1) }
        ]);
        var manager = new TaskManager(store);
        manager.Load();

        var summary = manager.BuildTodaySummary(now);

        Assert.Contains("Tarea de hoy", summary);
        Assert.Contains("vencida", summary.ToLowerInvariant());
    }

    // ---------- Diseño D3: Reopen ----------

    [Fact]
    public void Reopen_ClearsCompletedAt()
    {
        var store = new MemoryTaskStore([new NexoTask { Title = "Tarea" }]);
        var manager = new TaskManager(store);
        manager.Load();
        var id = manager.GetAll().Single().Id;
        manager.Complete(id);

        var result = manager.Reopen(id);

        Assert.True(result.Success);
        var task = manager.GetAll().Single();
        Assert.False(task.IsCompleted);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Reopen_PersistsTheChange()
    {
        var store = new MemoryTaskStore([new NexoTask { Title = "Tarea" }]);
        var manager = new TaskManager(store);
        manager.Load();
        var id = manager.GetAll().Single().Id;
        manager.Complete(id);

        manager.Reopen(id);

        Assert.False(store.Tasks.Single().IsCompleted);
    }

    [Fact]
    public void Reopen_OnAlreadyPendingTask_IsANoOpNotAFailure()
    {
        var store = new MemoryTaskStore([new NexoTask { Title = "Tarea" }]);
        var manager = new TaskManager(store);
        manager.Load();
        var id = manager.GetAll().Single().Id;

        var result = manager.Reopen(id);

        Assert.True(result.Success);
        Assert.False(manager.GetAll().Single().IsCompleted);
    }

    [Fact]
    public void Reopen_OnMissingTask_Fails()
    {
        var manager = new TaskManager(new MemoryTaskStore());
        manager.Load();

        var result = manager.Reopen(Guid.NewGuid());

        Assert.False(result.Success);
    }

    // ---------- Lectura fallida: no se escribe encima ----------

    [Theory]
    [InlineData(DataLoadStatus.Unreadable)]
    [InlineData(DataLoadStatus.Corrupt)]
    public void WhenTheFileCouldNotBeRead_NothingIsEverSavedOverIt(DataLoadStatus status)
    {
        // El defecto: la lectura fallida devolvía una lista vacía y el primer clic la escribía
        // encima de lo que la persona sí tenía. Ahora se puede trabajar, pero en memoria.
        var store = new MemoryTaskStore { Outcome = new DataLoadOutcome(status, "no se pudo leer") };
        var manager = new TaskManager(store);
        manager.Load();

        var created = manager.Create("Tarea de esta sesión");
        var completed = manager.Complete(created.Id);
        var deleted = manager.Delete(created.Id);

        Assert.True(manager.IsPersistenceSuspended);
        Assert.Equal(status, manager.LoadOutcome.Status);
        Assert.True(completed.Success);
        Assert.True(deleted.Success);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void WhenTheFileCouldNotBeRead_TheWorkStaysInMemory()
    {
        var store = new MemoryTaskStore { Outcome = DataLoadOutcome.Unreadable("bloqueado") };
        var manager = new TaskManager(store);
        manager.Load();

        manager.Create("Sigue en pantalla");

        Assert.Single(manager.GetAll());
        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData(DataLoadStatus.Ok)]
    [InlineData(DataLoadStatus.Missing)]
    public void WhenThereWasNothingToLose_SavingWorksAsBefore(DataLoadStatus status)
    {
        var store = new MemoryTaskStore { Outcome = new DataLoadOutcome(status) };
        var manager = new TaskManager(store);
        manager.Load();

        manager.Create("Normal");

        Assert.False(manager.IsPersistenceSuspended);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void ReloadingReevaluatesTheMode()
    {
        // Restaurar una copia recarga: si el archivo ya se lee, vuelve a guardarse.
        var store = new MemoryTaskStore { Outcome = DataLoadOutcome.Unreadable("bloqueado") };
        var manager = new TaskManager(store);
        manager.Load();
        Assert.True(manager.IsPersistenceSuspended);

        store.Outcome = DataLoadOutcome.Ok;
        manager.Load();
        manager.Create("Ya guarda");

        Assert.False(manager.IsPersistenceSuspended);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public void AWriteFailure_KeepsTheChangeAndWarnsOnlyOnce()
    {
        var store = new MemoryTaskStore { FailWrites = true };
        var manager = new TaskManager(store);
        manager.Load();
        var warnings = new List<DataWriteFailure>();
        manager.WriteFailed += (_, failure) => warnings.Add(failure);

        manager.Create("Uno");
        manager.Create("Dos");

        Assert.Equal(2, manager.GetAll().Count);
        Assert.Single(warnings);

        // Cuando vuelve a funcionar se rearma: el siguiente fallo vuelve a avisar.
        store.FailWrites = false;
        manager.Create("Tres");
        store.FailWrites = true;
        manager.Create("Cuatro");
        Assert.Equal(2, warnings.Count);
    }

    private sealed class MemoryTaskStore : ITaskStore
    {
        public MemoryTaskStore(IEnumerable<NexoTask>? tasks = null)
        {
            Tasks = tasks?.Select(task => task.Copy()).ToList() ?? [];
        }

        public List<NexoTask> Tasks { get; private set; }

        public DataLoadOutcome Outcome { get; set; } = DataLoadOutcome.Ok;

        public bool FailWrites { get; set; }

        public int SaveCount { get; private set; }

        public DataLoadOutcome LastLoad => Outcome;

        public IReadOnlyList<NexoTask> Load() =>
            Tasks.Select(task => task.Copy()).ToArray();

        public void Save(IReadOnlyCollection<NexoTask> tasks)
        {
            if (FailWrites)
            {
                throw new SakuraDataWriteException("tasks.json", "disco lleno");
            }

            SaveCount++;
            Tasks = tasks.Select(task => task.Copy()).ToList();
        }
    }
}
