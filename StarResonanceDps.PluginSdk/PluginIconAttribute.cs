namespace StarResonanceDps.PluginSdk;

/// <summary>
/// The icon shown in the plugin list, at the top left of the plugin's window and as that window's icon:
/// the path of an image embedded in the plugin DLL as a WPF resource (<c>&lt;Resource Include="..." /&gt;</c>),
/// relative to the project, for example <c>Assets/Icon.ico</c>.
/// Use an <c>.ico</c> with several sizes, or a single image such as a <c>.png</c>.
/// The list and the top left show the largest size in the file.
/// Optional; without it the app icon is shown.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class PluginIconAttribute : Attribute
{
    /// <param name="resourcePath">The path of the image relative to the project, for example <c>Assets/Icon.ico</c>.</param>
    public PluginIconAttribute(string resourcePath)
    {
        ResourcePath = resourcePath ?? string.Empty;
    }

    /// <summary>The path of the image relative to the project.</summary>
    public string ResourcePath { get; }
}
