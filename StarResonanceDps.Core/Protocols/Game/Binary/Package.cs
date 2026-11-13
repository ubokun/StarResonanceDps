// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>持ち物の袋1つの差分。使うのはアイテムの変化で、ほかの項目は読み進めるためだけに読む。</summary>
public class Package : BlobType
{
    public override string? DebugName => "Package";

    /// <summary>アイテムの uuid → アイテム(追加は丸ごと、更新は変わった項目だけ)。</summary>
    public BlobHashMapDelta<long, Item>? ItemChanges;

    public Package()
    {
    }

    public Package(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.Package.TypeFieldNumber:
            case Zproto.Package.MaxCapacityFieldNumber:
            case Zproto.Package.ChangeVersionFieldNumber:
                blob.ReadInt();
                return true;
            case Zproto.Package.ItemCdFieldNumber:
                blob.ReadHashMapDelta<int, long>();
                return true;
            case Zproto.Package.ItemsFieldNumber:
                ItemChanges = blob.ReadHashMapDelta<long, Item>();
                return true;
            case Zproto.Package.PublicCdFieldNumber:
                blob.ReadLong();
                return true;
            default:
                return false;
        }
    }
}
