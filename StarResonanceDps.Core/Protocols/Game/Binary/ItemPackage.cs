// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>持ち物(<c>CharSerialize.itemPackage</c>)の差分。使うのは袋ごとの変化で、ほかの項目は読み進めるためだけに読む。</summary>
public class ItemPackage : BlobType
{
    public override string? DebugName => "ItemPackage";

    /// <summary>袋の番号 → 袋の差分。</summary>
    public BlobHashMapDelta<int, Package>? PackageChanges;

    public ItemPackage()
    {
    }

    public ItemPackage(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ItemPackage.PackagesFieldNumber:
                PackageChanges = blob.ReadHashMapDelta<int, Package>();
                return true;
            case Zproto.ItemPackage.UnlockItemsFieldNumber:
                blob.ReadHashMapDelta<int, int>();
                return true;
            case Zproto.ItemPackage.QuickBarFieldNumber:
            case Zproto.ItemPackage.ItemUuidFieldNumber:
                blob.ReadInt();
                return true;
            case Zproto.ItemPackage.UseGroupCdFieldNumber:
                blob.ReadHashMapDelta<int, long>();
                return true;
            default:
                return false;
        }
    }
}
