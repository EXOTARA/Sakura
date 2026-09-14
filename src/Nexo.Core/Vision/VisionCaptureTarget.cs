namespace Nexo.Core.Vision;

public sealed record VisionCaptureTarget(
    string Id,
    long NativeHandle,
    string Title,
    string Subtitle,
    VisionCaptureKind Kind,
    int Left,
    int Top,
    int Width,
    int Height,
    bool IsSensitive = false)
{
    // Las listas de UIA necesitan una etiqueta humana, no el volcado del record.
    public override string ToString() => Title;

    public string DisplayName => string.IsNullOrWhiteSpace(Subtitle)
        ? Title
        : $"{Title} · {Subtitle}";
}
