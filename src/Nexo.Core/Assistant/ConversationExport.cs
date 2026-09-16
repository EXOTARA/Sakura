using System.Globalization;
using System.Text;

namespace Nexo.Core.Assistant;

/// <summary>
/// 2026-09-16 — guardar una conversación como archivo Markdown. Hasta ahora solo salía dentro del
/// paquete de soporte; la investigación lo marca como algo que la gente busca para no perder lo
/// hablado.
/// </summary>
public static class ConversationExport
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    public static string ToMarkdown(IReadOnlyList<ConversationMessage> messages, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var builder = new StringBuilder();
        builder.Append("# Conversación con Sakura\n\n");
        builder.Append("Exportada el ").Append(now.ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm", Spanish)).Append(".\n");

        foreach (var message in messages)
        {
            var who = message.Role == ConversationRole.User ? "Tú" : "Sakura";
            builder.Append("\n## ").Append(who)
                .Append(" · ").Append(message.CreatedAt.ToString("HH:mm", CultureInfo.InvariantCulture))
                .Append("\n\n")
                .Append(message.Text.Trim())
                .Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>«2026-09-16 15.30 - Cómo resolver la bisección.md», sin caracteres que Windows no acepta.</summary>
    public static string FileName(IReadOnlyList<ConversationMessage> messages, DateTimeOffset now)
    {
        var first = messages.FirstOrDefault(message => message.Role == ConversationRole.User)?.Text ?? "Conversación";
        var words = string.Join(' ', first.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(6));
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(words.Where(ch => !invalid.Contains(ch)).ToArray()).Trim().TrimEnd('.');
        if (safe.Length > 60)
        {
            safe = safe[..60].TrimEnd();
        }

        if (safe.Length == 0)
        {
            safe = "Conversación";
        }

        return $"{now:yyyy-MM-dd HH.mm} - {safe}.md";
    }
}
