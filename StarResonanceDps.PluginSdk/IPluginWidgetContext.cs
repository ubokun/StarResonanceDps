namespace StarResonanceDps.PluginSdk;

/// <summary>
/// What the host gives one open widget window
/// (<see cref="IPluginWidgetProvider.CreateWidget"/> and <see cref="IPluginMultiWidgetProvider.CreateWidget"/>).
/// </summary>
public interface IPluginWidgetContext
{
    /// <summary>The <see cref="PluginWidgetDefinition.Id"/> of the widget.</summary>
    string WidgetId { get; }

    /// <summary>
    /// The current values of the widget's own settings (<see cref="PluginWidgetDefinition.SettingSections"/>).
    /// While the settings window is open these are the unsaved values the user is trying, so the widget shows them as a preview.
    /// Safe to read from any thread.
    /// </summary>
    PluginSettingValues Settings { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="Settings"/> has changed: the user changed a value in the settings window (preview), saved,
    /// discarded the changes or reset to the defaults. Not raised after the window has closed.
    /// </summary>
    event EventHandler? SettingsChanged;
}
