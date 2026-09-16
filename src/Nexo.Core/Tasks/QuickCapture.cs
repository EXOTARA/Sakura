using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexo.Core.Tasks;

/// <summary>Lo que se entendió de una línea escrita en Hoy.</summary>
public sealed record QuickCaptureResult(string Title, DateTimeOffset? DueAt, TaskPriority Priority);

/// <summary>
/// 2026-09-16 — capturar una tarea en una sola línea («entregar U2A2 el viernes a las 5 !»).
///
/// La investigación de productividad (docs/research) dice que un formulario con fecha, hora y
/// prioridad es lo que hace dejar de apuntar cosas. Aquí se escribe como se habla y la fecha se
/// deduce. Lo que no se entiende se queda en el título: nunca se inventa una fecha.
///
/// Convenciones:
/// <list type="bullet">
/// <item>Una fecha sin hora se guarda a las 00:00, que en Sakura significa «ese día, sin hora».</item>
/// <item>«a las 5» sin am/pm, de 1 a 7, se entiende por la tarde: nadie apunta tareas para las 5 de la madrugada.</item>
/// <item>Una hora sin fecha es para hoy si aún no pasó; si ya pasó, para mañana.</item>
/// <item>Sin fecha ni hora se usa el día que se está viendo, si se indica.</item>
/// </list>
/// </summary>
public static partial class QuickCapture
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    private static readonly string[] Weekdays = ["domingo", "lunes", "martes", "miercoles", "jueves", "viernes", "sabado"];

    private static readonly string[] Months =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    public static QuickCaptureResult Parse(string text, DateTimeOffset now, DateOnly? day = null)
    {
        var working = " " + (text ?? string.Empty).Trim() + " ";
        var priority = TaskPriority.Normal;
        DateOnly? date = null;
        TimeSpan? time = null;

        if (ImportantPattern().IsMatch(working))
        {
            priority = TaskPriority.High;
            working = ImportantPattern().Replace(working, " ");
        }

        var today = DateOnly.FromDateTime(now.Date);

        // «en la mañana» es una hora del día, no «mañana»: se deja en el título.
        var relative = RelativePattern().Match(working);
        if (relative.Success)
        {
            var word = Fold(relative.Groups["word"].Value);
            date = word switch
            {
                "hoy" => today,
                "pasado manana" => today.AddDays(2),
                _ => today.AddDays(1)
            };
            working = Cut(working, relative);
        }

        if (date is null && WeekdayPattern().Match(working) is { Success: true } weekday)
        {
            var target = Array.IndexOf(Weekdays, Fold(weekday.Groups["day"].Value));
            var ahead = (target - (int)today.DayOfWeek + 7) % 7;
            date = today.AddDays(ahead == 0 ? 7 : ahead);
            working = Cut(working, weekday);
        }

        if (date is null && MonthDayPattern().Match(working) is { Success: true } monthDay)
        {
            var month = Array.IndexOf(Months, Fold(monthDay.Groups["month"].Value)) + 1;
            if (TryDate(today, int.Parse(monthDay.Groups["day"].Value, CultureInfo.InvariantCulture), month, out var parsed))
            {
                date = parsed;
                working = Cut(working, monthDay);
            }
        }

        if (date is null && NumericDatePattern().Match(working) is { Success: true } numeric)
        {
            if (TryDate(
                    today,
                    int.Parse(numeric.Groups["day"].Value, CultureInfo.InvariantCulture),
                    int.Parse(numeric.Groups["month"].Value, CultureInfo.InvariantCulture),
                    out var parsed))
            {
                date = parsed;
                working = Cut(working, numeric);
            }
        }

        foreach (Match clock in TimePattern().Matches(working))
        {
            var hour = int.Parse(clock.Groups["hour"].Value, CultureInfo.InvariantCulture);
            var minute = clock.Groups["minute"].Success ? int.Parse(clock.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
            var meridiem = Fold(clock.Groups["meridiem"].Value).Replace(".", string.Empty).Replace(" ", string.Empty);
            var hasColon = clock.Groups["minute"].Success;
            var hasLead = clock.Groups["lead"].Success;

            // Un número suelto («capítulo 3») no es una hora: hace falta «a las», los dos puntos o am/pm.
            if (hasLead || hasColon || meridiem.Length > 0)
            {
                if (meridiem is "pm" or "delatarde" or "delanoche" && hour < 12)
                {
                    hour += 12;
                }
                else if (meridiem == "am" && hour == 12)
                {
                    hour = 0;
                }
                else if (meridiem.Length == 0 && !hasColon && hour is >= 1 and <= 7)
                {
                    hour += 12;
                }

                if (hour is >= 0 and < 24 && minute is >= 0 and < 60)
                {
                    time = new TimeSpan(hour, minute, 0);
                    working = Cut(working, clock);
                    break;
                }
            }
        }

        if (date is null && time is { } onlyTime)
        {
            date = now.TimeOfDay < onlyTime ? today : today.AddDays(1);
        }

        date ??= day;

        var title = CleanTitle(working);
        if (title.Length == 0)
        {
            title = (text ?? string.Empty).Trim();
        }

        DateTimeOffset? dueAt = null;
        if (date is { } chosen)
        {
            var local = chosen.ToDateTime(TimeOnly.FromTimeSpan(time ?? TimeSpan.Zero), DateTimeKind.Unspecified);
            dueAt = new DateTimeOffset(local, now.Offset);
        }

        return new QuickCaptureResult(title, dueAt, priority);
    }

    private static bool TryDate(DateOnly today, int day, int month, out DateOnly date)
    {
        date = default;
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(today.Year, month))
        {
            return false;
        }

        date = new DateOnly(today.Year, month, day);
        if (date < today)
        {
            // «el 3 de enero» escrito en septiembre es del año que viene.
            var next = today.Year + 1;
            if (day > DateTime.DaysInMonth(next, month))
            {
                return false;
            }

            date = new DateOnly(next, month, day);
        }

        return true;
    }

    private static string Cut(string text, Match match) =>
        text[..match.Index] + " " + text[(match.Index + match.Length)..];

    private static string CleanTitle(string text)
    {
        var cleaned = SpacesPattern().Replace(text, " ").Trim();
        cleaned = DanglingPattern().Replace(cleaned, string.Empty).Trim();
        cleaned = cleaned.TrimEnd(',', ';', ':', '-', '.').Trim();
        return cleaned.Length == 0 ? cleaned : char.ToUpper(cleaned[0], Spanish) + cleaned[1..];
    }

    /// <summary>Minúsculas y sin acentos, para comparar palabras escritas de cualquier forma.</summary>
    private static string Fold(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(decomposed.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    [GeneratedRegex(@"(?<=\s)(!+|importante:?)(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex ImportantPattern();

    [GeneratedRegex(@"(?<!(?:la|por|en|esta)\s)(?<=\s)(?:para\s+)?(?<word>pasado\s+ma[ñn]ana|ma[ñn]ana|hoy)(?=[\s,.])", RegexOptions.IgnoreCase)]
    private static partial Regex RelativePattern();

    [GeneratedRegex(@"(?<=\s)(?:para\s+)?(?:el\s+|este\s+|pr[oó]ximo\s+)?(?<day>lunes|martes|mi[eé]rcoles|jueves|viernes|s[aá]bado|domingo)(?=[\s,.])", RegexOptions.IgnoreCase)]
    private static partial Regex WeekdayPattern();

    [GeneratedRegex(@"(?<=\s)(?:para\s+)?(?:el\s+)?(?<day>\d{1,2})\s+de\s+(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)(?=[\s,.])", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDayPattern();

    [GeneratedRegex(@"(?<=\s)(?:para\s+)?(?:el\s+)?(?<day>\d{1,2})/(?<month>\d{1,2})(?=[\s,.])", RegexOptions.IgnoreCase)]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex(@"(?<=\s)(?<lead>a\s+las?\s+)?(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<meridiem>a\.?\s?m\.?|p\.?\s?m\.?|de\s+la\s+tarde|de\s+la\s+noche)?(?=[\s,.])", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesPattern();

    [GeneratedRegex(@"(\s+(para|el|a|de|y))+$", RegexOptions.IgnoreCase)]
    private static partial Regex DanglingPattern();
}
