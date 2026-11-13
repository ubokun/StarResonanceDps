// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// 個別に扱わず1つのまとまりとして見るバフの種類。
///
/// <para>
/// 料理も薬剤も、シーズン × 効果 × レベルの総当たりで数百件の別IDがあるが、
/// 同時に付くのは1つで、食べ直すたびに別のIDへ入れ替わる。個別のIDを追うと入れ替わりで見失うので、
/// <b>まとまりとして追う</b>。どのバフがどちらに入るかは <see cref="CombatDataCatalog.GetBuffGroup"/> が決める。
/// </para>
/// </summary>
public enum BuffGroup
{
    None = 0,

    /// <summary>料理。<c>Data/Localization/CuisineBuffs.json</c> にあるバフ。</summary>
    Cuisine = 1,

    /// <summary>薬剤。<c>Data/Localization/PotionBuffs.json</c> にあるバフ。</summary>
    Potion = 2
}
