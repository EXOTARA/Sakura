using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Nexo.Core.Ai;
using Nexo.Core.Diagnostics;

namespace Nexo.Windows.Ai;

public sealed class OllamaRuntimeService :
    IOllamaRuntimeService,
    IDisposable
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/ollama/ollama/releases/latest";
    private const string WindowsAssetName = "ollama-windows-amd64.zip";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _managedExecutablePath;
    private readonly SemaphoreSlim _runtimeGate = new(1, 1);

    public OllamaRuntimeService(
        HttpClient? httpClient = null,
        string? managedExecutablePath = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        _ownsClient = httpClient is null;
        _managedExecutablePath = string.IsNullOrWhiteSpace(managedExecutablePath)
            ? NexoDataPaths.OllamaExecutable
            : managedExecutablePath;
    }

    public async Task<OllamaRuntimeSnapshot> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        var managedInstalled = File.Exists(_managedExecutablePath);

        if (await IsEndpointRunningAsync(
                OllamaRuntimeEndpoints.ManagedTagsEndpoint,
                cancellationToken))
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedRunning,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "La IA local administrada por Sakura está funcionando.");
        }

        if (await IsEndpointRunningAsync(
                OllamaRuntimeEndpoints.ExternalTagsEndpoint,
                cancellationToken))
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ExternalRunning,
                OllamaRuntimeEndpoints.ExternalBaseUrl,
                null,
                "Se detectó una instalación externa de Ollama en funcionamiento.");
        }

        if (managedInstalled)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "La IA local de Sakura está instalada, pero no está iniciada.");
        }

        return new OllamaRuntimeSnapshot(
            OllamaRuntimeState.Unavailable,
            OllamaRuntimeEndpoints.ManagedBaseUrl,
            null,
            "Ollama no está disponible. Sakura puede instalarlo por ti.");
    }

    public async Task<OllamaRuntimeSnapshot> InstallManagedAsync(
        IProgress<OllamaRuntimeInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(_managedExecutablePath))
        {
            progress?.Report(new OllamaRuntimeInstallProgress(
                "installed",
                "La IA local ya está instalada. Iniciándola…"));
            return await StartManagedAsync(cancellationToken);
        }

        var installerDirectory = NexoDataPaths.OllamaInstallerTempDirectory;
        var archivePath = Path.Combine(installerDirectory, WindowsAssetName);
        var stagingDirectory = Path.Combine(installerDirectory, "staging");

        try
        {
            ResetDirectory(installerDirectory);
            Directory.CreateDirectory(stagingDirectory);

            progress?.Report(new OllamaRuntimeInstallProgress(
                "release",
                "Buscando la versión oficial más reciente de Ollama…"));

            var asset = await GetLatestWindowsAssetAsync(cancellationToken);
            if (asset is null)
            {
                return InstallationFailure(
                    "No encontré el paquete oficial de Ollama para Windows.");
            }

            if (string.IsNullOrWhiteSpace(asset.Sha256))
            {
                return InstallationFailure(
                    "La publicación oficial no incluyó una firma SHA-256 verificable.");
            }

            progress?.Report(new OllamaRuntimeInstallProgress(
                "download",
                "Descargando el motor de IA local…",
                0,
                asset.Size));

            await DownloadAssetAsync(
                asset,
                archivePath,
                progress,
                cancellationToken);

            progress?.Report(new OllamaRuntimeInstallProgress(
                "verify",
                "Verificando la integridad de la descarga…"));

            var actualHash = await ComputeSha256Async(
                archivePath,
                cancellationToken);

            if (!string.Equals(
                    actualHash,
                    asset.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return InstallationFailure(
                    "La descarga no superó la verificación de integridad. Se eliminó el archivo.");
            }

            progress?.Report(new OllamaRuntimeInstallProgress(
                "extract",
                "Preparando la IA local…"));

            ZipFile.ExtractToDirectory(
                archivePath,
                stagingDirectory,
                overwriteFiles: true);

            var stagedExecutable = Directory
                .EnumerateFiles(
                    stagingDirectory,
                    "ollama.exe",
                    SearchOption.AllDirectories)
                .FirstOrDefault();

            if (stagedExecutable is null)
            {
                return InstallationFailure(
                    "El paquete descargado no contiene ollama.exe.");
            }

            var payloadDirectory = Path.GetDirectoryName(stagedExecutable);
            if (string.IsNullOrWhiteSpace(payloadDirectory))
            {
                return InstallationFailure(
                    "El paquete de Ollama tiene una estructura no válida.");
            }

            ReplaceRuntimeDirectory(payloadDirectory);

            if (!File.Exists(_managedExecutablePath))
            {
                return InstallationFailure(
                    "La instalación terminó, pero Sakura no encontró ollama.exe.");
            }

            progress?.Report(new OllamaRuntimeInstallProgress(
                "start",
                "Iniciando la IA local…"));

            return await StartManagedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return InstallationFailure("La instalación fue cancelada.");
        }
        catch (HttpRequestException exception)
        {
            return InstallationFailure(
                $"No pude descargar Ollama: {exception.Message}");
        }
        catch (InvalidDataException)
        {
            return InstallationFailure(
                "El archivo descargado no es un ZIP válido.");
        }
        catch (UnauthorizedAccessException)
        {
            return InstallationFailure(
                "Windows impidió escribir los archivos de la IA local.");
        }
        catch (IOException exception)
        {
            return InstallationFailure(
                $"No pude preparar los archivos de Ollama: {exception.Message}");
        }
        catch (JsonException)
        {
            return InstallationFailure(
                "GitHub respondió con información de publicación no válida.");
        }
        catch (Exception exception)
        {
            return InstallationFailure(
                $"No pude instalar la IA local: {exception.Message}");
        }
        finally
        {
            TryDeleteDirectory(installerDirectory);
        }
    }

    public async Task<OllamaRuntimeSnapshot> StartManagedAsync(
        CancellationToken cancellationToken = default)
    {
        await _runtimeGate.WaitAsync(cancellationToken);
        try
        {
            return await StartManagedCoreAsync(cancellationToken);
        }
        finally
        {
            _runtimeGate.Release();
        }
    }

    private async Task<OllamaRuntimeSnapshot> StartManagedCoreAsync(
        CancellationToken cancellationToken)
    {
        if (await IsEndpointRunningAsync(
                OllamaRuntimeEndpoints.ManagedTagsEndpoint,
                cancellationToken))
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedRunning,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "La IA local administrada por Sakura está funcionando.");
        }

        if (!File.Exists(_managedExecutablePath))
        {
            return InstallationFailure(
                "La IA local todavía no está instalada.");
        }

        // 2026-09-16 — a un amigo de Adler le salió «Ollama se cerró antes de completar el inicio» a
        // mitad de descargar el modelo. Durante una descarga Ollama puede tardar más de dos segundos
        // en contestar; el vigilante lo daba por caído y lanzaba un segundo «ollama serve» en el
        // mismo puerto, que se cerraba al instante, y ese mensaje tapaba la descarga. Si el nuestro
        // sigue vivo, no se arranca otro: se le da más tiempo y, si sigue sin contestar, se dice que
        // está ocupado.
        if (IsManagedProcessAlive())
        {
            var answered = await IsEndpointRunningAsync(
                OllamaRuntimeEndpoints.ManagedTagsEndpoint,
                cancellationToken,
                TimeSpan.FromSeconds(15));

            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedRunning,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                answered
                    ? "La IA local administrada por Sakura está funcionando."
                    : "La IA local está ocupada (por ejemplo, descargando un modelo).");
        }

        var runtimeDirectory = Path.GetDirectoryName(_managedExecutablePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "La ruta de la IA local no es válida.");
        }

        Directory.CreateDirectory(runtimeDirectory);
        Directory.CreateDirectory(NexoDataPaths.OllamaModelsDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = _managedExecutablePath,
            Arguments = "serve",
            WorkingDirectory = runtimeDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.Environment["OLLAMA_HOST"] = "127.0.0.1:11435";
        startInfo.Environment["OLLAMA_MODELS"] =
            NexoDataPaths.OllamaModelsDirectory;

        Process? process = null;

        try
        {
            process = Process.Start(startInfo);

            if (process is null)
            {
                return new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    OllamaRuntimeEndpoints.ManagedBaseUrl,
                    _managedExecutablePath,
                    "Windows no pudo iniciar la IA local.");
            }

            var started = await WaitForManagedEndpointAsync(
                process,
                cancellationToken);

            if (!started)
            {
                return new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    OllamaRuntimeEndpoints.ManagedBaseUrl,
                    _managedExecutablePath,
                    process.HasExited
                        ? "Ollama se cerró antes de completar el inicio."
                        : "Ollama tardó demasiado en responder.");
            }

            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedRunning,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "La IA local administrada por Sakura está funcionando.");
        }
        catch (OperationCanceledException)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "El inicio de la IA local fue cancelado.");
        }
        catch (Win32Exception exception)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                $"Windows no pudo iniciar Ollama: {exception.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "Windows impidió iniciar la IA local.");
        }
        catch (Exception exception)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                $"No pude iniciar la IA local: {exception.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    public async Task<OllamaRuntimeSnapshot> StopManagedAsync(
        CancellationToken cancellationToken = default)
    {
        await _runtimeGate.WaitAsync(cancellationToken);
        try
        {
            foreach (var process in Process.GetProcessesByName("ollama"))
            {
                using (process)
                {
                    if (!IsManagedProcess(process, _managedExecutablePath))
                    {
                        continue;
                    }

                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);

                            using var timeout =
                                CancellationTokenSource.CreateLinkedTokenSource(
                                    cancellationToken);
                            timeout.CancelAfter(TimeSpan.FromSeconds(5));
                            await process.WaitForExitAsync(timeout.Token);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // El proceso terminó mientras Nexo lo cerraba.
                    }
                    catch (Win32Exception)
                    {
                        // Se verificará el endpoint antes de informar el resultado.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Se verificará el endpoint antes de informar el resultado.
                    }
                    catch (OperationCanceledException)
                        when (!cancellationToken.IsCancellationRequested)
                    {
                        // El proceso tardó en cerrar; se verificará el endpoint.
                    }
                }
            }

            if (await IsEndpointRunningAsync(
                    OllamaRuntimeEndpoints.ManagedTagsEndpoint,
                    cancellationToken))
            {
                return new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedRunning,
                    OllamaRuntimeEndpoints.ManagedBaseUrl,
                    _managedExecutablePath,
                    "No pude detener la IA local administrada por Sakura.");
            }

            return File.Exists(_managedExecutablePath)
                ? new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    OllamaRuntimeEndpoints.ManagedBaseUrl,
                    _managedExecutablePath,
                    "La IA local administrada por Sakura se detuvo correctamente.")
                : InstallationFailure("La IA local no está instalada.");
        }
        catch (OperationCanceledException)
        {
            return new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedRunning,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                _managedExecutablePath,
                "Se canceló el cierre de la IA local.");
        }
        finally
        {
            _runtimeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }

        _runtimeGate.Dispose();
    }

    private async Task<ReleaseAsset?> GetLatestWindowsAssetAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            LatestReleaseApi);
        request.Headers.UserAgent.ParseAdd("Sakura/0.10");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(
            timeout.Token);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: timeout.Token);

        if (!document.RootElement.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in assets.EnumerateArray())
        {
            var name = ReadString(item, "name");
            if (!string.Equals(
                    name,
                    WindowsAssetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var downloadUrl = ReadString(item, "browser_download_url");
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return null;
            }

            long? size = null;
            if (item.TryGetProperty("size", out var sizeElement) &&
                sizeElement.TryGetInt64(out var parsedSize))
            {
                size = parsedSize;
            }

            var digest = ReadString(item, "digest");
            var sha256 = digest?.StartsWith(
                    "sha256:",
                    StringComparison.OrdinalIgnoreCase) == true
                ? digest[7..].Trim()
                : null;

            return new ReleaseAsset(
                name ?? WindowsAssetName,
                downloadUrl,
                sha256,
                size);
        }

        return null;
    }

    private async Task DownloadAssetAsync(
        ReleaseAsset asset,
        string destinationPath,
        IProgress<OllamaRuntimeInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            asset.DownloadUrl);
        request.Headers.UserAgent.ParseAdd("Sakura/0.10");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
        await using var source = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long completedBytes = 0;

        // 2026-09-16 — prueba desde cero: se avisaba en cada trozo de 80 KB, cientos de veces por
        // segundo, y la ventana gastaba más de un núcleo solo en repintar el porcentaje. Basta con
        // unas cuantas veces por segundo.
        var lastReport = DateTime.MinValue;

        while (true)
        {
            var read = await source.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);
            completedBytes += read;

            var now = DateTime.UtcNow;
            if (now - lastReport < TimeSpan.FromMilliseconds(250))
            {
                continue;
            }

            lastReport = now;
            progress?.Report(new OllamaRuntimeInstallProgress(
                "download",
                "Descargando el motor de IA local…",
                completedBytes,
                totalBytes));
        }

        progress?.Report(new OllamaRuntimeInstallProgress(
            "download",
            "Descargando el motor de IA local…",
            completedBytes,
            totalBytes));
    }

    private static async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Si ya hay un Ollama de Sakura vivo, aunque no conteste a tiempo.</summary>
    private bool IsManagedProcessAlive()
    {
        foreach (var process in Process.GetProcessesByName("ollama"))
        {
            using (process)
            {
                if (!process.HasExited && IsManagedProcess(process, _managedExecutablePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> IsEndpointRunningAsync(
        string endpoint,
        CancellationToken cancellationToken,
        TimeSpan? wait = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(wait ?? TimeSpan.FromSeconds(2));

        try
        {
            using var response = await _httpClient.GetAsync(
                endpoint,
                timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private async Task<bool> WaitForManagedEndpointAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                return false;
            }

            if (await IsEndpointRunningAsync(
                    OllamaRuntimeEndpoints.ManagedTagsEndpoint,
                    cancellationToken))
            {
                return true;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(350),
                cancellationToken);
        }

        return false;
    }

    private static bool IsManagedProcess(
        Process process,
        string expectedExecutablePath)
    {
        try
        {
            var processPath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(processPath),
                Path.GetFullPath(expectedExecutablePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private void ReplaceRuntimeDirectory(string payloadDirectory)
    {
        var runtimeDirectory = Path.GetDirectoryName(_managedExecutablePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new InvalidOperationException(
                "La ruta del runtime administrado no es válida.");
        }

        if (Directory.Exists(runtimeDirectory))
        {
            Directory.Delete(runtimeDirectory, recursive: true);
        }

        Directory.CreateDirectory(runtimeDirectory);
        CopyDirectory(payloadDirectory, runtimeDirectory);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Los temporales se limpiarán en el siguiente intento o diagnóstico.
        }
    }

    private static OllamaRuntimeSnapshot InstallationFailure(string message) =>
        new(
            OllamaRuntimeState.Unavailable,
            OllamaRuntimeEndpoints.ManagedBaseUrl,
            null,
            message);

    private static string? ReadString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private sealed record ReleaseAsset(
        string Name,
        string DownloadUrl,
        string? Sha256,
        long? Size);
}
