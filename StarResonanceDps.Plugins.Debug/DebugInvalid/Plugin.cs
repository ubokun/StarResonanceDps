// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

// PluginVersion をわざと書かない。
[assembly: PluginRegistration(typeof(DebugInvalid.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 版の属性なし")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]

namespace DebugInvalid;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("ここは開けないはずです。");

    public void Shutdown()
    {
    }
}
