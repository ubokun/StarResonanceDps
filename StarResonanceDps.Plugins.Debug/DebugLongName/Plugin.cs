// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.Plugins.Debug;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugLongName.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: とても長い名前のプラグインで、一覧の行の幅に入りきらないときに名前が切れて版が残ることを確かめるためのものです。窓の幅を変えながら見てください。")]
[assembly: PluginGenre("ja-JP", "デバッグ: とても長いジャンル名で2段目の幅に入りきらないときの見え方を確かめるためのものです")]
[assembly: PluginAuthor("とても長い制作者の名前で2段目の幅に入りきらないときの見え方を確かめるためのものです")]
[assembly: PluginVersion("123456.123456.123456")]

namespace DebugLongName;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    public void Initialize(IPluginContext context)
    {
    }

    public FrameworkElement CreateContent() => DebugParts.CreateWindowContent("窓のタイトルにも長い名前が出ます。");

    public void Shutdown()
    {
    }
}
