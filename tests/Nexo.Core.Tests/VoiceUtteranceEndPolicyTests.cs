using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class VoiceUtteranceEndPolicyTests
{
    [Fact]
    public void ShouldComplete_WaitsForEnoughSpeech()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 200,
            TrailingSilenceMilliseconds: 2_000,
            LiveAudioMilliseconds: 3_000);

        Assert.False(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }

    [Fact]
    public void ShouldComplete_DoesNotCutOnBriefPause()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 900,
            TrailingSilenceMilliseconds: 900,
            LiveAudioMilliseconds: 3_000);

        Assert.False(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }

    [Fact]
    public void ShouldComplete_EndsAfterConfirmedSpeechAndTrailingSilence()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 1_200,
            TrailingSilenceMilliseconds: 1_500,
            LiveAudioMilliseconds: 3_000);

        Assert.True(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }


    [Fact]
    public void ShouldComplete_ProtectsTheInitialListeningWindow()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 900,
            TrailingSilenceMilliseconds: 1_500,
            LiveAudioMilliseconds: 1_800);

        Assert.False(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }

    [Fact]
    public void ShouldComplete_RejectsUnsafeTrailingSilence()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 1_000,
            TrailingSilenceMilliseconds: 1_000,
            LiveAudioMilliseconds: 3_000);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VoiceUtteranceEndPolicy.ShouldComplete(
                snapshot,
                TimeSpan.FromMilliseconds(250)));
    }

    // ---------- rendirse cuando nadie habla ----------

    private static readonly TimeSpan Wait =
        TimeSpan.FromMilliseconds(VoiceUtteranceEndPolicy.SilenceBeforeSpeechMilliseconds);

    [Fact]
    public void ShouldAbandon_GivesUpWhenNobodyEverSpoke()
    {
        // El caso que motivó todo esto: la palabra de activación salta sola, no hay nadie
        // delante, y la escucha se quedaba abierta hasta agotar los veinte segundos del máximo.
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: false,
            SpeechMilliseconds: 0,
            TrailingSilenceMilliseconds: 3_000,
            LiveAudioMilliseconds: 3_000);

        Assert.True(VoiceUtteranceEndPolicy.ShouldAbandon(snapshot, Wait));
    }

    [Fact]
    public void ShouldAbandon_LeavesRoomToTakeABreath()
    {
        // Justo después del nombre no ha hablado nadie todavía, y eso es normal.
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: false,
            SpeechMilliseconds: 0,
            TrailingSilenceMilliseconds: 1_200,
            LiveAudioMilliseconds: 1_200);

        Assert.False(VoiceUtteranceEndPolicy.ShouldAbandon(snapshot, Wait));
    }

    [Fact]
    public void ShouldAbandon_NeverCutsSomeoneWhoAlreadyStartedTalking()
    {
        // Una pausa larga a mitad de la orden supera de sobra el límite, y aun así no se abandona:
        // ahí ya hubo voz y quien está pensando la segunda mitad de su frase merece terminarla.
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 600,
            TrailingSilenceMilliseconds: 9_000,
            LiveAudioMilliseconds: 12_000);

        Assert.False(VoiceUtteranceEndPolicy.ShouldAbandon(snapshot, Wait));
    }

    [Fact]
    public void ShouldAbandon_RejectsAnImpatientWait()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: false,
            SpeechMilliseconds: 0,
            TrailingSilenceMilliseconds: 800,
            LiveAudioMilliseconds: 800);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VoiceUtteranceEndPolicy.ShouldAbandon(snapshot, TimeSpan.FromMilliseconds(400)));
    }
}
