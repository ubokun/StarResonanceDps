using System.Globalization;

namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Provides the host application's current UI culture and notifies plugins when it changes.
/// Plugins own their translated resources and use this service only as the culture source.
/// </summary>
public interface IPluginLocalizationService
{
    /// <summary>The host's current UI culture: <c>ja-JP</c>, <c>ko-KR</c>, <c>zh-CN</c> or <c>en-US</c>.</summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Raised on the UI thread after <see cref="CurrentCulture"/> has changed. A language the user chooses in the host's settings
    /// takes effect before it is saved, and is undone when it is not saved; both raise this event.
    /// </summary>
    event EventHandler? CultureChanged;
}
