namespace Nexo.Core.Resources;

/// <summary>
/// 2026-09-28 — jugando a pantalla completa, Alt + A abría el panel de Sakura: <c>RegisterHotKey</c>
/// hace que Windows se coma la combinación y el juego nunca la recibe. La única forma de dejársela
/// al juego es soltar el atajo mientras dure. Esta es la decisión de cuándo soltar, aparte de Windows
/// para poder probarla; usa la misma lista de procesos ignorados que el gobernador de recursos para
/// que ambos coincidan en qué cuenta como «juego» (el escritorio y el menú Inicio no cuentan).
/// </summary>
public static class GlobalHotkeyReleasePolicy
{
    public static bool ShouldRelease(
        bool isForegroundFullScreen,
        string? foregroundProcessName,
        bool isOwnWindow,
        bool resourceGovernorEnabled) =>
        resourceGovernorEnabled &&
        isForegroundFullScreen &&
        !isOwnWindow &&
        !ResourceGovernorPolicy.IsIgnoredFullScreenProcess(foregroundProcessName);
}
