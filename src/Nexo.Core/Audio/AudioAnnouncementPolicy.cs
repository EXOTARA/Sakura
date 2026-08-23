namespace Nexo.Core.Audio;

/// <summary>
/// Diseño D77 — cuándo merece la pena avisar de un cambio de audio.
///
/// Hasta ahora se avisaba siempre, y eso convirtió el deslizador de volumen en una fuente de ruido:
/// cada movimiento sacaba una cápsula y dejaba una entrada más en «acciones recientes». Ajustar el
/// volumen no es un acto que ocurra una vez — se toca, se escucha, se vuelve a tocar — así que un
/// aviso por cambio son cinco avisos por decisión.
///
/// La regla no es «avisar menos». Es: **no le cuentes a alguien lo que acaba de hacer con su propia
/// mano y está viendo**. Quien arrastra el deslizador de Audio ya tiene tres realimentaciones
/// delante — el deslizador se mueve, el número cambia y el sonido cambia—; una cápsula encima solo
/// repite lo que ya sabe.
///
/// Cuando el cambio viene de otro sitio —una orden de voz, una rutina, la paleta— la cápsula es la
/// ÚNICA realimentación que hay, y ahí se avisa siempre.
///
/// Los fallos se avisan en los dos casos, mano propia incluida: que algo no se pueda hacer nunca es
/// evidente por sí solo, y un deslizador que se mueve mientras el volumen real no cambia es
/// exactamente el caso en el que hace falta decirlo.
/// </summary>
public static class AudioAnnouncementPolicy
{
    /// <param name="status">Cómo terminó la acción.</param>
    /// <param name="adjustedByHand">
    /// Si quien lo pidió lo hizo manipulando directamente el control, con el resultado a la vista.
    /// </param>
    public static bool ShouldAnnounce(AudioActionStatus status, bool adjustedByHand)
    {
        if (!adjustedByHand)
        {
            return true;
        }

        return status != AudioActionStatus.Success;
    }
}
