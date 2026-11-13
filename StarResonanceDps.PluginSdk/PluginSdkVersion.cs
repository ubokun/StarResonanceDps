namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The API version of the plugin SDK, compiled into a plugin through <see cref="PluginRegistrationAttribute"/>.
/// The host loads a plugin only when the plugin's number equals the host's. The number changes only when a release breaks
/// the SDK's public interfaces, and it is the major version of the SDK package.
/// </summary>
public static class PluginSdkVersion
{
    /// <summary>The API version of this SDK.</summary>
    public const int Current = 1;
}
