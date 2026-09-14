namespace Nexo.Core.Distribution;

/// <summary>
/// Diseño D89 — por dónde llegó esta copia de Sakura.
///
/// Hasta ahora solo había una respuesta: el instalador o el zip de GitHub. La Microsoft Store añade
/// otra, y no es una diferencia de envoltorio. Dentro de un paquete MSIX Windows virtualiza las
/// escrituras en el registro del usuario, la carpeta del programa es de solo lectura, la Store
/// actualiza por su cuenta, y sus políticas prohíben cosas que la copia directa hace a propósito.
/// </summary>
public enum DistributionChannel
{
    /// <summary>Instalador de Inno o zip portable, descargados de GitHub.</summary>
    Direct,

    /// <summary>Paquete MSIX instalado desde Microsoft Store.</summary>
    MicrosoftStore
}
