using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D58 — lo que se abre por roce tiene que saber irse solo.
///
/// Adler lo dijo así: pasar el ratón sin querer deja Sakura abierta «a pesar de que ya no pasó
/// nada», y eso es invasivo. 2026-10: también pidió quitar la excepción por «atención» — tocar
/// algo ya no debe dejarlo pegado para siempre, solo importa si el puntero está fuera.
/// </summary>
public sealed class UnattendedRevealPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 4, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset LeftAgo(TimeSpan ago) => Now - ago;

    [Fact]
    public void AfterTheGrace_WithNobodyThere_ItRetracts() =>
        Assert.True(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: LeftAgo(TimeSpan.FromSeconds(3)),
            Now));

    [Fact]
    public void WithinTheGrace_ItStays()
    {
        // Salirse un momento a por el teclado no es abandonar la ventana.
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: LeftAgo(TimeSpan.FromSeconds(1)),
            Now));
    }

    [Fact]
    public void WithThePointerInside_ItStays() =>
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: null,
            Now));

    [Fact]
    public void AfterTheGrace_EvenIfItWasTouched_ItRetracts()
    {
        // 2026-10 — Adler pidió quitar la excepción: haber tocado algo (escribir, foco) ya no
        // mantiene el panel abierto si el puntero lleva fuera más que la gracia.
        Assert.True(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: LeftAgo(TimeSpan.FromMinutes(5)),
            Now));
    }

    [Fact]
    public void WhatWasOpenedOnPurpose_StaysOnPurpose()
    {
        // Atajo o icono de la bandeja: retirar algo que se acaba de pedir es desobedecer.
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: false,
            outsideSince: LeftAgo(TimeSpan.FromMinutes(5)),
            Now));
    }

    [Fact]
    public void AClockThatWentBackwards_IsNotAnElapsedWait() =>
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: Now + TimeSpan.FromSeconds(30),
            Now));
}
