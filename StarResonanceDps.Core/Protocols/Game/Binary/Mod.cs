// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>モジュール(<c>CharSerialize.mod</c>)の差分。使うのは装着中の枠の変化で、ほかの項目は読み進めるためだけに読む。</summary>
public class Mod : BlobType
{
    public override string? DebugName => "Mod";

    /// <summary>枠の番号 → 装着しているモジュールの uuid。</summary>
    public BlobHashMapDelta<int, long>? SlotChanges;

    public Mod()
    {
    }

    public Mod(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.Mod.ModSlotsFieldNumber:
                SlotChanges = blob.ReadHashMapDelta<int, long>();
                return true;
            case Zproto.Mod.ModInfosFieldNumber:
                blob.ReadHashMapDelta<long, SkippedBlob>();
                return true;
            default:
                return false;
        }
    }
}
