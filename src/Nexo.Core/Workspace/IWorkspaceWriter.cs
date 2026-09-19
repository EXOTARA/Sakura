namespace Nexo.Core.Workspace;

/// <summary>Diseño D14 — un cambio propuesto sobre UN archivo. Un paso, un archivo.</summary>
public sealed record WorkspaceEdit(string RelativePath, string NewContent, string Description);

/// <summary>
/// Resultado de un paso de escritura. Mismo papel que <c>OptimizationStepResult</c> en la Fase 4,
/// pero en su propio tipo: compartirlo ataría el acompañante de proyecto a la optimización sin que
/// tengan nada que ver.
/// </summary>
public sealed record WorkspaceStepResult(bool Success, string Detail)
{
    public static WorkspaceStepResult Ok(string detail) => new(true, detail);

    public static WorkspaceStepResult Failed(string detail) => new(false, detail);
}

public sealed record WorkspaceWriteResult(bool Success, string Detail, Guid CheckpointId)
{
    public static WorkspaceWriteResult Written(string detail, Guid checkpointId) =>
        new(true, detail, checkpointId);

    public static WorkspaceWriteResult Refused(string detail) => new(false, detail, Guid.Empty);
}

/// <summary>
/// Diseño D14 (Fase 5, nivel 4) — escritura real en el proyecto autorizado. Interfaz separada de
/// <see cref="IWorkspaceReader"/> a propósito: quien solo tiene el lector no puede escribir ni por
/// accidente, y quien pide el escritor está pidiendo algo distinto y visible en el código.
///
/// No decide nada. Ni permisos, ni rutas, ni checkpoints: eso es de
/// <see cref="WorkspaceEditCoordinator"/>. Aquí solo se toca el disco.
/// </summary>
public interface IWorkspaceWriter
{
    /// <summary>
    /// Lee lo que hay ahora para poder volver a ello. <c>Existed</c> false si no existe.
    ///
    /// <c>Readable</c> false significa que no se PUDO leer (bloqueado, sin permiso, ruta no
    /// resoluble): es distinto de «no existe». Confundirlos hacía tratar como archivo nuevo uno que
    /// simplemente no se podía leer, reemplazarlo y, al deshacer, borrarlo.
    /// </summary>
    (bool Existed, bool Readable, string Content) ReadForCheckpoint(string authorizedRoot, string relativePath);

    WorkspaceStepResult WriteFile(string authorizedRoot, string relativePath, string content);

    /// <summary>Borra un archivo que Sakura creó. Solo se usa al deshacer.</summary>
    WorkspaceStepResult DeleteFile(string authorizedRoot, string relativePath);
}
