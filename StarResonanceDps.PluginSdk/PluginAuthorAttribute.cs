namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The author shown in the plugin list. Required.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class PluginAuthorAttribute : Attribute
{
    /// <param name="author">The author name. Must not be blank.</param>
    public PluginAuthorAttribute(string author)
    {
        Author = author ?? string.Empty;
    }

    /// <summary>The author name.</summary>
    public string Author { get; }
}
