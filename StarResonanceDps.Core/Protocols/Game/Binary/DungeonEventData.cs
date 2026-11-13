// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonEventData : BlobType
{
    public int? EventId;
    public int? StartTime;
    public Zproto.DungeonEventState? State;
    public Zproto.DungeonEventResult? Result;
    public Dictionary<int, DungeonTargetData>? DungeonTarget;

    public DungeonEventData()
    {
    }

    public DungeonEventData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonEventData.EventIdFieldNumber:
                EventId = blob.ReadInt();
                return true;
            case Zproto.DungeonEventData.StartTimeFieldNumber:
                StartTime = blob.ReadInt();
                return true;
            case Zproto.DungeonEventData.StateFieldNumber:
                State = (Zproto.DungeonEventState)blob.ReadInt();
                return true;
            case Zproto.DungeonEventData.ResultFieldNumber:
                Result = (Zproto.DungeonEventResult)blob.ReadInt();
                return true;
            case Zproto.DungeonEventData.DungeonTargetFieldNumber:
                DungeonTarget = blob.ReadHashMap<int, DungeonTargetData>();
                return true;
            default:
                return false;
        }
    }
}
