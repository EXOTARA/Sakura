namespace Nexo.Core.Productization;

/// <summary>
/// 2026-09-16 — «Enviar comentarios» (Adler quiere opiniones de gente real).
///
/// Sakura no manda nada por su cuenta: arma el texto y abre el navegador en la página de reportes
/// del proyecto con el texto ya puesto, o lo copia para enviarlo por otro medio. Solo lleva lo que la
/// persona escribió y, si lo deja marcado, la versión de Sakura y de Windows; nada más del equipo.
/// </summary>
public static class FeedbackReport
{
    public const string IssuesUrl = "https://github.com/EXOTARA/Sakura/issues/new";

    /// <summary>Tope para que la dirección no pase de lo que aceptan los navegadores.</summary>
    public const int MaximumLength = 4000;

    public static bool CanSend(string? text) => !string.IsNullOrWhiteSpace(text);

    public static string Body(string text, string? version, string? windows)
    {
        var body = text.Trim();
        if (body.Length > MaximumLength)
        {
            body = body[..MaximumLength];
        }

        if (version is not null || windows is not null)
        {
            body += "\n\n---\n";
            if (version is not null)
            {
                body += $"Sakura {version}\n";
            }

            if (windows is not null)
            {
                body += $"{windows}\n";
            }
        }

        return body;
    }

    /// <summary>El título: la primera línea, corta.</summary>
    public static string Title(string text)
    {
        var first = text.Trim().Split('\n', 2)[0].Trim();
        return first.Length <= 70 ? first : first[..67].TrimEnd() + "…";
    }

    public static string IssueUrl(string text, string? version, string? windows) =>
        $"{IssuesUrl}?title={Uri.EscapeDataString("Comentario: " + Title(text))}" +
        $"&body={Uri.EscapeDataString(Body(text, version, windows))}";
}
