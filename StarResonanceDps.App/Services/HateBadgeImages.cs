// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StarResonanceDps.App.Services;

/// <summary>
/// エンティティリストのヘイトの枠のバッジの絵(Icons.xaml の <c>Icon.HateTarget</c> / <c>Icon.HateGauge</c>)。
///
/// <para>
/// 狙っている相手の印は、<see cref="AccentBrush"/> を絵の形(<see cref="TargetMask"/>)で抜いて描く。
/// 絵はゲームの絵から焼き込みの影を抜いた白い形にしてあるので、影は何も付かない(効果の影も付けない)。
/// </para>
///
/// <para>
/// ヘイトのゲージは、絵の形を下から <see cref="AccentBrush"/> で塗り、塗っていない上の部分を黒で塗る。
/// 割合は形の見える量(見える画素 = 不透明度が <see cref="GaugeVisibleAlphaThreshold"/> 以上の画素の不透明度の合計)で割る。
/// 割合 r のとき、塗る線より下の見える量が全体の r になる(高さで割ると、形の下の細い部分で量がほとんど塗られない)。
/// 0% は見える一番下の行の下端(黒だけ)、100% は見える一番上の行の上端(全部塗る)。満タンの値は <see cref="GaugeFullValue"/>。
/// </para>
///
/// どの絵も初めて使うときに1回だけ作り、凍らせて使い回す(画面のスレッドで引く)。
/// </summary>
public static class HateBadgeImages
{
    /// <summary>ゲージが満タンになる値(ヘイトの上限として決めた値)。越える値は満タンで止める。</summary>
    public const long GaugeFullValue = 999_999_999L;

    /// <summary>
    /// ゲージの充填の範囲に数える画素の不透明度の下限(10%)。これより薄い画素は見えないので数えない
    /// (数えると 0% と 100% の点が見える形の外に出る)。
    /// </summary>
    private const byte GaugeVisibleAlphaThreshold = 26;

    private const string TargetImageResourceKey = "Icon.HateTarget";
    private const string GaugeImageResourceKey = "Icon.HateGauge";

    private static readonly Color AccentColor = Color.FromRgb(0xFF, 0x7D, 0x4B);

    private static ImageBrush? _targetMask;
    private static ImageBrush? _gaugeMask;
    private static SolidColorBrush? _accentBrush;
    private static GaugeMassProfile? _gaugeMassProfile;

    /// <summary>狙っている相手の印の形。</summary>
    public static ImageBrush TargetMask => _targetMask ??= CreateMask(LoadBitmap(TargetImageResourceKey));

    /// <summary>ヘイトのゲージの形(黒の地と充填の両方を抜く)。</summary>
    public static ImageBrush GaugeMask => _gaugeMask ??= CreateMask(LoadBitmap(GaugeImageResourceKey));

    /// <summary>狙っている相手の印とヘイトのゲージの充填の色。</summary>
    public static SolidColorBrush AccentBrush => _accentBrush ??= CreateBrush(AccentColor);

    /// <summary>ヘイトの値をゲージの割合(0〜1)にする。</summary>
    public static double GetGaugeRatio(long hateValue)
    {
        return Math.Clamp(hateValue / (double)GaugeFullValue, 0d, 1d);
    }

    /// <summary>
    /// ゲージの充填の高さ(ゲージの下端から)。<paramref name="width"/> と <paramref name="height"/> はゲージの実際の大きさ(XAML で決める)。
    /// 塗る線は、線より下の見える量が全体の <paramref name="ratio"/> になる位置。
    /// 絵はゲージの中に縦横比を保って中央に置かれる(<see cref="Stretch.Uniform"/>)ので、絵の行の位置をその置き方でゲージの座標に直す。
    /// </summary>
    public static double GetGaugeFillHeight(double ratio, double width, double height)
    {
        if (width <= 0d || height <= 0d)
        {
            return 0d;
        }

        var profile = _gaugeMassProfile ??= MeasureGaugeMassProfile(LoadBitmap(GaugeImageResourceKey));
        var scale = Math.Min(width / profile.ImageWidth, height / profile.ImageHeight);
        var offsetY = (height - profile.ImageHeight * scale) / 2d;
        return height - (offsetY + GetFillLineRow(profile, Math.Clamp(ratio, 0d, 1d)) * scale);
    }

    /// <summary>
    /// 塗る線の位置(絵の上端からの行の座標)。下の行から見える量を足し上げ、行の中では量が一様にあるとして割る。
    /// 0 なら見える一番下の行の下端、1 なら見える一番上の行の上端。
    /// </summary>
    private static double GetFillLineRow(GaugeMassProfile profile, double ratio)
    {
        var target = ratio * profile.TotalMass;
        var accumulated = 0d;
        for (var row = profile.RowMass.Length - 1; row >= 0; row--)
        {
            var mass = profile.RowMass[row];
            if (mass <= 0d)
            {
                continue;
            }

            if (accumulated + mass >= target)
            {
                return row + 1 - (target - accumulated) / mass;
            }

            accumulated += mass;
        }

        // 足し上げの丸めで全体にわずかに届かなかったとき(割合 1)。
        return profile.TopRow;
    }

    private static BitmapSource LoadBitmap(string resourceKey)
    {
        return (BitmapSource)Application.Current.FindResource(resourceKey);
    }

    /// <summary>
    /// 絵の行ごとの見える量(<see cref="GaugeVisibleAlphaThreshold"/> 以上の画素の不透明度の合計)を測る。
    /// ゲージの大きさに依らないので1回だけ測る。
    /// </summary>
    private static GaugeMassProfile MeasureGaugeMassProfile(BitmapSource source)
    {
        var pixels = ReadBgraPixels(source, out var width, out var height, out var stride);
        var rowMass = new double[height];
        var topRow = -1;
        var totalMass = 0d;
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var alpha = pixels[row * stride + column * 4 + 3];
                if (alpha >= GaugeVisibleAlphaThreshold)
                {
                    rowMass[row] += alpha;
                }
            }

            if (rowMass[row] > 0d && topRow < 0)
            {
                topRow = row;
            }

            totalMass += rowMass[row];
        }

        if (topRow < 0)
        {
            throw new InvalidOperationException($"The image '{GaugeImageResourceKey}' has no visible pixels.");
        }

        return new GaugeMassProfile(rowMass, totalMass, topRow, width, height);
    }

    private static byte[] ReadBgraPixels(BitmapSource source, out int width, out int height, out int stride)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        width = converted.PixelWidth;
        height = converted.PixelHeight;
        stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static ImageBrush CreateMask(ImageSource source)
    {
        var brush = new ImageBrush(source)
        {
            Stretch = Stretch.Uniform
        };
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <param name="RowMass">行ごとの見える量(絵の上の行から)。</param>
    /// <param name="TotalMass">見える量の全体。</param>
    /// <param name="TopRow">見える画素がある一番上の行。</param>
    /// <param name="ImageWidth">絵の幅(画素)。</param>
    /// <param name="ImageHeight">絵の高さ(画素)。</param>
    private sealed record GaugeMassProfile(double[] RowMass, double TotalMass, int TopRow, int ImageWidth, int ImageHeight);
}
