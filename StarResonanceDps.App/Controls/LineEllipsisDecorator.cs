// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// 子の <see cref="TextBlock"/> に <see cref="Text"/> を出す。与えられた高さに入りきらなければ、入る行だけを出し、その最後の1行を「…」だけの行にする
/// (1行しか入らなければ「…」だけ)。
///
/// <para>
/// TextBlock の TextTrimming の「…」は折り返しで続く行にしか付かず、改行で区切った行には付かない。また上限のある高さでは入る行のぶんの高さを返すので、
/// 親が行の高さの刻みで伸び縮みする。ここでは行を数えて「…」の行を足し、入りきらないときは与えられた高さを全部使う(親は 1px ずつ動く)。
/// 子の TextBlock には TextTrimming を付けない(書体・色・折り返しは子のスタイルが決める)。
/// </para>
/// </summary>
public sealed class LineEllipsisDecorator : Decorator
{
    private const string Ellipsis = "…";

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(LineEllipsisDecorator),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is not TextBlock block)
        {
            return base.MeasureOverride(constraint);
        }

        var text = Text ?? string.Empty;
        if (double.IsPositiveInfinity(constraint.Height)
            || MeasureTextHeight(block, text, constraint.Width) <= constraint.Height)
        {
            block.Text = text;
            block.Measure(constraint);
            return block.DesiredSize;
        }

        // 「…」の行を足して入る一番長い頭の部分を、切ってよい位置の中から二分探索する。
        var cutPositions = GetCutPositions(text);
        var low = 0;
        var high = cutPositions.Count - 1;
        while (low < high)
        {
            var index = (low + high + 1) / 2;
            if (MeasureTextHeight(block, Truncate(text, cutPositions[index]), constraint.Width) <= constraint.Height)
            {
                low = index;
            }
            else
            {
                high = index - 1;
            }
        }

        block.Text = Truncate(text, cutPositions[low]);
        block.Measure(constraint);
        return new Size(block.DesiredSize.Width, constraint.Height);
    }

    /// <summary>頭から <paramref name="length"/> を残し、末尾の空白と改行を落として「…」だけの行を足す。残す部分が空なら「…」だけ。</summary>
    private static string Truncate(string text, int length)
    {
        var kept = text[..length].TrimEnd();
        return kept.Length == 0 ? Ellipsis : kept + "\n" + Ellipsis;
    }

    /// <summary>
    /// 切ってよい位置(残す長さ)を小さい順に返す。文字(結合文字・サロゲートを分けない単位)の切れ目のうち、単語の文字が続く途中は外す。
    /// 単語で折り返す言語は単語の途中で切ると、残した最後の行が全文のときの折り返しと違う所で終わるため。
    /// </summary>
    private static List<int> GetCutPositions(string text)
    {
        var cutPositions = new List<int>();
        foreach (var start in StringInfo.ParseCombiningCharacters(text))
        {
            if (start == 0 || !(IsWordCharacter(text[start - 1]) && IsWordCharacter(text[start])))
            {
                cutPositions.Add(start);
            }
        }

        if (cutPositions.Count == 0 || cutPositions[^1] != text.Length)
        {
            cutPositions.Add(text.Length);
        }

        return cutPositions;
    }

    /// <summary>
    /// 単語で折り返す文字(ラテン・ギリシャ・キリル文字などの英字と数字)。U+1100 より後(ハングル・かな・漢字など)は、WPF が文字の切れ目で折り返すので含めない。
    /// </summary>
    private static bool IsWordCharacter(char value)
    {
        return value < 0x1100 && char.IsLetterOrDigit(value);
    }

    private static double MeasureTextHeight(TextBlock block, string text, double width)
    {
        block.Text = text;
        block.Measure(new Size(width, double.PositiveInfinity));
        return block.DesiredSize.Height;
    }
}
