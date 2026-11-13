// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class TempAttrTable
    {
        public Dictionary<string, TempAttr> Data = new();
    }

    public class TempAttr
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Desc { get; set; } = null!;
        public int AttrType { get; set; }
        public int LogicType { get; set; }
        public List<int> AttrParams { get; set; } = null!;
        public int LowerLimit { get; set; }
        public int UpperLimit { get; set; }
        public bool IsSyncClient { get; set; }
        public string AttrDesc { get; set; } = null!;
        public string AttrIcon { get; set; } = null!;

        public Zproto.ETempAttrEffectType GetProtoAttrType()
        {
            return (Zproto.ETempAttrEffectType)AttrType;
        }

        public Zproto.ETempAttrType GetProtoLogicType()
        {
            return (Zproto.ETempAttrType)LogicType;
        }
    }
}
