// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonPlayerList : BlobType
{
    public Dictionary<uint, DungeonPlayerInfo> PlayerInfos = null!;

    public DungeonPlayerList()
    {
    }

    public DungeonPlayerList(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonPlayerList.PlayerInfosFieldNumber:
                PlayerInfos = blob.ReadHashMap<uint, DungeonPlayerInfo>();
                return true;
            default:
                return false;
        }
    }
}
