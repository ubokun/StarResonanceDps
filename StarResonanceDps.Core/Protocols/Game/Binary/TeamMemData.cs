// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class TeamMemData : BlobType
{
    public long? CharId;
    public uint? EnterTime;
    public int? CallStatus;
    public int? TalentId;
    public int? OnlineStatus;
    public int? SceneId;
    public bool? VoiceIsOpen;
    public int? GroupId;
    public TeamMemberSocialData? SocialData;

    public TeamMemData()
    {
    }

    public TeamMemData(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.TeamMemData.CharIdFieldNumber:
                CharId = blob.ReadLong();
                return true;
            case Zproto.TeamMemData.EnterTimeFieldNumber:
                EnterTime = blob.ReadUInt();
                return true;
            case Zproto.TeamMemData.CallStatusFieldNumber:
                CallStatus = blob.ReadInt();
                return true;
            case Zproto.TeamMemData.TalentIdFieldNumber:
                TalentId = blob.ReadInt();
                return true;
            case Zproto.TeamMemData.OnlineStatusFieldNumber:
                OnlineStatus = blob.ReadInt();
                return true;
            case Zproto.TeamMemData.SceneIdFieldNumber:
                SceneId = blob.ReadInt();
                return true;
            case Zproto.TeamMemData.VoiceIsOpenFieldNumber:
                return false;
            case Zproto.TeamMemData.GroupIdFieldNumber:
                GroupId = blob.ReadInt();
                return true;
            case Zproto.TeamMemData.SocialDataFieldNumber:
                SocialData = new(blob);
                return true;
            default:
                return false;
        }
    }
}
