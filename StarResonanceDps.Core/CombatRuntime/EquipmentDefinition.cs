// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 装備1つの定義。<c>Data/Generated/Equips.json</c> の <c>Equips</c> の1項目で、
/// <see cref="CombatDataCatalog.GetEquipment"/> が返す。
/// </summary>
/// <param name="Id">装備ID(<c>EquipTable.Id</c>)。</param>
/// <param name="Part">部位(<c>EquipTable.EquipPart</c>、200〜210)。</param>
/// <param name="Quality">品質(<c>ItemTable.Quality</c>)。</param>
/// <param name="PerfectUpperLimit">
/// 完成度の上限(<c>EquipTable.PerfectUpperLimit</c> の2要素目)。属性の値の上限をこれで決める。
/// </param>
/// <param name="MainStat">主ステータスの系の番号(11010 / 11020 / 11030)。基礎に出なければ 0。</param>
/// <param name="Stages">
/// 突破の段階ごとの定義。添字が段階で、段階0 は <c>EquipTable</c>、段階1〜 は <c>EquipBreakThroughTable</c>。
/// </param>
/// <param name="Recast">改鋳の属性庫(<c>EquipTable.RecastingAttrLibId</c>)。表の配列のまま <c>[型, 庫ID…]</c>、無ければ空。</param>
/// <param name="Rare">レアの属性庫(<c>EquipTable.QualityChildAttrLibId</c>)。形は <paramref name="Recast"/> と同じ。</param>
public sealed record EquipmentDefinition(
    int Id,
    int Part,
    int Quality,
    int PerfectUpperLimit,
    int MainStat,
    IReadOnlyList<EquipmentStage> Stages,
    IReadOnlyList<int> Recast,
    IReadOnlyList<int> Rare);

/// <summary>突破の1段階。</summary>
/// <param name="Gs">装備Lv(<c>EquipGs</c>)。</param>
/// <param name="Basic">基礎の属性庫。表の配列のまま <c>[型, 庫ID…]</c>、無ければ空。</param>
/// <param name="Advanced">進化の属性庫。形は <paramref name="Basic"/> と同じ。</param>
public sealed record EquipmentStage(
    int Gs,
    IReadOnlyList<int> Basic,
    IReadOnlyList<int> Advanced);

/// <summary>
/// 属性庫の1行。<see cref="CombatDataCatalog.GetEquipmentAttrLibRows"/> が庫ごとに表の並び順で、
/// <see cref="CombatDataCatalog.GetEquipmentAttrRow"/> が行IDで返す。
/// </summary>
/// <param name="Id">行ID(表の <c>Id</c>)。自分の装備の値は通信でこの行ID を指す。</param>
/// <param name="Parts">付く部位(<c>AllowPart</c>)。0 は全部位。</param>
/// <param name="Specs">付く特化(<c>TalentSchoolId</c>)。型1 の庫は空。</param>
/// <param name="Effects">行の効果を表の順に並べたもの。</param>
public sealed record EquipmentAttrLibRow(
    int Id,
    IReadOnlyList<int> Parts,
    IReadOnlyList<int> Specs,
    IReadOnlyList<EquipmentAttrEffect> Effects);

/// <summary>属性庫の行の効果1つ。</summary>
/// <param name="Kind">種類。1 = 属性、3 = バフ、5 = 一時属性。</param>
/// <param name="Id">属性・バフ・一時属性の番号。</param>
/// <param name="Min">値の下限(完成度 0)。</param>
/// <param name="Max">値の上限(完成度 100)。</param>
/// <param name="Format">値の書き方。0 = そのままの数、1 = %(値 ÷ 100)。</param>
public sealed record EquipmentAttrEffect(
    int Kind,
    int Id,
    long Min,
    long Max,
    int Format);
