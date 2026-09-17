using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nexo.App.Motion;
using Nexo.App.Views.Controls;
using Nexo.Core.Assistant;
using Nexo.Core.Shell;

namespace Nexo.App.Views;

public partial class AssistantView : UserControl
{
    private readonly List<ConversationMessage> _messages = [];
    private readonly StringBuilder _streamingBuffer = new();
    private Border? _streamingBubble;
    private TextBlock? _streamingTextBlock;
    private Controls.SakuraLoader? _streamingLoader;
    private bool _streamingHasContent;
    private string _aiProviderStatus = "Sin IA · las órdenes locales funcionan";
    private bool _saveHistory;
    private int _recentMessageLimit = 8;

    public event EventHandler<PromptSubmittedEventArgs>? PromptSubmitted;
    public event EventHandler? ConversationChanged;
    public event EventHandler? ConversationCleared;
    public event EventHandler? VoiceInputStarted;
    public event EventHandler? VoiceInputStopped;
    public event EventHandler? VisionCaptureRequested;
    public event EventHandler? VisionAttachmentCleared;

    private bool _voiceInputActive;
    private bool _voiceAvailable;

    public AssistantView()
    {
        InitializeComponent();
        RenderConversation();
    }

    public void ConfigureHistory(bool saveHistory, int recentMessageLimit)
    {
        _saveHistory = saveHistory;
        _recentMessageLimit = Math.Clamp(recentMessageLimit, 4, 30);
        TrimTransientConversation();
        RenderConversation();
    }

    public void LoadConversation(IEnumerable<ConversationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        _messages.Clear();
        _messages.AddRange(messages.Where(message => !string.IsNullOrWhiteSpace(message.Text)));
        TrimTransientConversation();
        RenderConversation();
    }

    public IReadOnlyList<ConversationMessage> GetConversationSnapshot() => _messages.ToArray();

    /// <summary>
    /// La entrada escalonada de la primera apertura (ver <see cref="EntranceChoreography"/>): los
    /// bloques crecen vacíos, luego entra lo que llevan, el saludo palabra a palabra y las sugerencias
    /// una detrás de otra. Quien la llama decide si toca; aquí solo se reproduce.
    /// </summary>
    public void PlayEntrance()
    {
        if (!SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        var welcome = EmptyStatePanel.Visibility == Visibility.Visible;
        var words = welcome ? EmptyStateTitle.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length : 0;
        var chips = welcome ? QuickPromptsPanel.Children.OfType<FrameworkElement>().ToList() : [];

        // De una conversación larga solo entran escalonados los últimos mensajes, los que se ven; los
        // de más arriba ya aparecen puestos.
        var messages = welcome
            ? []
            : ConversationPanel.Children.OfType<FrameworkElement>().TakeLast(6).ToList();

        var plan = EntranceChoreography.Plan(words, chips.Count, messages.Count);

        EntranceMotion.Rise(HeaderRow, plan.Header, offset: 6);
        EntranceMotion.GrowBlock(ComposerBorder, plan.ComposerBlock);
        EntranceMotion.Rise(ComposerContent, plan.ComposerContent, offset: 6);

        if (welcome)
        {
            EntranceMotion.GrowBlock(EmptyStatePanel, plan.CardBlock);
            EntranceMotion.Pop(EmptyStateMark, plan.Mark, from: 0.6);
            EntranceMotion.RevealWords(EmptyStateTitle, plan.Words);
            EntranceMotion.Rise(EmptyStateDescription, plan.Description, offset: 8);

            for (var i = 0; i < chips.Count; i++)
            {
                EntranceMotion.Pop(chips[i], plan.Chips[i]);
            }

            return;
        }

        for (var i = 0; i < messages.Count; i++)
        {
            EntranceMotion.Rise(messages[i], plan.Messages[i], offset: 14);
        }
    }

    public void FocusPrompt()
    {
        Dispatcher.BeginInvoke(() =>
        {
            PromptBox.Focus();
            Keyboard.Focus(PromptBox);
        }, DispatcherPriority.Input);
    }

    public void SetAiProviderStatus(string detail)
    {
        _aiProviderStatus = detail;
        if (AiProviderStatusText is not null)
        {
            AiProviderStatusText.Text = detail;
        }
    }

    public bool HasVisionAttachment =>
        VisionAttachmentPanel is not null &&
        VisionAttachmentPanel.Visibility == Visibility.Visible;

    public void SetVisionAvailability(bool available)
    {
        if (VisionButton is not null)
        {
            VisionButton.IsEnabled = available;
            VisionButton.ToolTip = available
                ? "Mirar la ventana activa · Ctrl + Shift + Espacio"
                : "Lens está desactivado en Personalizar";
        }
    }

    public void SetVisionAttachment(
        string sourceTitle,
        byte[] pngBytes,
        bool isVisualContext = false)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        VisionPreviewImage.Source = LoadBitmap(pngBytes);
        VisionSourceTitleText.Text = sourceTitle;
        VisionAttachmentTitleText.Text = isVisualContext
            ? "Contexto visual activo"
            : "Captura lista";
        VisionAttachmentHintText.Text = isVisualContext
            ? "Puedes hacer varias preguntas durante los próximos 2 minutos."
            : "Se enviará solo con tu siguiente consulta.";
        VisionAttachmentPanel.Visibility = Visibility.Visible;
        FocusPrompt();
    }

    public void ClearVisionAttachment()
    {
        if (VisionPreviewImage is not null)
        {
            VisionPreviewImage.Source = null;
        }

        if (VisionSourceTitleText is not null)
        {
            VisionSourceTitleText.Text = string.Empty;
        }

        if (VisionAttachmentPanel is not null)
        {
            VisionAttachmentPanel.Visibility = Visibility.Collapsed;
        }
    }

    public void SetAiActivity(string? activity)
    {
        if (AiProviderStatusText is null)
        {
            return;
        }

        AiProviderStatusText.Text = string.IsNullOrWhiteSpace(activity)
            ? _aiProviderStatus
            : $"{_aiProviderStatus} · {activity}";
    }

    public void SetVoiceAvailability(bool available, string detail)
    {
        _voiceAvailable = available;

        if (MicButton is null || VoiceStatusText is null)
        {
            return;
        }

        if (!_voiceInputActive)
        {
            MicButton.IsEnabled = available;
            MicButton.ClearValue(BackgroundProperty);
        }

        MicButton.ToolTip = detail;
        VoiceStatusText.Text = detail;
        VoiceStatusText.Visibility = available
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public void SetVoiceState(AssistantVoiceState state, string? detail = null)
    {
        if (MicButton is null || VoiceStatusText is null)
        {
            return;
        }

        switch (state)
        {
            case AssistantVoiceState.Listening:
                MicButton.IsEnabled = true;
                MicButton.Background = (Brush)FindResource("BrushAccentSoft");
                VoiceStatusText.Text = detail ?? "Escuchando… pulsa otra vez cuando termines.";
                VoiceStatusText.Visibility = Visibility.Visible;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;

            case AssistantVoiceState.Processing:
                _voiceInputActive = false;
                MicButton.IsEnabled = false;
                VoiceStatusText.Text = detail ?? "Convirtiendo tu voz en una orden…";
                VoiceStatusText.Visibility = Visibility.Visible;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;

            case AssistantVoiceState.Error:
                _voiceInputActive = false;
                MicButton.IsEnabled = _voiceAvailable;
                MicButton.ClearValue(BackgroundProperty);
                VoiceStatusText.Text = detail ?? "No pude usar el micrófono.";
                VoiceStatusText.Visibility = Visibility.Visible;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;

            default:
                _voiceInputActive = false;
                MicButton.IsEnabled = _voiceAvailable;
                MicButton.ClearValue(BackgroundProperty);
                VoiceStatusText.Text = detail ??
                    "Voz local lista. Pulsa el icono para hablar y vuelve a pulsarlo para terminar.";
                VoiceStatusText.Visibility = Visibility.Collapsed;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;
        }
    }

    public void BeginSakuraStreamingMessage(string placeholder = "Pensando…")
    {
        CancelSakuraStreamingMessage();

        ShowConversationSurface();
        _streamingBuffer.Clear();
        _streamingHasContent = false;
        _streamingBubble = CreateMessageBubble(
            placeholder,
            HorizontalAlignment.Left,
            (Brush)FindResource("BrushSurfaceRaised"),
            allowSections: false);
        _streamingTextBlock = FindMessageTextBlock(_streamingBubble);

        // 2026-09-15 — el cargador de pétalos junto a «Pensando…», como en la píldora. Se quita con la
        // primera palabra que llega.
        if (_streamingTextBlock is not null)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            _streamingLoader = new Controls.SakuraLoader { Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
            _streamingTextBlock.VerticalAlignment = VerticalAlignment.Center;
            _streamingBubble.Child = null;
            Grid.SetColumn(_streamingTextBlock, 1);
            row.Children.Add(_streamingLoader);
            row.Children.Add(_streamingTextBlock);
            _streamingBubble.Child = row;
        }

        ConversationPanel.Children.Add(_streamingBubble);
        ScrollConversationToEnd();
    }

    public void AppendSakuraStreamingText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_streamingBubble is null || _streamingTextBlock is null)
        {
            BeginSakuraStreamingMessage();
        }

        if (!_streamingHasContent)
        {
            _streamingBuffer.Clear();
            _streamingTextBlock!.Text = string.Empty;
            _streamingHasContent = true;
            if (_streamingLoader is not null)
            {
                _streamingLoader.Visibility = Visibility.Collapsed;
            }
        }

        _streamingBuffer.Append(text);
        _streamingTextBlock!.Text = _streamingBuffer.ToString();
        ScrollConversationToEnd();
    }

    public string CompleteSakuraStreamingMessage()
    {
        var text = _streamingBuffer.ToString().Trim();
        ClearStreamingReferences(removeBubble: false);

        if (string.IsNullOrWhiteSpace(text))
        {
            RenderConversation();
            return string.Empty;
        }

        _messages.Add(new ConversationMessage(
            ConversationRole.Assistant,
            text,
            DateTimeOffset.Now));
        TrimTransientConversation();
        RenderConversation();
        ConversationChanged?.Invoke(this, EventArgs.Empty);
        return text;
    }

    public void CancelSakuraStreamingMessage()
    {
        var hadStreamingBubble = _streamingBubble is not null;
        ClearStreamingReferences(removeBubble: true);
        if (hadStreamingBubble && _messages.Count == 0)
        {
            RenderConversation();
        }
    }

    public void AddUserMessage(string text)
    {
        AddMessage(new ConversationMessage(
            ConversationRole.User,
            text,
            DateTimeOffset.Now));
    }

    public void AddSakuraMessage(string text)
    {
        AddMessage(new ConversationMessage(
            ConversationRole.Assistant,
            text,
            DateTimeOffset.Now));
    }

    private void AddMessage(ConversationMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        _messages.Add(message);
        TrimTransientConversation();
        RenderConversation();
        ConversationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TrimTransientConversation()
    {
        if (_saveHistory || _messages.Count <= _recentMessageLimit)
        {
            return;
        }

        _messages.RemoveRange(0, _messages.Count - _recentMessageLimit);
    }

    private void RenderConversation()
    {
        if (ConversationPanel is null)
        {
            return;
        }

        ClearStreamingReferences(removeBubble: false);
        ConversationPanel.Children.Clear();

        var hasMessages = _messages.Count > 0;
        EmptyStatePanel.Visibility = hasMessages
            ? Visibility.Collapsed
            : Visibility.Visible;
        ConversationScroll.Visibility = hasMessages
            ? Visibility.Visible
            : Visibility.Collapsed;

        foreach (var message in _messages)
        {
            var isUser = message.Role == ConversationRole.User;
            ConversationPanel.Children.Add(CreateMessageBubble(
                message.Text,
                isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                (Brush)FindResource(isUser ? "BrushAccentSoft" : "BrushSurfaceRaised")));
        }

        if (_messages.Count > 0 && _messages[^1].Role == ConversationRole.Assistant)
        {
            ConversationPanel.Children.Add(CreateFollowUpChips());
        }

        ScrollConversationToEnd();
    }

    /// <summary>
    /// 2026-09-15 — botones para seguir con la última respuesta sin saber cómo pedirlo: «Más corto»,
    /// «¿Primer paso?», «Ponlo a prueba»… Cada uno se envía como un mensaje más, así que la respuesta
    /// anterior va en el contexto. Solo debajo de la última: debajo de todas serían ruido.
    /// </summary>
    private static readonly (DocumentSaveFormat Format, string Label)[] SaveFormats =
    [
        (DocumentSaveFormat.Word, "Word"),
        (DocumentSaveFormat.Excel, "Excel"),
        (DocumentSaveFormat.PowerPoint, "PowerPoint")
    ];

    /// <summary>2026-09-16 — se pidió guardar la conversación como archivo.</summary>
    public event EventHandler? ExportRequested;

    /// <summary>
    /// 2026-09-16 — Borrador de tarea, fase 3: se elige el tipo de trabajo y Sakura arma el borrador
    /// en la conversación, con las secciones de ese tipo. El tipo que Sakura sugiere (leyendo lo que
    /// la persona escribió) va primero y marcado; elegir es de la persona. Después se guarda con
    /// «Guardar como Word», que añade ecuaciones, fuentes y la marca de IA.
    /// </summary>
    private MenuItem CreateAssignmentMenu()
    {
        var taskText = string.Join(
            '\n',
            _messages.Where(message => message.Role == ConversationRole.User).Select(message => message.Text));

        // 2026-09-16 — fase 4: el tipo y los datos de la portada se confirman en su propia ventana.
        // La conversación completa cuenta para detectar un examen: la explicación de la ventana
        // (Ctrl+Shift+Espacio) describe lo que se veía en pantalla.
        var conversation = string.Join('\n', _messages.Select(message => message.Text));
        var menu = new MenuItem { Header = "Borrador de tarea…" };
        menu.Click += (_, _) => AssignmentDraftRequested?.Invoke(this, taskText + "\n" + conversation);
        return menu;
    }

    /// <summary>Se pidió el Borrador de tarea; lleva el texto de la conversación.</summary>
    public event EventHandler<string>? AssignmentDraftRequested;

    /// <summary>Manda un mensaje como si la persona lo hubiera escrito.</summary>
    public void SubmitPrompt(string prompt) =>
        PromptSubmitted?.Invoke(this, new PromptSubmittedEventArgs(prompt));

    /// <summary>
    /// 2026-09-16 — las dos filas de botones bajo cada respuesta pesaban más que la respuesta (Adler).
    /// Quedan detrás de un «+» pequeño: un clic abre el mismo menú con «Seguir» y «Guardar como».
    /// </summary>
    private FrameworkElement CreateFollowUpChips()
    {
        var menu = new ContextMenu();

        foreach (var followUp in Nexo.Core.Assistant.AnswerFollowUps.All)
        {
            var item = new MenuItem { Header = followUp.Label, ToolTip = followUp.Prompt };
            var prompt = followUp.Prompt;
            item.Click += (_, _) => PromptSubmitted?.Invoke(this, new PromptSubmittedEventArgs(prompt));
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var answer = _messages[^1].Text;
        foreach (var (format, label) in SaveFormats)
        {
            var item = new MenuItem { Header = $"Guardar como {label}" };
            var chosen = format;
            item.Click += (_, _) => DocumentSaveRequested?.Invoke(this, new DocumentSaveEventArgs(answer, chosen));
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(CreateAssignmentMenu());

        menu.Items.Add(new Separator());
        var export = new MenuItem { Header = "Exportar la conversación" };
        export.Click += (_, _) => ExportRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(export);

        var more = new Button
        {
            Content = "+",
            Width = 28,
            Height = 28,
            FontSize = 15,
            Margin = new Thickness(2, 6, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = (Brush)FindResource("BrushTextSecondary"),
            Background = Brushes.Transparent,
            Style = (Style)FindResource("IconButtonStyle"),
            ToolTip = "Seguir con la respuesta o guardarla",
            ContextMenu = menu
        };
        System.Windows.Automation.AutomationProperties.SetName(more, "Más opciones para la respuesta");
        more.Click += (_, _) =>
        {
            menu.PlacementTarget = more;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        };

        return more;
    }

    private void ShowConversationSurface()
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        ConversationScroll.Visibility = Visibility.Visible;
    }

    private void ScrollConversationToEnd()
    {
        Dispatcher.BeginInvoke(
            new Action(ConversationScroll.ScrollToEnd),
            DispatcherPriority.Background);
    }

    private void ClearStreamingReferences(bool removeBubble)
    {
        if (removeBubble && _streamingBubble is not null && ConversationPanel is not null)
        {
            ConversationPanel.Children.Remove(_streamingBubble);
        }

        _streamingBubble = null;
        _streamingTextBlock = null;
        _streamingLoader = null;
        _streamingBuffer.Clear();
        _streamingHasContent = false;
    }

    private void VisionButton_Click(object sender, RoutedEventArgs e)
    {
        VisionCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DiscardVisionButton_Click(object sender, RoutedEventArgs e)
    {
        ClearVisionAttachment();
        VisionAttachmentCleared?.Invoke(this, EventArgs.Empty);
    }

    private void MicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceInputActive)
        {
            EndVoiceInput();
            return;
        }

        BeginVoiceInput();
    }

    private void BeginVoiceInput()
    {
        if (_voiceInputActive || MicButton.IsEnabled == false)
        {
            return;
        }

        _voiceInputActive = true;
        VoiceInputStarted?.Invoke(this, EventArgs.Empty);
    }

    private void EndVoiceInput()
    {
        if (!_voiceInputActive)
        {
            return;
        }

        _voiceInputActive = false;
        VoiceInputStopped?.Invoke(this, EventArgs.Empty);
    }

    private void QuickPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string prompt } || string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        PromptBox.Text = prompt;
        PromptBox.CaretIndex = PromptBox.Text.Length;
        FocusPrompt();
    }

    private void PromptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PromptHintText is not null)
        {
            PromptHintText.Visibility = string.IsNullOrEmpty(PromptBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            SubmitPrompt();
            e.Handled = true;
            return;
        }

        // Diseño D58 — pegar una imagen.
        //
        // El texto ya se pegaba solo: es un TextBox. Una imagen no, porque un TextBox la descarta
        // sin decir nada, y «no pasa nada» es la peor respuesta posible a un Ctrl+V —deja pensando
        // si falló el copiado, el pegado o el programa. Se intercepta solo cuando de verdad hay una
        // imagen en el portapapeles; en cualquier otro caso el TextBox sigue pegando como siempre.
        var isPaste =
            (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ||
            (e.Key == Key.Insert && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);

        if (isPaste && TryPasteImage())
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Se dispara cuando alguien pega una imagen en el cuadro de escribir. Los bytes van ya en PNG.
    /// </summary>
    public event EventHandler<PastedImageEventArgs>? ImagePasted;

    /// <summary>
    /// Saca una imagen del portapapeles, si la hay. Devuelve si se quedó con el pegado.
    ///
    /// Se aceptan las dos formas en que llega una imagen, porque para quien la copia son la misma
    /// cosa: un mapa de bits —una captura, algo copiado del navegador— y un archivo copiado desde
    /// el explorador. Distinguirlas sería pedirle a la persona que sepa qué hizo Windows por dentro.
    /// </summary>
    private bool TryPasteImage()
    {
        try
        {
            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bitmap)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var buffer = new MemoryStream();
                encoder.Save(buffer);

                ImagePasted?.Invoke(
                    this, new PastedImageEventArgs("Imagen pegada", buffer.ToArray()));
                return true;
            }

            if (Clipboard.ContainsFileDropList())
            {
                foreach (var path in Clipboard.GetFileDropList())
                {
                    if (path is null || !IsImageFile(path) || !File.Exists(path))
                    {
                        continue;
                    }

                    ImagePasted?.Invoke(
                        this,
                        new PastedImageEventArgs(
                            Path.GetFileName(path), ToPngBytes(File.ReadAllBytes(path))));
                    return true;
                }
            }
        }
        catch (Exception exception) when (
            exception is ExternalException or IOException or NotSupportedException or
                UnauthorizedAccessException or OutOfMemoryException)
        {
            // El portapapeles es de todo el sistema y otro programa puede tenerlo tomado justo
            // ahora. Devolver falso deja que el TextBox intente el pegado normal, que es lo más
            // parecido a no haber hecho nada.
        }

        return false;
    }

    private static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Todo se normaliza a PNG. Aguas abajo el adjunto se anuncia como <c>image/png</c>, así que
    /// mandar un JPEG con esa etiqueta es mentirle al proveedor sobre lo que lleva dentro.
    /// </summary>
    private static byte[] ToPngBytes(byte[] original)
    {
        using var source = new MemoryStream(original);
        var decoded = BitmapFrame.Create(
            source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(decoded));

        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        return buffer.ToArray();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitPrompt();
    }

    private void ClearConversationButton_Click(object sender, RoutedEventArgs e)
    {
        CancelSakuraStreamingMessage();
        _messages.Clear();
        if (HasVisionAttachment)
        {
            ClearVisionAttachment();
            VisionAttachmentCleared?.Invoke(this, EventArgs.Empty);
        }
        RenderConversation();
        ConversationCleared?.Invoke(this, EventArgs.Empty);
        FocusPrompt();
    }

    private void SubmitPrompt()
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt) && HasVisionAttachment)
        {
            prompt = "Describe lo importante de esta captura y señala cualquier error visible.";
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        PromptBox.Clear();
        PromptSubmitted?.Invoke(this, new PromptSubmittedEventArgs(prompt));
        FocusPrompt();
    }

    private static BitmapImage LoadBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private Border CreateMessageBubble(
        string text,
        HorizontalAlignment alignment,
        Brush background,
        bool allowSections = true)
    {
        var isUser = alignment == HorizontalAlignment.Right;

        // Diseño D58 — una respuesta con apartados se dibuja con apartados.
        //
        // Solo del lado de Sakura: lo que escribe la persona se enseña tal cual lo escribió, y
        // reinterpretar su texto como si tuviera secciones sería corregirle.
        //
        // Y nunca mientras se está escribiendo. Reconocer sobre un texto a medias parte por donde
        // el modelo aún no ha terminado, así que las cajas se formarían y se desharían a cada
        // trozo; se reconoce una vez, cuando la respuesta ya está entera.
        if (!isUser && allowSections)
        {
            var sections = AnswerSections.Parse(text);
            if (sections.Count > 0)
            {
                return CreateSectionedBubble(text, sections, background);
            }
        }

        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20,
            Foreground = (Brush)Application.Current.FindResource("BrushTextPrimary")
        };

        // 2026-09-14 — una respuesta de Sakura ya terminada se dibuja con su formato (negritas,
        // listas, tablas) en vez de enseñar los asteriscos y las barras. Lo que escribe la persona y
        // la respuesta mientras llega se quedan como texto: la primera es suya, y la segunda a medias
        // partiría una tabla o una negrita por donde el modelo aún no ha terminado.
        UIElement content = !isUser && allowSections
            ? AnswerRenderer.Render(text)
            : textBlock;

        var bubble = new Border
        {
            Margin = new Thickness(0, 7, 0, 0),
            Padding = new Thickness(13, 11, 13, 11),
            CornerRadius = isUser
                ? new CornerRadius(17, 17, 6, 17)
                : new CornerRadius(17, 17, 17, 6),
            Background = background,
            BorderBrush = (Brush)Application.Current.FindResource(
                isUser ? "BrushAccentBorder" : "BrushBorder"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = alignment,
            MaxWidth = isUser ? 370 : 430,
            Child = content
        };

        // Se lee del TextBlock y no del texto de creación: una respuesta que llega a trozos sigue
        // creciendo, y copiar lo que había al construirla daría media frase. La ya terminada se
        // copia tal cual la escribió el modelo.
        AttachCopyMenu(bubble, () => ReferenceEquals(content, textBlock) ? textBlock.Text : text, isUser);
        return bubble;
    }

    /// <summary>
    /// La misma burbuja, pero con cada apartado en su propia caja.
    ///
    /// La entradilla —lo que va antes del primer título— se deja suelta, sin caja: es una frase de
    /// presentación y meterla en un recuadro con borde la hace parecer un apartado más.
    /// </summary>
    private Border CreateSectionedBubble(
        string fullText,
        IReadOnlyList<AnswerSection> sections,
        Brush background)
    {
        var stack = new StackPanel();

        foreach (var section in sections)
        {
            if (section.Title.Length == 0)
            {
                if (section.Body.Length == 0)
                {
                    continue;
                }

                var intro = (FrameworkElement)AnswerRenderer.Render(section.Body);
                intro.Margin = new Thickness(0, 0, 0, 4);
                stack.Children.Add(intro);
                continue;
            }

            var card = new StackPanel();
            card.Children.Add(new TextBlock
            {
                Text = section.Title,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("BrushAccent")
            });

            if (section.Body.Length > 0)
            {
                var body = (FrameworkElement)AnswerRenderer.Render(section.Body);
                body.Margin = new Thickness(0, 5, 0, 0);
                card.Children.Add(body);
            }

            stack.Children.Add(new Border
            {
                Margin = new Thickness(0, 7, 0, 0),
                Padding = new Thickness(11, 9, 11, 10),
                CornerRadius = (CornerRadius)Application.Current.FindResource("RadiusMedium"),
                Background = (Brush)Application.Current.FindResource("BrushSurface"),
                BorderBrush = (Brush)Application.Current.FindResource("BrushBorder"),
                BorderThickness = new Thickness(1),
                Child = card
            });
        }

        var bubble = new Border
        {
            Margin = new Thickness(0, 7, 0, 0),
            Padding = new Thickness(11, 9, 11, 12),
            CornerRadius = new CornerRadius(17, 17, 17, 6),
            Background = background,
            BorderBrush = (Brush)Application.Current.FindResource("BrushBorder"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 430,
            Child = stack
        };

        // Se copia el texto original, no lo que se ve: quien copia una receta quiere la receta,
        // no la reconstrucción de las cajas por las que se ha repartido.
        AttachCopyMenu(bubble, () => fullText, isUser: false);
        return bubble;
    }

    /// <summary>
    /// Diseño D58 — poder llevarse lo que Sakura responde.
    ///
    /// Un <c>TextBlock</c> de WPF no deja seleccionar, así que hasta ahora una respuesta se podía
    /// leer y nada más: para usarla había que copiarla a mano. El menú contextual se pone en la
    /// burbuja entera y no solo en el texto, porque apuntar al texto exacto dentro de una burbuja
    /// con relleno es justo el gesto que falla.
    ///
    /// Se lee del <c>TextBlock</c> en el momento de copiar y no del texto con el que se creó: una
    /// respuesta que llega a trozos sigue creciendo después de construida, y copiar lo que había
    /// al principio daría media frase.
    /// </summary>
    private void AttachCopyMenu(Border bubble, Func<string?> content, bool isUser)
    {
        var copy = new MenuItem { Header = "Copiar" };
        copy.Click += (_, _) => CopyToClipboard(content());

        var menu = new ContextMenu { Items = { copy } };

        // Diseño D59 — solo del lado de Sakura: guardar como documento lo que uno mismo acaba de
        // escribir no es una necesidad, y el menú se lee mejor con una opción que con dos.
        if (!isUser)
        {
            // 2026-09-15 — también como Excel y como PowerPoint, además de Word.
            foreach (var (format, label) in SaveFormats)
            {
                var save = new MenuItem { Header = "Guardar en el escritorio como " + label };
                save.Click += (_, _) =>
                {
                    var text = content();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        DocumentSaveRequested?.Invoke(this, new DocumentSaveEventArgs(text, format));
                    }
                };

                menu.Items.Add(save);
            }

            // Diseño D89 — política 11.16 de Microsoft Store: cómo reportar una respuesta de IA
            // inapropiada. El texto se copia al portapapeles, en este equipo, y la persona decide si lo
            // pega en el reporte: no se manda nada por su cuenta.
            var report = new MenuItem { Header = "Reportar esta respuesta" };
            report.Click += (_, _) =>
            {
                CopyToClipboard(content());
                AiContentReportRequested?.Invoke(this, EventArgs.Empty);
            };

            menu.Items.Add(report);
        }

        bubble.ContextMenu = menu;
    }

    /// <summary>Se pidió dejar una respuesta como documento.</summary>
    public event EventHandler<DocumentSaveEventArgs>? DocumentSaveRequested;

    /// <summary>Diseño D89 — se pidió reportar una respuesta; el texto ya está en el portapapeles.</summary>
    public event EventHandler? AiContentReportRequested;

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception exception) when (
            exception is ExternalException or COMException)
        {
            // Otro programa puede tener el portapapeles tomado. Reintentar o avisar por esto
            // molestaría más que el propio fallo: se vuelve a intentar con otro clic.
        }
    }

    private static TextBlock? FindMessageTextBlock(DependencyObject root)
    {
        if (root is TextBlock textBlock)
        {
            return textBlock;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var found = FindMessageTextBlock(VisualTreeHelper.GetChild(root, index));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    // Compatibilidad temporal para extensiones que todavía usan los nombres internos anteriores.
    public void BeginNexoStreamingMessage(string placeholder = "Pensando…") =>
        BeginSakuraStreamingMessage(placeholder);

    public void AppendNexoStreamingText(string text) => AppendSakuraStreamingText(text);

    public string CompleteNexoStreamingMessage() => CompleteSakuraStreamingMessage();

    public void CancelNexoStreamingMessage() => CancelSakuraStreamingMessage();

    public void AddNexoMessage(string text) => AddSakuraMessage(text);
}

public enum AssistantVoiceState
{
    Idle,
    Listening,
    Processing,
    Error
}

public sealed class PromptSubmittedEventArgs : EventArgs
{
    public PromptSubmittedEventArgs(string prompt)
    {
        Prompt = prompt;
    }

    public string Prompt { get; }
}

/// <summary>Una imagen que alguien pegó en el cuadro de escribir, ya en PNG.</summary>
public sealed class PastedImageEventArgs(string title, byte[] pngBytes) : EventArgs
{
    public string Title { get; } = title;

    public byte[] PngBytes { get; } = pngBytes;
}

/// <summary>Una respuesta que alguien quiere conservar como documento.</summary>
public sealed class DocumentSaveEventArgs(string answer, DocumentSaveFormat format = DocumentSaveFormat.Word) : EventArgs
{
    public string Answer { get; } = answer;

    public DocumentSaveFormat Format { get; } = format;
}

/// <summary>Los formatos en que se puede guardar una respuesta.</summary>
public enum DocumentSaveFormat
{
    Word,
    Excel,
    PowerPoint
}
