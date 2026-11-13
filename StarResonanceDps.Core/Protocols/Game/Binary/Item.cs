// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>
/// アイテム1つ。追加では丸ごと、更新では変わった項目だけが届く(届かなかった項目は <c>null</c>)。
/// 使うのは uuid・アイテムID・品質・装備の値・モジュールの効果とリンク数で、ほかの項目は読み進めるためだけに読む。
/// </summary>
public class Item : BlobType
{
    public override string? DebugName => "Item";

    public long? Uuid;
    public int? ConfigId;
    public int? Quality;
    public EquipAttr? EquipAttr;
    public ModNewAttr? ModNewAttr;
    public ModPartLevel? ModPartLevel;

    public Item()
    {
    }

    public Item(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.Item.UuidFieldNumber:
                Uuid = blob.ReadLong();
                return true;
            case Zproto.Item.ConfigIdFieldNumber:
                ConfigId = blob.ReadInt();
                return true;
            case Zproto.Item.EquipAttrFieldNumber:
                EquipAttr = new(blob);
                return true;
            case Zproto.Item.ModNewAttrFieldNumber:
                ModNewAttr = new(blob);
                return true;
            case Zproto.Item.ModPartLevelFieldNumber:
                ModPartLevel = new(blob);
                return true;
            case Zproto.Item.QualityFieldNumber:
                Quality = blob.ReadInt();
                return true;
            case Zproto.Item.CountFieldNumber:
            case Zproto.Item.CreateTimeFieldNumber:
            case Zproto.Item.ExpireTimeFieldNumber:
            case Zproto.Item.CoolDownExpireTimeFieldNumber:
                blob.ReadLong();
                return true;
            case Zproto.Item.InvalidFieldNumber:
            case Zproto.Item.BindFlagFieldNumber:
            case Zproto.Item.OptSrcFieldNumber:
            case Zproto.Item.RewardIdFieldNumber:
            case Zproto.Item.GeneSourceFieldNumber:
                blob.ReadInt();
                return true;
            case Zproto.Item.LockedFieldNumber:
            case Zproto.Item.StarMarkFieldNumber:
                blob.ReadByte();
                return true;
            case Zproto.Item.ExtendAttrFieldNumber:
                blob.ReadHashMapDelta<int, SkippedBlob>();
                return true;
            case Zproto.Item.GeneSequenceFieldNumber:
            case Zproto.Item.SelectItemsFieldNumber:
                blob.ReadHashMapDelta<int, int>();
                return true;
            default:
                return false;
        }
    }
}
