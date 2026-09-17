using Nexo.Core.Files;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class FileSearchPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 15, 0, 0, TimeSpan.FromHours(-6));

    [Theory]
    [InlineData(null, false)]
    [InlineData(" a ", false)]
    [InlineData("u2", true)]
    public void ShortQueries_AreNotSearched(string? query, bool expected) =>
        Assert.Equal(expected, FileSearchPolicy.CanSearch(query));

    [Fact]
    public void NameMatches_ComeFirst_ThenTheNewest_WithoutDuplicates()
    {
        var results = FileSearchPolicy.Rank(
            [
                new FileSearchResult("notas.docx", @"C:\u\notas.docx", @"C:\u", Now),
                new FileSearchResult("bisección.xlsx", @"C:\u\bisección.xlsx", @"C:\u", Now.AddDays(-5)),
                new FileSearchResult("Bisección final.xlsx", @"C:\u\Bisección final.xlsx", @"C:\u", Now.AddDays(-1)),
                new FileSearchResult("bisección.xlsx", @"C:\U\BISECCIÓN.XLSX", @"C:\u", Now.AddDays(-5))
            ],
            "bisección",
            10);

        Assert.Equal(["Bisección final.xlsx", "bisección.xlsx", "notas.docx"], results.Select(r => r.Name));
    }

    [Theory]
    [InlineData(0, "hoy")]
    [InlineData(1, "ayer")]
    [InlineData(3, "hace 3 días")]
    [InlineData(30, "17 de agosto")]
    public void When_ReadsNaturally(int daysAgo, string expected) =>
        Assert.Equal(expected, FileSearchPolicy.When(Now.AddDays(-daysAgo), Now));

    [Fact]
    public void Roots_SkipEmptyAndRepeated()
    {
        var roots = FileSearchPolicy.Roots(folder => folder == Environment.SpecialFolder.MyMusic ? string.Empty : @"C:\u\Docs", @"C:\u");

        Assert.Equal([@"C:\u\Docs", @"C:\u\Downloads"], roots);
    }
}
