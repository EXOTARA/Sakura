using System.Text;
using Nexo.Core.Ai;
using Nexo.Core.Assistant;
using Nexo.Core.Resources;
using Nexo.Core.Vision;
using Nexo.Windows.Vision;

namespace Nexo.App;

/// <summary>
/// 2026-09-15 — Ctrl + Shift + Espacio explica la ventana que tienes delante (Adler: «en vez de tomar
/// la captura simplemente debería darte la información que necesitas: qué es, qué sucede, cómo
/// resolver, qué pasos hacer»). Antes solo la capturaba y esperaba una pregunta.
///
/// Lee la ventana igual que Lens —imagen, texto por OCR y elementos de la interfaz, todo pasado por el
/// redactor de datos sensibles— y contesta en la píldora con cuatro apartados. Si el modelo no admite
/// imágenes (el de Groq que usa Adler no las admite), se repite la petición solo con el texto leído, así
/// que funciona con cualquier modelo. Lo leído se queda como contexto un rato: las preguntas que se
/// hagan desde la píldora saben de qué ventana se habla.
/// </summary>
public partial class MainWindow
{
    private bool _explainingWindow;

    private async Task ExplainForegroundWindowAsync()
    {
        if (_isClosed || _explainingWindow)
        {
            return;
        }

        if (!_preferences.VisionEnabled)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning, "Lens está desactivado", "Actívalo desde Personalizar.", _preferences.Position, force: true);
            return;
        }

        var configuration = BuildAiConfiguration();
        if (!configuration.IsEnabled)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information, "IA desactivada", "Elige un proveedor desde Personalizar.", _preferences.Position, force: true);
            return;
        }

        var resourceDecision = await EnsureFreshResourceDecisionAsync();
        var aiAllowed = AiExecutionLocationPolicy.UsesLocalRuntime(configuration)
            ? resourceDecision.AllowLocalAi
            : resourceDecision.AllowRemoteAi;
        if (!aiAllowed || (_preferences.ProtectVisionWhenBusy && !resourceDecision.AllowVision))
        {
            PresentResourceRestriction(
                resourceDecision, "Explicar ventanas está pausado para no quitarte rendimiento.", fromVoice: false);
            return;
        }

        var windowHandle = _lastExternalWindowHandle;
        var ambient = _ambientContextProvider.Capture(windowHandle);
        if (windowHandle == 0 || ambient is null)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information, "No encontré qué explicar", "Activa una ventana y vuelve a intentarlo.", _preferences.Position, force: true);
            return;
        }

        if (ambient.IsSensitive)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information, "Ventana protegida", "Sakura no mira ventanas marcadas como sensibles.", _preferences.Position, force: true);
            return;
        }

        _explainingWindow = true;
        var title = string.IsNullOrWhiteSpace(ambient.WindowTitle) ? "la ventana activa" : ambient.WindowTitle!;
        _answerPillWindow.BeginAnswer($"Explicando «{title}»", _preferences.Position);
        LensIndicator.Visibility = System.Windows.Visibility.Visible;

        string? failure = null;
        var streamingStarted = false;
        try
        {
            var target = new VisionCaptureTarget(
                $"window:{windowHandle}", windowHandle, title, ambient.ProcessName ?? string.Empty,
                VisionCaptureKind.Window, 0, 0, 0, 0);
            var capture = await _screenCaptureService.CaptureAsync(target, _lifetimeCancellation.Token);
            if (!capture.IsSuccess || capture.PngBytes is null)
            {
                failure = $"No pude leer la ventana: {capture.Detail}";
                return;
            }

            var ocr = await _lensOcrService.RecognizeAsync(capture.PngBytes);
            var uia = _lensUiAutomationReader.Read(windowHandle);
            var redactedOcr = SensitiveContentRedactor.Redact(ocr);
            var redactedElements = SensitiveContentRedactor.Redact(uia.Elements);
            var imageBytes = ImageRedactor.RedactRegions(capture.PngBytes, SensitiveContentRedactor.FindSensitiveLines(ocr));
            var lens = LensContextBuilder.Build(LensMode.Explicar, title, redactedOcr, redactedElements);
            var image = AiImageAttachment.FromBytes(imageBytes, "image/png", title);

            // En el chat queda como una pregunta más, para que la conversación siga desde aquí.
            _assistantView.AddUserMessage($"Explícame la ventana «{title}»");

            await _aiGate.WaitAsync(_lifetimeCancellation.Token);
            string answer;
            bool usedImage;
            try
            {
                _assistantView.BeginSakuraStreamingMessage("Leyendo la ventana…");
                streamingStarted = true;
                (answer, usedImage) = await StreamExplanationAsync(configuration, lens, image);
            }
            finally
            {
                _aiGate.Release();
            }

            _assistantView.CompleteSakuraStreamingMessage();
            streamingStarted = false;

            if (string.IsNullOrWhiteSpace(answer))
            {
                failure = "No llegó ninguna explicación.";
                return;
            }

            // Lo leído se queda para las preguntas siguientes: la imagen solo si el modelo la aceptó.
            _visualContextMetadata = lens.SystemContext;
            _pendingVisionAttachment = usedImage ? image : null;
            _visualContextPersistent = true;
            _silentVisualContext = true;
            RestartVisualContextExpiry();

            ShowLensHighlights(answer, redactedOcr, redactedElements, uia);
        }
        catch (OperationCanceledException) when (!_lifetimeCancellation.IsCancellationRequested)
        {
            failure = "La explicación tardó demasiado y Sakura dejó de esperarla.";
        }
        catch (OperationCanceledException)
        {
            // Sakura se está cerrando.
        }
        catch (Exception exception)
        {
            failure = $"No pude explicar la ventana: {exception.Message}";
        }
        finally
        {
            if (streamingStarted)
            {
                _assistantView.CancelSakuraStreamingMessage();
            }

            LensIndicator.Visibility = System.Windows.Visibility.Collapsed;
            _answerPillWindow.CompleteAnswer(failure);
            _explainingWindow = false;
        }
    }

    /// <summary>
    /// Pide la explicación con la imagen y, si el proveedor la rechaza antes de escribir nada —lo que
    /// pasa con los modelos que no admiten imágenes—, la vuelve a pedir solo con el texto leído.
    /// </summary>
    private async Task<(string Answer, bool UsedImage)> StreamExplanationAsync(
        AiProviderConfiguration configuration, LensContext lens, AiImageAttachment image)
    {
        var messages = new[] { new ConversationMessage(ConversationRole.User, lens.Prompt, DateTimeOffset.Now) };
        var text = new StringBuilder();

        async Task StreamAsync(IReadOnlyList<AiImageAttachment>? images, AiRequestMode mode)
        {
            var request = new AiChatRequest(messages, NexoAiInstructions.Default, lens.SystemContext, images, mode);
            await foreach (var chunk in _aiChatService.StreamAsync(configuration, request, _lifetimeCancellation.Token))
            {
                if (string.IsNullOrEmpty(chunk))
                {
                    continue;
                }

                text.Append(chunk);
                _assistantView.AppendSakuraStreamingText(chunk);
                _answerPillWindow.AppendAnswer(chunk);
            }
        }

        try
        {
            await StreamAsync([image], lens.RequestMode);
            return (text.ToString(), true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException && text.Length == 0)
        {
            // Sin imagen, el modo de visión técnica sigue valiendo: el texto de OCR lleva el error.
            await StreamAsync(null, lens.RequestMode);
            return (text.ToString(), false);
        }
    }
}
