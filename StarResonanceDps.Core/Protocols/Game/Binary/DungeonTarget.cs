// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class DungeonTarget : BlobType
{
    /// <summary>目標の番号 → 目標の差分(足す・消す・更新)。</summary>
    public BlobHashMapDelta<int, DungeonTargetData>? TargetChanges;

    public DungeonTarget()
    {
    }

    public DungeonTarget(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index) {
            case Zproto.DungeonTarget.TargetDataFieldNumber:
                TargetChanges = blob.ReadHashMapDelta<int, DungeonTargetData>();
                return true;
            default:
                return false;
        }
    }
}
