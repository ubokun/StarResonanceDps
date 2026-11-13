// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class CharTeam : BlobType
{
    public long? TeamId;
    public long? LeaderId;
    public uint? TeamTargetId;
    public uint? TeamNum;
    public List<long>? CharIds;
    public bool? IsMatching;
    public int? CharTeamVersion;
    public Dictionary<long, TeamMemData> TeamMemberData = null!;

    public CharTeam()
    {
    }

    public CharTeam(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CharTeam.TeamIdFieldNumber:
                TeamId = blob.ReadLong();
                return true;
            case Zproto.CharTeam.LeaderIdFieldNumber:
                LeaderId = blob.ReadLong();
                return true;
            case Zproto.CharTeam.TeamTargetIdFieldNumber:
                TeamTargetId = blob.ReadUInt();
                return true;
            case Zproto.CharTeam.TeamNumFieldNumber:
                TeamNum = blob.ReadUInt();
                return true;
            case Zproto.CharTeam.CharIdsFieldNumber:
                CharIds = blob.ReadList<long>();
                return true;
            case Zproto.CharTeam.IsMatchingFieldNumber:
                return false;
            case Zproto.CharTeam.CharTeamVersionFieldNumber:
                CharTeamVersion = blob.ReadInt();
                return true;
            case Zproto.CharTeam.TeamMemberDataFieldNumber:
                TeamMemberData = blob.ReadHashMap<long, TeamMemData>();
                return true;
            default:
                return false;
        }
    }
}
