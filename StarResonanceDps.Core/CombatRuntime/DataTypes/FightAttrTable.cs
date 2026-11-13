// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.Frozen;
using System.Collections.Generic;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class FightAttrTable
    {
        public Dictionary<string, FightAttr> Data = new();

        /// <summary>
        /// 能力値の属性番号。<c>IsClass</c> が真の行の番号と、その派生(最終・合計・加算・追加・%・追加%)の番号。
        /// <c>IsClass</c> が偽の行(HP・シーズン階級・スタミナなどの状態の値)は入れない。
        /// </summary>
        public FrozenSet<int> StatAttrIds = FrozenSet<int>.Empty;
    }

    public class FightAttr
    {
        public int Id { get; set; }

        public bool IsClass { get; set; }

        public int AttrFinal { get; set; }

        public int AttrTotal { get; set; }

        public int AttrAdd { get; set; }

        public int AttrExAdd { get; set; }

        public int AttrPer { get; set; }

        public int AttrExPer { get; set; }
    }
}
