using Nexo.Core.Assistant;

namespace Nexo.Core.Ai;

public sealed record AiChatRequest(
    IReadOnlyList<ConversationMessage> Messages,
    string Instructions,
    string? SystemContext = null,
    IReadOnlyList<AiImageAttachment>? Images = null,
    AiRequestMode Mode = AiRequestMode.Standard,
    // 2026-09-16 — normalmente nulo: solo se pone al repetir una petición que el proveedor rechazó
    // por el tamaño de la respuesta (ver AiProviderLimits).
    int? MaxOutputTokens = null);
