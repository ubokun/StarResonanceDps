// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class SeasonRankInfo : BlobType
{
    public uint? CurRankStar;
    public List<uint>? ReceivedRankStar;

    public SeasonRankInfo()
    {
    }

    public SeasonRankInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.SeasonRankInfo.CurRanKStarFieldNumber:
                CurRankStar = blob.ReadUInt();
                return true;
            case Zproto.SeasonRankInfo.ReceivedRankStarFieldNumber:
                ReceivedRankStar = blob.ReadList<uint>();
                return true;
            default:
                return false;
        }
    }
}
