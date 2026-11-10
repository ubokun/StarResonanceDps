using System.Windows;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(SampleLocalization.SampleLocalizationPlugin), PluginSdkVersion.Current)]
// Attribute arguments must be constants, so the name and genre are written here instead of in Resources.
[assembly: PluginDisplayName("en-US", "Sample: Languages")]
[assembly: PluginDisplayName("ja-JP", "サンプル: 多言語")]
[assembly: PluginDisplayName("ko-KR", "샘플: 다국어")]
[assembly: PluginDisplayName("zh-CN", "示例: 多语言")]
[assembly: PluginGenre("en-US", "Sample")]
[assembly: PluginGenre("ja-JP", "サンプル")]
[assembly: PluginGenre("ko-KR", "샘플")]
[assembly: PluginGenre("zh-CN", "示例")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace SampleLocalization;

/// <summary>
/// A plugin with texts in the app's four languages, a settings file in the plugin's folder and the app's message window.
/// </summary>
public sealed class SampleLocalizationPlugin : IStarResonancePlugin, IPluginWindowProvider
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
    }

    public FrameworkElement CreateContent()
    {
        return new LocalizationView(_context ?? throw new InvalidOperationException("The plugin has not been initialized."));
    }

    public PluginWindowOptions GetWindowOptions()
    {
        return new PluginWindowOptions(width: 480, height: 280, minWidth: 360, minHeight: 200);
    }

    public void Shutdown()
    {
        _context = null;
    }
}
