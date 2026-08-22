using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D62 — cuándo mirar si hay actualización y cuándo ofrecerla.
///
/// Las reglas que se prueban aquí salen de lo que hace insoportable a un actualizador, no de lo que
/// le falta: preguntar dos veces lo mismo, devolverte a una beta que ya dejaste, y consultar la red
/// sin parar.
/// </summary>
public sealed class UpdateCheckPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static SakuraVersion V(string text)
    {
        Assert.True(SakuraVersion.TryParse(text, out var version));
        return version;
    }

    // ---------- cuándo mirar ----------

    [Fact]
    public void TheFirstTime_ItAlwaysChecks() =>
        Assert.True(UpdateCheckPolicy.ShouldCheck(
            lastCheckedAt: null, Now, automaticChecksEnabled: true));

    [Fact]
    public void JustAfterChecking_ItDoesNotCheckAgain()
    {
        // En un programa que arranca con el sistema, comprobar en cada arranque multiplica el
        // tráfico por algo que nadie pidió.
        Assert.False(UpdateCheckPolicy.ShouldCheck(
            Now.AddHours(-1), Now, automaticChecksEnabled: true));
    }

    [Fact]
    public void AfterADay_ItChecks() =>
        Assert.True(UpdateCheckPolicy.ShouldCheck(
            Now.AddHours(-25), Now, automaticChecksEnabled: true));

    [Fact]
    public void AClockThatWentBackwards_DoesNotBlockCheckingForever()
    {
        // Un cambio de zona horaria o una hora corregida dejaría la marca en el futuro, y comparar
        // a secas mantendría la comprobación bloqueada hasta que el tiempo la alcanzara.
        Assert.True(UpdateCheckPolicy.ShouldCheck(
            Now.AddDays(30), Now, automaticChecksEnabled: true));
    }

    [Fact]
    public void WhenAutomaticChecksAreOff_ItNeverChecks()
    {
        // Es la única petición de red que Sakura hace sin que nadie la pida en ese momento, y la
        // política de privacidad promete que se puede apagar. Aquí es donde esa promesa se cumple
        // o se rompe: si esta puerta se abre, da igual lo que diga el ajuste.
        Assert.False(UpdateCheckPolicy.ShouldCheck(
            lastCheckedAt: null, Now, automaticChecksEnabled: false));

        Assert.False(UpdateCheckPolicy.ShouldCheck(
            Now.AddDays(-100), Now, automaticChecksEnabled: false));
    }

    // ---------- qué ofrecer ----------

    [Fact]
    public void ANewerVersion_IsOffered()
    {
        var decision = UpdateCheckPolicy.Evaluate(
            V("0.9.5"), V("0.9.6"), skipped: null, wantsPreReleases: false);

        Assert.True(decision.ShouldOffer);
        Assert.Equal(UpdateVerdict.Available, decision.Verdict);
    }

    [Fact]
    public void TheSameVersion_IsNotOffered()
    {
        // Lo que evita que Sakura se ofrezca actualizarse a lo que ya tiene puesto.
        var decision = UpdateCheckPolicy.Evaluate(
            V("0.9.5-beta"), V("0.9.5-beta"), skipped: null, wantsPreReleases: true);

        Assert.Equal(UpdateVerdict.AlreadyCurrent, decision.Verdict);
    }

    [Fact]
    public void AnOlderVersion_IsNotOffered() =>
        Assert.Equal(
            UpdateVerdict.AlreadyCurrent,
            UpdateCheckPolicy.Evaluate(V("1.0.0"), V("0.9.9"), null, false).Verdict);

    [Fact]
    public void APreRelease_IsNotOfferedToSomeoneOnAFinal()
    {
        // Salir de beta es una decisión. Devolver a alguien a una beta porque su número es mayor es
        // lo contrario de lo que pidió.
        var decision = UpdateCheckPolicy.Evaluate(
            V("1.0.0"), V("1.1.0-beta"), skipped: null, wantsPreReleases: false);

        Assert.Equal(UpdateVerdict.PreReleaseNotWanted, decision.Verdict);
    }

    [Fact]
    public void APreRelease_IsOfferedToSomeoneAlreadyOnOne()
    {
        var decision = UpdateCheckPolicy.Evaluate(
            V("0.9.5-beta"), V("0.9.6-beta"), skipped: null, wantsPreReleases: true);

        Assert.True(decision.ShouldOffer);
    }

    [Fact]
    public void ARejectedVersion_IsNotAskedAboutAgain()
    {
        // «Ahora no» a la 1.2.0 no significa «vuelve a preguntarme mañana».
        var decision = UpdateCheckPolicy.Evaluate(
            V("1.1.0"), V("1.2.0"), skipped: V("1.2.0"), wantsPreReleases: false);

        Assert.Equal(UpdateVerdict.Skipped, decision.Verdict);
    }

    [Fact]
    public void ALaterVersionThanTheRejectedOne_IsStillOffered()
    {
        // El «no» era a aquella versión, no a todas las que vengan después.
        var decision = UpdateCheckPolicy.Evaluate(
            V("1.1.0"), V("1.3.0"), skipped: V("1.2.0"), wantsPreReleases: false);

        Assert.True(decision.ShouldOffer);
    }

    [Fact]
    public void WhatYouAreRunningDecidesWhetherYouGetPreReleases()
    {
        // Sin un ajuste que preguntar: lo que ya tienes puesto lo dice.
        Assert.True(UpdateCheckPolicy.AcceptsPreReleases(V("0.9.5-beta")));
        Assert.False(UpdateCheckPolicy.AcceptsPreReleases(V("1.0.0")));
    }
}
