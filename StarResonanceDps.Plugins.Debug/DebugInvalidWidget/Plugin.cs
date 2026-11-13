// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugInvalidWidget.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: ウィジェットの定義の誤り")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugInvalidWidget;

public sealed class Plugin : IStarResonancePlugin, IPluginWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    /// <summary>既定の幅は 0 より大きくないといけない。</summary>
    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions()
    {
        return
        [
            new PluginWidgetDefinition
            {
                Id = DebugParts.WidgetId,
                Names = DebugParts.Text("誤った定義"),
                DefaultWidth = 0,
                DefaultHeight = 80
            }
        ];
    }

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context) => DebugParts.CreateWidget();

    public void Shutdown()
    {
    }
}
