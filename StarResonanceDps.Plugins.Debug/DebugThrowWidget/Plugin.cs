// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugThrowWidget.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: ウィジェットを開くときに例外")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugThrowWidget;

public sealed class Plugin : IStarResonancePlugin, IPluginWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions() => DebugParts.CreateWidgetDefinitions();

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context)
    {
        throw new InvalidOperationException("Debug exception from CreateWidget.");
    }

    public void Shutdown()
    {
    }
}
