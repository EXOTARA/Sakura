using System.Diagnostics;
using System.Text;
using Nexo.Core.ComputerUse;

namespace Nexo.Windows.ComputerUse;

/// <summary>
/// Diseño D18 (Fase 7, nivel 4) — lo que de verdad toca el equipo. Dos métodos, los dos que
/// <see cref="WindowsComputerUseMethodProbe"/> declara disponibles.
///
/// El comando se lanza **sin shell** (<c>UseShellExecute = false</c>) y con el ejecutable y los
/// argumentos tal cual vienen del catálogo. No se compone ninguna cadena: no hay interpolación, no
/// hay entrada del usuario ni del modelo, y por tanto no hay nada que escapar. Una cadena que se
/// compone es una cadena en la que alguien acaba metiendo algo.
///
/// El portapapeles requiere hilo STA. Sakura lo llama desde el hilo de interfaz, que ya lo es; si
/// algún día se llamara desde otro sitio, fallaría de forma visible en vez de en silencio.
/// </summary>
public sealed class WindowsComputerUseExecutor : IComputerUseExecutor
{
    /// <summary>
    /// Corrección de un defecto real visto ejecutando «Ver la configuración de red»: la salida
    /// llegaba como «Concesi¢n obtenida» y «Direcci¢n f¡sica». Las herramientas de consola de Windows
    /// (<c>ipconfig</c>, <c>nslookup</c>…) escriben en la página de códigos OEM de la consola —CP850
    /// en un Windows en español—, pero al redirigir la salida sin decir nada, .NET la decodifica con
    /// la codificación ANSI por defecto del proceso. Coinciden en ASCII y difieren justo en las
    /// vocales acentuadas y la eñe, así que el fallo solo aparece en español, que es el idioma de la
    /// app. La salida de un diagnóstico ilegible no es un detalle estético: es el producto entero de
    /// la función.
    /// </summary>
    private static readonly Encoding ConsoleOutputEncoding = ResolveConsoleOutputEncoding();

    private static Encoding ResolveConsoleOutputEncoding()
    {
        // .NET (Core) solo trae UTF-8 y Latin1 de fábrica; las páginas OEM llegan con este proveedor.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        try
        {
            return Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException)
        {
            // Si esa página no está disponible, UTF-8 es mejor apuesta que la ANSI por defecto:
            // falla en menos sitios y nunca inventa un carácter distinto para un byte ASCII.
            return Encoding.UTF8;
        }
    }

    public string? ReadClipboard()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText()
                : string.Empty;
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Otro programa puede tener el portapapeles bloqueado. Devolver null y no una cadena
            // vacía importa: sin saber qué había, el coordinador no puede prometer deshacerlo.
            return null;
        }
    }

    public ComputerUseStepResult SetClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            return ComputerUseStepResult.Ok("Copiado.");
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            return ComputerUseStepResult.Failed(
                "Otro programa tiene el portapapeles ocupado, así que no pude copiarlo.");
        }
    }

    public ComputerUseStepResult RunSafeCommand(SafeShellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Última puerta antes del proceso: solo se ejecuta lo que está en el catálogo. La
        // comprobación se repite aunque el coordinador ya la haga, igual que en la escritura de
        // archivos: lanzar un proceso que no debía no tiene arreglo.
        if (!SafeShellCatalog.IsReadOnly(command))
        {
            return ComputerUseStepResult.Failed("Ese comando no está en la lista de permitidos.");
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command.Executable,
                    Arguments = command.Arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = ConsoleOutputEncoding,
                    StandardErrorEncoding = ConsoleOutputEncoding
                }
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            // Corrección de un defecto real: leer stdout entero y LUEGO stderr entero (como hacía
            // esto antes) puede interbloquearse — es el problema documentado de .NET con Process y
            // salida redirigida. Si el proceso llena el búfer de un canal mientras el padre sigue
            // leyendo el otro, el hijo se queda esperando que alguien vacíe un búfer que nadie está
            // leyendo, y el padre espera a que el hijo termine de escribir. `WaitForExit(timeout)`
            // nunca llegaba a proteger contra esto, porque se llamaba DESPUÉS de los dos
            // `ReadToEnd()`: un bloqueo real se habría colgado para siempre, sin límite de tiempo.
            // La lectura por eventos, en cambio, drena los dos canales a la vez mientras el proceso
            // corre.
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    output.AppendLine(args.Data);
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    error.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // El tiempo lo trae el comando: enumerar controladores no es esperar a ipconfig.
            if (!process.WaitForExit((int)command.Timeout.TotalMilliseconds))
            {
                TryKill(process);
                return ComputerUseStepResult.Failed(
                    $"«{command.Title}» tardó demasiado, así que lo corté.");
            }

            // La sobrecarga sin argumentos, llamada DESPUÉS de saber que el proceso ya terminó,
            // asegura que los últimos eventos de salida asíncrona ya se entregaron antes de leer
            // los StringBuilder — WaitForExit(int) por sí sola no lo garantiza.
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var errorText = error.ToString().Trim();
                return ComputerUseStepResult.Failed(
                    $"«{command.Title}» terminó con error{(string.IsNullOrWhiteSpace(errorText) ? "." : $": {errorText}")}");
            }

            return ComputerUseStepResult.Ok($"Listo: {command.Title}.", output.ToString());
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return ComputerUseStepResult.Failed($"No pude ejecutar «{command.Title}»: {exception.Message}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Ya había terminado o no se puede matar; no hay nada mejor que hacer aquí.
        }
    }
}
