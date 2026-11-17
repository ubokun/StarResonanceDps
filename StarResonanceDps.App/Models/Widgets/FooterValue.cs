// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// フッターの「見出し: 値」の値1つ。数字と、数字の後ろに付く単位(体・pt・K など)を分けて持つ。
/// フッターは数字を決まった幅の欄に右寄せし、単位をその後ろに置く(<c>Controls/FooterValuePair</c>)。
/// 見出しのコロンから数字の右端までの間隔が、言語と単位に関係なく同じになる。
/// </summary>
public readonly record struct FooterValue(string Number, string Unit)
{
    public static FooterValue Empty { get; } = new(string.Empty, string.Empty);

    /// <summary>
    /// 文言の書式(「{0}体」など)の <c>{0}</c> に数字を入れ、<c>{0}</c> の後ろを単位にする。
    /// <c>{0}</c> の前に文字がある書式は、その文字を数字の側に入れる(数字の右端の位置は変わらない)。
    /// </summary>
    /// <exception cref="InvalidOperationException">書式に <c>{0}</c> が無い(文言の誤り)。</exception>
    public static FooterValue FromFormat(string formatKey, string number)
    {
        const string Placeholder = "{0}";
        var format = LocalizationManager.Instance.GetString(formatKey);
        var index = format.IndexOf(Placeholder, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException($"The footer value format {formatKey} has no {Placeholder}: {format}");
        }

        return new FooterValue(format[..index] + number, format[(index + Placeholder.Length)..]);
    }
}
