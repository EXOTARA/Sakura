using Nexo.Core.Storage;

namespace Nexo.Core.Focus;

public interface IFocusStore
{
    FocusState Load();

    void Save(FocusState state);

    /// <summary>
    /// Cómo salió la última lectura. Tiene valor por omisión para que los almacenes que no pueden
    /// fallar al leer (los de memoria) no tengan que declararlo.
    /// </summary>
    DataLoadOutcome LastLoad => DataLoadOutcome.Ok;
}
