using Nexo.Core.Audit;
using Nexo.Core.Productization;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D20 (Fase 9) — el roadmap dice que *"un actualizador mal diseñado puede romper
/// instalaciones existentes — requiere el mismo rigor de reversibilidad que la Fase 4"*. Lo que se
/// prueba aquí es que una copia sin verificar no cuenta como copia.
/// </summary>
public sealed class ProductizationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 2, 16, 0, 0, TimeSpan.FromHours(-6));

    private sealed class FakeBackupService : IDataBackupService
    {
        public List<BackupFileOutcome> Outcomes { get; set; } = [];

        public bool Succeeds { get; set; } = true;

        public string Detail { get; set; } = "Copia hecha.";

        public List<string> Backups { get; } = ["20260802-100000"];

        public string? RestoredId { get; private set; }

        public BackupResult CreateBackup(IReadOnlyList<SakuraDataItem> items) =>
            new(Succeeds, "20260802-160000", Detail, Outcomes);

        public BackupResult Restore(string backupId)
        {
            RestoredId = backupId;
            return new BackupResult(Succeeds, backupId, Detail, Outcomes);
        }

        public IReadOnlyList<string> ListBackups() => Backups;
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public IReadOnlyList<AuditEntry> Read() => Entries;

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private static BackupFileOutcome Ok(string name) => new(name, true, true, "Copiado y verificado.");

    private static BackupFileOutcome Unverified(string name) =>
        new(name, true, false, "Copiado, pero el contenido no coincide.");

    private static BackupFileOutcome Missing(string name) => new(name, false, false, "No existe todavía.");

    // ---------- Inventario ----------

    [Fact]
    public void TheInventoryDescribesEveryFile()
    {
        // Sin descripción el inventario no sirve para lo único que existe: que la persona entienda
        // qué tiene guardado.
        Assert.All(SakuraDataInventory.All, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.FileName));
            Assert.False(string.IsNullOrWhiteSpace(item.Title));
            Assert.False(string.IsNullOrWhiteSpace(item.WhatItHolds));
        });
    }

    [Fact]
    public void TheInventoryHasNoDuplicates() =>
        Assert.Equal(
            SakuraDataInventory.All.Count,
            SakuraDataInventory.All.Select(item => item.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    [Fact]
    public void PersonalDataIsMarkedAsSuch()
    {
        Assert.Contains(SakuraDataInventory.Personal, item => item.FileName == "memory.dat");
        Assert.Contains(SakuraDataInventory.Personal, item => item.FileName == "tasks.json");

        // El registro de actividad NO es personal: dice qué hizo Sakura, no qué hiciste tú.
        Assert.DoesNotContain(SakuraDataInventory.Personal, item => item.FileName == "audit.json");
    }

    [Fact]
    public void HabitsAreInTheInventory_SoTheyAreBackedUpAndKeptOnUninstall()
    {
        // Se escribían desde el 2026-09-16 y la copia previa a actualizar no los copiaba.
        Assert.Contains(SakuraDataInventory.ToBackUp, item => item.FileName == "habits.json");
        Assert.Contains(SakuraDataInventory.Personal, item => item.FileName == "habits.json");
        Assert.Contains(
            UninstallPlanner.Build(UninstallDataChoice.Conservar).WillBeKept,
            item => item.FileName == "habits.json");
    }

    [Fact]
    public void TheOnlyEncryptedThingIsTheMemory() =>
        Assert.Equal(
            ["memory.dat"],
            SakuraDataInventory.All.Where(item => item.IsEncrypted).Select(item => item.FileName));

    [Fact]
    public void EverythingIsBackedUp() =>
        Assert.Equal(SakuraDataInventory.All.Count, SakuraDataInventory.ToBackUp.Count);

    // ---------- Preparar la actualización ----------

    [Fact]
    public void AVerifiedBackup_MakesTheUpdateSafe()
    {
        var backups = new FakeBackupService { Outcomes = [Ok("settings.json"), Ok("tasks.json")] };
        var coordinator = new UpdateSafetyCoordinator(backups, new FakeAuditLog(), () => Now);

        var readiness = coordinator.PrepareUpdate();

        Assert.True(readiness.CanUpdate);
        Assert.Contains("Puedes actualizar", readiness.Detail);
    }

    [Fact]
    public void AnUnverifiedFile_BlocksTheUpdate()
    {
        // El único momento en que la copia importa es cuando algo salió mal, y ahí ya no hay ocasión
        // de comprobarla.
        var backups = new FakeBackupService
        {
            Outcomes = [Ok("settings.json"), Unverified("memory.dat")]
        };

        var readiness = new UpdateSafetyCoordinator(backups, new FakeAuditLog(), () => Now).PrepareUpdate();

        Assert.False(readiness.CanUpdate);
        Assert.Contains("no quedaron verificados", readiness.Detail);
    }

    [Fact]
    public void AFileThatDoesNotExistYet_IsNotAFailure()
    {
        // Quien nunca activó la memoria no tiene memoria que copiar.
        var backups = new FakeBackupService { Outcomes = [Ok("settings.json"), Missing("memory.dat")] };

        var readiness = new UpdateSafetyCoordinator(backups, new FakeAuditLog(), () => Now).PrepareUpdate();

        Assert.True(readiness.CanUpdate);
    }

    [Fact]
    public void AFailedBackup_BlocksTheUpdate()
    {
        var backups = new FakeBackupService { Succeeds = false, Detail = "No pude crear la carpeta." };

        Assert.False(new UpdateSafetyCoordinator(backups, new FakeAuditLog(), () => Now)
            .PrepareUpdate().CanUpdate);
    }

    [Fact]
    public void PreparingAnUpdate_IsAudited_WithHowToGoBack()
    {
        var audit = new FakeAuditLog();
        var backups = new FakeBackupService { Outcomes = [Ok("settings.json")] };

        new UpdateSafetyCoordinator(backups, audit, () => Now).PrepareUpdate();

        var entry = Assert.Single(audit.Entries);
        Assert.True(entry.CanRevert);
        Assert.Contains("Restaurar", entry.RevertHint);
    }

    [Fact]
    public void ABlockedUpdate_IsAuditedWithoutPromisingAWayBack()
    {
        var audit = new FakeAuditLog();
        var backups = new FakeBackupService { Outcomes = [Unverified("memory.dat")] };

        new UpdateSafetyCoordinator(backups, audit, () => Now).PrepareUpdate();

        Assert.False(Assert.Single(audit.Entries).CanRevert);
    }

    [Fact]
    public void RollingBackRestoresTheRequestedBackup()
    {
        var backups = new FakeBackupService { Outcomes = [Ok("settings.json")] };

        new UpdateSafetyCoordinator(backups, new FakeAuditLog(), () => Now).Rollback("20260802-100000");

        Assert.Equal("20260802-100000", backups.RestoredId);
    }

    // ---------- Desinstalar ----------

    [Fact]
    public void KeepingYourData_DeletesOnlyWhatSakuraCanRebuild()
    {
        var plan = UninstallPlanner.Build(UninstallDataChoice.Conservar);

        Assert.All(plan.WillBeDeleted, item => Assert.False(item.IsPersonal));
        Assert.All(plan.WillBeKept, item => Assert.True(item.IsPersonal));
        Assert.NotEmpty(plan.WillBeKept);
    }

    [Fact]
    public void DeletingEverything_TakesTheMemoryToo()
    {
        var plan = UninstallPlanner.Build(UninstallDataChoice.Borrar);

        Assert.Contains(plan.WillBeDeleted, item => item.FileName == "memory.dat");
        Assert.Empty(plan.WillBeKept);
    }

    [Fact]
    public void AnUnknownChoice_KeepsYourData()
    {
        // Falla hacia el lado que no destruye nada.
        var plan = UninstallPlanner.Build((UninstallDataChoice)99);

        Assert.Equal(UninstallDataChoice.Conservar, plan.Choice);
        Assert.NotEmpty(plan.WillBeKept);
    }

    [Fact]
    public void TheSummarySaysBothHalves()
    {
        // Un resumen que solo cuenta una de las dos mitades es el que produce la sorpresa.
        var description = UninstallPlanner.Describe(UninstallPlanner.Build(UninstallDataChoice.Conservar));

        Assert.Contains("Se conservará", description);
        Assert.Contains("Se borrarán", description);
    }

    [Fact]
    public void DeletingEverythingSaysItCannotBeUndone() =>
        Assert.Contains(
            "No se puede deshacer",
            UninstallPlanner.Describe(UninstallPlanner.Build(UninstallDataChoice.Borrar)));
}
