using System.Text.RegularExpressions;

namespace Nexo.Windows.Tests.Design;

/// <summary>
/// Diseño D1 (Sakura Shell) — invariantes estructurales del marco visual principal, la barra
/// lateral y la navegación. <c>Nexo.App</c> no se referencia desde pruebas porque arrastra
/// <c>UseWPF</c> (ver notas de la Fase 1.1), así que estas pruebas leen el código fuente
/// directamente, igual que <c>CompositionInvariantTests</c> y <c>AdaptiveEngineUiInvariantTests</c>.
/// </summary>
public sealed class SakuraShellStructureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly string[] KnownDestinationsInOrder =
    [
        "Home", "Assistant", "Tasks", "Focus", "Routines", "Audio", "Capture", "System", "Settings"
    ];

    // ---------- 1. App carga ThemeResources ----------

    [Fact]
    public void App_LoadsThemeResourcesAggregator()
    {
        var content = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "App.xaml"));

        Assert.Contains("Themes/ThemeResources.xaml", content, StringComparison.Ordinal);
    }

    // ---------- 2. Recursos Sakura requeridos existen ----------

    [Fact]
    public void SakuraShellTokensAndStyles_AreDefined()
    {
        var colors = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes", "Colors.xaml"));
        var spacing = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes", "Spacing.xaml"));
        var controls = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes", "Controls.xaml"));

        Assert.Contains("x:Key=\"BrushSidebarSurface\"", colors, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ColorSidebarSurface\"", colors, StringComparison.Ordinal);

        string[] spacingKeys =
        [
            "RadiusShell", "SidebarWidthCollapsed", "SidebarWidthExpanded",
            "SidebarButtonWidthCollapsed", "SidebarButtonWidthExpanded",
            "NavigationIconSize", "NavigationIndicatorWidth"
        ];
        Assert.All(spacingKeys, key => Assert.Contains($"x:Key=\"{key}\"", spacing, StringComparison.Ordinal));

        string[] controlStyleKeys =
        [
            // Diseño D2.0: SakuraNavigationIconFilledStyle se renombró a
            // SakuraNavigationIconSelectedStyle al dejar de rellenar el icono (el relleno
            // convertía las geometrías de línea en bloques sólidos).
            "SakuraNavigationIconStyle", "SakuraNavigationIconSelectedStyle", "SakuraNavigationItemStyle",
            "SakuraSidebarToggleStyle", "SakuraPageTitleStyle", "SakuraPageSubtitleStyle", "SakuraShellCardStyle"
        ];
        Assert.All(controlStyleKeys, key => Assert.Contains($"x:Key=\"{key}\"", controls, StringComparison.Ordinal));
    }

    // ---------- 3. Claves de recursos únicas ----------

    [Fact]
    public void NewSakuraKeys_DoNotCollideWithExistingTokens()
    {
        // La suite completa de unicidad por archivo vive en DesignSystemResourceTests
        // (EachDictionary_HasNoDuplicateKeys), que ya escanea los 8 diccionarios de tema y por
        // lo tanto valida también las claves nuevas de este sprint. Aquí se verifica además que
        // ninguna clave nueva se repita respecto a las que ya existían antes de D1.
        var preExisting = new[] { "BrushBackground", "BrushSurface", "RadiusLarge", "RadiusPill", "Space12", "MotionBase" };
        var newKeys = new[]
        {
            "BrushSidebarSurface", "RadiusShell", "SidebarWidthCollapsed", "SidebarWidthExpanded",
            "SakuraNavigationIconStyle", "SakuraNavigationItemStyle", "SakuraSidebarToggleStyle",
            "SakuraPageTitleStyle", "SakuraPageSubtitleStyle", "SakuraShellCardStyle"
        };

        Assert.Empty(preExisting.Intersect(newKeys, StringComparer.Ordinal));
    }

    // ---------- 4. Referencias XAML válidas ----------

    [Fact]
    public void MainWindow_ResourceReferences_ResolveToKnownThemeKeys()
    {
        var mainWindow = ReadMainWindowXaml();
        var themeKeys = CollectAllThemeKeys();

        var referenced = Regex.Matches(mainWindow, @"\{(?:Static|Dynamic)Resource\s+([A-Za-z0-9_]+)\}")
            .Select(m => m.Groups[1].Value)
            .Where(key => key.StartsWith("Brush", StringComparison.Ordinal)
                || key.StartsWith("Color", StringComparison.Ordinal)
                || key.StartsWith("Radius", StringComparison.Ordinal)
                || key.StartsWith("FontSize", StringComparison.Ordinal)
                || key.StartsWith("FontFamily", StringComparison.Ordinal)
                || key.StartsWith("Space", StringComparison.Ordinal)
                || key.StartsWith("Motion", StringComparison.Ordinal)
                || key.StartsWith("Sidebar", StringComparison.Ordinal)
                || key.StartsWith("Navigation", StringComparison.Ordinal)
                || key.StartsWith("Sakura", StringComparison.Ordinal))
            .Distinct();

        Assert.All(referenced, key => Assert.Contains(key, themeKeys));
    }

    // ---------- 5. MainWindow no contiene colores hexadecimales literales nuevos ----------

    [Fact]
    public void MainWindow_ContainsNoLiteralHexColors()
    {
        var content = ReadMainWindowXaml();

        var matches = Regex.Matches(content, @"#[0-9A-Fa-f]{6,8}");

        Assert.Empty(matches);
    }

    // ---------- 6. Sidebar mantiene todas las entradas existentes ----------

    [Fact]
    public void Sidebar_PreservesEveryExistingNavigationEntry()
    {
        var content = ReadMainWindowXaml();

        string[] expectedButtonNames =
        [
            "HomeNavButton", "AssistantNavButton", "TasksNavButton", "FocusNavButton",
            "RoutinesNavButton", "AudioNavButton", "CaptureNavButton", "SystemNavButton",
            "SettingsNavButton"
        ];

        Assert.All(expectedButtonNames, name => Assert.Contains($"x:Name=\"{name}\"", content, StringComparison.Ordinal));
    }

    // ---------- 7. Orden funcional de navegación preservado ----------

    [Fact]
    public void Sidebar_PreservesFunctionalNavigationOrder()
    {
        var content = ReadMainWindowXaml();

        var positions = KnownDestinationsInOrder
            .Select(destination => content.IndexOf($"Tag=\"{destination}\"", StringComparison.Ordinal))
            .ToList();

        Assert.All(positions, position => Assert.True(position >= 0, "Falta una entrada de navegación esperada."));

        var sorted = positions.OrderBy(p => p).ToList();
        Assert.Equal(sorted, positions);
    }

    // ---------- 8. Cada elemento tiene nombre accesible ----------

    [Fact]
    public void NavigationElements_HaveAccessibleNames()
    {
        var content = ReadMainWindowXaml();

        string[] expectedAccessibleNames =
        [
            "Expandir o contraer navegación", "Inicio", "Asistente", "Pendientes de hoy", "Enfoque",
            "Atajos", "Audio", "Captura", "Sistema", "Personalización y perfiles",
            "Abrir paleta de comandos"
        ];

        Assert.All(
            expectedAccessibleNames,
            name => Assert.Contains($"AutomationProperties.Name=\"{name}\"", content, StringComparison.Ordinal));
    }

    // ---------- 9. Estado activo no depende únicamente de color ----------

    [Fact]
    public void ActiveState_IsNotColorOnly()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private void ApplyNavigationItemState(",
            "private void UpdateWorkspaceHeader(");

        // Color (Background/Foreground) es una de varias señales, nunca la única: el indicador,
        // el grosor de trazo del ícono (D2.0; antes era relleno) y el peso de la etiqueta
        // también cambian con la selección.
        Assert.Contains("button.Background =", body, StringComparison.Ordinal);
        Assert.Contains("indicator.Visibility =", body, StringComparison.Ordinal);
        Assert.Contains("label.FontWeight = selected", body, StringComparison.Ordinal);

        // El grosor del trazo es la señal que aguanta el caso difícil. Al plegar el rail (D36) el
        // elemento activo pasa a señalarse con un brillo, y tanto el brillo como el color de acento
        // son señales cromáticas: la barra indicadora y el peso de la etiqueta no ayudan ahí,
        // porque el indicador se oculta y la etiqueta no se ve. Lo que queda —y por eso se exige
        // explícitamente— es que el ícono del activo siga dibujándose con un trazo más grueso.
        Assert.Contains("icon.Style = ", body, StringComparison.Ordinal);
        Assert.Contains("SakuraNavigationIconSelectedStyle", body, StringComparison.Ordinal);
    }

    // ---------- 9b. El estado plegado conserva una señal no cromática ----------

    [Fact]
    public void CollapsedActiveState_KeepsANonColourSignal()
    {
        var spacing = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes", "Spacing.xaml"));

        var normal = ReadDouble(spacing, "NavigationIconStrokeThickness");
        var selected = ReadDouble(spacing, "NavigationIconStrokeThicknessSelected");

        // Si alguien igualara estos dos valores, con el rail plegado el elemento activo quedaría
        // distinguible solo por color —acento y brillo— y eso incumple WCAG 1.4.1. La diferencia
        // tiene que ser lo bastante grande para verse, no simbólica.
        Assert.True(
            selected >= normal * 1.25,
            $"El trazo del ícono activo ({selected}) apenas se distingue del normal ({normal}).");
    }

    private static double ReadDouble(string xaml, string key)
    {
        var match = Regex.Match(
            xaml,
            $"<sys:Double x:Key=\"{Regex.Escape(key)}\">([0-9.]+)</sys:Double>");

        Assert.True(match.Success, $"No encontré {key} en Spacing.xaml.");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    // ---------- 10. Tooltips presentes en modo contraído ----------

    [Fact]
    public void NavigationButtons_HaveTooltipsForCollapsedMode()
    {
        var content = ReadMainWindowXaml();

        // Los ToolTip no se ocultan al contraer: siguen siendo la única pista textual cuando
        // las etiquetas (*NavLabel) están Collapsed.
        var toolTipCount = Regex.Matches(content, @"ToolTip=""[^""]+""").Count;

        Assert.True(toolTipCount >= 9, "Se esperaban al menos 9 tooltips (8 módulos + Personalizar).");
    }

    // ---------- 11. Foco visible en elementos de navegación ----------

    [Fact]
    public void NavigationStyles_DefineAVisibleFocusState()
    {
        var controls = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes", "Controls.xaml"));

        var navigationItemStyle = ExtractMethodBody(controls, "x:Key=\"SakuraNavigationItemStyle\"", "x:Key=\"SakuraSidebarToggleStyle\"");
        var toggleStyle = ExtractMethodBody(controls, "x:Key=\"SakuraSidebarToggleStyle\"", "x:Key=\"SakuraPageTitleStyle\"");

        Assert.Contains("IsKeyboardFocused", navigationItemStyle, StringComparison.Ordinal);
        Assert.Contains("BrushFocusRing", navigationItemStyle, StringComparison.Ordinal);
        Assert.Contains("IsKeyboardFocused", toggleStyle, StringComparison.Ordinal);
        Assert.Contains("BrushFocusRing", toggleStyle, StringComparison.Ordinal);
    }

    // ---------- 12. Animaciones respetan ClientAreaAnimation ----------

    [Fact]
    public void ShellAnimations_RespectClientAreaAnimation()
    {
        var content = ReadMainWindowSource();

        Assert.Contains("SystemParameters.ClientAreaAnimation", content, StringComparison.Ordinal);
        Assert.Contains("ShellAnimationsAllowed", content, StringComparison.Ordinal);

        // Los dos únicos disparadores de animación del shell (navegación y expandir/contraer)
        // deben consultar la propiedad combinada, no solo la preferencia propia de Sakura.
        var navigateBody = ExtractMethodBody(content, "private void NavigateTo(", "private void FocusCurrentView(");
        var sidebarBody = ExtractMethodBody(content, "private void SetSideRailExpanded(", "private void ApplySideRailButtonLayout(");

        Assert.Contains("ShellAnimationsAllowed", navigateBody, StringComparison.Ordinal);
        Assert.Contains("ShellAnimationsAllowed", sidebarBody, StringComparison.Ordinal);
    }

    // ---------- 13. No existen animaciones en bucle ----------

    [Fact]
    public void ShellAnimations_NeverLoop()
    {
        var navigateBody = ExtractMethodBody(ReadMainWindowSource(), "private void NavigateTo(", "private void FocusCurrentView(");
        var sidebarBody = ExtractMethodBody(ReadMainWindowSource(), "private void SetSideRailExpanded(", "private void ApplySideRailButtonLayout(");

        Assert.DoesNotContain("RepeatBehavior", navigateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("RepeatBehavior", sidebarBody, StringComparison.Ordinal);
    }

    // ---------- 14. Duraciones no exceden 300 ms ----------

    [Fact]
    public void ShellMotionTokens_NeverExceed300Milliseconds()
    {
        // Motion.xaml también define MotionSlow (350 ms) para transiciones ajenas al shell, ya
        // existente antes de este sprint. Este diseño solo usa MotionFast (navegación) y
        // MotionBase (expandir/contraer sidebar), así que solo esos dos deben cumplir el límite
        // de 300 ms que exige el sprint D1.
        var motion = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes", "Motion.xaml"));

        foreach (var key in new[] { "MotionFast", "MotionBase" })
        {
            var match = Regex.Match(motion, $@"<Duration x:Key=""{key}"">([\d:.]+)</Duration>");
            Assert.True(match.Success, $"No se encontró el token de movimiento '{key}'.");

            var value = TimeSpan.Parse(match.Groups[1].Value);
            Assert.True(value.TotalMilliseconds <= 300, $"El token de movimiento '{key}' excede 300 ms.");
        }
    }

    // ---------- 15. Content host conserva el mecanismo actual de navegación ----------

    [Fact]
    public void ContentHost_PreservesTheExistingContentControlMechanism()
    {
        var xaml = ReadMainWindowXaml();
        var source = ReadMainWindowSource();

        Assert.Contains("x:Name=\"ModuleHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ContentControl", xaml, StringComparison.Ordinal);
        Assert.Contains("ModuleHost.Content = view;", source, StringComparison.Ordinal);
    }

    // ---------- 16. No se añadieron llamadas a servicios en code-behind visual ----------

    [Fact]
    public void VisualShellMethods_NeverConstructProductionServices()
    {
        var source = ReadMainWindowSource();
        string[] visualMethods =
        [
            "private void ApplyNavigationItemState(",
            "private void UpdateNavigationState(",
            "private void SetSideRailExpanded(",
            "private void ApplySideRailButtonLayout(",
            "private void ApplyShellOpacity("
        ];

        string[] forbiddenConstructions =
        [
            "new AiChatRouterService(", "new WindowsAudioMixerService(", "new WhisperVoiceInputService(",
            "new WindowsTextToSpeechService(", "new VoskWakeWordService(", "new WindowsScreenCaptureService(",
            "new WindowsHardwareCapabilityService(", "new WindowsAdaptiveEngineRegistry("
        ];

        foreach (var methodStart in visualMethods)
        {
            var start = source.IndexOf(methodStart, StringComparison.Ordinal);
            Assert.True(start >= 0, $"No se encontró el método '{methodStart}'.");

            var end = FindMethodEnd(source, start);
            var body = source[start..end];

            Assert.All(forbiddenConstructions, ctor => Assert.DoesNotContain(ctor, body, StringComparison.Ordinal));
        }
    }

    // ---------- 17. No se modificó VoiceCoordinator ----------

    [Fact]
    public void VoiceCoordinator_HasNoShellOrDesignReferences()
    {
        var content = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src", "Nexo.Windows", "Voice", "VoiceCoordinator.cs"));

        // Diseño D69 — antes esto buscaba la palabra "Sakura", que servía de atajo para "clases del
        // shell y del lenguaje visual". Desde que el producto se llama Sakura, esa palabra aparece
        // en comentarios y mensajes sin significar acoplamiento ninguno, así que la comprobación
        // pasa a nombrar lo que de verdad no debe estar aquí: ventanas, vistas y controles.
        Assert.DoesNotContain("SakuraPill", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Nexo.App", content, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellBorder", content, StringComparison.Ordinal);
        Assert.DoesNotContain("SideRail", content, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow", content, StringComparison.Ordinal);
    }

    // ---------- 18. No se modificó la política adaptativa ----------

    [Fact]
    public void AdaptiveEnginePolicy_HasNoShellOrDesignReferences()
    {
        var content = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src", "Nexo.Core", "AdaptiveEngine", "AdaptiveEnginePolicy.cs"));

        // Diseño D69 — igual que arriba: el nombre del producto ya no vale como señal de
        // acoplamiento, así que se comprueban los tipos del shell.
        Assert.DoesNotContain("SakuraPill", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Nexo.App", content, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow", content, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", content, StringComparison.Ordinal);
    }

    // ---------- 19. Nexo.Core permanece sin PackageReference ----------

    [Fact]
    public void NexoCoreProject_StillHasNoPackageReferences()
    {
        var content = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.Core", "Nexo.Core.csproj"));

        Assert.DoesNotContain("PackageReference", content, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- 20. MainWindow permanece sin IServiceProvider ----------

    [Fact]
    public void MainWindow_StillDoesNotReferenceIServiceProvider()
    {
        Assert.DoesNotContain("IServiceProvider", ReadMainWindowSource(), StringComparison.Ordinal);
    }

    // ---------- Comportamiento: expandida / contraída / cambio de sección / activa / colapso / teclado ----------

    [Fact]
    public void SideRailToggle_InvokesSetSideRailExpandedWithPersistence()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(), "private void SideRailToggleButton_Click(", "private void SetSideRailExpanded(");

        Assert.Contains("SetSideRailExpanded(!_sideRailExpanded, animate: true, persist: true);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySideRailButtonLayout_TogglesBrandTextAndEveryLabelAndButtonWidth()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(), "private void ApplySideRailButtonLayout(", "private void CommandPaletteButton_Click(");

        Assert.Contains("SideRailBrandText.Visibility = expanded", body, StringComparison.Ordinal);
        Assert.Contains("SettingsNavLabel", body, StringComparison.Ordinal);

        // Diseño D64 — aquí se comprobaba el ángulo del chevrón, que giraba según el lado del rail
        // (D26). Ese chevrón ya no existe: el botón lleva la marca de Sakura, elegida por Adler, y
        // a diecinueve píxeles no caben la flor y una flecha encima sin ensuciarse. La prueba se
        // cambia a conciencia, no porque estorbara: lo que comprobaba dejó de tener sujeto.
        //
        // Lo que sí tiene que seguir siendo cierto es que el botón cuente qué hace sin depender de
        // un dibujo, porque ahora el dibujo ya no lo dice.
        Assert.DoesNotContain("SideRailChevronRotate", body, StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"Expandir o contraer navegación\"",
            ReadMainWindowXaml(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void NavigateTo_UpdatesActiveSectionAndHeaderOnEveryCall()
    {
        var body = ExtractMethodBody(ReadMainWindowSource(), "private void NavigateTo(", "private void FocusCurrentView(");

        Assert.Contains("_currentDestination = destination;", body, StringComparison.Ordinal);
        Assert.Contains("UpdateNavigationState(destination);", body, StringComparison.Ordinal);
        Assert.Contains("UpdateWorkspaceHeader(destination);", body, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapeKey_CollapsesSidebarBeforeHidingTheWindow()
    {
        var content = ReadMainWindowSource();
        var start = content.IndexOf("private void Window_PreviewKeyDown(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = FindMethodEnd(content, start);
        var body = content[start..end];

        Assert.Contains("Key.Escape", body, StringComparison.Ordinal);
    }

    // ---------- Helpers ----------

    private static HashSet<string> CollectAllThemeKeys()
    {
        var themesDirectory = Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes");
        string[] themeFiles =
        [
            "Colors.xaml", "Typography.xaml", "Spacing.xaml", "Motion.xaml",
            "Brushes.xaml", "Brand.xaml", "Controls.xaml"
        ];

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in themeFiles)
        {
            var content = File.ReadAllText(Path.Combine(themesDirectory, file));
            foreach (Match match in Regex.Matches(content, @"x:Key=""([A-Za-z0-9_]+)"""))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    private static string ReadMainWindowXaml() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml"));

    private static string ReadMainWindowSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml.cs"));

    private static string ExtractMethodBody(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No se encontró '{startMarker}' en el archivo.");

        var end = content.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"No se encontró '{endMarker}' después de '{startMarker}'.");
        return content[start..end];
    }

    private static int FindMethodEnd(string content, int methodStart)
    {
        var braceStart = content.IndexOf('{', methodStart);
        var depth = 0;
        for (var i = braceStart; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }
        }

        throw new InvalidOperationException("No se encontró el cierre del método.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nexo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró Nexo.slnx desde el directorio de pruebas.");
    }
}
