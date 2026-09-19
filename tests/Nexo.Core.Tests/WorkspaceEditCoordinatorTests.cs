using Nexo.Core.Audit;
using Nexo.Core.Permissions;
using Nexo.Core.Workspace;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D14 (Fase 5, nivel 4) — es la primera vez que Sakura escribe en archivos de la persona, así
/// que lo que se prueba aquí es sobre todo cuándo se NIEGA a hacerlo y qué pasa cuando algo sale mal.
/// </summary>
public sealed class WorkspaceEditCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.FromHours(-6));

    private const string Root = @"C:\Proyectos\Sakura";

    // ---------- Dobles ----------

    private sealed class FakeWriter : IWorkspaceWriter
    {
        public Dictionary<string, string> Files { get; } = [];

        public bool FailWrite { get; set; }

        public bool FailDelete { get; set; }

        /// <summary>Simula que el disco quedó con otro contenido del pedido.</summary>
        public string? WriteInsteadOf { get; set; }

        /// <summary>Cuántas escrituras salen mal. -1 = siempre.</summary>
        public int MisbehavingWrites { get; set; } = -1;

        public List<string> Deleted { get; } = [];

        /// <summary>Simula un archivo que no se puede leer (bloqueado, sin permiso).</summary>
        public bool Unreadable { get; set; }

        public (bool Existed, bool Readable, string Content) ReadForCheckpoint(string authorizedRoot, string relativePath)
        {
            if (Unreadable)
            {
                return (false, false, string.Empty);
            }

            return Files.TryGetValue(relativePath, out var content)
                ? (true, true, content)
                : (false, true, string.Empty);
        }

        public WorkspaceStepResult WriteFile(string authorizedRoot, string relativePath, string content)
        {
            if (FailWrite)
            {
                return WorkspaceStepResult.Failed("El disco dijo que no.");
            }

            if (WriteInsteadOf is not null && MisbehavingWrites != 0)
            {
                if (MisbehavingWrites > 0)
                {
                    MisbehavingWrites--;
                }

                Files[relativePath] = WriteInsteadOf;
                return WorkspaceStepResult.Ok("Escrito.");
            }

            Files[relativePath] = content;
            return WorkspaceStepResult.Ok("Escrito.");
        }

        public WorkspaceStepResult DeleteFile(string authorizedRoot, string relativePath)
        {
            if (FailDelete)
            {
                return WorkspaceStepResult.Failed("No pude borrar.");
            }

            Files.Remove(relativePath);
            Deleted.Add(relativePath);
            return WorkspaceStepResult.Ok("Borrado.");
        }
    }

    private sealed class FakeCheckpointStore : IWorkspaceCheckpointStore
    {
        public List<WorkspaceCheckpoint> Saved { get; } = [];

        /// <summary>Simula un almacén que no consigue persistir.</summary>
        public bool DropEverything { get; set; }

        public IReadOnlyList<WorkspaceCheckpoint> Read() =>
            Saved.Select(checkpoint => checkpoint.Copy()).ToArray();

        public void Save(WorkspaceCheckpoint checkpoint)
        {
            if (!DropEverything)
            {
                Saved.Add(checkpoint.Copy());
            }
        }

        public void Remove(Guid checkpointId) => Saved.RemoveAll(entry => entry.Id == checkpointId);
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public IReadOnlyList<AuditEntry> Read() => Entries;

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    // ---------- Ayudas ----------

    private static WorkspaceSettings Settings(
        AutonomyLevel level = AutonomyLevel.EjecutarUnPaso,
        string path = Root) => new()
    {
        AuthorizedPath = path,
        AuthorizedAt = Now,
        AutonomyLevel = level
    };

    private static WorkspaceEdit Edit(string relativePath = "src\\Program.cs", string content = "clase nueva") =>
        new(relativePath, content, "Renombrar el método");

    private static (WorkspaceEditCoordinator Coordinator,
        FakeWriter Writer,
        FakeCheckpointStore Checkpoints,
        FakeAuditLog Audit) Build()
    {
        var writer = new FakeWriter();
        var checkpoints = new FakeCheckpointStore();
        var audit = new FakeAuditLog();

        return (new WorkspaceEditCoordinator(writer, checkpoints, audit, () => Now), writer, checkpoints, audit);
    }

    // ---------- El nivel de autonomía manda ----------

    [Theory]
    [InlineData(AutonomyLevel.Ver)]
    [InlineData(AutonomyLevel.Guiar)]
    [InlineData(AutonomyLevel.Proponer)]
    public void BelowLevelFour_NothingIsWritten(AutonomyLevel level)
    {
        var (coordinator, writer, checkpoints, _) = Build();

        var result = coordinator.Apply(Edit(), Settings(level));

        Assert.False(result.Success);
        Assert.Empty(writer.Files);
        Assert.Empty(checkpoints.Saved);
    }

    [Fact]
    public void WithNoAuthorizedFolder_NothingIsWritten()
    {
        var (coordinator, writer, _, _) = Build();

        var result = coordinator.Apply(Edit(), Settings(path: string.Empty));

        Assert.False(result.Success);
        Assert.Empty(writer.Files);
    }

    // ---------- La misma política que la lectura ----------

    [Theory]
    [InlineData(@"..\..\Windows\System32\algo.cs")]
    [InlineData(".env")]
    [InlineData("id_rsa")]
    [InlineData(@"node_modules\paquete\index.js")]
    [InlineData("foto.png")]
    public void PathsTheReaderWouldRefuse_AreAlsoRefusedForWriting(string relativePath)
    {
        // Que escribir tuviera sus propias reglas de contención sería la forma más fácil de que una
        // de las dos se quedara atrás.
        var (coordinator, writer, _, _) = Build();

        var result = coordinator.Apply(Edit(relativePath), Settings());

        Assert.False(result.Success);
        Assert.Empty(writer.Files);
    }

    [Fact]
    public void ContentTooLarge_IsRefused()
    {
        var (coordinator, writer, _, _) = Build();
        var huge = new string('x', (int)WorkspaceEditCoordinator.MaximumEditBytes + 1);

        Assert.False(coordinator.Apply(Edit(content: huge), Settings()).Success);
        Assert.Empty(writer.Files);
    }

    // ---------- El checkpoint va primero ----------

    [Fact]
    public void WithoutAPersistedCheckpoint_TheFileIsNotTouched()
    {
        // Sin copia previa no se escribe: es literalmente lo que el modelo de confianza exige para
        // el nivel 4.
        var (coordinator, writer, checkpoints, _) = Build();
        checkpoints.DropEverything = true;

        var result = coordinator.Apply(Edit(), Settings());

        Assert.False(result.Success);
        Assert.Contains("copia previa", result.Detail);
        Assert.Empty(writer.Files);
    }

    [Fact]
    public void TheCheckpointRemembersWhetherTheFileExisted()
    {
        var (coordinator, writer, checkpoints, _) = Build();
        writer.Files["src\\Program.cs"] = "contenido anterior";

        coordinator.Apply(Edit(), Settings());

        var checkpoint = Assert.Single(checkpoints.Saved);
        Assert.True(checkpoint.PreviousExisted);
        Assert.Equal("contenido anterior", checkpoint.PreviousContent);
    }

    [Fact]
    public void AFileThatCannotBeRead_IsNotReplaced_AndNoCheckpointIsSaved()
    {
        // «No pude leerlo» no es «no existía»: tratarlo como nuevo lo reemplazaba y, al deshacer, lo
        // borraba.
        var (coordinator, writer, checkpoints, audit) = Build();
        writer.Files["src\\Program.cs"] = "trabajo de la persona";
        writer.Unreadable = true;

        var result = coordinator.Apply(Edit(), Settings());

        Assert.False(result.Success);
        Assert.Contains("No pude leer", result.Detail);
        Assert.Equal("trabajo de la persona", writer.Files["src\\Program.cs"]);
        Assert.Empty(checkpoints.Saved);
        Assert.Contains(audit.Entries, entry => entry.Action == "Cambio no aplicado");
    }

    [Fact]
    public void RevertingWhenTheFileCannotBeRead_DoesNotClaimItIsGone()
    {
        var (coordinator, writer, checkpoints, _) = Build();
        writer.Files["src\\Program.cs"] = "antes";
        var applied = coordinator.Apply(Edit(content: "después"), Settings());
        Assert.True(applied.Success);

        writer.Unreadable = true;
        var result = coordinator.Revert(applied.CheckpointId, Settings());

        Assert.False(result.Success);
        Assert.DoesNotContain("ya no existe", result.Detail);
        Assert.Contains("No pude comprobar", result.Detail);
        Assert.Single(checkpoints.Saved);
    }

    // ---------- Camino feliz ----------

    [Fact]
    public void AConfirmedEdit_IsWritten_AndAudited()
    {
        var (coordinator, writer, _, audit) = Build();
        writer.Files["src\\Program.cs"] = "antes";

        var result = coordinator.Apply(Edit(content: "después"), Settings());

        Assert.True(result.Success);
        Assert.Equal("después", writer.Files["src\\Program.cs"]);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditCapability.Proyecto, entry.Capability);
        Assert.Equal("Archivo modificado", entry.Action);
        Assert.Equal(4, entry.AutonomyLevel);

        // El modelo de confianza exige que la entrada diga cómo revertirlo.
        Assert.True(entry.CanRevert);
        Assert.Contains("Deshacer", entry.RevertHint);
    }

    [Fact]
    public void CreatingANewFile_IsAuditedAsCreation()
    {
        var (coordinator, _, _, audit) = Build();

        coordinator.Apply(Edit(), Settings());

        Assert.Equal("Archivo creado", Assert.Single(audit.Entries).Action);
    }

    // ---------- Verificación releyendo ----------

    [Fact]
    public void IfTheFileDidNotEndUpAsAsked_TheChangeIsUndone()
    {
        var (coordinator, writer, checkpoints, _) = Build();
        writer.Files["src\\Program.cs"] = "antes";
        writer.WriteInsteadOf = "otra cosa";
        writer.MisbehavingWrites = 1;

        var result = coordinator.Apply(Edit(content: "después"), Settings());

        Assert.False(result.Success);
        Assert.Contains("lo dejé como estaba", result.Detail);

        // Un archivo a medio escribir es peor que uno sin tocar.
        Assert.Equal("antes", writer.Files["src\\Program.cs"]);
        Assert.Empty(checkpoints.Saved);
    }

    [Fact]
    public void IfTheRollbackAlsoFails_SakuraSaysSoInsteadOfClaimingItIsFine()
    {
        // "Lo dejé como estaba" es justo la frase que no puede decirse a la ligera: si la reversión
        // tampoco quedó bien, la persona necesita saberlo para mirar el archivo ella misma.
        var (coordinator, writer, _, _) = Build();
        writer.Files["src\\Program.cs"] = "antes";
        writer.WriteInsteadOf = "otra cosa";

        var result = coordinator.Apply(Edit(content: "después"), Settings());

        Assert.False(result.Success);
        Assert.Contains("Revísalo", result.Detail);
    }

    [Fact]
    public void IfTheWriteFails_NoCheckpointIsLeftBehind()
    {
        // Un checkpoint sin cambio real ofrecería un "deshacer" que no deshace nada.
        var (coordinator, _, checkpoints, _) = Build();
        var writerThatFails = new FakeWriter { FailWrite = true };
        var failing = new WorkspaceEditCoordinator(writerThatFails, checkpoints, new FakeAuditLog(), () => Now);

        Assert.False(failing.Apply(Edit(), Settings()).Success);
        Assert.Empty(checkpoints.Saved);
    }

    // ---------- Deshacer ----------

    [Fact]
    public void Undo_ReturnsAModifiedFileToItsPreviousContent()
    {
        var (coordinator, writer, _, _) = Build();
        writer.Files["src\\Program.cs"] = "antes";

        coordinator.Apply(Edit(content: "después"), Settings());
        var result = coordinator.RevertLast(Settings());

        Assert.True(result.Success);
        Assert.Equal("antes", writer.Files["src\\Program.cs"]);
    }

    [Fact]
    public void Undo_DeletesAFileThatSakuraCreated()
    {
        var (coordinator, writer, _, _) = Build();

        coordinator.Apply(Edit(content: "nuevo"), Settings());
        var result = coordinator.RevertLast(Settings());

        Assert.True(result.Success);
        Assert.DoesNotContain("src\\Program.cs", writer.Files.Keys);
        Assert.Contains("src\\Program.cs", writer.Deleted);
    }

    [Fact]
    public void UndoRefuses_IfTheFileChangedAfterSakuraWroteIt()
    {
        // Es el peor daño que esta capacidad puede causar: deshacer no devolvería el archivo a como
        // estaba, destruiría lo que la persona hizo después.
        var (coordinator, writer, _, _) = Build();
        writer.Files["src\\Program.cs"] = "antes";

        coordinator.Apply(Edit(content: "de Sakura"), Settings());
        writer.Files["src\\Program.cs"] = "editado a mano después";

        var result = coordinator.RevertLast(Settings());

        Assert.False(result.Success);
        Assert.Contains("perderías esos cambios", result.Detail);
        Assert.Equal("editado a mano después", writer.Files["src\\Program.cs"]);
    }

    [Fact]
    public void UndoRefuses_IfTheFolderIsNoLongerAuthorized()
    {
        var (coordinator, writer, _, _) = Build();
        coordinator.Apply(Edit(), Settings());

        var result = coordinator.RevertLast(Settings(path: string.Empty));

        Assert.False(result.Success);
        Assert.Contains("autorizada", result.Detail);
    }

    [Fact]
    public void WithNothingChanged_ThereIsNothingToUndo()
    {
        var (coordinator, _, _, _) = Build();

        Assert.False(coordinator.RevertLast(Settings()).Success);
    }

    [Fact]
    public void AnUndoneChange_IsAuditedToo()
    {
        var (coordinator, _, _, audit) = Build();

        coordinator.Apply(Edit(), Settings());
        coordinator.RevertLast(Settings());

        Assert.Contains(audit.Entries, entry => entry.Action == "Cambio deshecho");
    }

    [Fact]
    public void ARefusedChange_IsAuditedAsWell()
    {
        // Saber que Sakura intentó tocar un archivo y no pudo es de lo que una auditoría existe
        // para contar.
        var (coordinator, _, _, audit) = Build();

        coordinator.Apply(Edit(".env"), Settings());

        Assert.Contains(audit.Entries, entry => entry.Action == "Cambio no aplicado");
    }
}
