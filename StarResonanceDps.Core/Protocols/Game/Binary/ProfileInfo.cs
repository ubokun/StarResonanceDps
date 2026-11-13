// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class ProfileInfo : BlobType
{
    public int? ProfileId;
    public string? ProfileUrl;
    public string? HalfBodyUrl;

    public ProfileInfo()
    {
    }

    public ProfileInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ProfileInfo.ProfileIdFieldNumber:
                ProfileId = blob.ReadInt();
                return true;
            case Zproto.ProfileInfo.ProfileUrlFieldNumber:
                ProfileUrl = blob.ReadString();
                return true;
            case Zproto.ProfileInfo.HalfBodyUrlFieldNumber:
                HalfBodyUrl = blob.ReadString();
                return true;
            default:
                return false;
        }
    }
}
