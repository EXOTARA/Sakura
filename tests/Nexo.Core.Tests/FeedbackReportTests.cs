using Nexo.Core.Productization;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class FeedbackReportTests
{
    [Fact]
    public void TheLink_CarriesOnlyWhatWasWritten_AndTheVersionsIfAllowed()
    {
        var url = FeedbackReport.IssueUrl("El repaso sale dos veces\nal abrir", "0.30.26-beta", "Windows 11");

        Assert.StartsWith(FeedbackReport.IssuesUrl + "?title=", url);
        var body = Uri.UnescapeDataString(url[(url.IndexOf("&body=", StringComparison.Ordinal) + 6)..]);
        Assert.Equal("El repaso sale dos veces\nal abrir\n\n---\nSakura 0.30.26-beta\nWindows 11\n", body);
    }

    [Fact]
    public void WithoutVersions_TheBodyIsJustTheText() =>
        Assert.Equal("Hola", FeedbackReport.Body("  Hola ", null, null));

    [Fact]
    public void LongTitles_AreShortened() =>
        Assert.True(FeedbackReport.Title(new string('a', 200)).Length <= 70);

    [Fact]
    public void EmptyText_CannotBeSent() =>
        Assert.False(FeedbackReport.CanSend("   "));
}
