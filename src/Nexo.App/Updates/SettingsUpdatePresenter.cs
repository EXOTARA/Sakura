using Nexo.App.Views;
using Nexo.Core.Settings;
using Nexo.Core.Updates;

namespace Nexo.App.Updates;

/// <summary>
/// El flujo de actualizaciones enseñado donde siempre: la sección Actualizaciones de Personalizar, y la
/// cápsula para avisar de que hay versión nueva.
/// </summary>
public sealed class SettingsUpdatePresenter(SettingsView settings, Action<SakuraVersion> notifyAvailable) : IUpdatePresenter
{
    public void SetStatus(string status, bool busy) => settings.SetUpdateStatus(status, busy);

    public void ShowOffer(string? version, string? notes, string? targetFolder) =>
        settings.ShowUpdateOffer(version, notes, targetFolder);

    public void SetProgress(double fraction) => settings.SetUpdateProgress(fraction);

    public void ShowStoreManaged() => settings.ShowStoreManagedUpdates();

    public void NotifyAvailable(SakuraVersion version) => notifyAvailable(version);
}

/// <summary>Las preferencias de actualización leídas y escritas sobre las de Sakura.</summary>
public sealed class ShellUpdatePreferences(Func<ShellPreferences> preferences, Action save) : IUpdatePreferences
{
    public DateTimeOffset? LastCheckAt
    {
        get => preferences().LastUpdateCheckAt;
        set => preferences().LastUpdateCheckAt = value;
    }

    public string SkippedVersion
    {
        get => preferences().SkippedUpdateVersion;
        set => preferences().SkippedUpdateVersion = value;
    }

    public bool AutomaticCheckEnabled => preferences().AutomaticUpdateCheckEnabled;

    public void Save() => save();
}
