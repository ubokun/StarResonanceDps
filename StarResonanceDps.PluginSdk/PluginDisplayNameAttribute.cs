using System;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The plugin name shown in the plugin list, on the plugin's window and in the notification tab, per UI culture
/// (<c>ja-JP</c>, <c>ko-KR</c>, <c>zh-CN</c> or <c>en-US</c>). At least one is required, one per culture.
/// The host uses the name for the current UI culture, then for its language (<c>ja</c>), then <c>en-US</c>, then any name.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class PluginDisplayNameAttribute : Attribute
{
    /// <param name="cultureName">The culture name, for example <c>ja-JP</c>.</param>
    /// <param name="displayName">The name.</param>
    public PluginDisplayNameAttribute(string cultureName, string displayName)
    {
        CultureName = cultureName ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
    }

    /// <summary>The culture name.</summary>
    public string CultureName { get; }

    /// <summary>The name.</summary>
    public string DisplayName { get; }
}
