// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// タレントの木1本の形。<c>Data/Generated/RootBuffs.json</c> の <c>TalentTrees</c> の1項目で、
/// <see cref="CombatDataCatalog.GetTalentTree"/> が返す。位置はゲームの木の表の単位で、木の左上が 0、下と右へ増える。
/// </summary>
/// <param name="ProfessionId">職業ID。</param>
/// <param name="SubProfessionId">特化の番号(クラスR2 の木)。クラスR1 の木は 0。</param>
/// <param name="Width">節の位置の右端。</param>
/// <param name="Height">節の位置の下端。</param>
/// <param name="Nodes">節。表の番号の順。</param>
/// <param name="Lines">節をつなぐ線(表の <c>NextTalent</c>)。</param>
public sealed record TalentTreeDefinition(
    int ProfessionId,
    int SubProfessionId,
    double Width,
    double Height,
    IReadOnlyList<TalentTreeNode> Nodes,
    IReadOnlyList<TalentTreeLine> Lines);

/// <summary>木の節1つ。</summary>
/// <param name="Id">節の番号(木の表の番号)。自分の取った節の一覧(<c>ProfessionTalentInfo.TalentNodeIds</c>)の番号と同じ。</param>
/// <param name="X">位置。</param>
/// <param name="Y">位置。</param>
/// <param name="Buffs">節のタレントが付ける根のバフ(タレント)。能力値だけの節と特化型の根は空。</param>
/// <param name="IsSpecRoot">クラスR2 の木の根(特化型のタレント)か。</param>
/// <param name="TalentId">節のタレントの番号。名前は <see cref="CombatDataCatalog.GetTalentName"/> で引く。</param>
/// <param name="IsAbility">アビリティの節か(タレントの種類が 4)。能力値の小さい節と特化型の根は false。</param>
/// <param name="Icon">節のタレントの絵の全体のパス(表の値のまま)。</param>
/// <param name="Role">役割(その木の職業の役割のタグ)。絵の背景の色を選ぶ。</param>
/// <param name="SkillReplacements">節のタレントの技の置き換え(表の <c>[6, 元の技, 置き換え後の技]</c>)。無ければ空。</param>
public sealed record TalentTreeNode(
    int Id,
    double X,
    double Y,
    IReadOnlyList<int> Buffs,
    bool IsSpecRoot,
    int TalentId,
    bool IsAbility,
    string Icon,
    DataTypes.Enums.Professions.ERoleType Role,
    IReadOnlyList<TalentSkillReplacement> SkillReplacements);

/// <summary>タレントの技の置き換え1つ。</summary>
/// <param name="OriginalSkillId">元の技。</param>
/// <param name="ReplacedSkillId">置き換え後の技。</param>
public readonly record struct TalentSkillReplacement(
    int OriginalSkillId,
    int ReplacedSkillId);

/// <summary>節をつなぐ線1本。両端は節の位置。</summary>
public sealed record TalentTreeLine(
    double X1,
    double Y1,
    double X2,
    double Y2);
