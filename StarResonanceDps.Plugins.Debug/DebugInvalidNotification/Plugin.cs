// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugInvalidNotification.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 通知の定義の誤り")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugInvalidNotification;

public sealed class Plugin : IStarResonancePlugin, IPluginNotificationProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    /// <summary>既定の文は1つ以上要る。</summary>
    public IReadOnlyList<PluginNotificationDefinition> GetNotificationDefinitions()
    {
        return
        [
            new PluginNotificationDefinition
            {
                Id = DebugParts.NotificationId,
                Names = DebugParts.Text("誤った定義"),
                DefaultTexts = new Dictionary<string, string>()
            }
        ];
    }

    public void Shutdown()
    {
    }
}
