// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class FightPoint : BlobType
{
    public int? TotalFightPoint;
    public Dictionary<int, FightPointData> FightPointData = null!;

    public FightPoint()
    {
    }

    public FightPoint(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.FightPoint.TotalFightPointFieldNumber:
                TotalFightPoint = blob.ReadInt();
                return true;
            case Zproto.FightPoint.FightPointDataFieldNumber:
                FightPointData = blob.ReadHashMap<int, FightPointData>();
                return true;
            default:
                return false;
        }
    }
}