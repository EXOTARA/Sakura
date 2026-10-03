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

    // Revisión adversarial 2026-10-03: en la consola clásica (conhost), Windows atribuye la ventana al
    // primer programa que la usa, no a conhost. Abrir python, ssh, node o ubuntu directamente (Win + R,
    // doble clic) da una consola cuyo proceso no está en la lista de arriba, y ahí Enter ejecuta igual.
    // La clase de ventana no depende de qué programa corra dentro.
    private static readonly HashSet<string> TerminalWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ConsoleWindowClass",              // conhost, la consola clásica
        "CASCADIA_HOSTING_WINDOW_CLASS",   // Windows Terminal
        "PseudoConsoleWindow",             // ventana oculta de una pseudoconsola
        "VirtualConsoleClass",             // ConEmu
        "mintty",                          // Git Bash, Cygwin, MSYS2
        "PuTTY"
    };

    public static bool IsTerminalWindowClass(string? windowClass) =>
        !string.IsNullOrWhiteSpace(windowClass) && TerminalWindowClasses.Contains(windowClass.Trim());

    /// <summary>
    /// True si escribir <paramref name="text"/> en esa ventana pulsaría Enter en una terminal. Si el
    /// nombre del proceso no se pudo leer, se trata como terminal (fallo cerrado). Limitación: el
    /// terminal integrado de un IDE (Code, JetBrains) es el mismo proceso que el editor y no se
    /// distingue (L20).
    /// </summary>
    public static bool WouldRunCommands(string? processName, string? text) =>
        WouldRunCommands(processName, windowClass: null, text);

    /// <summary>
    /// Igual, pero también cuenta como terminal una ventana cuya clase es de consola, sea cual sea el
    /// programa que corre dentro.
    /// </summary>
    public static bool WouldRunCommands(string? processName, string? windowClass, string? text) =>
        (string.IsNullOrWhiteSpace(processName) || IsTerminal(processName) || IsTerminalWindowClass(windowClass)) &&
        text is not null && text.AsSpan().IndexOfAny('\r', '\n') >= 0;
}
