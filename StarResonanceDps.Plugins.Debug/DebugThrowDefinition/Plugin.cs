// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugThrowDefinition.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 定義を読むときに例外")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugThrowDefinition;

public sealed class Plugin : IStarResonancePlugin, IPluginWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions()
    {
        throw new InvalidOperationException("Debug exception from GetWidgetDefinitions.");
    }

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context) => DebugParts.CreateWidget();

    public void Shutdown()
    {
    }
}
