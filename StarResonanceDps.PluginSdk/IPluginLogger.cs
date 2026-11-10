namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Writes to the host's log, which the host shows in its Logs tab and saves to <c>Data\CombatRuntime.log</c> in the app folder.
/// Each line starts with <c>[Plugin:&lt;assembly name&gt;]</c>.
/// </summary>
public interface IPluginLogger
{
    /// <summary>Writes an information line.</summary>
    void Info(string message);

    /// <summary>Writes a warning line.</summary>
    void Warning(string message);

    /// <summary>Writes an error line, with the exception when one is given.</summary>
    void Error(string message, Exception? exception = null);
}
