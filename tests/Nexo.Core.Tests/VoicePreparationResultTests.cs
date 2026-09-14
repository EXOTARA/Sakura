using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class VoicePreparationResultTests
{
    [Fact]
    public void Ready_UsesFriendlyDefaultDetail()
    {
        var result = VoicePreparationResult.Ready();

        Assert.True(result.IsReady);
        Assert.Equal("Voz local lista.", result.Detail);
    }

    [Fact]
    public void Unavailable_TrimsProvidedDetail()
    {
        var result = VoicePreparationResult.Unavailable("  Sin conexión.  ");

        Assert.False(result.IsReady);
        Assert.Equal("Sin conexión.", result.Detail);
    }

    [Fact]
    public void Downloading_ClampsNegativeByteCount()
    {
        var progress = VoicePreparationProgress.Downloading(-50);

        Assert.Equal(0, progress.BytesDownloaded);
        Assert.Contains("0 MB", progress.Detail);
    }

    [Fact]
    public void Fraction_FollowsTheDownload_AndWaitsAt99UntilItIsComplete()
    {
        Assert.Null(VoicePreparationProgress.Preparing("Preparando…").Fraction);
        Assert.Null(VoicePreparationProgress.Downloading(0).Fraction);

        var half = VoicePreparationProgress.Downloading(VoicePreparationProgress.ExpectedWhisperBaseBytes / 2);
        Assert.Equal(0.5, half.Fraction!.Value, 2);

        var all = VoicePreparationProgress.Downloading(VoicePreparationProgress.ExpectedWhisperBaseBytes);
        Assert.Equal(0.99, all.Fraction);

        var more = VoicePreparationProgress.Downloading(VoicePreparationProgress.ExpectedWhisperBaseBytes * 2);
        Assert.Equal(0.99, more.Fraction);
    }
}
