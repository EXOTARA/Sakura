namespace Nexo.Core.Branding;

/// <summary>
/// Identidad pública de la aplicación.
///
/// Diseño D69 — el producto se llama Sakura. El nombre anterior era Kohana y antes de ese, Nexo;
/// los dos siguen aquí porque hay preferencias, carpetas e instalaciones que los llevan escritos.
/// </summary>
public static class ProductIdentity
{
    public const string ProductName = "Sakura";
    public const string PreviousProductName = "Kohana";
    public const string Tagline = "Tu Windows, en flor.";
    public const string AssistantDescription = "agente personal para Windows";
    /// <summary>
    /// Diseño D70 — la carpeta de datos se llama Sakura, y los dos nombres anteriores siguen
    /// existiendo como una cadena, no como un solo escalón.
    ///
    /// Hace falta que sea una cadena porque los modelos de voz de quien viene de lejos —tres giga
    /// y medio— siguen en la carpeta de la etapa Nexo, y la de Kohana nunca los copió: se leían en
    /// vivo desde la vieja. Con un solo nombre anterior, renombrar dejaba a Sakura sin encontrarlos.
    /// </summary>
    public const string DataDirectoryName = "Sakura";

    public static readonly IReadOnlyList<string> PreviousDataDirectoryNames = ["Kohana", "Nexo"];

    /// <summary>El nombre anterior inmediato. Se conserva para quien solo necesita uno.</summary>
    public const string LegacyDataDirectoryName = "Kohana";

    /// <summary>
    /// Diseño D70 — el ejecutable pasa a llamarse Sakura.exe. El instalador borra el anterior y
    /// rehace acceso directo y arranque con Windows; una copia publicada a mano encima se queda con
    /// los dos hasta que se limpie.
    /// </summary>
    public const string ExecutableName = "Sakura.exe";
    public const string LegacyExecutableName = "Kohana.exe";
    public const string DefaultWakePhrase = "Oye Sakura";
    public const string ShortWakePhrase = "Sakura";
    public const string RepositoryOwner = "EXOTARA";
    public const string RepositoryName = "Sakura";
    public const string RepositoryUrl = "https://github.com/EXOTARA/Sakura";
    public const string SupportName = "Sakura Support";

    /// <summary>
    /// Diseño D89 — dónde se reporta una respuesta de IA inapropiada. La política 11.16 de Microsoft
    /// Store exige que la persona tenga cómo hacerlo, y tiene sentido igual fuera de la Store. Abre la
    /// plantilla de incidencia; el texto de la respuesta no viaja en el enlace.
    /// </summary>
    public const string AiContentReportUrl = RepositoryUrl + "/issues/new?template=contenido-ia.yml";

    public static string DisplayNameWithTagline => $"{ProductName} — {Tagline}";
}
