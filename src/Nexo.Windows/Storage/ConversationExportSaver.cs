using System.ComponentModel;
using System.IO;
using System.Text;
using Nexo.Core.Assistant;
using Nexo.Core.Storage;

namespace Nexo.Windows.Storage;

/// <summary>Cómo acabó exportar la conversación: guardada o no, y si se pudo abrir la carpeta.</summary>
public readonly record struct ConversationExportOutcome(
    bool Saved, bool FolderOpened, string FileName, string Message);

/// <summary>
/// Guardar la conversación y abrir el explorador son dos cosas distintas y se cuentan por separado:
/// si solo falla abrir la carpeta, el archivo SÍ se guardó y decir «No pude guardarla» sería falso y
/// contradiría la cápsula de éxito que ya salió. Vive aquí, y no en la ventana, para poder probarlo
/// sin ella. Los mensajes llevan solo el nombre del archivo, nunca la ruta ni el texto crudo de la
/// excepción (trae el usuario de Windows).
/// </summary>
public static class ConversationExportSaver
{
    public static ConversationExportOutcome Save(
        string folder,
        IReadOnlyList<ConversationMessage> messages,
        DateTimeOffset now,
        Action<string> openFolderSelecting)
    {
        ArgumentNullException.ThrowIfNull(openFolderSelecting);

        FreshFileResult saved;
        try
        {
            Directory.CreateDirectory(folder);

            // Numera («… (2).md») en vez de pisar: el nombre lleva la hora al minuto.
            saved = FreshFileWriter.Write(
                folder,
                ConversationExport.FileName(messages, now),
                ConversationExport.ToMarkdown(messages, now),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new(false, false, string.Empty, SakuraDataWriteException.ReasonFor(exception));
        }

        if (!saved.Saved)
        {
            return new(false, false, string.Empty, saved.Message);
        }

        var fileName = Path.GetFileName(saved.FullPath);
        try
        {
            openFolderSelecting(saved.FullPath);
            return new(true, true, fileName, string.Empty);
        }
        catch (Exception exception) when (
            exception is Win32Exception or IOException or InvalidOperationException)
        {
            return new(true, false, fileName, "Conversación guardada; no pude abrir la carpeta.");
        }
    }
}
