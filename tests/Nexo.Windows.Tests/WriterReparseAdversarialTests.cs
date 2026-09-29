using Nexo.Windows.Workspace;

namespace Nexo.Windows.Tests;

/// <summary>Pruebas adversarias (tester 2026-09-28) contra CrossesReparsePoint.</summary>
public sealed class WriterReparseAdversarialTests : IDisposable
{
    private readonly string _base;
    private readonly string _root;
    private readonly string _outside;
    private readonly FileSystemWorkspaceWriter _writer = new();
    private readonly List<string> _links = [];

    public WriterReparseAdversarialTests()
    {
        _base = Path.Combine(Path.GetTempPath(), "kohana-adv-tests", Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_base, "proj");
        _outside = Path.Combine(_base, "fuera");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        foreach (var link in _links)
        {
            try { if (Directory.Exists(link)) Directory.Delete(link); else File.Delete(link); } catch { }
        }
        try { Directory.Delete(_base, recursive: true); } catch { }
    }

    private void Junction(string link, string target)
    {
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { CreateNoWindow = true, UseShellExecute = false })!;
        p.WaitForExit();
        Assert.Equal(0, p.ExitCode);
        _links.Add(link);
    }

    [Fact]
    public void FileSymlink_ToOutsideFile_IsRefused()
    {
        var victim = Path.Combine(_outside, "victima.txt");
        File.WriteAllText(victim, "original");
        var link = Path.Combine(_root, "a.txt");
        File.CreateSymbolicLink(link, victim);
        _links.Add(link);

        var r = _writer.WriteFile(_root, "a.txt", "PISADO");

        Assert.Equal("original", File.ReadAllText(victim));
        Assert.False(r.Success);
    }

    [Fact]
    public void DanglingFileSymlink_AsTmp_IsRefused()
    {
        var target = Path.Combine(_outside, "nuevo.txt"); // no existe
        var link = Path.Combine(_root, "a.txt.kohana-tmp");
        File.CreateSymbolicLink(link, target);
        _links.Add(link);

        var r = _writer.WriteFile(_root, "a.txt", "PISADO");

        Assert.False(File.Exists(target));
        Assert.False(r.Success);
    }

    [Fact]
    public void DeepJunction_NonExistentDestination_IsRefused()
    {
        Directory.CreateDirectory(Path.Combine(_root, "a", "b"));
        Junction(Path.Combine(_root, "a", "b", "link"), _outside);

        var r = _writer.WriteFile(_root, Path.Combine("a", "b", "link", "c", "d", "x.txt"), "PISADO");

        Assert.Empty(Directory.GetFileSystemEntries(_outside));
        Assert.False(r.Success);
    }

    [Fact]
    public void Junction_DifferentCase_IsRefused()
    {
        Junction(Path.Combine(_root, "docs"), _outside);

        var r = _writer.WriteFile(_root, @"DOCS\x.txt", "PISADO");

        Assert.Empty(Directory.GetFileSystemEntries(_outside));
        Assert.False(r.Success);
    }

    [Fact]
    public void Junction_ReenteredWithDotDot_IsRefused()
    {
        Junction(Path.Combine(_root, "docs"), _outside);

        var r = _writer.WriteFile(_root, @"x\..\docs\x.txt", "PISADO");

        Assert.Empty(Directory.GetFileSystemEntries(_outside));
        Assert.False(r.Success);
    }

    [Fact]
    public void Junction_RootGivenWithTrailingSeparatorAndDotSegments_IsRefused()
    {
        Junction(Path.Combine(_root, "docs"), _outside);
        var weirdRoot = _root + @"\sub\..\";

        var r = _writer.WriteFile(weirdRoot, @"docs\x.txt", "PISADO");

        Assert.Empty(Directory.GetFileSystemEntries(_outside));
        Assert.False(r.Success);
    }

    [Fact]
    public void Junction_RootGivenWithExtendedPrefix_IsRefused()
    {
        Junction(Path.Combine(_root, "docs"), _outside);

        var r = _writer.WriteFile(@"\\?\" + _root, @"docs\x.txt", "PISADO");

        Assert.Empty(Directory.GetFileSystemEntries(_outside));
        Assert.False(r.Success);
    }

    [Fact]
    public void Junction_RelativePathWithExtendedPrefix_IsRefused()
    {
        Junction(Path.Combine(_root, "docs"), _outside);

        var r = _writer.WriteFile(_root, @"\\?\" + Path.Combine(_root, "docs", "x.txt"), "PISADO");

        Assert.Empty(Directory.GetFileSystemEntries(_outside));
        Assert.False(r.Success);
    }

    [Fact]
    public void Junction_TrailingDotOrSpaceInSegment_IsRefused()
    {
        Junction(Path.Combine(_root, "docs"), _outside);

        var r = _writer.WriteFile(_root, @"docs.\x.txt", "PISADO");

        Assert.Empty(Directory.GetFileSystemEntries(_outside));
        Assert.False(r.Success);
    }

    [Fact]
    public void DeleteViaFileSymlink_DoesNotRemoveOutsideFile()
    {
        var victim = Path.Combine(_outside, "victima.txt");
        File.WriteAllText(victim, "original");
        var link = Path.Combine(_root, "a.txt");
        File.CreateSymbolicLink(link, victim);
        _links.Add(link);

        var r = _writer.DeleteFile(_root, "a.txt");

        Assert.True(File.Exists(victim));
        Assert.False(r.Success);
    }
}
