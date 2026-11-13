// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.Protocols.Game.Binary;

/// <summary>
/// 中身を使わない入れ子。map の値など、読み進めるためだけに読む。
/// 項目は1つも解釈しないので、値は入れ子の長さで丸ごと飛ぶ。
/// </summary>
public class SkippedBlob : BlobType
{
    public SkippedBlob()
    {
    }

    public SkippedBlob(BlobReader blob) : base(ref blob)
    {
    }

    public override bool ParseField(int index, ref BlobReader blob)
    {
        return false;
    }
}
