// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class SeasonRoleLevelData : BlobType
{
    public Dictionary<int, SeasonRoleLevel>? SeasonRoleLevelMap;

    public SeasonRoleLevelData()
    {
    }

    public SeasonRoleLevelData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.SeasonRoleLevelData.SeasonRoleLevelMapFieldNumber:
                SeasonRoleLevelMap = blob.ReadHashMap<int, SeasonRoleLevel>();
                return true;
            default:
                return false;
        }
    }
}
