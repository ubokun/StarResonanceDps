// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 潜在心相晶情報・モジュール情報の1行(根のバフ1つか、自分の装着中のモジュール効果1つ)。値で比べる(中身が同じ行は差し替えない)。
/// </summary>
/// <param name="Name">名前(内部ID注記を含む)。</param>
/// <param name="IconPath">絵のファイル。分からない(型が分からない上級・ファイルが無い)ときは null。</param>
/// <param name="Kind">根のバフの種類。絵の大きさ・位置・影を切り替える。</param>
/// <param name="LevelText">段階(自分のモジュール効果の行だけ)。ほかの行は null で、段階の欄を出さない。</param>
public sealed record PlayerRootBuffEntry(
    string Name,
    string? IconPath,
    RootBuffKind Kind,
    string? LevelText = null)
{
    /// <summary>絵が分からないか。はてなを出す。</summary>
    public bool ShowsUnknownIcon => IconPath is null;

    /// <summary>モジュール効果の絵か。バフ一覧と同じく、絵を小さくして左へずらす。</summary>
    public bool IsModuleEffectIcon => Kind == RootBuffKind.ModuleEffect;

    /// <summary>因子の絵か。バフ一覧と同じく、絵を左へずらす。</summary>
    public bool IsFactorIcon => Kind == RootBuffKind.Factor;

    /// <summary>心相晶のノード・上級の絵か。白い絵なので影を付ける。</summary>
    public bool IsSeasonTalentIcon => Kind is RootBuffKind.SeasonTalentNode or RootBuffKind.SeasonTalentAdvanced;
}
