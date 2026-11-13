// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

internal sealed class PluginHostContext : IPluginContext
{
    public PluginHostContext(
        IPluginSettingsStore settings,
        IPluginMessageService messages,
        IPluginLocalizationService localization,
        IPluginLogger logger,
        IPluginGameDataService gameData,
        IPluginNameService names,
        IPluginPacketService packets,
        IPluginNotificationService notifications,
        CancellationToken stopping)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        GameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        Names = names ?? throw new ArgumentNullException(nameof(names));
        Packets = packets ?? throw new ArgumentNullException(nameof(packets));
        Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        Stopping = stopping;
    }

    public IPluginSettingsStore Settings { get; }

    public IPluginMessageService Messages { get; }

    public IPluginLocalizationService Localization { get; }

    public IPluginLogger Logger { get; }

    public IPluginGameDataService GameData { get; }

    public IPluginNameService Names { get; }

    public IPluginPacketService Packets { get; }

    public IPluginNotificationService Notifications { get; }

    public CancellationToken Stopping { get; }
}
