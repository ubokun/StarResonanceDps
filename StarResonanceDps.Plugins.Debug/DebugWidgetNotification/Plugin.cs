// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugWidgetNotification.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: ウィジェットと通知")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugWidgetNotification;

public sealed class Plugin : IStarResonancePlugin, IPluginWidgetProvider, IPluginNotificationProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public IReadOnlyList<PluginWidgetDefinition> GetWidgetDefinitions() => DebugParts.CreateWidgetDefinitions();

    public PluginWidgetContent CreateWidget(string widgetId, IPluginWidgetContext context) => DebugParts.CreateWidget();

    public IReadOnlyList<PluginNotificationDefinition> GetNotificationDefinitions() => DebugParts.CreateNotificationDefinitions();

    public void Shutdown()
    {
    }
}
