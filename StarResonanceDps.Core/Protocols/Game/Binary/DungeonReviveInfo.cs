// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonReviveInfo(BlobReader blob) : BlobType(ref blob)
{
    public List<int>? ReviveIds;
    public Dictionary<int, int>? ReviveMap;
    
    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonReviveInfo.ReviveIdsFieldNumber:
                ReviveIds = blob.ReadList<int>();
                return true;
            case Zproto.DungeonReviveInfo.ReviveMapFieldNumber:
                ReviveMap = blob.ReadHashMap<int, int>();
                return true;
            default:
                return false;
        }
    }
}