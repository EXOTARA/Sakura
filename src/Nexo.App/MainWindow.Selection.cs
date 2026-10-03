using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using Nexo.App.Permissions;
using Nexo.App.Shell;
using Nexo.Core.Ai;
using Nexo.Core.Assistant;
using Nexo.Core.Audit;

namespace Nexo.App;

/// <summary>
/// 2026-09-16 — Alt+Shift+E (o la primera combinación libre): reescribir, corregir, resumir o traducir el texto seleccionado en
/// cualquier aplicación y, si se quiere, ponerlo en su lugar.
///
/// Leer la selección:
/// <list type="number">
/// <item>Primero por UI Automation, que no toca el portapapeles.</item>
/// <item>Si la aplicación no la expone, con Ctrl+C. Solo se hace si el portapapeles tiene texto o
/// está vacío, porque solo se puede devolver lo que se puede guardar; una imagen copiada no se pisa.</item>
/// </list>
/// Escribir el resultado usa el mismo camino que el dictado, con sus mismas negativas: no escribe si
/// la ventana cambió o si es sensible.
/// </summary>
public partial class MainWindow
{
    private const int SelectionHotkeyId = 0x4E71;

    private SelectionActionsWindow? _selectionWindow;
    private long _selectionTargetWindow;
    private string _selectionText = string.Empty;
    private bool _selectionBusy;

    // 2026-09-16 — Alt+Shift+F, buscar archivos. Vive aquí porque comparte el registro de atajos
    // globales nuevos del rediseño.
    private const int FileSearchHotkeyId = 0x4E72;
    private const uint VirtualKeyF = 0x46;
    private FileSearchWindow? _fileSearchWindow;

    private void ShowFileSearch()
    {
        _fileSearchWindow ??= new FileSearchWindow(new Nexo.Windows.Files.WindowsFileSearchService());
        _fileSearchWindow.ShowAtTop();
    }

    private bool RegisterSelectionHotkey(IntPtr windowHandle)
    {
        // 2026-09-16 — Alt+Shift+R lo tenía otra aplicación en el equipo de Adler. Se prueban varias
        // combinaciones y se usa la primera libre; la hoja de atajos enseña la que quedó.
        foreach (var (key, letter) in SelectionHotkeyCandidates)
        {
            if (RegisterHotKey(windowHandle, SelectionHotkeyId, ModAlt | ModShift, key))
            {
                SelectionHotkeyLabel = $"Alt + Shift + {letter}";
                _dashboardWindow.View.SetSelectionShortcut(letter);
                return true;
            }
        }

        // Este método también se reintenta en silencio en cada reconciliación: solo se toca la
        // interfaz cuando cambia (la primera vez o al perder una que tenía).
        if (SelectionHotkeyLabel is not null || !_hotkeysReportedBusy.Contains(SelectionHotkeyId))
        {
            SelectionHotkeyLabel = null;
            _dashboardWindow.View.SetSelectionShortcut(null);
        }

        ReportHotkeyBusy(
            SelectionHotkeyId,
            "Las combinaciones para el texto seleccionado (Alt + Shift + E, W o Q) ya las usa otra aplicación. " +
            "Esa función sigue disponible desde la paleta: «Texto seleccionado».");
        return false;
    }

    private static readonly (uint Key, string Letter)[] SelectionHotkeyCandidates =
    [
        (0x45, "E"),
        (0x57, "W"),
        (0x51, "Q")
    ];

    /// <summary>La combinación que quedó registrada, o null si ninguna estaba libre.</summary>
    private string? SelectionHotkeyLabel { get; set; }

    private async Task OnSelectionHotkeyAsync()
    {
        if (_selectionBusy)
        {
            return;
        }

        _selectionBusy = true;
        try
        {
            var target = GetForegroundWindow();
            if (target == IntPtr.Zero || target == new System.Windows.Interop.WindowInteropHelper(this).Handle)
            {
                ShowSelectionNotice($"Selecciona texto en otra aplicación y pulsa {SelectionHotkeyLabel ?? "el atajo"}.");
                return;
            }

            if (_ambientContextProvider.Capture(target.ToInt64()) is { IsSensitive: true })
            {
                ShowSelectionNotice("Esa ventana está marcada como sensible; Sakura no lee su texto.");
                return;
            }

            var text = ReadSelectionWithAutomation() ?? await CopySelectionAsync();
            var (canRun, detail) = SelectionRewrite.Check(text, _preferences.AiProvider != AiProviderKind.Disabled);
            if (!canRun)
            {
                ShowSelectionNotice(detail);
                return;
            }

            _selectionTargetWindow = target.ToInt64();
            _selectionText = text!;

            if (_selectionWindow is null)
            {
                _selectionWindow = new SelectionActionsWindow();
                _selectionWindow.ActionChosen += async (_, action) => await RunSelectionActionAsync(action);
                _selectionWindow.ReplaceRequested += async (_, result) => await ReplaceSelectionAsync(result);
            }

            _selectionWindow.ShowFor(_selectionText);
        }
        finally
        {
            _selectionBusy = false;
        }
    }

    private async Task RunSelectionActionAsync(SelectionAction action)
    {
        var window = _selectionWindow!;
        var request = new AiChatRequest(
            [new ConversationMessage(ConversationRole.User, SelectionRewrite.BuildPrompt(action, _selectionText), DateTimeOffset.Now)],
            NexoAiInstructions.Default,
            SystemContext: string.Empty,
            Images: [],
            AiRequestMode.Standard);

        var received = new StringBuilder();
        try
        {
            await foreach (var chunk in _aiChatService.StreamAsync(BuildAiConfiguration(), request, _lifetimeCancellation.Token))
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    received.Append(chunk);
                    window.Append(chunk);
                }
            }
        }
        catch (OperationCanceledException) when (!_lifetimeCancellation.IsCancellationRequested)
        {
            // Lo recibido se queda; se cierra con lo que haya.
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            window.Complete("No pude terminar: " + exception.Message);
            return;
        }

        window.Complete(received.Length == 0
            ? "No llegó respuesta. Comprueba el modelo en Personalizar → IA."
            : null);
    }

    private async Task ReplaceSelectionAsync(string result)
    {
        // Auditoría 2026-09-28: antes escribía sin consultar los permisos de Flow. Si se deniega, el
        // resultado tampoco va al portapapeles (igual que el dictado: la ventana pudo ser una que se
        // quiso proteger) y el aviso solo lleva el motivo.
        var denial = FlowExclusionCheck.DenyAiReplacement(
            _preferences.Permissions,
            _ambientContextProvider.Capture(_selectionTargetWindow),
            result,
            ReadWindowClass(new IntPtr(_selectionTargetWindow)));
        if (denial is not null)
        {
            RecordAudit(
                AuditCapability.Permisos,
                "Acción denegada (Flow)",
                $"Reemplazar el texto seleccionado — {denial}",
                "Permiso denegado");
            ShowSelectionNotice(denial);
            return;
        }

        var target = new IntPtr(_selectionTargetWindow);
        SetForegroundWindow(target);
        await Task.Delay(180);

        // La selección sigue marcada en la otra aplicación: escribir la sustituye.
        var insertion = _flowTextInserter.Insert(result, _selectionTargetWindow);
        if (!insertion.IsInserted)
        {
            try
            {
                Clipboard.SetText(result);
            }
            catch (ExternalException)
            {
            }

            ShowSelectionNotice(insertion.Detail + " El resultado quedó copiado para pegarlo tú.");
        }
    }

    private static string? ReadSelectionWithAutomation()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused?.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) == true &&
                pattern is TextPattern textPattern)
            {
                var text = string.Concat(textPattern.GetSelection().Select(range => range.GetText(SelectionRewrite.MaximumLength + 1)));
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
        }

        return null;
    }

    private async Task<string?> CopySelectionAsync()
    {
        string? previous;
        try
        {
            if (!Clipboard.ContainsText() && Clipboard.GetDataObject()?.GetFormats().Length > 0)
            {
                // Hay algo que no es texto (una imagen, archivos): no se puede devolver, así que no se toca.
                return null;
            }

            previous = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (ExternalException)
        {
            return null;
        }

        // Alt y Shift siguen pulsadas del atajo: Ctrl+C con ellas sería otra combinación.
        for (var waited = 0; waited < 1000 && (IsKeyDown(VkMenu) || IsKeyDown(VkShift)); waited += 25)
        {
            await Task.Delay(25);
        }

        const string marker = "​ sakura-selection ​";
        try
        {
            Clipboard.SetText(marker);
        }
        catch (ExternalException)
        {
            return null;
        }

        keybd_event(VkControl, 0, 0, 0);
        keybd_event(VkC, 0, 0, 0);
        keybd_event(VkC, 0, KeyUp, 0);
        keybd_event(VkControl, 0, KeyUp, 0);

        string? copied = null;
        for (var waited = 0; waited < 600; waited += 40)
        {
            await Task.Delay(40);
            try
            {
                var current = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                if (current is not null && current != marker)
                {
                    copied = current;
                    break;
                }
            }
            catch (ExternalException)
            {
            }
        }

        try
        {
            if (previous is null)
            {
                Clipboard.Clear();
            }
            else
            {
                Clipboard.SetText(previous);
            }
        }
        catch (ExternalException)
        {
        }

        return copied;
    }

    private void ShowSelectionNotice(string detail) =>
        _capsuleWindow.ShowMessage(CapsuleKind.Warning, "Texto seleccionado", detail, _preferences.Position);

    private const byte VkControl = 0x11;
    private const byte VkShift = 0x10;
    private const byte VkMenu = 0x12;
    private const byte VkC = 0x43;
    private const uint KeyUp = 0x0002;

    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scan, uint flags, nint extraInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    // Revisión adversarial 2026-10-03: la clase de ventana distingue una consola aunque el proceso que
    // Windows le atribuye sea python, ssh o cualquier otro programa de consola (TerminalWindowPolicy).
    // Si no se puede leer queda vacía y manda el nombre del proceso, como antes.
    private static string? ReadWindowClass(IntPtr window)
    {
        var builder = new StringBuilder(256);
        return window != IntPtr.Zero && GetClassName(window, builder, builder.Capacity) > 0
            ? builder.ToString()
            : null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);
}
