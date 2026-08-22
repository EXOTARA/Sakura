namespace Nexo.Core.Voice;

public readonly record struct VoiceUtteranceTimingSnapshot(
    bool SpeechDetected,
    int SpeechMilliseconds,
    int TrailingSilenceMilliseconds,
    int LiveAudioMilliseconds);

public static class VoiceUtteranceEndPolicy
{
    public const int MinimumSpeechMilliseconds = 350;
    public const int MinimumLiveAudioMilliseconds = 2_500;

    /// <summary>
    /// Cuánto se espera a que alguien empiece a hablar antes de rendirse.
    ///
    /// Es una pregunta distinta de «¿ha terminado de hablar?» y hasta ahora se contestaban con el
    /// mismo número: si nadie decía nada, la escucha aguantaba hasta agotar la duración máxima —
    /// veinte segundos de halo en pantalla por un falso positivo de la palabra de activación. Eso
    /// no es esperar, es estorbar.
    ///
    /// Tres segundos: de sobra para coger aire después de decir el nombre, y poco para que una
    /// activación equivocada se note. En cuanto se detecta voz este límite deja de contar y manda
    /// el de la frase completa, que sí puede ser largo.
    /// </summary>
    public const int SilenceBeforeSpeechMilliseconds = 3_000;

    public static bool ShouldComplete(
        VoiceUtteranceTimingSnapshot snapshot,
        TimeSpan requiredTrailingSilence)
    {
        if (requiredTrailingSilence < TimeSpan.FromMilliseconds(300))
        {
            throw new ArgumentOutOfRangeException(nameof(requiredTrailingSilence));
        }

        return snapshot.SpeechDetected &&
               snapshot.SpeechMilliseconds >= MinimumSpeechMilliseconds &&
               snapshot.LiveAudioMilliseconds >= MinimumLiveAudioMilliseconds &&
               snapshot.TrailingSilenceMilliseconds >=
                   requiredTrailingSilence.TotalMilliseconds;
    }

    /// <summary>
    /// Si hay que dejarlo porque nadie ha hablado.
    ///
    /// Sólo se rinde cuando NO se ha detectado voz en ningún momento. Una pausa larga a mitad de
    /// una orden no cuenta: ahí ya hubo voz, y cortar a quien está pensando la segunda mitad de su
    /// frase sería peor que esperar.
    /// </summary>
    public static bool ShouldAbandon(
        VoiceUtteranceTimingSnapshot snapshot,
        TimeSpan silenceBeforeSpeech)
    {
        if (silenceBeforeSpeech < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(silenceBeforeSpeech));
        }

        return !snapshot.SpeechDetected &&
               snapshot.LiveAudioMilliseconds >= silenceBeforeSpeech.TotalMilliseconds;
    }
}
