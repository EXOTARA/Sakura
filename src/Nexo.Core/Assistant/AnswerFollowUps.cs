namespace Nexo.Core.Assistant;

/// <summary>Un botón debajo de una respuesta: lo que dice y lo que le pide a Sakura.</summary>
public sealed record AnswerFollowUp(string Label, string Prompt)
{
    public override string ToString() => Label;
}

/// <summary>
/// 2026-09-15 — botones para seguir con una respuesta sin saber cómo pedirlo (Adler). Salen de las
/// frases que mejor funcionan con un asistente: pedir menos texto, una explicación más sencilla, el
/// primer paso, algo concreto y que busque los fallos en vez de dar la razón.
///
/// Cada uno manda su frase como un mensaje más de la conversación, así que la respuesta anterior está
/// en el contexto y Sakura sabe a qué se refiere.
/// </summary>
public static class AnswerFollowUps
{
    public static IReadOnlyList<AnswerFollowUp> All { get; } =
    [
        new("Más corto", "Dímelo más corto, solo con lo esencial."),
        new("Más fácil", "Explícamelo más fácil, como a alguien que no sabe del tema."),
        new("¿Primer paso?", "¿Cuál es el primer paso que puedo dar ahora mismo?"),
        new("Hazlo lista", "Conviértelo en una lista de pasos que pueda seguir."),
        new("Con ejemplos", "Hazlo más concreto, con ejemplos, cifras y nombres."),
        new("Ponlo a prueba", "Busca los puntos débiles de lo que me dijiste. No me des la razón por dármela.")
    ];

    /// <summary>
    /// Los botones que caben en la píldora de respuesta, que es estrecha: sin «Hazlo lista», que es el
    /// menos útil cuando la respuesta ya viene en puntos, como casi todas las de la píldora.
    /// </summary>
    public static IReadOnlyList<AnswerFollowUp> Compact { get; } =
        [.. All.Where(followUp => followUp.Label != "Hazlo lista")];
}
