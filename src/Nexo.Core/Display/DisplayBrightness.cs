namespace Nexo.Core.Display;

/// <summary>
/// El brillo del monitor tal como está ahora. <paramref name="IsAvailable"/> en falso no es un
/// error: la mayoría de monitores externos no exponen el brillo por cable, y eso es normal.
/// </summary>
public sealed record BrightnessSnapshot(
    bool IsAvailable,
    int Percent,
    string? ErrorMessage = null)
{
    public static BrightnessSnapshot Unavailable(string message) =>
        new(false, 0, message);
}

public interface IDisplayBrightnessService
{
    BrightnessSnapshot ReadSnapshot();

    bool TrySetBrightness(int percent);

    /// <summary>Cuántas pantallas hay; la 0 es la principal.</summary>
    int DisplayCount { get; }

    /// <summary>A qué pantalla van las lecturas y los cambios (<see cref="BrightnessTarget.All"/> = todas).</summary>
    int SelectedDisplay { get; set; }
}

/// <summary>
/// 2026-09-16 — con dos pantallas el mando solo movía la principal (Adler). Ahora se elige cuál:
/// cada una por separado o todas a la vez. El orden al ir tocando es principal → segunda → … → todas.
/// </summary>
public static class BrightnessTarget
{
    public const int All = -1;

    public static int Next(int current, int count) =>
        count <= 1
            ? 0
            : current == All
                ? 0
                : current + 1 >= count ? All : current + 1;

    public static int Clamp(int selected, int count) =>
        selected == All && count > 1 ? All : Math.Clamp(selected, 0, Math.Max(0, count - 1));

    /// <summary>«Pantalla 1», «Pantalla 2», «Todas»; nada si solo hay una.</summary>
    public static string? Label(int selected, int count) =>
        count <= 1 ? null : selected == All ? "Todas" : $"Pantalla {selected + 1}";
}
