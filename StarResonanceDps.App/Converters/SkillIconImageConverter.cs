// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace StarResonanceDps.App.Converters;

/// <summary>
/// 推移グラフの技のアイコンの絵のファイル(<c>CombatIconResolver</c> が在ることを確かめたもの)を画像にする。
/// 画像はファイルごとに1回だけ読んで凍らせ、使い回す(画面のスレッドで引く)。
/// パスが無いか読めなければ null(テンプレートがクラス不明のアイコンで出す)。読めなかったときはログに残す(グラフごと止めない)。
/// </summary>
public sealed class SkillIconImageConverter : IValueConverter
{
    /// <summary>ファイルのパスごとの画像。読めなかったファイルは null を控える。</summary>
    private static readonly Dictionary<string, ImageSource?> Images = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string path ? Load(path) : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static ImageSource? Load(string path)
    {
        if (Images.TryGetValue(path, out var cached))
        {
            return cached;
        }

        ImageSource? image = null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load skill icon {Path}; the unknown-class icon is shown instead", path);
        }

        Images[path] = image;
        return image;
    }
}
