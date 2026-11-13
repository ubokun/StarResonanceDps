// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonPlayerInfo : BlobType
{
    public long? CharId;
    public SocialData? SocialData;

    public DungeonPlayerInfo()
    {
    }

    public DungeonPlayerInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonPlayerInfo.CharIdFieldNumber:
                CharId = blob.ReadLong();
                return true;
            case Zproto.DungeonPlayerInfo.SocialDataFieldNumber:
                SocialData = new(blob);
                return true;
            default:
                return false;
        }
    }
}
