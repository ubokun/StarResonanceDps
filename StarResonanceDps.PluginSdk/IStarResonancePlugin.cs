namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The entry type of a plugin, named by <see cref="PluginRegistrationAttribute"/>: a public class that is not nested, abstract or generic,
/// with a public parameterless constructor.
/// The host creates it once when the app starts, or right after the user adds the plugin, and calls <see cref="Initialize"/>.
/// It calls <see cref="Shutdown"/> when the app exits or when the host stops the plugin, also after <see cref="Initialize"/> threw.
/// What the plugin adds to the app is declared by also implementing <see cref="IPluginWindowProvider"/>,
/// <see cref="IPluginWidgetProvider"/>, <see cref="IPluginMultiWidgetProvider"/> and <see cref="IPluginNotificationProvider"/>.
/// A plugin that implements none of them runs in the background only.
/// </summary>
public interface IStarResonancePlugin
{
    /// <summary>
    /// Called once on the UI thread after the host has created the plugin. Keep <paramref name="context"/> for later use.
    /// The host reads the widget and notification definitions after this method returns, so notifications cannot be sent from here.
    /// When this method throws, the host stops the plugin until the app restarts.
    /// </summary>
    /// <param name="context">What the host gives the plugin.</param>
    void Initialize(IPluginContext context);

    /// <summary>
    /// Called once on the UI thread when the app exits or when the host stops the plugin, after the plugin's windows have closed
    /// and <see cref="IPluginContext.Stopping"/> has been canceled. Stop the plugin's timers and background work here.
    /// An exception thrown here is only logged.
    /// </summary>
    void Shutdown();
}
