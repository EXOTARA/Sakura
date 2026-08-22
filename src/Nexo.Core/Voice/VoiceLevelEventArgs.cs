namespace Nexo.Core.Voice;

/// <summary>Diseño D72 — nivel del micrófono, de 0 a 1, medido sobre el búfer que acaba de llegar.</summary>
public sealed class VoiceLevelEventArgs(double level) : EventArgs
{
    public double Level { get; } = level;
}
