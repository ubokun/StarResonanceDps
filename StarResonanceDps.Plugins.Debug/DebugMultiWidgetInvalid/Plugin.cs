// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugMultiWidgetInvalid.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 複数起動のウィジェットの相手の誤り")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugMultiWidgetInvalid;

public sealed class Plugin : IStarResonancePlugin, IPluginMultiWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetMultiWidgetDefinitions() => DebugParts.CreateWidgetDefinitions();

    /// <summary>相手の ID は重なってはいけない。一覧を読んだときにプラグインが止まる。</summary>
    public IReadOnlyList<PluginWidgetTarget> GetWidgetTargets(string widgetId)
    {
        return
        [
            new PluginWidgetTarget { Id = "same", Names = DebugParts.Text("相手1") },
            new PluginWidgetTarget { Id = "same", Names = DebugParts.Text("相手2") }
        ];
    }

    public PluginWidgetContent CreateWidget(string widgetId, string targetId, IPluginWidgetContext context) => DebugParts.CreateWidget();

    public void Shutdown()
    {
    }
}
