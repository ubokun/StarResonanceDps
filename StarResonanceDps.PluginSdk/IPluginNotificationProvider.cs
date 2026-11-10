namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Implemented by plugins that add items to the host's notification tab.
/// The entry type implements this interface next to <see cref="IStarResonancePlugin"/>.
///
/// <para>
/// The host calls <see cref="GetNotificationDefinitions"/> once after <see cref="IStarResonancePlugin.Initialize"/> returns
/// (after the widget definitions when the plugin also adds widgets), and shows the items after the host's own items, under the plugin's name.
/// The items are off until the user checks them. The host owns the row with its check box, the text box and its preview, and how the notification is delivered
/// (Windows notification or speech, volume, voice). The plugin decides when to notify and asks the host through
/// <see cref="IPluginContext.Notifications"/>.
/// </para>
///
/// <para>
/// When a definition is invalid (see <see cref="PluginNotificationDefinition"/>), the host stops the plugin and shows it as an error in the plugin list.
/// </para>
/// </summary>
public interface IPluginNotificationProvider
{
    /// <summary>
    /// The notification items this plugin adds, in the order they are listed.
    /// </summary>
    IReadOnlyList<PluginNotificationDefinition> GetNotificationDefinitions();
}
