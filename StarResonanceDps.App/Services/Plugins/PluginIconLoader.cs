// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインのアイコンを、プラグインの DLL の中の絵(<c>PluginIcon</c> の属性のパス)から読む。読むのは一覧の行を作るときの1回だけで、行と窓で共有する。
/// 大きさごとの絵を持つファイル(ICO)は一番大きい1枚を返す。窓のアイコンには、WPF がこの1枚の <see cref="BitmapFrame.Decoder"/> から合う大きさを選ぶ。
/// 属性が無い・読めないときは null を返し、アプリのアイコンを出す(読めなかったことはログに書く)。
/// </summary>
public static class PluginIconLoader
{
    public static ImageSource? Load(PluginInfo pluginInfo)
    {
        if (pluginInfo.IconPath is not { } iconPath)
        {
            return null;
        }

        try
        {
            var uri = new Uri($"pack://application:,,,/{pluginInfo.Key};component/{iconPath.TrimStart('/')}", UriKind.Absolute);
            var decoder = BitmapDecoder.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var image = decoder.Frames.MaxBy(frame => frame.PixelWidth)
                ?? throw new FormatException("The image has no frames.");
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or FormatException)
        {
            Log.Warning(
                exception,
                "[Plugin:{Plugin}] Failed to load the plugin icon {Path}; the app icon is shown instead",
                pluginInfo.Key,
                iconPath);
            return null;
        }
    }
}
