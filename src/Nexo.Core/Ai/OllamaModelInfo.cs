namespace Nexo.Core.Ai;

public sealed record OllamaModelInfo(
    string Name,
    long SizeBytes,
    DateTimeOffset? ModifiedAt)
{
    // Las listas de UIA necesitan una etiqueta humana, no el volcado del record.
    public override string ToString() => Name;

    public string SizeDisplay => FormatSize(SizeBytes);

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "Tamaño no disponible";
        }

        var gigabytes = bytes / 1024d / 1024d / 1024d;
        return gigabytes >= 1
            ? $"{gigabytes:0.0} GB"
            : $"{bytes / 1024d / 1024d:0} MB";
    }
}
