// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonTimerInfo : BlobType
{
    public Zproto.EDungeonTimerType? TimerType;
    public int? StartTime;
    public int? DungeonTimes;
    public Zproto.EDungeonTimerDirection? Direction;
    public int? Index;
    public int? ChangeTime;
    public Zproto.EDungeonTimerEffectType? EffectType;
    public int? PauseTime;
    public int? PauseTotalTime;
    public Zproto.EDungeonTimerTimerLookType? OutLookType;

    public DungeonTimerInfo()
    {
    }

    public DungeonTimerInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonTimerInfo.TypeFieldNumber:
                TimerType = (Zproto.EDungeonTimerType)blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.StartTimeFieldNumber:
                StartTime = blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.DungeonTimesFieldNumber:
                DungeonTimes = blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.DirectionFieldNumber:
                Direction = (Zproto.EDungeonTimerDirection)blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.IndexFieldNumber:
                Index = blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.ChangeTimeFieldNumber:
                ChangeTime = blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.EffectTypeFieldNumber:
                EffectType = (Zproto.EDungeonTimerEffectType)blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.PauseTimeFieldNumber:
                PauseTime = blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.PauseTotalTimeFieldNumber:
                PauseTotalTime = blob.ReadInt();
                return true;
            case Zproto.DungeonTimerInfo.OutLookTypeFieldNumber:
                OutLookType = (Zproto.EDungeonTimerTimerLookType)blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}
