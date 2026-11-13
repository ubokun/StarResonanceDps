// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>モジュールの効果(<c>Item.modNewAttr</c>)の差分。使うのは効果の番号の並びで、ほかの項目は読み進めるためだけに読む。</summary>
public class ModNewAttr : BlobType
{
    public override string? DebugName => "ModNewAttr";

    /// <summary>効果の番号の並び。届いたときは並び全体。</summary>
    public List<int>? ModParts;

    public ModNewAttr()
    {
    }

    public ModNewAttr(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ModNewAttr.ModPartsFieldNumber:
                ModParts = blob.ReadList<int>();
                return true;
            case Zproto.ModNewAttr.UpgradeRecordsFieldNumber:
                blob.ReadList<SkippedBlob>();
                return true;
            default:
                return false;
        }
    }
}
