namespace Nexo.Core.Shell;

/// <summary>Un atajo: las teclas, separadas por « + », y lo que hace.</summary>
public sealed record CheatItem(string Keys, string Action)
{
    public IReadOnlyList<string> KeyParts { get; } =
        Keys.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Lo que anuncia el lector de pantalla al recorrer la lista.</summary>
    public override string ToString() => $"{Keys}: {Action}";
}

public sealed record CheatGroup(string Title, IReadOnlyList<CheatItem> Items)
{
    public override string ToString() => Title;
}

/// <summary>
/// La pestaña «Cheats» del panel superior (Adler, 2026-09-14): antes se llamaba «Voz» y solo tenía el
/// estado de la escucha y dos atajos. Ahora junta todos los atajos de Sakura, unos cuantos de Windows
/// que casi nadie conoce y que ahorran mucho, y las frases que se le pueden decir.
///
/// **Los de Sakura son los que registra de verdad** <c>MainWindow.Window_SourceInitialized</c>; los de Windows,
/// los que trae Windows 11 de fábrica. Una combinación mal anunciada en una chuleta es peor que no
/// tenerla: se prueba, no hace nada y ya no se vuelve a mirar la lista.
/// </summary>
public static class CheatSheet
{
    public static IReadOnlyList<CheatGroup> Build(bool dictationEnabled)
    {
        var voiceAndScreen = new List<CheatItem>
        {
            new("Alt + V", "Hablarle a Sakura"),
            new("Ctrl + Shift + Espacio", "Explicar la ventana que tienes delante"),
            new("Ctrl + Shift + T", "Traducir un trozo de pantalla")
        };

        // El dictado se puede apagar en Personalizar; entonces el atajo no está registrado.
        if (dictationEnabled)
        {
            voiceAndScreen.Insert(1, new CheatItem("Ctrl + Shift + D", "Dictar en cualquier aplicación"));
        }

        return
        [
            new("Sakura",
            [
                new("Alt + A", "Abrir o recoger Sakura"),
                new("Ctrl + Espacio", "Preguntar desde cualquier aplicación"),
                new("Alt + Shift + A", "Vistazo rápido al equipo"),
                new("Ctrl + K", "Centro de comandos, con Sakura abierta"),
                new("Esc", "Recoger Sakura")
            ]),
            new("Voz y pantalla", voiceAndScreen),
            new("Windows",
            [
                new("Win + V", "Historial del portapapeles"),
                new("Win + Shift + S", "Recortar la pantalla"),
                new("Win + .", "Emojis y símbolos"),
                new("Win + D", "Mostrar el escritorio"),
                new("Win + L", "Bloquear el equipo"),
                new("Ctrl + Shift + Esc", "Administrador de tareas")
            ])
        ];
    }
}
