namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The genre shown in the plugin list, per UI culture (for example <c>ja-JP</c>, <c>en-US</c>).
/// Required for plugins that implement <see cref="IPluginWindowProvider"/>; one culture is enough.
/// Ignored for plugins without a window.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class PluginGenreAttribute : Attribute
{
    /// <param name="cultureName">The culture name, for example <c>ja-JP</c>.</param>
    /// <param name="genre">The genre.</param>
    public PluginGenreAttribute(string cultureName, string genre)
    {
        CultureName = cultureName ?? string.Empty;
        Genre = genre ?? string.Empty;
    }

    /// <summary>The culture name.</summary>
    public string CultureName { get; }

    /// <summary>The genre.</summary>
    public string Genre { get; }
}
