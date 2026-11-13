// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugWindowNotification.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 窓と通知")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugWindowNotification;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider, IPluginNotificationProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("窓と通知を持つプラグインです。");

    public IReadOnlyList<PluginNotificationDefinition> GetNotificationDefinitions() => DebugParts.CreateNotificationDefinitions();

    public void Shutdown()
    {
    }
}
