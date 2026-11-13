// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class SeasonRankData : BlobType
{
    public Dictionary<uint, uint>? SeasonRanks;

    public SeasonRankData()
    {
    }

    public SeasonRankData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.SeasonRankData.SeasonRanksFieldNumber:
                SeasonRanks = blob.ReadHashMap<uint, uint>();
                return true;
            default:
                return false;
        }
    }
}
