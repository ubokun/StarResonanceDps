namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Implemented by plugins that add widgets which open one window per target, for example one window per player or per slot.
/// The entry type implements this interface next to <see cref="IStarResonancePlugin"/>; a plugin may also implement <see cref="IPluginWidgetProvider"/>.
/// The plugin decides what the targets are: a fixed list, the nearby players of <see cref="IPluginGameDataService.Players"/>, or anything else.
///
/// <para>
/// The host calls <see cref="GetMultiWidgetDefinitions"/> once, right after <see cref="IStarResonancePlugin.Initialize"/> returns, and adds one card per definition.
/// The card's name becomes a drop-down list of the targets, and its start button opens or closes the window of the chosen target. The same target never has two windows.
/// Each window keeps its own target, position and size; the settings, theme, pin and click-through are shared by all windows of the widget.
/// </para>
///
/// <para>
/// The card selects the target with <see cref="PluginWidgetTarget.IsDefault"/>, or otherwise the first target, until the user chooses one.
/// While the list is empty and no window is open, the card shows only the widget's name, and its start button opens an empty window.
/// The host draws that window with the widget's name as its title and does not call the plugin; a plugin writes nothing for it.
/// When a target appears, the host calls <see cref="CreateWidget"/> for the target with <see cref="PluginWidgetTarget.IsDefault"/>, or otherwise the first target,
/// and puts the content in that window. When that target already has a window, the empty window closes.
/// A target that left the list keeps its open window, whose title keeps the last known name.
/// The next time the app starts, the windows open again with the saved target IDs, even when those targets are not in the list, and so does an empty window.
/// </para>
///
/// <para>
/// When a definition or the list of targets is invalid (see <see cref="PluginWidgetDefinition"/> and <see cref="PluginWidgetTarget"/>), or this interface throws, the host stops the plugin.
/// When the plugin is stopped, the host closes its widget windows and removes its cards; the saved settings and targets are kept.
/// </para>
/// </summary>
public interface IPluginMultiWidgetProvider
{
    /// <summary>
    /// The widgets this plugin adds, in the order their cards are listed. Their cards follow those of <see cref="IPluginWidgetProvider"/>,
    /// and widget IDs are unique among both interfaces.
    /// </summary>
    IReadOnlyList<PluginWidgetDefinition> GetMultiWidgetDefinitions();

    /// <summary>
    /// The targets of a widget, in list order. Called on the UI thread when the user opens the drop-down list, and,
    /// while the user has not chosen a target and the card has not selected one with <see cref="PluginWidgetTarget.IsDefault"/> or while an empty window is open,
    /// every second and whenever the host updates the cards (for example when the player list, the nearby entities or the open windows change).
    /// It can be called several times a second, so keep it light.
    /// It may return an empty list for the moment, such as while no player is nearby; a method that always returns an empty list is a build error.
    /// </summary>
    IReadOnlyList<PluginWidgetTarget> GetWidgetTargets(string widgetId);

    /// <summary>
    /// Creates the content of the window for a target. Called on the UI thread each time a window opens for a target, and when an empty window gets its target.
    /// <paramref name="targetId"/> is never <c>null</c>. It may no longer be in the list (the window is reopening when the app starts), so show what you can for it.
    /// When <see cref="PluginWidgetContent.Body"/> or <see cref="PluginWidgetContent.Footer"/> implements <see cref="IDisposable"/>,
    /// the host disposes it after the window closes; release subscriptions to host events there.
    /// </summary>
    PluginWidgetContent CreateWidget(string widgetId, string targetId, IPluginWidgetContext context);
}
