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
/// 絵を灰色にする(アビリティ情報の分からない節の背景とタレントの絵)。受けるのは絵のファイルのパス(<c>CombatIconResolver</c> が在ることを確かめたもの)か、資源の絵。
/// 明るさは 0.2126 R + 0.7152 G + 0.0722 B で、透明度はそのまま。
/// 画像は元の絵ごとに1回だけ作って凍らせ、使い回す(画面のスレッドで引く)。
/// ファイルが読めなければ null(テンプレートがはてなで出す)。読めなかったときはログに残す。
/// </summary>
public sealed class GrayscaleImageConverter : IValueConverter
{
    /// <summary>ファイルのパスごとの灰色の画像。読めなかったファイルは null を控える。</summary>
    private static readonly Dictionary<string, ImageSource?> FileImages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>資源の絵ごとの灰色の画像。</summary>
    private static readonly Dictionary<BitmapSource, ImageSource> ResourceImages = new(ReferenceEqualityComparer.Instance);

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            string path => LoadFile(path),
            BitmapSource bitmap => LoadResource(bitmap),
            _ => null
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static ImageSource? LoadFile(string path)
    {
        if (FileImages.TryGetValue(path, out var cached))
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
            image = CreateGrayscale(bitmap);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load talent icon {Path}; the unknown icon is shown instead", path);
        }

        FileImages[path] = image;
        return image;
    }

    private static ImageSource LoadResource(BitmapSource bitmap)
    {
        if (!ResourceImages.TryGetValue(bitmap, out var image))
        {
            image = CreateGrayscale(bitmap);
            ResourceImages[bitmap] = image;
        }

        return image;
    }

    private static BitmapSource CreateGrayscale(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);
        for (var index = 0; index < pixels.Length; index += 4)
        {
            var gray = (byte)Math.Round(0.0722 * pixels[index] + 0.7152 * pixels[index + 1] + 0.2126 * pixels[index + 2]);
            pixels[index] = gray;
            pixels[index + 1] = gray;
            pixels[index + 2] = gray;
        }

        var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
