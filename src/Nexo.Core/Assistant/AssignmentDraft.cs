using System.Globalization;
using System.Text;

namespace Nexo.Core.Assistant;

public enum AssignmentKind
{
    Exercises,
    Essay,
    Report,
    Research,
    Presentation,
    Generic
}

/// <summary>
/// 2026-09-16 — Borrador de tarea, fase 3: plantillas por tipo de trabajo (decidido con Adler en la
/// ronda de preguntas del Borrador).
///
/// Reglas que no se negocian:
/// <list type="bullet">
/// <item>Andamiar, no resolver. El borrador ordena el trabajo, plantea el procedimiento y deja
/// marcado lo que la persona tiene que hacer; no entrega resultados para copiar.</item>
/// <item>Sirve para cualquier tarea, no solo la de Adler: la plantilla sale del tipo de trabajo, no
/// de una materia.</item>
/// <item>Los datos personales (nombre, matrícula…) no se inventan: quedan como huecos visibles.</item>
/// </list>
/// El documento sale con la marca de IA de siempre (<see cref="Documents.DocumentDisclosure"/>).
/// </summary>
/// <summary>
/// 2026-09-16 — los datos de la portada que la persona confirma. Los personales (nombre, matrícula,
/// carrera, universidad) se pueden recordar; los de la actividad cambian cada vez y no se guardan.
/// Lo que quede vacío va como hueco visible, nunca inventado.
/// </summary>
public sealed record AssignmentDetails(
    string? Name = null,
    string? StudentId = null,
    string? Program = null,
    string? School = null,
    string? Subject = null,
    string? Teacher = null,
    string? Activity = null)
{
    public string ToPromptLines()
    {
        (string Label, string? Value)[] fields =
        [
            ("Nombre", Name), ("Matrícula", StudentId), ("Carrera", Program), ("Universidad", School),
            ("Asignatura", Subject), ("Profesor", Teacher), ("Actividad", Activity)
        ];

        var given = fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)).ToList();
        return given.Count == 0
            ? string.Empty
            : "\n- Datos confirmados para la portada (úsalos tal cual): " +
              string.Join("; ", given.Select(field => $"{field.Label}: {field.Value!.Trim()}")) + ".";
    }
}

public static partial class AssignmentDraft
{
    public static IReadOnlyList<AssignmentKind> All { get; } =
    [
        AssignmentKind.Exercises,
        AssignmentKind.Essay,
        AssignmentKind.Report,
        AssignmentKind.Research,
        AssignmentKind.Presentation,
        AssignmentKind.Generic
    ];

    public static string Label(AssignmentKind kind) => kind switch
    {
        AssignmentKind.Exercises => "Ejercicios o problemas",
        AssignmentKind.Essay => "Ensayo",
        AssignmentKind.Report => "Informe o práctica",
        AssignmentKind.Research => "Investigación o resumen",
        AssignmentKind.Presentation => "Mapa conceptual o presentación",
        _ => "Otro tipo de trabajo"
    };

    /// <summary>Las secciones del documento, en orden.</summary>
    public static IReadOnlyList<string> Sections(AssignmentKind kind) => kind switch
    {
        AssignmentKind.Exercises =>
        [
            "Portada", "Objetivos", "Introducción", "Planteamiento del problema", "Marco teórico",
            "Desarrollo por inciso", "Resultados", "Conclusiones", "Referencias", "Anexos"
        ],
        AssignmentKind.Essay =>
        [
            "Portada", "Introducción", "Desarrollo", "Contraargumentos", "Conclusión", "Referencias"
        ],
        AssignmentKind.Report =>
        [
            "Portada", "Objetivos", "Introducción", "Marco teórico", "Materiales y método",
            "Resultados", "Análisis de resultados", "Conclusiones", "Referencias", "Anexos"
        ],
        AssignmentKind.Research =>
        [
            "Portada", "Introducción", "Marco teórico", "Desarrollo del tema", "Conclusiones", "Referencias"
        ],
        AssignmentKind.Presentation =>
        [
            "Portada", "Idea central", "Conceptos principales", "Relaciones entre conceptos", "Cierre", "Referencias"
        ],
        _ =>
        [
            "Portada", "Introducción", "Desarrollo", "Conclusiones", "Referencias"
        ]
    };

    /// <summary>
    /// El tipo que Sakura propone según el texto de la tarea. Solo es una sugerencia: la persona elige
    /// (decidido con Adler: preguntar antes, con la opción de Sakura marcada).
    /// </summary>
    public static AssignmentKind Suggest(string? taskText)
    {
        var text = Fold(taskText ?? string.Empty);
        if (text.Length == 0)
        {
            return AssignmentKind.Generic;
        }

        (AssignmentKind Kind, string[] Words)[] signals =
        [
            (AssignmentKind.Presentation, ["mapa conceptual", "mapa mental", "presentacion", "diapositiva", "infografia", "cuadro sinoptico", "genially", "canva"]),
            (AssignmentKind.Report, ["practica", "laboratorio", "reporte de", "informe", "bitacora", "experimento"]),
            (AssignmentKind.Essay, ["ensayo", "opinion", "argumenta", "postura", "reflexion"]),
            (AssignmentKind.Exercises, ["ejercicio", "problema", "resuelve", "calcula", "determina", "encuentra el valor", "demuestra", "inciso", "metodo de", "biseccion", "derivada", "integral", "ecuacion"]),
            (AssignmentKind.Research, ["investiga", "investigacion", "resumen", "sintesis", "monografia", "describe", "explica"])
        ];

        var best = AssignmentKind.Generic;
        var bestScore = 0;
        foreach (var (kind, words) in signals)
        {
            var score = words.Count(word => text.Contains(word, StringComparison.Ordinal));
            if (score > bestScore)
            {
                best = kind;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// 2026-09-16 — fase 5: ¿es un examen? Solo cuenta con señales fuertes y juntas (decidido con
    /// Adler: mejor dejar pasar un examen dudoso que tratar una tarea como examen): la palabra
    /// examen o prueba, y algo que solo tiene un examen en curso (tiempo restante, «pregunta 3 de
    /// 10», enviar respuestas…).
    /// </summary>
    public static bool LooksLikeExam(string? text)
    {
        var folded = Fold(text ?? string.Empty);
        string[] exam = ["examen", "evaluacion parcial", "evaluacion final", "quiz", "cuestionario calificado", "prueba de"];
        string[] inProgress =
        [
            "tiempo restante", "minutos restantes", "tiempo limite", "temporizador", "enviar respuestas",
            "enviar examen", "terminar intento", "intento 1", "finalizar intento", "entregar examen",
            "preguntas sin responder", "calificacion automatica"
        ];

        var hasExam = exam.Any(word => folded.Contains(word, StringComparison.Ordinal));
        var hasProgress = inProgress.Any(word => folded.Contains(word, StringComparison.Ordinal)) ||
                          QuestionCounter().IsMatch(folded);
        return hasExam && hasProgress;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"pregunta\s+\d+\s+de\s+\d+")]
    private static partial System.Text.RegularExpressions.Regex QuestionCounter();

    /// <summary>
    /// Modo tutor (decidido con Adler): con un examen no se hace borrador; se explica cómo se llega a
    /// la respuesta con otro ejemplo, para aprender el método sin resolver las preguntas.
    /// </summary>
    public static string BuildTutorPrompt() =>
        "Lo que te compartí parece un examen, así que no respondas sus preguntas ni me des los resultados. " +
        "Explícame el método para resolver ese tipo de ejercicio paso a paso, usando un ejemplo distinto con otros datos, " +
        "y dime en qué fijarme para no equivocarme. Si hay fórmulas, escríbelas con $…$. " +
        "Al final, dame una pequeña guía de estudio de ese tema.";

    /// <summary>La petición que se manda al modelo, en la misma conversación donde está la tarea.</summary>
    public static string BuildPrompt(AssignmentKind kind, AssignmentDetails? details) =>
        BuildPrompt(kind) + (details?.ToPromptLines() ?? string.Empty);

    /// <summary>La petición que se manda al modelo, en la misma conversación donde está la tarea.</summary>
    public static string BuildPrompt(AssignmentKind kind)
    {
        var builder = new StringBuilder();
        builder.Append("Hazme el borrador de la tarea que te compartí en esta conversación, como ")
            .Append(Label(kind).ToLower(CultureInfo.GetCultureInfo("es-MX")))
            .Append(". Es una base para que yo lo termine, no el trabajo resuelto.\n\n");

        builder.Append("Usa exactamente estas secciones, cada una como título con ##, en este orden: ")
            .Append(string.Join(", ", Sections(kind)))
            .Append(".\n\n");

        builder.Append("Reglas:\n");
        builder.Append("- Portada: pon los datos que aparezcan en la tarea (asignatura, profesor, actividad). Los datos personales que no conozcas déjalos como [Nombre], [Matrícula], [Carrera] y [Universidad]; no los inventes.\n");
        builder.Append("- No entregues resultados para copiar. Plantea el procedimiento, explica qué hacer en cada paso y marca con [Completa aquí: …] lo que me toca a mí.\n");

        builder.Append(kind switch
        {
            AssignmentKind.Exercises =>
                "- En «Desarrollo por inciso» haz un apartado por inciso: datos, fórmula o método a usar y los pasos, sin el resultado final.\n" +
                "- Escribe las fórmulas con $…$ en línea y $$…$$ en su propio renglón.\n" +
                "- Si hacen falta iteraciones o datos en columnas, deja una tabla en Markdown con los encabezados y la primera fila como ejemplo.\n" +
                "- Si una gráfica ayuda, escribe en su propio renglón «[Gráfica de GeoGebra: qué debe mostrar]».\n",
            AssignmentKind.Essay =>
                "- Propón una tesis en la introducción y deja en el desarrollo los argumentos como ideas guía de dos o tres frases cada una, para que yo los redacte con mi voz.\n",
            AssignmentKind.Report =>
                "- En «Resultados» deja las tablas con sus encabezados y sin datos inventados; los datos son los que yo obtenga.\n" +
                "- Escribe las fórmulas con $…$ en línea y $$…$$ en su propio renglón.\n",
            AssignmentKind.Research =>
                "- En el desarrollo organiza el tema en subtítulos con los puntos que debo investigar y una idea de por dónde empezar en cada uno.\n",
            AssignmentKind.Presentation =>
                "- Organiza los conceptos como una jerarquía corta (tema, conceptos, relaciones) y sugiere una imagen con «Imagen: …» donde ayude.\n",
            _ =>
                "- Adapta el desarrollo a lo que pide la tarea, con subtítulos claros.\n"
        });

        builder.Append("- En «Marco teórico» o donde se citen ideas, di qué conceptos conviene respaldar con fuentes; Sakura añade fuentes reales al guardar el documento. No inventes referencias.\n");
        builder.Append("- Escribe en español, claro y sin relleno.");
        return builder.ToString();
    }

    private static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
