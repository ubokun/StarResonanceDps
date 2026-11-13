// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>モジュールのリンク数(<c>Item.modPartLevel</c>)の差分。</summary>
public class ModPartLevel : BlobType
{
    public override string? DebugName => "ModPartLevel";

    /// <summary>効果の番号 → リンク数。</summary>
    public BlobHashMapDelta<int, int>? LevelChanges;

    public ModPartLevel()
    {
    }

    public ModPartLevel(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.ModPartLevel.LevelsFieldNumber:
                LevelChanges = blob.ReadHashMapDelta<int, int>();
                return true;
            default:
                return false;
        }
    }
}
