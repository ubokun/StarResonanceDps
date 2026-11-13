// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using static StarResonanceDps.Core.CombatRuntime.DataTypes.Enums.Professions;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>アビリティ情報の木の節の出し方。</summary>
public enum TalentTreeNodeKind
{
    /// <summary>能力値だけの節と、取っていないアビリティ。灰色の点。</summary>
    Dot,

    /// <summary>取っているアビリティ。役割の色の背景の上にタレントの絵。</summary>
    Taken,

    /// <summary>クラスR2 の木の根(特化型)。特化型の背景の上に根のタレントの絵。</summary>
    SpecRoot,

    /// <summary>取っているか分からないアビリティ(ほかのプレイヤーの、バフを付けないアビリティ)。役割の背景とタレントの絵を灰色にして出す。</summary>
    Unknown
}

/// <summary>
/// アビリティ情報の木の節1つ。値で比べる(中身が同じ節は差し替えない)。
/// </summary>
/// <param name="X">位置(木の表の単位。画面の位置は XAML の倍率で決める)。</param>
/// <param name="Y">位置。</param>
/// <param name="Kind">出し方。</param>
/// <param name="IconPath">絵のファイル。点は null。点でない節でファイルが無ければ null(はてなを出す)。</param>
/// <param name="TalentRole">取っているアビリティと分からないアビリティの役割。背景の色を選ぶ。ほかは None。</param>
/// <param name="ToolTipText">
/// TIPS の文字。取っているアビリティと根はタレントの名前(根は特化型のフルネーム)、分からないアビリティはタレントの名前の後ろに括弧で解放状況が分からないこと、
/// 点は空(TIPS を出さない)。
/// </param>
public sealed record TalentTreeNodeEntry(
    double X,
    double Y,
    TalentTreeNodeKind Kind,
    string? IconPath,
    ERoleType TalentRole,
    string ToolTipText)
{
    /// <summary>灰色の点か。</summary>
    public bool IsDot => Kind == TalentTreeNodeKind.Dot;

    /// <summary>TIPS を出すか(点には出さない)。</summary>
    public bool HasToolTip => !IsDot;

    /// <summary>特化型の根か。背景を特化型のものにする。</summary>
    public bool IsSpecRoot => Kind == TalentTreeNodeKind.SpecRoot;

    /// <summary>取っているか分からないアビリティか。背景と絵を灰色にする。</summary>
    public bool IsUnknown => Kind == TalentTreeNodeKind.Unknown;

    /// <summary>絵が分からないか。はてなを出す。</summary>
    public bool ShowsUnknownIcon => !IsDot && IconPath is null;

    /// <summary>背景が防御(青)か。</summary>
    public bool IsTankTalent => TalentRole == ERoleType.Tank;

    /// <summary>背景が支援(緑)か。</summary>
    public bool IsHealerTalent => TalentRole == ERoleType.Healer;

    /// <summary>背景が攻撃(赤)か。</summary>
    public bool IsDpsTalent => TalentRole == ERoleType.DPS;
}
