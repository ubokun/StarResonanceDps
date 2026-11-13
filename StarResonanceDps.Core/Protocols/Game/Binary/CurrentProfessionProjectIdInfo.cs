// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public sealed class CurrentProfessionProjectIdInfo : BlobType
{
    public int? CurrentProfessionProjectId;

    public CurrentProfessionProjectIdInfo()
    {
    }

    public CurrentProfessionProjectIdInfo(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        if (index != Zproto.CurrentProfessionProjectIdInfo.CurrentProfessionProjectIdFieldNumber)
        {
            return false;
        }

        CurrentProfessionProjectId = blob.ReadInt();
        return true;
    }
}
