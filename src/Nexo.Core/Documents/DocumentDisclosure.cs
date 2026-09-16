namespace Nexo.Core.Documents;

/// <summary>
/// 2026-09-16 — la nota de uso de IA que llevan los documentos que escribe Sakura (Adler: «que sirva
/// como una base y no como una solución para burlar detectores… dejar una marca de agua aclarando el
/// uso de IA»).
///
/// Va en todos: en el pie de cada página del Word, en el pie de las hojas de Excel y en la última
/// diapositiva. Dice dos cosas, y las dos importan: que el texto se escribió con ayuda de IA —que es
/// lo que muchas escuelas piden declarar— y que es un borrador que hay que revisar antes de
/// entregarlo. No se puede quitar desde Sakura: una declaración con interruptor no declara nada.
/// Quien quiera cambiarla puede hacerlo en su propio documento, que es suyo.
/// </summary>
public static class DocumentDisclosure
{
    /// <summary>Para el pie de página, donde el sitio es poco.</summary>
    public const string Short = "Borrador elaborado con ayuda de IA en Sakura · Revísalo antes de entregarlo";

    /// <summary>Para una línea suelta al final, cuando hay sitio para explicarlo.</summary>
    public const string Long =
        "Este documento se elaboró con ayuda de inteligencia artificial en Sakura. " +
        "Es un borrador: revisa el contenido, comprueba los datos y las fuentes, y añade lo tuyo antes de entregarlo.";
}
