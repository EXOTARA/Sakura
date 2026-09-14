# Sakura Shell v1 — Diseño D1

> Primer sprint que produce un cambio visual evidente en Kohana. Rediseña el marco principal
> (barra lateral, marca, navegación, encabezado, superficie de contenido) sobre la Design System
> Foundation 0.1 ya existente. No rediseña el contenido profundo de cada pantalla — eso es de
> sprints posteriores.
>
> **Diseño D1.1 (hotfix):** el build de este sprint tenía un defecto bloqueante — crash inmediato
> al navegar a cualquier sección distinta de Inicio. Corregido sin cambios visuales; ver
> "Hotfix D1.1 — crash de navegación" más abajo y
> `artifacts\Kohana-Design-D1.1-Navigation-Hotfix-Informe.md` para el detalle completo.
>
> **Estado (2026-07-25): APROBADO por el usuario e integrado en `release/kohana-1.0-rc`.** El smoke
> test manual confirmó arranque correcto, navegación por las nueve secciones sin crash y Engine
> Registry mostrando correctamente el motor recomendado/configurado. Queda **un defecto visual
> abierto** en los iconos seleccionados de navegación (ver más abajo), asignado a Diseño D2.

## Dirección visual: Sakura Nocturna

Grafito oscuro como base, superficies ligeramente elevadas, texto claro cálido, y un acento
Sakura apagado que actúa como firma — nunca como fondo dominante. La identidad busca sentirse
nativa de Windows, tecnológica y serena, no infantil ni "kawaii", no genérica de dashboard web,
no una copia de Discord/Spotify/Copilot/ChatGPT. Sin pétalos decorativos flotando sin función.

## Principios aplicados en este sprint

- **Un color con propósito, no decoración.** El acento Sakura (`BrushAccent`) aparece solo donde
  comunica algo: el indicador de sección activa, el ícono relleno de la selección, el badge del
  botón de expandir/contraer, y los anillos de foco. En ningún otro lugar del shell.
- **Menos capas, más claridad.** Se eliminaron dos elementos puramente decorativos: las dos
  elipses con `BlurEffect` (glows de fondo) y el degradado interno redundante que se superponía
  al fondo del `ShellBorder`. Ninguno comunicaba información ni estado; ambos costaban GPU sin
  aportar legibilidad. El borde del shell pasó de un degradado de tres colores a un borde sólido
  (`BrushBorder`) con una sombra sutil — más cercano a cómo Windows 11 trata las superficies
  Fluent, y más "sobrio" que "recargado".
- **Estado nunca solo por color.** La sección activa de la barra lateral se comunica con cuatro
  señales simultáneas: superficie elevada (fondo `BrushAccentSoft`), un indicador vertical tipo
  "tallo" a la izquierda del ícono, el ícono cambia de solo-trazo a relleno, y la etiqueta gana
  peso de fuente. Cualquier persona que no perciba diferencias de color todavía puede identificar
  la sección activa por forma y posición.

## Decisiones de color

Un solo color nuevo: `ColorSidebarSurface` (`#10131C`) / `BrushSidebarSurface`, para que la barra
lateral tenga un fondo perceptiblemente distinto del `BrushSurface` que usa la tarjeta de
contenido — el resto de la paleta (`BrushBackground`, `BrushSurface`, `BrushBorder`,
`BrushAccent`, `BrushAccentSoft`, `BrushAccentBorder`, `BrushFocusRing`) ya existía en la
Fundación 0.1 y se reutiliza tal cual. `MainWindow.xaml` terminó el sprint sin ningún color
hexadecimal literal — verificado por prueba (`MainWindow_ContainsNoLiteralHexColors`).

## Jerarquía

`SakuraPageTitleStyle` (usa `FontSizeDisplay`, 22, SemiBold) para el título de página en el
encabezado del shell; `SakuraPageSubtitleStyle` (usa `FontSizeXSmall`, 11.5) para la descripción
breve debajo. Ambos ya existían como valores literales en `WorkspaceTitleText`/
`WorkspaceSubtitleText`; ahora son estilos nombrados y reutilizables.

## Navegación

La barra lateral conserva exactamente las nueve entradas y su orden funcional
(`Home → Assistant → Tasks → Focus → Routines → Audio → Capture → System → Settings`,
verificado por prueba contra `ShellNavigationPolicy.KnownDestinations`) y todos los `Tag`/`Click`
existentes — el rediseño es puramente visual. Cada botón usa ahora `SakuraNavigationItemStyle`
(antes `SideNavButtonStyle`, local a `MainWindow.xaml`) y expone `AutomationProperties.Name` con
el nombre de la sección en español, además del `ToolTip` que ya existía.

## Marca

El área superior de la barra lateral conserva el nombre "Kohana" y el descriptor "Tu Windows, en
flor" (visibles solo cuando la navegación está expandida, igual que antes), dentro del botón de
expandir/contraer. El badge cuadrado que envuelve el ícono de panel pasó de `BrushSurfaceRaised` a
`BrushAccentSoft` con borde `BrushAccentBorder` — un toque cálido de marca en el único punto fijo
de la barra lateral, sin necesidad de un logo nuevo. El símbolo floral vectorial completo
(`KohanaFlowerMarkStyle`, cinco pétalos) ya existía en `Brand.xaml` desde la Fundación 0.1 y sigue
sin usarse en el shell — este sprint no crea el ícono final de instalador ni reemplaza binarios
de icono, según el alcance definido.

## Estados

Hover, pressed, disabled y focus siguen viviendo en `NavButtonStyle`/`SakuraNavigationItemStyle` y
`SakuraSidebarToggleStyle` como triggers de `ControlTemplate` (no como animaciones): fondo en
hover, opacidad reducida en pressed/disabled, y ahora `BrushFocusRing` (en vez de `BrushAccent`)
para el borde de foco por teclado — el token existía en la Fundación 0.1 pero no se consumía en
ningún control todavía. El estado seleccionado (ver "Principios") es independiente de estos
cuatro y se aplica desde `ApplyNavigationItemState` en el code-behind.

## Hotfix D1.1 — crash de navegación

El build de este sprint se cerraba de inmediato al seleccionar cualquier sección distinta de
Inicio — bloqueante total, reportado por el usuario tras probar el ZIP de Diseño D1. La causa no
era la transición ni el `ContentControl` de navegación: `BrushFocusRing` (mencionado arriba, en
"Estados") vivía en `Themes/Brushes.xaml` referenciando por `StaticResource` la clave
`ColorAccentBorder`, definida en `Themes/Colors.xaml` — un archivo distinto. Esa referencia cruzada
entre diccionarios de tema es frágil en WPF: solo se resuelve de forma fiable cuando ambas claves
están en el mismo archivo. `BrushFocusRing` existía desde la Fundación 0.1 sin ningún consumidor
real (ver nota original más abajo), así que el fallo quedó latente hasta que este mismo sprint lo
conectó al disparador `IsKeyboardFocused` de `SakuraNavigationItemStyle`/`SakuraSidebarToggleStyle`
— el primer consumidor real, expuesto en cuanto un control recién insertado en el árbol visual
recibía el foco, es decir, en cada cambio de sección.

Corrección (Diseño D1.1): `BrushFocusRing` (junto con `BrushTextMuted` y `BrushError`, con el mismo
patrón de referencia cruzada) se movieron a `Colors.xaml`, junto a los `Color*` que referencian, de
forma que la resolución sea siempre dentro del mismo archivo. Ningún cambio visual: el mismo color,
el mismo token, el mismo consumidor — solo cambió en qué archivo vive la definición. Detalle de
causa raíz, evidencia (stack trace del registro de eventos de Windows) y pruebas de regresión WPF
reales añadidas en `docs/stable-release/IMPLEMENTATION_LOG.md` (sección "Diseño D1.1") y en
`artifacts\Kohana-Design-D1.1-Navigation-Hotfix-Informe.md`.

## Movimiento

Dos transiciones, ambas ya existentes antes de este sprint, ahora usando tokens en vez de
literales:

- Cambio de sección: opacidad + deslizamiento de 14px, `MotionFast` (120 ms), `MotionEaseOut`.
- Expandir/contraer sidebar: ancho, `MotionBase` (200 ms), `MotionEaseOut`.

Ambas respetan `_preferences.AnimationsEnabled` **y** `SystemParameters.ClientAreaAnimation` (la
preferencia de accesibilidad de Windows) a través de la propiedad `ShellAnimationsAllowed` — antes
solo se consultaba la preferencia propia de Kohana. Si cualquiera de las dos está desactivada, el
cambio se aplica de inmediato, sin transición. Ninguna animación usa `RepeatBehavior` (nada en
bucle) ni excede 300 ms.

## Accesibilidad

`AutomationProperties.Name` en los nueve botones de navegación, el botón de expandir/contraer, el
botón de paleta de comandos y el indicador de estado activo. Los tooltips en modo contraído ya
existían y se conservaron. El foco visible ahora usa el token semántico `BrushFocusRing`. El
orden de tabulación no cambió (sigue el orden de declaración en el XAML, igual que antes). No se
realizó todavía una auditoría completa WCAG de todas las vistas — ese alcance es explícitamente de
un sprint posterior; este cubre shell y navegación únicamente.

## Rendimiento

Eliminar las dos elipses con `BlurEffect` y el degradado interno redundante reduce el costo de
composición del shell (menos superficies con efectos, menos overdraw) sin ningún cambio funcional
— una simplificación, no una regresión medible añadida.

## Recursos creados

**`Colors.xaml`:** `ColorSidebarSurface`, `BrushSidebarSurface`.
**`Spacing.xaml`:** `RadiusShell`, `SidebarWidthCollapsed`, `SidebarWidthExpanded`,
`SidebarButtonWidthCollapsed`, `SidebarButtonWidthExpanded`, `NavigationIconSize`,
`NavigationIndicatorWidth`.
**`Controls.xaml`:** `SakuraNavigationIconStyle`, `SakuraNavigationIconFilledStyle`,
`SakuraNavigationItemStyle`, `SakuraSidebarToggleStyle`, `SakuraPageTitleStyle`,
`SakuraPageSubtitleStyle`, `SakuraShellCardStyle`.

Ningún archivo de tema nuevo: todo se agregó a los diccionarios existentes, así que
`DesignSystemResourceTests` (merge order, unicidad de claves, referencias válidas, estilos
principales preservados) sigue cubriendo estas adiciones sin cambios en esa prueba.

## Elementos no rediseñados en este sprint

- Contenido interno de Settings, System, Assistant, Tasks, Focus, Routines, Audio y Capture (solo
  se quitó el título duplicado de cada una; el resto de cada vista es idéntico).
- El símbolo floral completo (`KohanaFlowerMarkStyle`) no se aplicó en ningún lugar todavía.
- Iconografía floral por módulo (la idea de "flor abierta" para Inicio, "burbuja con pétalo" para
  Chat, etc., descrita en `SAKURA_BRAND_FOUNDATION.md`) — los íconos de línea actuales de
  `Brand.xaml` se conservan sin cambios.
- Modo claro / alto contraste.
- Arrastre de ventana (`DragMove`) — el shell sigue sin esa capacidad; no se añadió.
- Icono final de instalador ni binarios de marca.

## Limitaciones conocidas

- Hover no tiene una transición de opacidad animada (100–150 ms sugeridos por el sprint); los
  triggers de `ControlTemplate` cambian instantáneamente, igual que antes de este sprint. Añadir
  una transición suave de hover requeriría convertir esos triggers en `Storyboard`s con
  `EnterActions`/`ExitActions`, fuera del alcance de este sprint.
- La distinción dedicada/integrada de motores no aplica aquí (pertenece a Fase 2.2); se menciona
  solo para evitar confusión con el uso de "tokens" en ambos sprints.
- El ancho de la barra lateral se tokenizó con los mismos valores literales que ya tenía
  (68/194 px) para minimizar el riesgo de recorte de texto o desalineación; no se verificó
  exhaustivamente en escalado de texto 125/150/200% más allá de que los estilos usan `FontSize`
  en unidades independientes del dispositivo (comportamiento heredado, no nuevo de este sprint).

## Defecto visual abierto — iconos de navegación seleccionados

Detectado por el usuario durante el smoke manual de D1.1 (2026-07-25), **posterior** a la
implementación de este sprint y distinto del crash ya corregido:

Al seleccionar casi cualquier sección, el contenedor recibe correctamente su fondo/acento rosa, pero
el icono interior **pierde su silueta de línea reconocible** y aparece como un pequeño bloque o
cuadrado sólido magenta, con apenas una marca visible dentro. El icono no seleccionado sí conserva
una silueta clara. No produce crash ni impide navegar.

Este sprint introdujo `SakuraNavigationIconStyle` y `SakuraNavigationIconFilledStyle` junto con el
cambio de "solo trazo → relleno" como una de las cuatro señales del estado activo (ver "Principios"
arriba). La intención era que el icono activo se rellenara **conservando su forma**; el resultado
observado no cumple ese requisito. **No es comportamiento intencional.**

Queda abierto como **primer defecto de Diseño D2** (tarea D2.0). El requisito de aceptación es que
el icono seleccionado conserve la misma identidad visual que el no seleccionado: puede cambiar
color, grosor o variante outline/filled, pero debe seguir siendo inequívocamente reconocible,
centrado, de tamaño consistente, sin recorte, y sin alterar el tamaño del botón ni la posición del
texto. Detalle y corrección en `docs/design/SAKURA_COMMAND_CENTER_V2.md`.

## Plan del sprint D2 (sugerido, no iniciado)

1. Aplicar `KohanaFlowerMarkStyle` en un punto real del shell (posible candidato: el estado vacío
   del Asistente, que ya lo usa) y evaluar si el badge de marca de la barra lateral debería
   evolucionar hacia el símbolo completo en vez del ícono de panel actual.
2. Transiciones de hover suaves (100–150 ms) vía `Storyboard` en los estilos de navegación.
3. Rediseño de contenido de al menos una vista (candidato: Inicio, que no tiene título propio que
   migrar y es la pantalla de entrada).
4. Iconografía floral distintiva por módulo, según `SAKURA_BRAND_FOUNDATION.md` §"Sistema de
   iconos".
5. Modo claro / alto contraste sobre los mismos tokens semánticos.
6. Auditoría de accesibilidad dedicada (WCAG) sobre el resto de las vistas.
