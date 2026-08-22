namespace Nexo.Windows.Tests;

/// <summary>
/// Serializa las pruebas que mutan el override global de <c>NexoDataPaths</c>.
///
/// Es estado estático de proceso: quien lo cambia lo cambia para todos. xUnit ejecuta clases
/// distintas en paralelo, así que dos clases que lo tocan a la vez no fallan siempre — fallan a
/// veces, que es peor. Nexo.Core.Tests ya resolvió lo mismo con su propia colección.
///
/// Toda clase nueva de este ensamblado que llame a <c>UseExplicitDataRoot</c> tiene que entrar aquí.
/// </summary>
[CollectionDefinition(Name)]
public sealed class WindowsDataPathsCollection
{
    public const string Name = "WindowsDataPaths";
}
