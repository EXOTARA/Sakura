using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Nexo.Core.Updates;

using Nexo.Core.Branding;

namespace Nexo.Windows.Updates;

/// <summary>Lo que se encontró al mirar si hay versión nueva.</summary>
public readonly record struct UpdateLookup(
    bool Found,
    UpdateManifest? Manifest,
    string Message)
{
    public static UpdateLookup Nothing(string message) => new(false, null, message);

    public static UpdateLookup Offer(UpdateManifest manifest) =>
        new(true, manifest, string.Empty);
}

/// <summary>
/// Diseño D64 — junta las piezas: mira, decide, descarga, prepara y llama al ayudante.
///
/// Aquí no se decide nada nuevo. Cada regla vive en su sitio —
/// <see cref="UpdateCheckPolicy"/> dice si toca mirar y si merece ofrecerse,
/// <see cref="UpdateManifestReader"/> dice si el anuncio vale,
/// <see cref="UpdateSwapPathPolicy"/> dice qué carpetas se pueden tocar — y esto solo las llama en
/// orden. Es a propósito: un orquestador que además opina es un orquestador que hay que probar con
/// la red delante.
///
/// **Nada de esto ocurre solo todavía.** El disparo es manual mientras no se haya visto funcionar
/// sobre una instalación de verdad. Un actualizador que se activa por su cuenta antes de estar
/// comprobado es la peor forma posible de descubrir que tenía un fallo.
/// </summary>
public sealed class WindowsUpdateService(HttpClient? client = null)
{
    private const string Owner = "EXOTARA";
    private const string Repository = "Nexo";

    private readonly HttpClient _client = client ?? CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();

        // GitHub rechaza las peticiones sin identificación de cliente. Sin esto, la consulta
        // devuelve un 403 que parece un problema de permisos y no lo es.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Sakura", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(20);

        return client;
    }

    /// <summary>
    /// Mira si hay algo más nuevo. No descarga nada: solo consulta y decide.
    /// </summary>
    public async Task<UpdateLookup> LookForUpdateAsync(
        SakuraVersion current,
        SakuraVersion? skipped,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _client.GetStringAsync(
                GitHubReleaseReader.ReleasesUrl(Owner, Repository), cancellationToken);

            var release = GitHubReleaseReader.Read(json);
            if (!release.IsUsable)
            {
                return UpdateLookup.Nothing(release.Problem);
            }

            // La huella viaja en su propio archivo. Es pequeño, así que se baja entero.
            var checksum = GitHubReleaseReader.ReadChecksum(
                await _client.GetStringAsync(release.ChecksumUrl, cancellationToken));

            var manifest = UpdateManifestReader.Read(
                release.Version,
                release.PackageUrl,
                checksum,
                release.PackageSize.ToString(),
                release.Notes);

            if (!manifest.IsUsable)
            {
                return UpdateLookup.Nothing(manifest.Problem);
            }

            var decision = UpdateCheckPolicy.Evaluate(
                current,
                manifest.Manifest!.Version,
                skipped,
                UpdateCheckPolicy.AcceptsPreReleases(current));

            return decision.ShouldOffer
                ? UpdateLookup.Offer(manifest.Manifest)
                : UpdateLookup.Nothing(Explain(decision.Verdict));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException)
        {
            // Estar sin conexión no es un fallo de Sakura y no debe contarse como tal.
            return UpdateLookup.Nothing("No se pudo consultar si hay una versión nueva.");
        }
    }

    private static string Explain(UpdateVerdict verdict) => verdict switch
    {
        UpdateVerdict.AlreadyCurrent => "Sakura ya está en la última versión.",
        UpdateVerdict.PreReleaseNotWanted => "Solo hay una versión preliminar, y no la usas.",
        UpdateVerdict.Skipped => "Esa versión ya la dejaste pasar.",
        _ => string.Empty
    };

    /// <summary>
    /// Descarga, desempaqueta al lado de la instalación y deja el ayudante listo. Devuelve la ruta
    /// del guion, o vacío con el motivo.
    ///
    /// Lo que **no** hace es cerrar Sakura: eso lo decide quien llama, porque cerrar la aplicación
    /// de alguien es una acción suya y no de una tarea de fondo.
    /// </summary>
    public async Task<(bool Ready, string HelperPath, string Problem)> PrepareAsync(
        UpdateManifest manifest,
        string installFolder,
        string workFolder,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var paths = UpdateSwapPathPolicy.Resolve(installFolder);
        if (!paths.IsSafe)
        {
            return (false, string.Empty, paths.Problem);
        }

        var download = await new WindowsUpdateDownloader(_client)
            .DownloadAsync(manifest, workFolder, progress, cancellationToken);

        if (!download.Ok)
        {
            return (false, string.Empty, download.Problem);
        }

        try
        {
            // Se desempaqueta al lado, nunca encima: mientras esto ocurre la instalación sigue
            // intacta y cancelar no cuesta nada.
            if (Directory.Exists(paths.Staged))
            {
                Directory.Delete(paths.Staged, recursive: true);
            }

            ZipFile.ExtractToDirectory(download.PackagePath, paths.Staged);

            // Un paquete que se desempaqueta pero no trae el ejecutable no sirve, y descubrirlo
            // ahora es gratis: descubrirlo después de mover la instalación, no.
            // Diseño D70 — el paquete puede venir con el nombre nuevo o con el anterior: una
            // instalación que todavía no ha saltado el cambio de nombre se actualiza igual, y una
            // que ya saltó no debe rechazar un paquete viejo por llamarse distinto.
            var staged = Path.Combine(paths.Staged, ProductIdentity.ExecutableName);
            if (!File.Exists(staged))
            {
                staged = Path.Combine(paths.Staged, ProductIdentity.LegacyExecutableName);
            }

            if (!File.Exists(staged))
            {
                Directory.Delete(paths.Staged, recursive: true);
                return (false, string.Empty, "El paquete descargado no contiene Sakura.");
            }

            Directory.CreateDirectory(workFolder);
            var helperPath = Path.Combine(workFolder, "aplicar-actualizacion.ps1");

            await File.WriteAllTextAsync(
                helperPath,
                UpdateHelperScript.Build(
                    paths,
                    Environment.ProcessId,
                    Path.Combine(paths.Install, Path.GetFileName(staged)),
                    // Es el ejecutable que corre ahora, dentro de la carpeta que la vuelta atrás
                    // devuelve a su sitio: tras un fallo, el ayudante abre este. Cadena vacía =
                    // «no hay candidato» (no ocurre en un proceso normal).
                    Environment.ProcessPath ?? string.Empty,
                    download.PackagePath,
                    manifest.Version.ToString()),
                // Con BOM a propósito: powershell.exe 5.1 lee un guion UTF-8 sin BOM como ANSI, y con
                // «José» en la ruta del perfil las rutas llegaban mal escritas, el ayudante no
                // encontraba la carpeta preparada y la actualización no funcionaba nunca (medido
                // ejecutando el guion de verdad).
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                cancellationToken);

            return (true, helperPath, string.Empty);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return (false, string.Empty, $"No se pudo preparar la actualización: {exception.Message}");
        }
    }

    /// <summary>
    /// Lanza el ayudante y devuelve si arrancó. Quien llama debe cerrar Sakura justo después: el
    /// guion espera a que este proceso termine antes de tocar nada.
    /// </summary>
    public static bool LaunchHelper(string helperPath)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,

                // Diseño D67 — el ayudante NO puede trabajar desde la carpeta que va a mover.
                //
                // Sin esto hereda el directorio de trabajo de Sakura, que es su propia carpeta de
                // instalación. En Windows, un proceso cuyo directorio actual está dentro de una
                // carpeta **impide moverla**: el ayudante retenía justo lo que venía a mover, el
                // primer movimiento fallaba, la recuperación dejaba todo como estaba y Sakura no
                // volvía a abrirse. Dos intentos en vivo con el mismo síntoma y dos causas
                // distintas; esta es la segunda.
                //
                // Se usa el directorio del propio guión, que vive en los datos de Sakura y nunca es
                // la instalación.
                WorkingDirectory = Path.GetDirectoryName(helperPath) ?? Path.GetTempPath()
            };

            // Los argumentos van de uno en uno para que una ruta con espacios no se parta en dos.
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(helperPath);

            return Process.Start(start) is not null;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
