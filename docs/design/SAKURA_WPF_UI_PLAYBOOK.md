# Manual de interfaz de Sakura — WPF sobre Windows 11

> Documento **para el agente**, no para el usuario. Se lee antes de tocar cualquier XAML, animación
> o binding de esta aplicación. Existe porque las reglas de UI que circulan por ahí están escritas
> para web y móvil, y aplicarlas de memoria a WPF produce consejos que aquí no significan nada
> ("usa `transform` en vez de `width`") o que son directamente falsos ("WPF recorta el radio al que
> quepa" — no lo hace, y esa suposición produjo píldoras puntiagudas durante meses).
>
> Todo lo que hay aquí está comprobado en este repositorio o medido en el equipo de Adler.

## 0. Lo que no se negocia

1. **Cero dependencias que no sean MIT/Apache/BSD.** Decisión de producto (`PRODUCT_VISION` §C).
2. **Todo se empaqueta.** La aplicación es autocontenida; no hay CDN ni descarga en caliente de nada
   que no sea un modelo de voz declarado.
3. **Nada bloquea el hilo de interfaz.** Ni DDC/CI, ni disco, ni IA, ni OCR.
4. **Si no se ha visto en pantalla, no está terminado.** Este proyecto lleva tres defectos graves
   encontrados solo al mirar: acrílico aplicado e invisible, píldoras puntiagudas y un bucle de
   Dispatcher al 32% de un núcleo. Ninguno lo vio una prueba en verde.

---

## 1. Librerías compatibles con este stack

TFM: `net10.0-windows10.0.26100.0`, WPF, C# 13. Nada de WinUI 3 (otro modelo de ventanas), nada de
MAUI, nada de Avalonia.

### Adoptadas o recomendadas

| Paquete | Licencia | Para qué | Estado |
|---|---|---|---|
| `CommunityToolkit.Mvvm` | MIT | `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `IAsyncRelayCommand` con generadores de código | **Recomendado adoptar.** Es la salida real del God Object |
| `Microsoft.Xaml.Behaviors.Wpf` | MIT | Comportamientos y triggers sin code-behind (arrastre, puntero, foco) | **Recomendado** cuando el code-behind solo existe para enganchar un evento |
| `SkiaSharp` + `SkiaSharp.Views.WPF` | MIT | Dibujo por fotograma a 60 fps sin pasar por el árbol visual de WPF | **Solo** para el visualizador de voz y espectros. No para UI normal |
| `NAudio` | MIT | WASAPI loopback, captura de micrófono, mezclador | Ya integrado |
| `Whisper.net`, `Vosk` | MIT / Apache-2.0 | Transcripción y palabra de activación | Ya integrado |
| `Microsoft.Extensions.DependencyInjection` | MIT | Composition root | Ya integrado |

### Evaluadas y descartadas

| Paquete | Por qué no |
|---|---|
| **WPF-UI (lepo.co)** | Trae su propio `FluentWindow`, backdrop y tema. Sakura ya resuelve DWM (D62) y tiene lenguaje visual propio; adoptarlo sería pelearse con dos sistemas de tema a la vez |
| **ModernWpf** | Sin mantenimiento activo desde 2022 |
| **MahApps.Metro** | Estética Metro, no Windows 11; pesado |
| **LiveCharts2** | Solo si aparecen gráficas de verdad. Hoy los medidores son `Path` propios y eso basta |
| **MaterialDesignInXAML** | Material 2, choca de frente con la referencia Caelestia |

**Regla:** una librería de UI nueva tiene que justificar por qué no basta con un `ControlTemplate`.
En WPF casi siempre basta.

---

## 2. Separación MVVM en ESTE código

El problema real no es la teoría, es `MainWindow.xaml.cs`: **más de 9.000 líneas** que mezclan
navegación, IA, voz, visión, permisos, temporizadores y presentación. Es la limitación L2, abierta
desde julio.

### Reglas de corte

1. **Nexo.Core no conoce WPF.** Ni `System.Windows`, ni `Dispatcher`, ni `Brush`. Hay pruebas que lo
   verifican leyendo el archivo. Si una política necesita un color, devuelve `RgbColor`, no
   `SolidColorBrush`.
2. **Nexo.Windows conoce Win32 y no conoce la vista.** Nada de `MainWindow` ni de
   `System.Windows.Controls` ahí dentro.
3. **La vista no decide.** Un `*.xaml.cs` puede enganchar eventos, animar, medir y llamar a un
   coordinador. No puede tomar decisiones de producto, parsear texto ni hablar con hardware.
4. **Lo que se pueda probar sin abrir una ventana, se prueba sin abrir una ventana.** Ese es el
   criterio para decidir si algo va a Core: no "es lógica", sino "¿puedo comprobarlo con xUnit sin un
   `Dispatcher`?".

### Cómo salir del God Object sin romper nada

No se reescribe de golpe. Se extrae por capacidad, con caracterización antes:

```
1. Elegir UNA capacidad (enfoque, audio, memoria...).
2. Escribir pruebas que fijen el comportamiento ACTUAL desde fuera.
3. Crear <Capacidad>ViewModel : ObservableObject en Nexo.App/ViewModels/.
4. Mover estado y comandos. La vista se queda con el binding.
5. Las pruebas siguen en verde o el cambio no se acepta.
```

`CommunityToolkit.Mvvm` reduce eso a un atributo:

```csharp
public sealed partial class FocusViewModel : ObservableObject
{
    [ObservableProperty] private TimeSpan _remaining;
    [ObservableProperty] private bool _isRunning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync(CancellationToken token) { /* ... */ }

    private bool CanStart() => !IsRunning;
}
```

`[RelayCommand]` sobre un `Task` genera un `IAsyncRelayCommand` que **ya trae** estado de ejecución,
deshabilitado mientras corre y cancelación. Es justo lo que hoy se hace a mano con banderas booleanas
repartidas por el code-behind.

---

## 3. Validar XAML complejo

WPF falla en tiempo de ejecución donde otros stacks fallan al compilar. Las cuatro redes que este
repositorio ya tiene, y que hay que seguir usando:

### 3.1 Recursos que no existen

`{StaticResource X}` con `X` inexistente **tumba la aplicación al arrancar**; `{DynamicResource X}`
falla en silencio y deja el control sin estilo. Hay pruebas que cargan los diccionarios de verdad y
comprueban cada clave.

> **Regla:** cualquier clave nueva en `Themes/` entra acompañada de su comprobación. Renombrar una
> clave sin actualizar sus usos es un fallo de ejecución que ninguna compilación ve.

### 3.2 Pruebas con WPF de verdad

`StaWpfFixture` levanta un `Dispatcher` real en un hilo STA. Sirve para lo que el análisis de texto no
puede ver:

- que un estilo **resuelve y maqueta** (`Measure`/`Arrange`/`UpdateLayout` sin excepción);
- que un control no encola trabajo infinito (`Dispatcher.Hooks.OperationPosted` contado — así se cazó
  el bucle de D57);
- que una propiedad adjunta hace lo que promete (`PillCornerTests`).

### 3.3 Barridos sobre el XAML como texto

Baratos y sorprendentemente eficaces:

- ningún control interactivo sin `AutomationProperties.Name`;
- ninguna ventana vuelve a declarar `AllowsTransparency` salvo las dos justificadas;
- ningún literal de color fuera de `Themes/`.

### 3.4 Trazas de binding

Un binding roto no se ve; se escribe en la ventana de salida.

```xml
<TextBlock Text="{Binding Nombre, diagnostics:PresentationTraceSources.TraceLevel=High}" />
```

**Nunca dejarlo puesto.** En depuración, subir el nivel global una sesión cuando algo "no aparece":

```csharp
PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
```

### 3.5 Trampas de WPF que ya nos han mordido

| Trampa | Qué pasa | Cómo se evita |
|---|---|---|
| `CornerRadius` mayor que la mitad del alto | **No se recorta**: dibuja los arcos pedidos y salen dos puntas (forma de lente) | `PillCorner.IsPill`, que mide el alto real |
| `ClipToBounds` sobre un borde redondeado | Recorta al **rectángulo**, ignora el radio | `Clip = new RectangleGeometry(rect, rx, ry)` |
| `{StaticResource}` a una clave definida después | Excepción al cargar | Definirla antes o usar `DynamicResource` |
| `x:Name` dentro de un `ControlTemplate` | No es accesible desde fuera | `GetTemplateChild` sobre la plantilla |
| Estilo implícito sin `x:Key` | Afecta a TODOS los controles de ese tipo | Usar claves salvo que la intención sea global |
| `Freeze()` olvidado | Cada `Brush`/`Geometry` no congelado se comprueba por hilo en cada acceso | Congelar todo lo estático |

---

## 4. Bindings asíncronos

WPF no sabe esperar un `Task`. Reglas:

1. **Nada de `async void`** salvo manejadores de evento, y ahí con `try/catch` que no deje escapar
   nada: una excepción en `async void` mata el proceso.
2. **La propiedad no devuelve `Task`.** El comando asíncrono escribe el resultado en una propiedad
   observable cuando termina.
3. **Estados explícitos**: `IsLoading`, `Error`, `Result`. Un binding a `null` mientras carga es
   indistinguible de un fallo.
4. **`ConfigureAwait(false)` en Core y Windows; nunca en la vista**, que sí necesita volver al hilo de
   interfaz.
5. **Cancelación siempre.** `IAsyncRelayCommand` la trae; a mano, un `CancellationTokenSource` por
   operación, cancelado al cerrar.
6. **Cuidado con `OperationCanceledException`**: un tiempo de espera agotado del cliente HTTP llega
   como cancelación. Excluirla de un `catch` deja la operación colgada para siempre — fue el fallo
   D63 que dejó Lens muerta dieciséis días.
7. **Marshalling explícito**: los callbacks de NAudio, WASAPI, Vosk o un temporizador de sistema
   llegan en OTRO hilo. Tocar un `DependencyObject` desde ahí lanza.

```csharp
// El evento de audio llega en el hilo de captura.
_capture.DataAvailable += (_, e) =>
{
    var nivel = CalcularNivel(e.Buffer, e.BytesRecorded);   // fuera del hilo de UI
    _ui.BeginInvoke(() => Halo.Nivel = nivel, DispatcherPriority.Render);
};
```

---

## 5. 60 fps en WPF: lo que cuesta de verdad

El presupuesto es **16,6 ms por fotograma**, y en WPF se va por sitios distintos que en web:

| Coste | Por qué | Qué hacer |
|---|---|---|
| `AllowsTransparency="True"` | Compone la ventana **por software**; la GPU no participa | Eliminado en D62 (DWM). No reintroducir salvo transparencia por píxel real |
| `DropShadowEffect`, `BlurEffect` | Se recalculan cuando cambia **cualquier cosa** dentro del elemento que los lleva | Sombra: DWM. Desenfoque: acrílico del sistema |
| Animar `Width`/`Height`/`Margin` | Dispara medida y organización en cada fotograma | Animar `RenderTransform` y `Opacity`: no tocan el layout |
| `DispatcherTimer` a 60 Hz | Compite con el render y no está sincronizado con el vsync | `CompositionTarget.Rendering` para lo que va por fotograma |
| Reencolar en el `Dispatcher` | Un reintento sin condición de salida es un bucle infinito (D57: 32% de un núcleo, sin excepción) | Nunca reintentar sin límite; dejar que `Loaded`/`SizeChanged` avisen |
| Brochas y geometrías sin congelar | Comprobación de afinidad de hilo en cada acceso | `.Freeze()` |
| Árbol visual profundo por fotograma | Cada `Border`/`Path` es un nodo que se recorre | Para visualizadores: `DrawingVisual`, `SkiaSharp`, o un `Path` con `StreamGeometry` |

### Medir, no suponer

```csharp
var tier = RenderCapability.Tier >> 16;   // 0 = software, 2 = GPU completa
```

```powershell
$p1 = (Get-Process Sakura).TotalProcessorTime; Start-Sleep 5
$p2 = (Get-Process Sakura).TotalProcessorTime
(($p2 - $p1).TotalMilliseconds / 5000 / [Environment]::ProcessorCount) * 100
```

Referencia medida el 2026-08-18 tras D62: **0,2 % en reposo con el shell abierto**. Cualquier cifra de
un dígito alto en reposo es un bucle escondido.

### Animación con sensación nativa

- Duración 150–300 ms para microinteracciones; la salida al 60–70 % de la entrada.
- Curvas: `CubicEase{EaseOut}` para entrar, `EaseIn` para salir. Para inercia, `BackEase` o
  `ElasticEase` con amplitud baja — el equivalente WPF a la física de resorte de Apple.
- **Interrumpibles**: `BeginAnimation(prop, null)` antes de arrancar la siguiente.
- **Nunca dejar algo invisible y clicable**: `IsHitTestVisible = false` al EMPEZAR la salida, no en
  `Completed` (D56/D57).
- Respetar `SystemParameters.ClientAreaAnimation` y el ajuste de movimiento reducido de Windows.
- **Toda superficie que entra tiene que saber salir.** Cada panel necesita su animación de salida con
  el mismo vocabulario que la de entrada; si una entra deslizándose y desaparece de golpe, se lee
  como un fallo. Es lo que hoy pasa con los controles rápidos frente al cajón superior.

---

## 6. El visualizador de voz (halo tipo Siri)

Al detectar la palabra de activación aparece la marca de Sakura flotando, con **anillos que se
expanden reaccionando al sonido**.

1. **Ventana propia**, sin activación (`WS_EX_NOACTIVATE`), fuera de la barra de tareas, siempre
   encima. Al no aceptar foco no interrumpe lo que la persona esté haciendo.
2. **Transparencia por píxel de verdad** (`AllowsTransparency="True"`) — excepción justificada a D62,
   igual que el resaltado de Lens: entre los anillos tiene que haber agujero, no fondo.
3. **Un solo elemento que dibuja**: `DrawingVisual` o `SKElement`. Nada de crear un `Ellipse` por
   anillo y por fotograma.
4. **Nivel de audio en Core, dibujo en la vista.** El cálculo (RMS, suavizado, umbral, caída) es
   política probable con pruebas; la vista interpola y pinta.
5. **Suavizado obligatorio**: ataque ~80 ms, caída ~350 ms. Sin filtro los anillos tiemblan en vez de
   respirar.
6. **Presupuesto**: 4–5 anillos vivos como mucho, emitidos cada ~180 ms mientras haya voz. El anillo
   que ya no se ve se destruye.
7. **Ciclo de vida atado al reconocimiento**: aparece con la palabra de activación, respira mientras
   escucha, se va con la respuesta. No queda flotando.

---

## 7. Lenguaje visual

- **Base plana y tonal.** Superficies que se distinguen por luminosidad (Material You / Caelestia).
  Liquid Glass está descartado desde el 2026-08-17.
- **De Apple se toma la ORGANIZACIÓN, no la estética**: jerarquía, agrupación, aire generoso, escalas
  semánticas de tipografía y espacio. Nada de translucidez decorativa.
- **Escalas** (`Themes/Spacing.xaml`): retícula de 8 px, radios 10/14/20/28, nueve niveles de texto
  nombrados por función. Un literal nuevo en una vista es una regresión.
- **Esquinas de ventana: rectas** (D69). DWM no admite radio a medida y el arco enseñaba el borde del
  sistema como una mancha.
- **Sakura encoge, no crece.** Es el agente de chat; Pendientes, Rutinas, Sistema y Captura salen del
  chat, no se apilan en un panel permanente.
- **Iconos**: geometrías propias en retícula de 24, trazo 1,65, todo dentro de 4..20. Nunca emoji.

---

## 8. Lista de comprobación antes de dar por hecho un cambio de UI

- [ ] Compila **sin advertencias** en Release.
- [ ] Suite completa en verde.
- [ ] Publicado en el equipo real y **fotografiado**, con zoom en la zona tocada.
- [ ] CPU en reposo medida después del cambio.
- [ ] Ningún control interactivo nuevo sin `AutomationProperties.Name`.
- [ ] Ninguna animación deja algo invisible y clicable.
- [ ] Toda superficie que entra tiene su animación de salida.
- [ ] Ningún `catch` nuevo se traga `OperationCanceledException` sin distinguir el cierre.
- [ ] Los literales nuevos de espacio y tipografía salen de los diccionarios, no del aire.
