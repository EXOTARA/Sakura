using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class CaptureFileNamesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 9, 5, 7, TimeSpan.FromHours(-6));

    [Fact]
    public void Screenshot_GoesToASakuraFolderWithDateAndTime()
    {
        var path = CaptureFileNames.ScreenshotPath(@"C:\Users\A\Pictures", Now, _ => false);
        Assert.Equal(@"C:\Users\A\Pictures\Sakura\Captura 2026-09-15 09-05-07.png", path);
    }

    [Fact]
    public void Recording_IsAnMp4InVideos()
    {
        var path = CaptureFileNames.RecordingPath(@"C:\Users\A\Videos", Now, _ => false);
        Assert.Equal(@"C:\Users\A\Videos\Sakura\Grabación 2026-09-15 09-05-07.mp4", path);
    }

    [Fact]
    public void AnExistingFile_IsNeverOverwritten()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\P\Sakura\Captura 2026-09-15 09-05-07.png",
            @"C:\P\Sakura\Captura 2026-09-15 09-05-07 (2).png"
        };

        Assert.Equal(@"C:\P\Sakura\Captura 2026-09-15 09-05-07 (3).png",
            CaptureFileNames.ScreenshotPath(@"C:\P", Now, taken.Contains));
    }
}
