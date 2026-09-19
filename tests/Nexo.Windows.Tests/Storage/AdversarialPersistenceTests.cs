using System.IO;
using Nexo.Core.Focus;
using Nexo.Core.Storage;
using Nexo.Core.Tasks;
using Nexo.Windows.Focus;
using Nexo.Windows.Tasks;
using Xunit;

namespace Nexo.Windows.Tests.Storage;

/// <summary>
/// Pruebas adversarias del bloque «Hoy y Enfoque no se pierden». Las marcadas [FALLA] demuestran un
/// defecto real; las marcadas [GUARDA] fijan que la protección central aguanta de punta a punta con
/// los almacenes reales (no con fakes).
/// </summary>
public sealed class AdversarialPersistenceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sakura-adv-" + Guid.NewGuid().ToString("N"));

    public AdversarialPersistenceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string TasksPath => Path.Combine(_root, "tasks.json");
    private string FocusPath => Path.Combine(_root, "focus.json");

    // ---------- [FALLA] JSON válido con forma inesperada rompe el arranque ----------

    [Fact]
    public void FALLA_TasksJson_WithANullElement_DoesNotCrashTheManager()
    {
        File.WriteAllText(TasksPath, "[null]");
        var manager = new TaskManager(new JsonTaskStore(TasksPath));

        var exception = Record.Exception(manager.Load);

        Assert.Null(exception);
    }

    [Fact]
    public void FALLA_FocusJson_WithANullHistoryEntry_DoesNotCrashTheManager()
    {
        File.WriteAllText(FocusPath, "{\"History\":[null]}");
        var manager = new FocusManager(new JsonFocusStore(FocusPath));

        var exception = Record.Exception(manager.Load);

        Assert.Null(exception);
    }

    // ---------- [FALLA] recuperación que dice guardar y no guarda ----------

    [Fact]
    public void FALLA_RecoverAfterRestart_ReturnsARecoveryThatIsNotInTheHistory_WhenOlderThan90Days()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new JsonFocusStore(FocusPath);
        store.Save(new FocusState
        {
            ActiveTimer = new FocusTimer
            {
                Id = Guid.NewGuid(),
                Label = "Vieja",
                Kind = FocusSessionKind.Focus,
                StartedAt = now.AddDays(-120),
                EndsAt = now.AddDays(-120).AddMinutes(25),
                Duration = TimeSpan.FromMinutes(25),
                PausedRemaining = TimeSpan.FromMinutes(25),
                Status = FocusTimerStatus.Running
            }
        });
        var manager = new FocusManager(new JsonFocusStore(FocusPath));
        manager.Load();

        var recovery = manager.RecoverAfterRestart(now);

        // La ventana le dirá a la persona «Cuento 25 minutos en tu historial».
        Assert.True(recovery is null || manager.GetHistory().Count == 1,
            "Se devolvió una recuperación pero la sesión no está en el historial (TrimHistory la descartó).");
    }

    // ---------- [FALLA] privacidad: rutas con el usuario de Windows en el motivo ----------

    [Fact]
    public void FALLA_TheWriteFailureReason_DoesNotCarryTheWindowsProfilePath()
    {
        var store = new JsonTaskStore(TasksPath);
        store.Save([new NexoTask { Title = "x" }]);
        Directory.CreateDirectory(TasksPath + ".tmp");

        var exception = Assert.Throws<SakuraDataWriteException>(
            () => store.Save([new NexoTask { Title = "y" }]));

        // MainWindow.ReportDataWriteFailed mete Reason en un mensaje del asistente; esos mensajes
        // viajan en AiChatRequest.Messages (MainWindow ~6493) y en ExportConversation.
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.DoesNotContain(profile, exception.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- [GUARDA] la protección central, con almacenes reales ----------

    [Fact]
    public void GUARDA_LockedFile_ManagerMutations_NeverTouchTheOriginal()
    {
        const string original = "[{\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Title\":\"Importante\"}]";
        File.WriteAllText(TasksPath, original);
        var manager = new TaskManager(new JsonTaskStore(TasksPath));

        using (new FileStream(TasksPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            manager.Load();
        }

        Assert.True(manager.IsPersistenceSuspended);
        var created = manager.Create("Nueva");
        manager.Complete(created.Id);
        manager.Delete(created.Id);
        manager.CollectDueReminders(DateTimeOffset.Now);

        Assert.Equal(original, File.ReadAllText(TasksPath));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public void GUARDA_CorruptFileThatCannotBeRenamed_IsNeverOverwritten()
    {
        const string original = "[{\"Title\": \"trunc";
        File.WriteAllText(TasksPath, original);
        var store = new JsonTaskStore(TasksPath);
        var manager = new TaskManager(store);

        // Legible pero no renombrable: el renombrado falla y el archivo sigue en su sitio.
        using (new FileStream(TasksPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            manager.Load();
        }

        Assert.Equal(DataLoadStatus.Corrupt, manager.LoadOutcome.Status);
        Assert.Null(manager.LoadOutcome.PreservedPath);
        manager.Create("Nueva");
        Assert.Equal(original, File.ReadAllText(TasksPath));
    }

    [Fact]
    public void GUARDA_FocusUnreadable_RunningTheTimerDoesNotTouchTheFile()
    {
        const string original = "{\"History\":[]}";
        File.WriteAllText(FocusPath, original);
        var manager = new FocusManager(new JsonFocusStore(FocusPath));

        using (new FileStream(FocusPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            manager.Load();
        }

        var now = DateTimeOffset.Now;
        manager.Start(TimeSpan.FromMinutes(5), null, FocusSessionKind.Focus, now);
        manager.Finish(now.AddMinutes(1));
        manager.CollectCompletion(now.AddHours(1));

        Assert.Equal(original, File.ReadAllText(FocusPath));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void GUARDA_ValidJsonOfAcceptableShape_IsNotAnError(string content)
    {
        File.WriteAllText(TasksPath, content);
        var store = new JsonTaskStore(TasksPath);
        store.Load();
        Assert.True(store.LastLoad.IsSafeToOverwrite);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"hola\"")]
    [InlineData("42")]
    [InlineData("   ")]
    [InlineData("")]
    public void GUARDA_WrongShapeEmptyOrBlank_IsCorruptNotOk(string content)
    {
        File.WriteAllText(TasksPath, content);
        var store = new JsonTaskStore(TasksPath);
        store.Load();
        Assert.Equal(DataLoadStatus.Corrupt, store.LastLoad.Status);
    }

    [Fact]
    public void GUARDA_FocusJsonWithAnArrayWhereAnObjectGoes_IsCorrupt()
    {
        File.WriteAllText(FocusPath, "[]");
        var store = new JsonFocusStore(FocusPath);
        store.Load();
        Assert.Equal(DataLoadStatus.Corrupt, store.LastLoad.Status);
    }

    [Fact]
    public void GUARDA_MissingFolder_IsMissing_NotAnError()
    {
        var store = new JsonTaskStore(Path.Combine(_root, "no", "existe", "tasks.json"));
        store.Load();
        Assert.Equal(DataLoadStatus.Missing, store.LastLoad.Status);
        Assert.True(store.LastLoad.IsSafeToOverwrite);
    }

    [Fact]
    public void GUARDA_ReloadAfterUnlock_ReactivatesSaving_AndDropsMemoryWork()
    {
        const string original = "[{\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Title\":\"Importante\"}]";
        File.WriteAllText(TasksPath, original);
        var manager = new TaskManager(new JsonTaskStore(TasksPath));
        using (new FileStream(TasksPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            manager.Load();
        }

        manager.Create("Solo en memoria");
        manager.Load();

        Assert.False(manager.IsPersistenceSuspended);
        Assert.Single(manager.GetAll());
        Assert.Equal("Importante", manager.GetAll()[0].Title);
    }

    [Fact]
    public void GUARDA_RecoverAfterRestart_TwiceDoesNotDuplicate_AndEndsAtInTheFutureStays()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var store = new JsonFocusStore(FocusPath);
        store.Save(new FocusState
        {
            ActiveTimer = new FocusTimer
            {
                Id = Guid.NewGuid(),
                Label = "Vencida",
                Kind = FocusSessionKind.Focus,
                StartedAt = now.AddHours(-2),
                EndsAt = now.AddHours(-2).AddMinutes(25),
                Duration = TimeSpan.FromMinutes(25),
                PausedRemaining = TimeSpan.FromMinutes(25),
                Status = FocusTimerStatus.Running
            }
        });

        var first = new FocusManager(new JsonFocusStore(FocusPath));
        first.Load();
        Assert.NotNull(first.RecoverAfterRestart(now));

        var second = new FocusManager(new JsonFocusStore(FocusPath));
        second.Load();
        Assert.Null(second.RecoverAfterRestart(now));
        Assert.Single(second.GetHistory());

        // Reloj movido hacia atrás: EndsAt queda en el futuro y no se cierra.
        var third = new FocusManager(new JsonFocusStore(FocusPath));
        third.Load();
        third.Start(TimeSpan.FromMinutes(25), null, FocusSessionKind.Focus, now);
        var fourth = new FocusManager(new JsonFocusStore(FocusPath));
        fourth.Load();
        Assert.Null(fourth.RecoverAfterRestart(now.AddHours(-5)));
        Assert.NotNull(fourth.GetSnapshot(now).ActiveTimer);
    }

    [Fact]
    public void GUARDA_UnreadableButRenamableFile_IsNotSetAside()
    {
        // Un bloqueo que impide leer pero deja renombrar (FileShare.Delete). Es el único caso en que
        // la prueba con FileShare.None NO detecta una regresión: ahí el renombrado falla solo.
        File.WriteAllText(TasksPath, "[]");
        var store = new JsonTaskStore(TasksPath);

        using (new FileStream(TasksPath, FileMode.Open, FileAccess.Write, FileShare.Delete))
        {
            store.Load();
        }

        Assert.Equal(DataLoadStatus.Unreadable, store.LastLoad.Status);
        Assert.True(File.Exists(TasksPath), "El archivo ilegible se apartó (renombró) en vez de dejarlo.");
    }
}
