// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugThrowShutdown.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 終了時に例外")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugThrowShutdown;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("アプリを閉じるかアンインストールすると Shutdown が例外を出します。画面には何も出ず、ログに「Plugin shutdown failed」が残ります。");

    public void Shutdown()
    {
        throw new InvalidOperationException("Debug exception from Shutdown.");
    }
}
