namespace Nexo.Core.Voice;

/// <summary>Una línea de la pestaña Voz: qué es, cómo está, y si eso es un problema.</summary>
public readonly record struct VoiceGlanceRow(string Label, string Value, bool NeedsAttention);

/// <summary>
/// Diseño D81 — la pestaña «Voz» del panel.
///
/// Contesta a una pregunta concreta y frecuente: **¿por qué no me está oyendo?** Hasta ahora la
/// respuesta estaba repartida entre Personalizar → Voz, el estado del micrófono y saberse los
/// atajos, y eso significa que quien tenía el problema era justo quien no podía encontrarla.
///
/// Es un vistazo, no un panel de ajustes: enseña el estado y los atajos, y no cambia nada. Lo que
/// se toca sigue estando en Personalizar, donde hay sitio para explicarlo.
///
/// **Lo que está mal se marca.** Sin micrófono, sin modelos o con la escucha apagada, la línea pide
/// atención — porque son exactamente las tres causas de que Sakura no conteste, y una lista donde
/// todo se ve igual obliga a leerla entera para descubrir cuál falla.
/// </summary>
public static class VoiceGlancePolicy
{
    public static IReadOnlyList<VoiceGlanceRow> Describe(
        bool wakeWordEnabled,
        string? wakeWordPhrase,
        bool modelsReady,
        string? inputDeviceName,
        bool dictationEnabled)
    {
        var phrase = string.IsNullOrWhiteSpace(wakeWordPhrase) ? null : wakeWordPhrase.Trim();

        return
        [
            new VoiceGlanceRow(
                "Escucha",
                wakeWordEnabled
                    ? phrase is null ? "Atenta" : $"Atenta a «{phrase}»"
                    : "Apagada",
                NeedsAttention: !wakeWordEnabled),

            new VoiceGlanceRow(
                "Micrófono",
                string.IsNullOrWhiteSpace(inputDeviceName)
                    ? "Ninguno disponible"
                    : inputDeviceName.Trim(),
                NeedsAttention: string.IsNullOrWhiteSpace(inputDeviceName)),

            new VoiceGlanceRow(
                "Modelos de voz",
                modelsReady ? "Listos, en este equipo" : "Sin preparar",
                NeedsAttention: !modelsReady),

            // Los dos atajos no son estado: son lo que hay que saber para usar la voz, y este es el
            // único sitio donde alguien va a ir a buscarlos cuando no se acuerde.
            new VoiceGlanceRow("Escuchar ahora", "Alt + V", NeedsAttention: false),

            new VoiceGlanceRow(
                "Dictado global",
                dictationEnabled ? "Ctrl + Shift + D" : "Apagado",
                NeedsAttention: false)
        ];
    }
}
