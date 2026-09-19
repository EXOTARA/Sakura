using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

public sealed class DocumentFileNameAdversarialTests
{
    private static string Trailing(string s) => s;

    [Theory]
    [InlineData("CON​")]
    [InlineData("nul.")]
    [InlineData("  aux  ")]
    [InlineData("COM0")]
    public void Reserved_variants_never_produce_device_names(string title)
    {
        var name = DocumentFileName.Sanitize(title, ".docx", "Respuesta");
        var head = name.Split('.')[0].Trim();
        Assert.DoesNotContain(head, new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cut_never_leaves_a_lone_surrogate()
    {
        var title = new string('a', 95) + "😀😀😀";
        var name = DocumentFileName.Sanitize(title, ".docx", "x");
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsHighSurrogate(name[i])) { Assert.True(i + 1 < name.Length && char.IsLowSurrogate(name[i + 1])); i++; }
            else Assert.False(char.IsLowSurrogate(name[i]));
        }
    }

    [Fact]
    public void Trailing_dot_after_extension_is_not_doubled()
    {
        Assert.Equal("informe.docx", DocumentFileName.Sanitize("informe.docx.", ".docx", "x"));
    }

    [Fact]
    public void Length_including_suffix_stays_bounded()
    {
        var name = DocumentFileName.Sanitize("CON" + new string('a', 200), ".docx", "x");
        Assert.True(name.Length <= 96 + 5 + 12, name.Length.ToString());
    }

    [Fact]
    public void Bidi_override_and_controls_are_not_kept()
    {
        var name = DocumentFileName.Sanitize("abc‮txt.exe", ".docx", "x");
        Assert.DoesNotContain('‮', name);
    }

    [Fact]
    public void Only_zero_width_falls_back()
    {
        var name = DocumentFileName.Sanitize("​​", ".docx", "Respuesta");
        Assert.Equal("Respuesta.docx", name);
    }
}
