// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugIconMissing.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: アイコンのパスの誤り")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]
[assembly: PluginIcon("Assets/missing.ico")]

namespace DebugIconMissing;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("アプリのアイコンが出て、ログに警告が残ります。");

    public void Shutdown()
    {
    }
}
