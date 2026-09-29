namespace Nexo.Core.Resources;

/// <summary>Qué soltar y qué registrar para llegar al estado deseado de los atajos globales.</summary>
public sealed record GlobalHotkeyPlan(IReadOnlyList<int> Unregister, IReadOnlyList<int> Register)
{
    public static GlobalHotkeyPlan Empty { get; } = new([], []);
}

/// <summary>
/// 2026-09-28 — la reconciliación de atajos no puede ser una sola bandera «soltados sí/no»: si al
/// volver de un juego otra aplicación se quedó con un atajo, ese atajo quedaba muerto hasta reiniciar.
/// Se compara lo que de verdad está registrado con lo que debería estarlo, id por id: así es
/// idempotente (nada que hacer si ya coincide) y reintenta solo lo que falló. Pura: los
/// <c>RegisterHotKey</c> reales se quedan en la ventana.
/// </summary>
public static class GlobalHotkeyReconciler
{
    public static GlobalHotkeyPlan Plan(
        bool release,
        IReadOnlyCollection<int> registered,
        IReadOnlyCollection<int> alwaysWanted,
        int flowHotkeyId,
        bool flowEnabled)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(alwaysWanted);

        // Flow solo se quiere si la persona lo tiene activado; si estaba registrado y ya no, se suelta.
        var wanted = release
            ? new HashSet<int>()
            : new HashSet<int>(alwaysWanted);
        if (!release && flowEnabled)
        {
            wanted.Add(flowHotkeyId);
        }

        var unregister = registered.Where(id => !wanted.Contains(id)).ToList();
        var register = wanted.Where(id => !registered.Contains(id)).ToList();

        return unregister.Count == 0 && register.Count == 0
            ? GlobalHotkeyPlan.Empty
            : new GlobalHotkeyPlan(unregister, register);
    }
}
