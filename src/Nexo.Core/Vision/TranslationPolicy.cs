using System.Globalization;

namespace Nexo.Core.Vision;

/// <summary>Qué hacer con lo que se leyó en la zona elegida.</summary>
public sealed record TranslationRequest(bool CanTranslate, string Prompt, string Detail)
{
    public static TranslationRequest Refused(string detail) => new(false, string.Empty, detail);
}

/// <summary>
/// Diseño D86 — traducir un trozo de pantalla.
///
/// El gesto es el de una captura de pantalla y el resultado es texto: se arrastra un rectángulo
/// sobre algo que no se entiende y sale traducido. Sirve para lo que ninguna otra herramienta
/// alcanza — un menú de un juego, un error dentro de una imagen, un PDF que no deja seleccionar.
///
/// Dos reglas que no son de presentación y por eso viven aquí:
///
/// **Si el OCR no leyó nada, no se pregunta a nadie.** Mandar una imagen en blanco a un modelo
/// gasta una llamada y devuelve una disculpa educada. Es más rápido y más honesto decir que ahí no
/// había texto.
///
/// **Lo que estaba tapado por ser sensible sigue tapado.** El texto llega ya redactado por
/// <see cref="SensitiveContentRedactor"/>, y esta política no lo desredacta ni pide el original: si
/// alguien arrastra el recuadro sobre un gestor de contraseñas, lo que viaja son las marcas, no la
/// contraseña.
/// </summary>
public static class TranslationPolicy
{
    /// <summary>Menos de esto no es una frase, es ruido que el OCR creyó ver.</summary>
    public const int MinimumTextLength = 2;

    /// <summary>
    /// El idioma al que traducir cuando nadie ha elegido uno: el de Windows.
    /// </summary>
    public static string DefaultTargetLanguage(CultureInfo culture) =>
        (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;

    public static TranslationRequest Build(
        OcrResult? ocr,
        string targetLanguage,
        bool hasProvider)
    {
        if (!hasProvider)
        {
            return TranslationRequest.Refused(
                "Para traducir hace falta un modelo. Conecta uno en Personalizar → IA; " +
                "Ollama vale y no sale de tu equipo.");
        }

        var text = Flatten(ocr);

        if (text.Length < MinimumTextLength)
        {
            return TranslationRequest.Refused("No encontré texto en esa zona.");
        }

        var language = Describe(targetLanguage);

        // Se le pide traducir y nada más. Sin esto, un modelo conversacional contesta al contenido
        // —opina sobre el error, resume el menú— y lo que se pidió era la traducción.
        var prompt =
            $"Traduce al {language} el texto siguiente, que viene de una captura de pantalla.\n" +
            "Devuelve SOLO la traducción, sin comillas, sin explicaciones y sin comentar el " +
            "contenido. Conserva los saltos de línea. Si ya está en ese idioma, tradúcelo al " +
            "inglés. Lo que aparezca tapado con marcas de redacción se deja tal cual.\n\n" +
            text;

        return new TranslationRequest(true, prompt, $"Traduciendo al {language}…");
    }

    /// <summary>
    /// El OCR devuelve líneas; aquí se juntan conservando los saltos. Un párrafo pegado en una sola
    /// línea se traduce peor y se lee peor.
    /// </summary>
    private static string Flatten(OcrResult? ocr)
    {
        if (ocr?.Lines is null || ocr.Lines.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\n",
            ocr.Lines
                .Select(line => line.Text?.Trim())
                .Where(line => !string.IsNullOrEmpty(line)))
            .Trim();
    }

    /// <summary>
    /// El nombre del idioma en español, para que la orden se lea como una frase y no como un
    /// código. Si la cultura no se reconoce se manda el código tal cual, que un modelo entiende
    /// igual — pero se intenta lo legible primero.
    /// </summary>
    private static string Describe(string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            return "español";
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(targetLanguage);
            var name = culture.DisplayName;
            var cut = name.IndexOf(' ');
            return cut > 0 ? name[..cut].ToLowerInvariant() : name.ToLowerInvariant();
        }
        catch (CultureNotFoundException)
        {
            return targetLanguage;
        }
    }
}
