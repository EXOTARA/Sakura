namespace Nexo.Core.Voice;

/// <summary>Un grupo de frases de ejemplo: de qué van y cómo se dicen.</summary>
public sealed record VoiceCommandExampleGroup(string Title, IReadOnlyList<string> Phrases);

/// <summary>
/// Lo que se le puede decir a Sakura, para la pestaña Voz del panel (Adler, 2026-09-14).
///
/// Son ejemplos, no el catálogo entero: pocos y de cosas distintas, para que se entienda el tipo de
/// frase que funciona. **Todas se resuelven en el equipo, sin IA**, y hay una prueba que pasa cada
/// una por la misma cadena de despacho que usa la ventana principal. Una frase que se anuncia aquí y
/// después acaba en «la IA está desactivada» sería peor que no anunciarla.
/// </summary>
public static class VoiceCommandExamples
{
    public static IReadOnlyList<VoiceCommandExampleGroup> Groups { get; } =
    [
        new("Abrir", ["abre la calculadora", "abre descargas", "abre PowerShell"]),
        new("Volumen", ["baja Spotify al 50", "silencia Discord"]),
        new("Enfoque", ["inicia una sesión de enfoque de 25 minutos", "cuánto tiempo me queda", "pausa el temporizador"]),
        new("Tu equipo", ["cómo está mi PC", "qué tengo pendiente hoy", "muestra Peek"])
    ];
}
