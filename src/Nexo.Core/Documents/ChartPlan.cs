using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Nexo.Core.Assistant;

namespace Nexo.Core.Documents;

public enum ChartKind
{
    /// <summary>Columnas verticales: lo normal para comparar unas pocas cosas.</summary>
    Column,

    /// <summary>Barras horizontales: cuando las etiquetas son largas y no caben debajo.</summary>
    Bar,

    /// <summary>Líneas: cuando la primera columna es el paso del tiempo.</summary>
    Line,

    /// <summary>Pastel: cuando las cifras son las partes de un todo.</summary>
    Pie
}

/// <summary>Una serie de la gráfica: la columna de la tabla de la que sale y sus valores.</summary>
public sealed record ChartSeries(string Name, int Column, IReadOnlyList<double> Values);

/// <summary>Una gráfica: el tipo, las etiquetas (la primera columna) y las series (el resto).</summary>
public sealed record ChartSpec(ChartKind Kind, string CategoryTitle, IReadOnlyList<string> Categories, IReadOnlyList<ChartSeries> Series, bool IsPercent)
{
    /// <summary>Con una sola serie y pocas etiquetas, cada barra lleva su cifra: se lee sin mirar el eje.</summary>
    public bool ShowValues => Kind == ChartKind.Pie || (Series.Count == 1 && Categories.Count <= 10);
}

/// <summary>
/// 2026-09-15 — cuándo una tabla de la respuesta se dibuja como gráfica y de qué tipo (Adler: «con
/// gráficas y esas cosas»).
///
/// Solo es gráfica una tabla que de verdad lo es: la primera columna son etiquetas y todas las demás
/// son cifras de la misma clase (todo porcentajes o todo números, de un tamaño parecido). Una tabla de
/// «Métrica | Meta | Actual» con porcentajes y cantidades mezclados, o una comparación con texto, se
/// queda como tabla: dibujarla inventaría una relación que no existe.
///
/// El tipo sale primero de lo que dice el título («evolución», «distribución», «gráfica de barras…»)
/// y, si no dice nada, de los datos: fechas o años → líneas; porcentajes que suman 100 → pastel;
/// etiquetas largas → barras horizontales; lo demás, columnas.
/// </summary>
public static partial class ChartPlan
{
    public const int MaximumCategories = 12;
    private const int MaximumSeries = 6;

    public static ChartSpec? From(AnswerTable table, string? hint = null)
    {
        var header = table.Header.Select(cell => AnswerMarkdown.ToPlainText(cell).Trim()).ToList();
        var rows = table.Rows.Select(row => row.Select(cell => AnswerMarkdown.ToPlainText(cell).Trim()).ToList()).ToList();

        if (header.Count < 2 || header.Count - 1 > MaximumSeries || rows.Count < 2 || rows.Count > MaximumCategories)
        {
            return null;
        }

        var categories = rows.Select(row => row.Count > 0 ? row[0] : string.Empty).ToList();
        if (categories.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        var series = new List<ChartSeries>();
        var percentKinds = new HashSet<bool>();
        for (var column = 1; column < header.Count; column++)
        {
            var values = new List<double>();
            foreach (var row in rows)
            {
                if (column >= row.Count || SpreadsheetDocumentBuilder.ParseNumber(row[column]) is not { } parsed)
                {
                    return null;
                }

                values.Add((double)parsed.Value);
                percentKinds.Add(parsed.IsPercent);
            }

            series.Add(new ChartSeries(string.IsNullOrWhiteSpace(header[column]) ? $"Serie {column}" : header[column], column, values));
        }

        if (percentKinds.Count != 1)
        {
            return null;
        }

        // Series de tamaños muy distintos («Año | Habitantes | Tasa») no se comparan en un mismo eje.
        if (series.Count > 1)
        {
            var peaks = series.Select(item => item.Values.Max(Math.Abs)).ToList();
            if (peaks.Min() <= 0 || peaks.Max() / peaks.Min() > 50)
            {
                return null;
            }
        }

        var percent = percentKinds.Single();
        return new ChartSpec(ChooseKind(hint, header[0], categories, series, percent), header[0], categories, series, percent);
    }

    private static ChartKind ChooseKind(string? hint, string categoryTitle, IReadOnlyList<string> categories, IReadOnlyList<ChartSeries> series, bool percent)
    {
        var words = Simplify((hint ?? string.Empty) + " " + categoryTitle);
        var single = series.Count == 1 && series[0].Values.All(value => value >= 0) && categories.Count <= 8;

        if (PieWords().IsMatch(words) && single)
        {
            return ChartKind.Pie;
        }

        if (LineWords().IsMatch(words) && categories.Count >= 3)
        {
            return ChartKind.Line;
        }

        var longLabels = categories.Average(label => label.Length) > 14 || categories.Max(label => label.Length) > 24;
        if (BarWords().IsMatch(words))
        {
            return longLabels ? ChartKind.Bar : ChartKind.Column;
        }

        if (categories.Count >= 3 && categories.All(label => TimeLabel().IsMatch(Simplify(label))))
        {
            return ChartKind.Line;
        }

        if (single && percent && categories.Count <= 6 && Math.Abs(series[0].Values.Sum() - 1) <= 0.03)
        {
            return ChartKind.Pie;
        }

        return longLabels || categories.Count > 8 ? ChartKind.Bar : ChartKind.Column;
    }

    /// <summary>En minúsculas y sin tildes: «Evolución» y «evolucion» dicen lo mismo.</summary>
    private static string Simplify(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"\b(pastel|circular|tarta|proporcion(es)?|distribucion|reparto|composicion|cuota)\b")]
    private static partial Regex PieWords();

    [GeneratedRegex(@"\b(lineas?|evolucion|tendencia|historic[oa]s?|a lo largo|crecimiento anual|por ano|por mes)\b")]
    private static partial Regex LineWords();

    [GeneratedRegex(@"\b(barras?|columnas)\b")]
    private static partial Regex BarWords();

    [GeneratedRegex(@"^((19|20)\d{2}|(ene|feb|mar|abr|may|jun|jul|ago|sep|sept|oct|nov|dic)[a-z]*\.?( (de )?\d{2,4})?|(t|q|trimestre|semestre) ?[1-4]( \d{4})?|(semana|mes|dia|ano|week|month|year) \d+|\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?|\d{4}-\d{2}(-\d{2})?)$")]
    private static partial Regex TimeLabel();
}
