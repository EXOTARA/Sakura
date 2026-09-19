namespace Nexo.Core.Storage;

/// <summary>Cómo salió la lectura de un archivo de datos de la persona.</summary>
public enum DataLoadStatus
{
    /// <summary>Se leyó bien.</summary>
    Ok,

    /// <summary>No existía todavía: es la primera vez, no hay nada que perder.</summary>
    Missing,

    /// <summary>Existía pero su contenido no se pudo interpretar (truncado, vacío, ceros...).</summary>
    Corrupt,

    /// <summary>Existía pero no se pudo abrir (bloqueado por otro programa, sin permiso...).</summary>
    Unreadable
}

/// <summary>
/// Cuatro cosas distintas se parecían desde fuera a «una lista vacía»: primera vez, archivo dañado,
/// archivo que no se pudo abrir y archivo vacío de verdad. Sin distinguirlas, el primer guardado tras
/// una lectura fallida escribía la lista vacía encima de lo que la persona sí tenía. Esto es lo que
/// permite a quien carga saber si escribir encima es seguro.
/// </summary>
public sealed record DataLoadOutcome(
    DataLoadStatus Status,
    string Detail = "",
    string? PreservedPath = null)
{
    public static DataLoadOutcome Ok { get; } = new(DataLoadStatus.Ok);

    public static DataLoadOutcome Missing { get; } = new(DataLoadStatus.Missing);

    public static DataLoadOutcome Corrupt(string detail, string? preservedPath) =>
        new(DataLoadStatus.Corrupt, detail, preservedPath);

    public static DataLoadOutcome Unreadable(string detail) =>
        new(DataLoadStatus.Unreadable, detail);

    /// <summary>
    /// Solo cuando no hay nada que perder. Con un archivo dañado o ilegible, lo que hay en disco puede
    /// ser lo único que queda de los datos de la persona.
    /// </summary>
    public bool IsSafeToOverwrite => Status is DataLoadStatus.Ok or DataLoadStatus.Missing;
}
