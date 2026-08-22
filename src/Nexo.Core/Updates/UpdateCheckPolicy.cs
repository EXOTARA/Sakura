namespace Nexo.Core.Updates;

/// <summary>Por qué no hay nada que ofrecer, cuando no lo hay.</summary>
public enum UpdateVerdict
{
    /// <summary>Hay una versión más nueva y se puede ofrecer.</summary>
    Available,

    /// <summary>Lo publicado no es más nuevo que lo instalado.</summary>
    AlreadyCurrent,

    /// <summary>Es una preliminar y este equipo no las quiere.</summary>
    PreReleaseNotWanted,

    /// <summary>Esta misma versión ya se ofreció y la persona dijo que no.</summary>
    Skipped
}

/// <summary>Qué hacer con lo que se ha encontrado.</summary>
public readonly record struct UpdateDecision(UpdateVerdict Verdict, SakuraVersion Version)
{
    public bool ShouldOffer => Verdict == UpdateVerdict.Available;
}

/// <summary>
/// Diseño D62 — cuándo mirar si hay actualización, y cuándo ofrecerla.
///
/// Son dos preguntas distintas y conviene no mezclarlas: mirar es barato y se puede hacer solo;
/// ofrecer interrumpe a alguien y hay que merecérselo.
///
/// Las reglas salen de lo que hace insoportable a un actualizador, no de lo que le falta:
///
/// **No se pregunta dos veces por la misma versión.** Quien dice «ahora no» a la 1.2.0 no está
/// diciendo «vuelve a preguntarme mañana»: está diciendo que no quiere esa. Se le vuelve a hablar
/// cuando salga otra.
///
/// **Una preliminar no se le ofrece a quien está en una final.** Salir de beta es una decisión, y
/// devolver a alguien a una beta porque su número es mayor es exactamente lo contrario de lo que
/// pidió. Quien ya está en una preliminar sí las recibe: ahí ya eligió.
///
/// **No se mira sin parar.** Cada comprobación es una petición de red que nadie ha pedido, y en un
/// programa que arranca con el sistema eso se multiplica por cada arranque.
/// </summary>
public static class UpdateCheckPolicy
{
    /// <summary>
    /// Cuánto se espera entre comprobaciones.
    ///
    /// Un día es suficiente: las versiones de Sakura no salen por horas, y comprobar más a menudo
    /// solo añade tráfico que nadie nota salvo cuando falla.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Si toca mirar. Se separa de <see cref="Evaluate"/> porque esto decide si se hace una
    /// petición de red, y eso tiene que poder decidirse sin haberla hecho todavía.
    ///
    /// <paramref name="automaticChecksEnabled"/> es el ajuste de Ajustes → Actualizaciones. Se pasa
    /// aquí y no se comprueba en la ventana para que la única puerta a esa petición de red sea esta
    /// función, que es la que está probada.
    /// </summary>
    public static bool ShouldCheck(
        DateTimeOffset? lastCheckedAt,
        DateTimeOffset now,
        bool automaticChecksEnabled)
    {
        // Quien lo apagó no quiere que se mire menos, quiere que no se mire.
        if (!automaticChecksEnabled)
        {
            return false;
        }

        if (lastCheckedAt is not { } last)
        {
            return true;
        }

        var elapsed = now - last;

        // Un reloj que va hacia atrás —cambio de zona horaria, hora corregida— no puede dejar la
        // comprobación bloqueada hasta que el tiempo alcance a la marca guardada.
        return elapsed < TimeSpan.Zero || elapsed >= MinimumInterval;
    }

    /// <summary>
    /// Qué hacer con la versión encontrada.
    ///
    /// <paramref name="skipped"/> es la última que la persona rechazó, o <c>null</c> si ninguna.
    /// <paramref name="wantsPreReleases"/> se deduce de la versión instalada, no de un ajuste: quien
    /// corre una beta ya dijo que las quiere, y quien corre una final también dijo lo suyo.
    /// </summary>
    public static UpdateDecision Evaluate(
        SakuraVersion current,
        SakuraVersion available,
        SakuraVersion? skipped,
        bool wantsPreReleases)
    {
        if (available <= current)
        {
            return new UpdateDecision(UpdateVerdict.AlreadyCurrent, available);
        }

        if (available.IsPreRelease && !wantsPreReleases)
        {
            return new UpdateDecision(UpdateVerdict.PreReleaseNotWanted, available);
        }

        // Se compara la versión, no «si hay algo rechazado»: una posterior a la rechazada sí se
        // ofrece, porque el «no» era a aquella y no a todas las futuras.
        if (skipped is { } refused && available.CompareTo(refused) == 0)
        {
            return new UpdateDecision(UpdateVerdict.Skipped, available);
        }

        return new UpdateDecision(UpdateVerdict.Available, available);
    }

    /// <summary>
    /// Si este equipo acepta preliminares, deducido de lo que tiene instalado.
    ///
    /// Está aquí y no en las preferencias a propósito: un ajuste más que nadie entiende —«¿quiero
    /// versiones preliminares?»— es un ajuste que se deja como venga y luego sorprende. Lo que ya
    /// tienes puesto dice lo mismo sin preguntar nada.
    /// </summary>
    public static bool AcceptsPreReleases(SakuraVersion current) => current.IsPreRelease;
}
