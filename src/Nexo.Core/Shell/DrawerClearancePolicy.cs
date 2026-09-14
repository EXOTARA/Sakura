namespace Nexo.Core.Shell;

/// <summary>Un rectángulo en coordenadas de pantalla, en píxeles independientes del dispositivo.</summary>
public readonly record struct ScreenBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

/// <summary>
/// Cuánto tiene que bajar el borde superior del shell para que el panel superior no lo tape.
///
/// Adler (2026-09-14): «el panel de arriba tapa el panel de la derecha; al detectarlo debería
/// encogerse un poco hacia abajo hasta que no lo tape». Esto decide ese «un poco»: lo justo para
/// quedar debajo del panel con un respiro, nada si no se tocan, y nunca tanto que el shell se quede
/// en una tira inservible.
/// </summary>
public static class DrawerClearancePolicy
{
    /// <summary>Aire entre el borde inferior del panel superior y el shell, cuando se apartan.</summary>
    public const double Gap = 12;

    /// <summary>Alto por debajo del cual el shell ya no se encoge más: prefiere quedar tapado a ser ilegible.</summary>
    public const double MinimumShellHeight = 320;

    public static double TopInset(ScreenBounds shell, ScreenBounds drawer)
    {
        // Solo cuenta si se solapan de lado a lado: con el shell en otro monitor, o en el otro
        // extremo de la pantalla, no hay nada que apartar.
        var overlapsHorizontally = drawer.Left < shell.Right && drawer.Right > shell.Left;
        if (!overlapsHorizontally || drawer.Bottom <= shell.Top || drawer.Height <= 0)
        {
            return 0;
        }

        var needed = drawer.Bottom + Gap - shell.Top;
        var available = Math.Max(0, shell.Height - MinimumShellHeight);
        return Math.Clamp(needed, 0, available);
    }
}
