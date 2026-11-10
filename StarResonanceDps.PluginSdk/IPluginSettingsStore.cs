namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides JSON settings files stored in the plugin's own folder (<c>Plugins\&lt;assembly name&gt;\</c>).
/// Each plugin explicitly chooses the file name it owns.
/// The files are kept when the plugin is updated, and deleted with the folder when it is uninstalled.
/// Do not use the names <c>uninstall.marker</c> and <c>app_terminated.marker</c>, or names starting with <c>update.pending</c>:
/// the host uses them in the same folder.
/// </summary>
public interface IPluginSettingsStore
{
    /// <summary>The full path of the plugin's folder.</summary>
    string SettingsDirectory { get; }

    /// <summary>
    /// The full path of a file in <see cref="SettingsDirectory"/>.
    /// Throws <see cref="ArgumentException"/> when <paramref name="fileName"/> is blank or is not a plain file name.
    /// </summary>
    string GetFilePath(string fileName);

    /// <summary>
    /// Reads a JSON file with System.Text.Json (property names are matched without case).
    /// Returns <c>new T()</c> when the file does not exist, and throws <see cref="System.IO.InvalidDataException"/> when it cannot be read or parsed.
    /// </summary>
    T Load<T>(string fileName) where T : class, new();

    /// <summary>Writes a JSON file. Throws when the file cannot be written.</summary>
    void Save<T>(string fileName, T value) where T : class;
}
