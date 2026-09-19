using Nexo.Windows.Storage;

namespace Nexo.Windows.Tests.Documents;

public sealed class FreshFileWriterAdversarialTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sakura-adv", Guid.NewGuid().ToString("N"));
    public FreshFileWriterAdversarialTests() => Directory.CreateDirectory(_folder);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    [Fact]
    public void A_folder_with_the_same_name_is_skipped_not_fatal()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "Informe.docx"));
        var r = FreshFileWriter.Write(_folder, "Informe.docx", new byte[] { 1 });
        Assert.True(r.Saved, r.Message);
        Assert.EndsWith("Informe (2).docx", r.FullPath);
    }

    [Fact]
    public void Numbered_name_ending_in_2_counts_on()
    {
        File.WriteAllText(Path.Combine(_folder, "A (2).docx"), "x");
        var r = FreshFileWriter.Write(_folder, "A (2).docx", new byte[] { 1 });
        Assert.True(r.Saved);
        Assert.EndsWith("A (2) (2).docx", r.FullPath);
    }

    [Fact]
    public void Parallel_writes_lose_nothing()
    {
        var results = new System.Collections.Concurrent.ConcurrentBag<FreshFileResult>();
        Parallel.For(0, 50, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
            results.Add(FreshFileWriter.Write(_folder, "P.docx", new byte[] { (byte)i })));
        Assert.All(results, r => Assert.True(r.Saved, r.Message));
        Assert.Equal(50, results.Select(r => r.FullPath).Distinct().Count());
        Assert.Equal(50, Directory.GetFiles(_folder).Length);
        var bytes = Directory.GetFiles(_folder).Select(f => File.ReadAllBytes(f)[0]).OrderBy(b => b).ToArray();
        Assert.Equal(Enumerable.Range(0, 50).Select(i => (byte)i), bytes);
    }

    [Fact]
    public void Callback_throwing_other_exception_leaves_no_file()
    {
        try
        {
            FreshFileWriter.Write(_folder, "B.docx", s => { s.WriteByte(1); throw new InvalidOperationException("x"); });
        }
        catch (InvalidOperationException) { }
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Failed_delete_of_partial_is_reported_not_thrown()
    {
        var r = FreshFileWriter.Write(_folder, "C.docx", s =>
        {
            s.WriteByte(1);
            // lock is held by the stream itself (FileShare.None) so delete fails
            throw new IOException("disk full");
        });
        Assert.False(r.Saved);
    }

    [Fact]
    public void Path_over_260_does_not_throw_and_reports()
    {
        var deep = _folder;
        for (var i = 0; i < 6; i++) deep = Path.Combine(deep, new string('d', 50));
        Directory.CreateDirectory(deep);
        var r = FreshFileWriter.Write(deep, new string('n', 100) + ".docx", new byte[] { 1 });
        Assert.True(r.Saved || r.Message.Length > 0);
    }

    [Fact]
    public void Lone_surrogate_name_does_not_throw()
    {
        var r = FreshFileWriter.Write(_folder, "a\uD83D.docx", new byte[] { 1 });
        Assert.True(r.Saved || r.Message.Length > 0);
    }
}

