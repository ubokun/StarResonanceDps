// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugWindowWidget.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 窓とウィジェット")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugWindowWidget;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider, IPluginWidgetProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("窓とウィジェットを持つプラグインです。");

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions() => DebugParts.CreateWidgetDefinitions();

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context) => DebugParts.CreateWidget();

    public void Shutdown()
    {
    }
}
