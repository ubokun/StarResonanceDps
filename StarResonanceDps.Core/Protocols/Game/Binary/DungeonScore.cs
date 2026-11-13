// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Zproto;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonScore(BlobReader blob) : BlobType(ref blob)
{
    public int? TotalScore;
    public int? CurRatio;

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.DungeonScore.TotalScoreFieldNumber:
                TotalScore = blob.ReadInt();
                return true;
            case Zproto.DungeonScore.CurRatioFieldNumber:
                CurRatio = blob.ReadInt();
                return true;
            default:
                return false;
        }
    }
}