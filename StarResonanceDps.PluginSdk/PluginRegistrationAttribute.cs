using System;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Names the plugin's entry type (<see cref="IStarResonancePlugin"/>). Required, once:
/// <c>[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class PluginRegistrationAttribute : Attribute
{
    /// <param name="entryType">The entry type.</param>
    /// <param name="apiVersion">Always <see cref="PluginSdkVersion.Current"/>.</param>
    public PluginRegistrationAttribute(Type entryType, int apiVersion)
    {
        EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
        ApiVersion = apiVersion;
    }

    /// <summary>The entry type.</summary>
    public Type EntryType { get; }

    /// <summary>The SDK API version the plugin was built with.</summary>
    public int ApiVersion { get; }
}
