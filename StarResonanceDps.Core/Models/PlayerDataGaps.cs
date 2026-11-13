// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Models;

/// <summary>
/// 他のプレイヤーについて、まだ届いていない情報(ウィジェットの状態の印の材料)。自分はいつも <see cref="None"/>(自分の欠けは
/// <see cref="CombatRuntime.GameReceptionSnapshot"/> で決める)。
/// </summary>
[Flags]
public enum PlayerDataGaps
{
    None = 0,

    /// <summary>名前・クラス・能力スコアなど。出現時の情報もパーティの社交データも受けていない。</summary>
    Identity = 1,

    /// <summary>出現時の情報を受けていない(料理と自然回復の回復を数えられない)。パーティの社交データでは埋まらない。</summary>
    AppearAttributes = 2,

    /// <summary>技の段階の一覧(G○)を受けていない。出現時の情報にもパーティの控えにも無い。</summary>
    SkillLevels = 4,
}
