// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Frozen;
using System.Collections.Generic;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class CookCuisineTable
    {
        /// <summary>行の説明のバフ(<c>NameDesign</c> 生命恢复)。これを説明に持つ行が、HP を一定量ずつ回復する料理。</summary>
        public const int HealthRegenDescriptionId = 2032050;

        public Dictionary<string, CookCuisine> Data = new();

        /// <summary>
        /// 回復の料理が付けるバフと、そのバフの1回の回復量(<c>BuffPar</c> の2番目)。
        /// 同じバフに違う回復量を持つ行があるバフは、どちらか分からないので入れない(読み込み時にエラーを出す)。
        /// </summary>
        public FrozenDictionary<int, int> RegenAmountsByBuffId = FrozenDictionary<int, int>.Empty;
    }

    public class CookCuisine
    {
        public int Id { get; set; }

        /// <summary>付けるバフの並び。1つずつ <c>[バフID, 値, …, 持続(ミリ秒)]</c>。</summary>
        public List<List<int>> BuffPar { get; set; } = null!;

        public int Description { get; set; }
    }
}
