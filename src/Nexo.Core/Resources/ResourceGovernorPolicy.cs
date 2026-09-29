namespace Nexo.Core.Resources;

public static class ResourceGovernorPolicy
{
    public const double BusyGpuThreshold = 88;
    public const double BusyCpuThreshold = 92;
    public const double BusyMemoryThreshold = 92;

    public static ResourceGovernorDecision Evaluate(ResourceGovernorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Snapshot);

        if (input.IsForegroundFullScreen &&
            !IsIgnoredFullScreenProcess(input.ForegroundProcessName))
        {
            var application = string.IsNullOrWhiteSpace(input.ForegroundProcessName)
                ? "la aplicación a pantalla completa"
                : input.ForegroundProcessName;

            return new ResourceGovernorDecision(
                ResourceMode.Game,
                $"{application} está usando la pantalla completa.",
                AllowLocalCommands: true,
                AllowLocalAi: false,
                AllowRemoteAi: false,
                AllowVision: false,
                PauseWakeWord: true,
                SuppressTransientOverlays: true);
        }

        var gpuBusy = input.Snapshot.GpuUsagePercent.HasValue &&
                      input.Snapshot.GpuUsagePercent.Value >= BusyGpuThreshold;
        var cpuBusy = input.Snapshot.CpuUsagePercent.HasValue &&
                      input.Snapshot.CpuUsagePercent.Value >= BusyCpuThreshold;
        var memoryBusy = input.Snapshot.MemoryUsagePercent >= BusyMemoryThreshold;

        if (gpuBusy || cpuBusy || memoryBusy)
        {
            var reasons = new List<string>(3);
            if (gpuBusy)
            {
                reasons.Add($"GPU {input.Snapshot.GpuUsagePercent:0}%");
            }

            if (cpuBusy)
            {
                reasons.Add($"CPU {input.Snapshot.CpuUsagePercent:0}%");
            }

            if (memoryBusy)
            {
                reasons.Add($"RAM {input.Snapshot.MemoryUsagePercent:0}%");
            }

            return new ResourceGovernorDecision(
                ResourceMode.Busy,
                $"El equipo está ocupado: {string.Join(" · ", reasons)}.",
                AllowLocalCommands: true,
                AllowLocalAi: false,
                AllowRemoteAi: true,
                AllowVision: false,
                PauseWakeWord: false,
                SuppressTransientOverlays: false);
        }

        return ResourceGovernorDecision.Normal;
    }

    public static bool IsIgnoredFullScreenProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return processName.Trim().ToLowerInvariant() is
            "sakura" or
            "kohana" or
            "nexo" or
            "explorer" or
            "searchhost" or
            "startmenuexperiencehost" or
            "snippingtool" or
            "screenclippinghost" or
            "shellexperiencehost";
    }
}
