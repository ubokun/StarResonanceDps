namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The game server of the client the host is currently capturing.
/// </summary>
public enum PluginGameServer
{
    /// <summary>No game connection has been identified yet, or the client is not a known server build.</summary>
    Unknown = 0,

    /// <summary>The Asia server.</summary>
    Asia,

    /// <summary>The China server.</summary>
    China,

    /// <summary>The Global server.</summary>
    Global,

    /// <summary>The Southeast Asia server.</summary>
    SoutheastAsia
}
