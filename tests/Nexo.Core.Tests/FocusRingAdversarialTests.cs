using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class FocusRingAdversarialTests
{
    [Fact]
    public void FocusRingMeetsThreeToOneOnEverySurfaceForGreysAndExtremes()
    {
        var accents = new List<RgbColor>();
        for (var v = 0; v < 256; v++) accents.Add(new RgbColor((byte)v, (byte)v, (byte)v));
        foreach (var r in new byte[] { 0, 255 })
            foreach (var g in new byte[] { 0, 255 })
                foreach (var b in new byte[] { 0, 255 })
                    accents.Add(new RgbColor(r, g, b));
        for (var h = 0; h < 360; h += 3)
            foreach (var l in new[] { 0.0, 0.02, 0.2, 0.5, 0.8, 0.98, 1.0 })
                accents.Add(ColorMath.FromHsl(h, 1.0, l));

        foreach (var accent in accents)
        {
            var theme = SakuraThemeBuilder.FromAccent(accent);
            var ring = SakuraThemeBuilder.FocusRing(theme);
            foreach (var (name, surface) in new[]
            {
                ("Background", theme.Background), ("Surface", theme.Surface), ("SurfaceRaised", theme.SurfaceRaised),
                ("SurfaceHover", theme.SurfaceHover), ("SidebarSurface", theme.SidebarSurface), ("Input", theme.Input),
                ("AccentSoft", theme.AccentSoft)
            })
            {
                Assert.True(
                    ColorMath.ContrastRatio(ring, surface) >= SakuraThemeBuilder.MinimumFocusContrast,
                    $"accent {accent.ToHex()} ring {ring.ToHex()} vs {name} {surface.ToHex()} = {ColorMath.ContrastRatio(ring, surface):F2}");
            }
        }
    }

    [Fact]
    public void FocusRingKeepsTheAccentWhenItAlreadyPasses()
    {
        var accent = RgbColor.FromHex("#F4A6C0");
        var theme = SakuraThemeBuilder.FromAccent(accent);
        Assert.Equal(accent, SakuraThemeBuilder.FocusRing(theme));
    }
}
