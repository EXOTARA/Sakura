using Nexo.Core.Audit;

namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D14 (Fase 5, nivel 4 — "Ejecutar un paso") — el único camino por el que Sakura escribe en
/// un proyecto. Todo lo que la fase exige antes de permitir escritura pasa por aquí, en este orden:
///
/// 1. **Nivel de autonomía suficiente.** Nivel 4 o más. Por debajo, Sakura guía; no toca.
/// 2. **La ruta pasa por <see cref="WorkspacePathPolicy"/>**, la misma que gobierna la lectura. Que
///    escribir tenga sus propias reglas de contención sería la forma más fácil de que una de las dos
///    se quedara atrás.
/// 3. **Checkpoint ANTES de escribir.** El modelo de confianza lo marca como requisito, no como
///    opción: sin snapshot previo, la acción no debe ni ofrecerse en nivel 4.
/// 4. **Verificación releyendo.** Que la escritura no lance excepción no significa que el archivo
///    quedara con el contenido pedido.
/// 5. **Si la verificación falla, se deshace.** Un archivo a medio escribir es peor que uno sin
///    tocar.
/// 6. **Queda en el Audit Log** con cómo revertirlo. El modelo lo exige a partir del nivel 4.
///
/// Un paso es UN archivo. Nivel 4 es literalmente "ejecuta un único paso confirmado explícitamente";
/// encadenar varios es el nivel 5, que todavía no está disponible.
/// </summary>
public sealed class WorkspaceEditCoordinator
{
    /// <summary>
    /// Tope del contenido que Sakura puede escribir, igual que el de lectura. Un archivo que no
    /// puede leerse entero tampoco debería poder reemplazarse entero.
    /// </summary>
    public const long MaximumEditBytes = WorkspacePathPolicy.MaximumFileBytes;

    private readonly IWorkspaceWriter _writer;
    private readonly IWorkspaceCheckpointStore _checkpoints;
    private readonly IAuditLog _audit;
    private readonly Func<DateTimeOffset> _clock;

    public WorkspaceEditCoordinator(
        IWorkspaceWriter writer,
        IWorkspaceCheckpointStore checkpoints,
        IAuditLog audit,
        Func<DateTimeOffset>? clock = null)
    {
        _writer = writer;
        _checkpoints = checkpoints;
        _audit = audit;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public IReadOnlyList<WorkspaceCheckpoint> Checkpoints => _checkpoints.Read();

    /// <summary>
    /// Comprueba si un cambio PODRÍA aplicarse, sin aplicarlo. Se usa para enseñar la vista previa:
    /// preguntar "¿aplico esto?" y descubrir después que estaba prohibido sería hacer perder el
    /// tiempo a la persona.
    /// </summary>
    public WorkspaceAccessVerdict CanApply(WorkspaceEdit edit, WorkspaceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (edit is null || string.IsNullOrWhiteSpace(edit.RelativePath))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.FueraDelWorkspace, "No me diste ningún archivo que cambiar.");
        }

        if (!settings.HasAuthorizedFolder)
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.SinAutorizacion,
                "No tengo ninguna carpeta autorizada, así que no puedo cambiar nada.");
        }

        if (!WorkspaceAutonomyPolicy.CanWrite(settings.AutonomyLevel))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.SinAutorizacion,
                "Con el nivel de autonomía actual solo puedo guiarte, no modificar archivos. " +
                "Puedes subirlo a «Ejecutar un paso» en Personalizar.");
        }

        // La ruta se compone contra la raíz autorizada y se juzga con la MISMA política que la
        // lectura: contención sobre rutas resueltas, nada de archivos de secretos, nada dentro de
        // carpetas de dependencias, y solo tipos de texto de proyecto.
        var fullPath = Path.Combine(settings.AuthorizedPath, edit.RelativePath);
        var verdict = WorkspacePathPolicy.CanRead(fullPath, settings.AuthorizedPath);
        if (!verdict.IsAllowed)
        {
            return verdict;
        }

        if (edit.NewContent is null || edit.NewContent.Length > MaximumEditBytes)
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.DemasiadoGrande,
                $"Ese contenido pasa de {MaximumEditBytes / 1024} KB, así que no lo escribo.");
        }

        return WorkspaceAccessVerdict.Allowed;
    }

    public WorkspaceWriteResult Apply(WorkspaceEdit edit, WorkspaceSettings settings)
    {
        var verdict = CanApply(edit, settings);
        if (!verdict.IsAllowed)
        {
            RecordRefusal(edit?.RelativePath, verdict.Message, settings);
            return WorkspaceWriteResult.Refused(verdict.Message);
        }

        // Paso 3: el checkpoint primero. Si la escritura falla a medias, la vuelta atrás ya existe.
        var (existed, readable, previousContent) = _writer.ReadForCheckpoint(
            settings.AuthorizedPath, edit!.RelativePath);

        // Si no se pudo LEER, no se sabe si el archivo existía: tratarlo como «nuevo» lo reemplazaba
        // y, al deshacer, lo borraba. Se rechaza antes de guardar ningún checkpoint. Sin rutas ni
        // mensajes del sistema: solo el nombre relativo, que ya está en el Audit Log.
        if (!readable)
        {
            var unreadable =
                $"No pude leer «{edit.RelativePath}» para guardar la copia previa, así que no lo " +
                "modifiqué.";

            RecordRefusal(edit.RelativePath, unreadable, settings);
            return WorkspaceWriteResult.Refused(unreadable);
        }

        var checkpoint = new WorkspaceCheckpoint
        {
            At = _clock(),
            RelativePath = edit.RelativePath,
            PreviousExisted = existed,
            PreviousContent = previousContent,
            WrittenContent = edit.NewContent,
            Description = edit.Description
        };

        _checkpoints.Save(checkpoint);

        // Y se comprueba que el checkpoint QUEDÓ guardado antes de tocar el archivo. Si el disco
        // está lleno o el almacén falló en silencio, escribir dejaría un cambio sin vuelta atrás:
        // exactamente lo que el modelo de confianza prohíbe en nivel 4.
        if (_checkpoints.Read().All(saved => saved.Id != checkpoint.Id))
        {
            const string detail =
                "No pude guardar la copia previa del archivo, así que no lo modifiqué: sin copia " +
                "previa no podrías deshacerlo.";

            RecordRefusal(edit.RelativePath, detail, settings);
            return WorkspaceWriteResult.Refused(detail);
        }

        var written = _writer.WriteFile(settings.AuthorizedPath, edit.RelativePath, edit.NewContent);
        if (!written.Success)
        {
            // No se escribió: el checkpoint no corresponde a ningún cambio real, y dejarlo ofrecería
            // un "deshacer" que no deshace nada.
            _checkpoints.Remove(checkpoint.Id);
            RecordRefusal(edit.RelativePath, written.Detail, settings);
            return WorkspaceWriteResult.Refused(written.Detail);
        }

        // Paso 4: verificar releyendo.
        var (_, _, actual) = _writer.ReadForCheckpoint(settings.AuthorizedPath, edit.RelativePath);
        if (!string.Equals(actual, edit.NewContent, StringComparison.Ordinal))
        {
            // Paso 5: deshacer lo que quedó a medias.
            var undone = RestoreFile(checkpoint, settings, verifyUnchanged: false);
            _checkpoints.Remove(checkpoint.Id);

            var detail = undone.Success
                ? "El archivo no quedó con el contenido que pedí, así que lo dejé como estaba."
                : "El archivo no quedó con el contenido que pedí y tampoco pude devolverlo a como " +
                  "estaba. Revísalo antes de seguir.";

            RecordRefusal(edit.RelativePath, detail, settings);
            return WorkspaceWriteResult.Refused(detail);
        }

        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Proyecto,
            Action = existed ? "Archivo modificado" : "Archivo creado",
            Detail = $"{edit.RelativePath} — {edit.Description}",
            Permission = "Carpeta autorizada y cambio confirmado por el usuario",
            AutonomyLevel = (int)settings.AutonomyLevel,
            RevertHint = $"Deshacer el cambio en {edit.RelativePath}.",
            RevertToken = checkpoint.Id.ToString()
        });

        return WorkspaceWriteResult.Written(
            existed
                ? $"Modifiqué {edit.RelativePath}. Puedes deshacerlo cuando quieras."
                : $"Creé {edit.RelativePath}. Puedes deshacerlo cuando quieras.",
            checkpoint.Id);
    }

    /// <summary>
    /// Deshace un cambio concreto. **Se niega si el archivo cambió desde que Sakura lo escribió**:
    /// en ese caso, deshacer no devolvería el archivo a como estaba, destruiría lo que la persona
    /// hizo después. Es el peor daño que esta capacidad puede causar y el único que no sería culpa
    /// de un fallo, sino del diseño.
    /// </summary>
    public WorkspaceWriteResult Revert(Guid checkpointId, WorkspaceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var checkpoint = _checkpoints.Read().FirstOrDefault(entry => entry.Id == checkpointId);
        if (checkpoint is null)
        {
            return WorkspaceWriteResult.Refused("No tengo guardado ese cambio, así que no puedo deshacerlo.");
        }

        if (!settings.HasAuthorizedFolder)
        {
            return WorkspaceWriteResult.Refused(
                "Ya no tengo autorizada esa carpeta, así que no puedo tocar nada dentro.");
        }

        var result = RestoreFile(checkpoint, settings, verifyUnchanged: true);
        if (!result.Success)
        {
            return WorkspaceWriteResult.Refused(result.Detail);
        }

        _checkpoints.Remove(checkpoint.Id);

        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Proyecto,
            Action = "Cambio deshecho",
            Detail = $"{checkpoint.RelativePath} — {checkpoint.Description}",
            Permission = "Reversión pedida por el usuario",
            AutonomyLevel = (int)settings.AutonomyLevel
        });

        return WorkspaceWriteResult.Written(result.Detail, checkpoint.Id);
    }

    public WorkspaceWriteResult RevertLast(WorkspaceSettings settings)
    {
        var last = _checkpoints.Read()
            .OrderByDescending(checkpoint => checkpoint.At)
            .FirstOrDefault();

        return last is null
            ? WorkspaceWriteResult.Refused("No he cambiado nada en tu proyecto, así que no hay nada que deshacer.")
            : Revert(last.Id, settings);
    }

    private WorkspaceStepResult RestoreFile(
        WorkspaceCheckpoint checkpoint,
        WorkspaceSettings settings,
        bool verifyUnchanged)
    {
        if (verifyUnchanged)
        {
            var (existsNow, readableNow, currentContent) = _writer.ReadForCheckpoint(
                settings.AuthorizedPath, checkpoint.RelativePath);

            // «No pude leerlo» no es «ya no existe»: decir lo segundo sería afirmar algo que no se sabe.
            if (!readableNow)
            {
                return WorkspaceStepResult.Failed(
                    $"No pude comprobar «{checkpoint.RelativePath}» en este momento, así que no lo toco.");
            }

            if (!existsNow)
            {
                return WorkspaceStepResult.Failed(
                    $"«{checkpoint.RelativePath}» ya no existe, así que no hay nada que devolver.");
            }

            if (!string.Equals(currentContent, checkpoint.WrittenContent, StringComparison.Ordinal))
            {
                return WorkspaceStepResult.Failed(
                    $"«{checkpoint.RelativePath}» cambió después de que yo lo escribiera. Si lo " +
                    "deshiciera ahora perderías esos cambios, así que no lo toco.");
            }
        }

        if (!checkpoint.PreviousExisted)
        {
            var deleted = _writer.DeleteFile(settings.AuthorizedPath, checkpoint.RelativePath);
            if (!deleted.Success)
            {
                return deleted;
            }

            // Verificar también al deshacer. Que la llamada no fallara no significa que el archivo
            // ya no esté, y "lo dejé como estaba" es justo la frase que no puede decirse a la
            // ligera.
            var (stillThere, readableAfter, _) = _writer.ReadForCheckpoint(
                settings.AuthorizedPath, checkpoint.RelativePath);

            if (!readableAfter)
            {
                return WorkspaceStepResult.Failed(
                    $"Pedí borrar {checkpoint.RelativePath} pero no pude comprobar si sigue ahí, " +
                    "así que no lo doy por hecho.");
            }

            return stillThere
                ? WorkspaceStepResult.Failed(
                    $"Pedí borrar {checkpoint.RelativePath} y sigue ahí, así que no lo doy por hecho.")
                : WorkspaceStepResult.Ok($"Borré {checkpoint.RelativePath}, que no existía antes.");
        }

        var restored = _writer.WriteFile(
            settings.AuthorizedPath, checkpoint.RelativePath, checkpoint.PreviousContent);

        if (!restored.Success)
        {
            return restored;
        }

        var (_, _, actual) = _writer.ReadForCheckpoint(settings.AuthorizedPath, checkpoint.RelativePath);

        return string.Equals(actual, checkpoint.PreviousContent, StringComparison.Ordinal)
            ? WorkspaceStepResult.Ok($"Devolví {checkpoint.RelativePath} a como estaba.")
            : WorkspaceStepResult.Failed(
                $"{checkpoint.RelativePath} no quedó con su contenido anterior, así que no doy la " +
                "reversión por hecha.");
    }

    /// <summary>
    /// Un intento rechazado también se registra. Saber que Sakura intentó tocar un archivo y no
    /// pudo es exactamente el tipo de cosa que una auditoría existe para contar.
    /// </summary>
    private void RecordRefusal(string? relativePath, string detail, WorkspaceSettings settings) =>
        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Proyecto,
            Action = "Cambio no aplicado",
            Detail = $"{relativePath ?? "(sin archivo)"} — {detail}",
            Permission = settings.HasAuthorizedFolder ? "Carpeta autorizada" : "Sin autorización",
            AutonomyLevel = (int)settings.AutonomyLevel
        });
}
