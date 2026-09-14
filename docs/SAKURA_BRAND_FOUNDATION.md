# Sakura Brand Foundation

> Este documento se llamaba `KOHANA_BRAND_FOUNDATION.md` y describía la etapa Kohana. Se reescribió
> en septiembre de 2026 para que diga lo que es cierto hoy. La fuente de verdad de los nombres es
> `src/Nexo.Core/Branding/ProductIdentity.cs`; si este documento y ese archivo no coinciden, manda
> el código.

## Nombre

El producto se llama **Sakura**. Antes se llamó Kohana, y antes Nexo. Los dos nombres anteriores
siguen existiendo en el código solo donde hacen falta para no romper instalaciones que ya los
llevan escritos (carpetas de datos, ejecutable viejo, preferencias).

**No se vuelve a renombrar código.** Si algún día el nombre público lleva apellido, es solo texto
visible.

Lema:

> **Tu Windows, en flor.**

## Marca

**El símbolo es el icono de la aplicación**: cuatro pétalos rosa y un destello de cuatro puntas.

| Parte | Color |
| --- | --- |
| Pétalos | `#F5B3CF` |
| Destello | `#AC333F` |

- Dentro de la app vive en `src/Nexo.App/Themes/Brand.xaml` como `SakuraFlowerMarkStyle`, con los
  trazados del SVG de 48 tal cual, sin redibujar.
- **Los colores de la marca son fijos y no siguen al acento.** El acento puede venir del fondo de
  pantalla, y una marca que cambia de color con el fondo deja de ser una marca. Todo lo demás de la
  interfaz sí sigue al acento.

> **Corrección.** La versión anterior de este documento decía que el símbolo era una flor de cinco
> pétalos. Era falso: el ejecutable siempre tuvo el icono de cuatro, y durante meses seis ventanas
> enseñaron una flor que no era la suya. Los archivos `Sakura.svg`/`.png` de cinco pétalos y el
> documento de marca de la etapa Kohana quedan desmentidos.

## Nombres técnicos

| Qué | Hoy | Anteriores que se siguen reconociendo |
| --- | --- | --- |
| Ejecutable | `Sakura.exe` | `Kohana.exe` (el instalador lo retira) |
| Carpeta de datos | `%LocalAppData%\Sakura` | `Kohana`, `Nexo` |
| Repositorio | `EXOTARA/Sakura` | `EXOTARA/Nexo` (GitHub redirige) |
| Proyectos .NET | `Nexo.App`, `Nexo.Core`, `Nexo.Windows` | — |

Los proyectos siguen llamándose `Nexo.*` a propósito: renombrar namespaces no le da nada a quien usa
Sakura y arriesga todo lo demás.

### Datos de etapas anteriores

La migración (`LegacyDataMigrator`) copia, no mueve; no borra el origen ni sobrescribe lo que ya
existe; y recorre **una cadena** de nombres anteriores, no uno solo, porque los modelos de voz de
quien viene de la etapa Nexo siguen en esa carpeta.

## Palabra de activación

- `Oye Sakura` — recomendada.
- `Sakura` — rápida.
- `Hey Sakura` — alternativa.

Solo se ha comprobado por tabla léxica. Falta medirla con voz real, a distancia y con ruido.

## Dirección visual

Superficies planas y tonales que se distinguen por luminosidad; el acento puede venir del fondo de
pantalla. De Apple se toma solo la organización: jerarquía, agrupación, espacio y escalas semánticas
de tipo y espaciado.

Antes de tocar XAML, leer `docs/design/SAKURA_WPF_UI_PLAYBOOK.md`.
