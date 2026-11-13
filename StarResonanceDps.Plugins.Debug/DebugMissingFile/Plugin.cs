// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using DebugMissingFile.Library;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugMissingFile.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 部品のDLLなし")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugMissingFile;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent(Marker.Text);

    public void Shutdown()
    {
    }
}
