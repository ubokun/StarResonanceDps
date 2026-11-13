// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class SeasonRankList : BlobType
{
    public Dictionary<uint, SeasonRankInfo>? SeasonRankList_;
    public uint? ShowArmbandId;

    public SeasonRankList()
    {
    }

    public SeasonRankList(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.SeasonRankList.SeasonRankList_FieldNumber:
                SeasonRankList_ = blob.ReadHashMap<uint, SeasonRankInfo>();
                return true;
            case Zproto.SeasonRankList.ShowArmbandIdFieldNumber:
                ShowArmbandId = blob.ReadUInt();
                return true;
            default:
                return false;
        }
    }
}
