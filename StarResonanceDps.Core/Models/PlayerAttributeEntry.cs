// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Models;

/// <summary>
/// 自分の実体に届いている属性1つ。ステータス詳細が、届いた行をそのまま並べるために使う。
///
/// <para>
/// <b>値は変換しない。</b> 単位も型も表の値と実際が一致しないことがあるので、届いたものを
/// そのまま文字列にする。どう見せるかは実データを見てから決める。
/// </para>
/// </summary>
/// <param name="AttrId">属性の番号。表示の並び順。</param>
/// <param name="EnumName">通信の属性の列挙名。列挙に無い番号はその数字が入る。</param>
/// <param name="ValueText">届いた値。数値以外(一覧・構造体)はその型の文字列表現になる。</param>
/// <param name="ChangeSign">
/// 差分で最後に変わった向き(1 は増、-1 は減、0 は記録なし)。能力値だけで、入場とバフの付け直しの変化は入らない
/// (<c>SelfAttributeChangeStore</c>)。
/// </param>
/// <param name="ChangedAtUtc">その差分の到着時刻。<paramref name="ChangeSign"/> が 0 なら既定値。</param>
public sealed record PlayerAttributeEntry(
    int AttrId,
    string EnumName,
    string ValueText,
    int ChangeSign = 0,
    DateTime ChangedAtUtc = default);
