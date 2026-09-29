namespace Nexo.Core.Ambient;

/// <summary>
/// 2026-09-28 (auditoría de seguridad) — el insertor de Flow convierte cada salto de línea en Enter.
/// En una terminal, Enter ejecuta lo escrito: texto de varias líneas que venga de la IA (no de lo
/// que la persona dictó) sería una orden ejecutada sin que nadie la leyera. Esta política responde
/// solo a «¿es una terminal?»; qué hacer con ello lo decide quien escribe.
/// </summary>
public static class TerminalWindowPolicy
{
    // Nombre de proceso sin «.exe», que es como lo entrega <see cref="AmbientContextSnapshot"/>.
    private static readonly HashSet<string> TerminalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsTerminal", "cmd", "powershell", "pwsh", "conhost", "OpenConsole", "wsl", "bash",
        "mintty", "alacritty", "wezterm-gui", "putty", "kitty", "ConEmu64", "ConEmuC64", "Hyper",
        "Tabby", "ttermpro", "MobaXterm", "Terminus", "cmder"
    };

    public static bool IsTerminal(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return TerminalProcesses.Contains(name);
    }

    /// <summary>
    /// True si escribir <paramref name="text"/> en esa ventana pulsaría Enter en una terminal. Si el
    /// nombre del proceso no se pudo leer, se trata como terminal (fallo cerrado). Limitación: el
    /// terminal integrado de un IDE (Code, JetBrains) es el mismo proceso que el editor y no se
    /// distingue (L20).
    /// </summary>
    public static bool WouldRunCommands(string? processName, string? text) =>
        (string.IsNullOrWhiteSpace(processName) || IsTerminal(processName)) &&
        text is not null && text.AsSpan().IndexOfAny('\r', '\n') >= 0;
}
