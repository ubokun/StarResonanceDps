// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonPioneer : BlobType
{
    public Dictionary<int, CompletedTargetInfo>? CompletedTargetThisTime;

    public DungeonPioneer()
    {
    }

    public DungeonPioneer(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonPioneer.CompletedTargetThisTimeFieldNumber:
                CompletedTargetThisTime = blob.ReadHashMap<int, CompletedTargetInfo>();
                return true;
            default:
                return false;
        }
    }
}