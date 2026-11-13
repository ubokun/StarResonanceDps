// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugMultiWidgetEmpty.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 複数起動のウィジェットの相手が空")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugMultiWidgetEmpty;

public sealed class Plugin : IStarResonancePlugin, IPluginMultiWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetMultiWidgetDefinitions() => DebugParts.CreateWidgetDefinitions();

    /// <summary>相手がいつも空。カードは名前だけで、起動ボタンは本体が出す空の窓を開く。窓は空のままで、プラグインは止まらない。</summary>
    public IReadOnlyList<PluginWidgetTarget> GetWidgetTargets(string widgetId) => [];

    public PluginWidgetContent CreateWidget(string widgetId, string targetId, IPluginWidgetContext context) => DebugParts.CreateWidget();

    public void Shutdown()
    {
    }
}
