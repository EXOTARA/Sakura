namespace Nexo.Core.Focus;

public sealed record FocusCompletion(
    string Label,
    FocusSessionKind Kind,
    TimeSpan Duration,
    DateTimeOffset CompletedAt,
    Guid? TaskId = null);

/// <summary>
/// Una sesión que terminó mientras Sakura estaba cerrada. La duración es la programada, no la
/// medida: el equipo pudo haber estado apagado, así que es una estimación y se cuenta como tal.
/// </summary>
public sealed record FocusRecovery(FocusCompletion Completion);
