# Sakura Design System — Foundation 0.1

> **Estado:** Fundación técnica. **No** es un rediseño ni una aprobación visual. Establece el
> vocabulario reutilizable (tokens y estilos base) preservando la apariencia actual. La revisión
> visual y el rediseño de pantallas son de un sprint posterior.

## 1. Principios

- **Nativo de Windows, no infantil.** Elegante, sobrio, técnico y cálido a la vez.
- **Modo oscuro como experiencia principal**, con preparación para modo claro y alto contraste.
- **Orgánico + tecnológico.** La identidad Sakura aporta calidez floral sin recargar; la base es
  gráfica y funcional.
- **Sutileza.** Animaciones y detalles discretos; legibilidad y jerarquía primero.
- **Semántica antes que literales.** Los componentes consumen tokens con significado
  (`BrushSurface`, `RadiusMedium`, `FontSizeBody`), no valores sueltos.

## 2. Identidad Sakura

- Marca definida en `Themes/Brand.xaml` (`SakuraFlowerMarkStyle`): el icono real de la aplicación,
  **cuatro pétalos** rosa `#F5B3CF` y un destello de cuatro puntas `#AC333F`. Los colores de la
  marca son fijos y no siguen al acento. *(Corregido en septiembre de 2026: esta sección decía
  «cinco pétalos con núcleo azul», la flor que la app enseñó por error hasta la 0.28.)*
- Paleta: grafito neutro para superficies + acento floral contenido (`#C4889A`) y un azul frío de
  apoyo (`#6B9CC8`). El acento nunca domina; puntúa.
- Iconografía de módulos: líneas sobrias, funcionales primero, con margen para detalles florales
  futuros (ver §7).

## 3. Tokens

Todos viven en `src/Nexo.App/Themes/` y se fusionan vía `ThemeResources.xaml` (único punto que
`App.xaml` referencia). Orden de fusión: Colors → Typography → Spacing → Motion → Brushes → Brand →
Controls (Colors antes que Brushes/Controls, que los consumen).

### 3.1 Color y brushes semánticos (`Colors.xaml`, `Brushes.xaml`)

| Semántica | Recurso | Valor |
|---|---|---|
| Fondo de app | `BrushBackground` | `#0C0E14` |
| Superficie | `BrushSurface` | `#111528` |
| Superficie elevada | `BrushSurfaceRaised` | `#151922` |
| Superficie hover | `BrushSurfaceHover` | `#202630` |
| Campo de entrada | `BrushInput` | `#0F131B` |
| Borde | `BrushBorder` | `#2A303A` |
| Texto primario | `BrushTextPrimary` | `#DFE1EA` |
| Texto secundario | `BrushTextSecondary` | `#AAAAB4` |
| Texto atenuado | `BrushTextMuted` → `ColorTextTertiary` | `#747986` |
| Acento | `BrushAccent` | `#C4889A` |
| Acento suave | `BrushAccentSoft` | `#2C2027` |
| Borde de acento | `BrushAccentBorder` | `#80C4889A` |
| Azul frío | `BrushCoolBlue` | `#6B9CC8` |
| Éxito | `BrushSuccess` | `#71CFA8` |
| Advertencia | `BrushWarning` | `#DDBE72` |
| Error | `BrushError` → `ColorDanger` | `#DF7E8A` |
| Anillo de foco | `BrushFocusRing` → `ColorAccentBorder` | `#80C4889A` |

`BrushTextMuted`, `BrushError` y `BrushFocusRing` son **alias semánticos** nuevos que reutilizan los
`Color*` existentes; no introducen paleta nueva. Se mantienen también los nombres previos
(`BrushTextTertiary`, `BrushDanger`) para no romper las vistas actuales.

### 3.2 Tipografía (`Typography.xaml`)

- `FontFamilyPrimary` = `Segoe UI Variable Text, Segoe UI` (el default real del shell).
- Escala (Double) que refleja los `FontSize` en uso hoy, para migración sin cambio visual:
  `FontSizeMicro 10.5` · `FontSizeXSmall 11.5` · `FontSizeSmall 12` · `FontSizeCaption 12.5` ·
  `FontSizeBase 13` · `FontSizeBody 14` · `FontSizeBodyLarge 15` · `FontSizeSubtitle 16` ·
  `FontSizeTitle 18` · `FontSizeTitleLarge 19` · `FontSizeDisplay 22`.

### 3.3 Espaciado y radios (`Spacing.xaml`)

- Espaciado (Double): `Space2/4/6/8/10/12/16/20/24`.
- Radios (CornerRadius): `RadiusSmall 9` · `RadiusMedium 12` · `RadiusLarge 13` · `RadiusPill 20`,
  los radios más repetidos en los estilos actuales.

### 3.4 Motion (`Motion.xaml`)

- Duraciones: `MotionInstant 0` · `MotionFast 0.12s` · `MotionBase 0.2s` · `MotionSlow 0.35s`.
- Curvas: `MotionEaseOut` (Cubic), `MotionEaseInOut` (Cubic), `MotionEaseOutSoft` (Quadratic).

## 4. Componentes

`Themes/Controls.xaml` ya define los estilos base y se conservan: `PrimaryButtonStyle`,
`SecondaryButtonStyle`, `NavButtonStyle`, `IconButtonStyle`, `CardStyle`, `SubtleCardStyle`,
`PromptTextBoxStyle`, `CompactTextBoxStyle`, `SectionTitleStyle`, `MutedTextStyle`, más estilos
`TargetType` para `TextBox`, `ComboBox`, `CheckBox`, `ListBox`, `Slider`, `ScrollBar`, `ToolTip`,
`Calendar`/`DatePicker`, `Window` y `UserControl`.

Estados cubiertos hoy por trigger en los botones/campos: `IsMouseOver` (hover), `IsPressed`
(pressed), `IsEnabled=False` (disabled), `IsKeyboardFocused` (borde de acento). El foco visual del
sistema está desactivado (`FocusVisualStyle = {x:Null}`) — ver §6.

En esta fundación, la adopción de tokens se aplicó a los radios de los estilos compartidos de
`Controls.xaml` (icon/nav/card → `RadiusMedium/Large/Small` vía `DynamicResource`), de forma
value-identical. El resto de literales se migrará de forma incremental y verificable.

## 5. Iconografía (dirección futura)

`Brand.xaml` contiene el set de iconos de módulos (`IconSakuraHome`, `…Assistant`, `…Tasks`,
`…Focus`, `…Routines`, `…Audio`, `…Capture`, `…System`, `…Settings`) y de acciones
(`IconSakuraMic`, `…Send`, `…Plus`, `…Look`, `IconCommandPalette`, `IconSidebarPanel`). Dirección:
conservar la geometría funcional y, más adelante, introducir detalles florales sutiles y un logo
final. **No** se reemplazan iconos en esta fundación.

## 6. Accesibilidad

- **Anillo de foco:** se define `BrushFocusRing` como token para el trabajo futuro. Hoy los
  controles conservan `FocusVisualStyle = {x:Null}` (sin cambio visual); reactivar un anillo de
  foco coherente en todos los controles es parte del sprint de accesibilidad.
- **AutomationProperties:** cobertura mínima actual. Esta fundación no las amplía de forma masiva;
  solo se añadirían correcciones locales evidentes. La accesibilidad completa (Narrator, contraste,
  navegación por teclado) es un sprint dedicado (criterio de salida del release).
- **Modo claro / alto contraste:** los tokens semánticos son el sustrato para definir variantes de
  paleta sin tocar los componentes; aún **no** implementados.

## 7. Lo que todavía NO se implementó

- Rediseño de pantallas o nuevo layout.
- Reemplazo de iconos o logo final.
- Animaciones complejas o coreografías de transición.
- Migración completa de literales (colores con alfa, tamaños, espaciados) a tokens en todas las
  vistas.
- Modo claro y alto contraste completos.
- Set ampliado de `AutomationProperties` y anillo de foco activo.

## 8. Plan del próximo sprint visual

1. Migrar de forma verificable el resto de literales duplicados (FontSize, espaciados, radios) de
   las vistas a los tokens, en lotes pequeños con verificación de carga.
2. Definir y aplicar un **anillo de foco** coherente (`BrushFocusRing`) en todos los controles
   interactivos.
3. Introducir variantes de paleta para **modo claro** y **alto contraste** sobre los mismos tokens.
4. Detalles florales sutiles en iconografía y un **logo** final.
5. Motion: aplicar `MotionFast/Base` y las curvas a hover/navegación de forma consistente.
6. Sprint dedicado de **accesibilidad** (AutomationProperties, teclado, Narrator, contraste).

> La aprobación visual requiere revisión humana; esta fundación **no** la declara aprobada.
