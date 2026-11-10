namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The version shown in the plugin list: three numbers separated by dots, for example <c>1.0.0</c>.
/// The list adds the leading "v", so do not write it here. Required.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class PluginVersionAttribute : Attribute
{
    /// <param name="version">The version, for example <c>1.0.0</c>.</param>
    public PluginVersionAttribute(string version)
    {
        Version = version ?? string.Empty;
    }

    /// <summary>The version.</summary>
    public string Version { get; }
}
