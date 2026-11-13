// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonFlowInfo(BlobReader blob) : BlobType(ref blob)
{
    public Zproto.EDungeonState? State;
    public int? ActiveTime;
    public int? ReadyTime;
    public int? PlayTime;
    public int? EndTime;
    public int? SettlementTime;
    public int? DungeonTimes;
    public int? Result;

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonFlowInfo.StateFieldNumber:
                State = (Zproto.EDungeonState)blob.ReadInt();
                return true;
            case Zproto.DungeonFlowInfo.ActiveTimeFieldNumber:
                ActiveTime = blob.ReadInt();
                return true;
            case Zproto.DungeonFlowInfo.ReadyTimeFieldNumber:
                ReadyTime = blob.ReadInt();
                return true;
            case Zproto.DungeonFlowInfo.PlayTimeFieldNumber:
                PlayTime = blob.ReadInt();
                return true;
            case Zproto.DungeonFlowInfo.EndTimeFieldNumber:
                EndTime = blob.ReadInt();
                return true;
            case Zproto.DungeonFlowInfo.SettlementTimeFieldNumber:
                SettlementTime = blob.ReadInt();
                return true;
            case Zproto.DungeonFlowInfo.DungeonTimesFieldNumber:
                DungeonTimes = blob.ReadInt();
                return true;
            case Zproto.DungeonFlowInfo.ResultFieldNumber:
                Result = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}