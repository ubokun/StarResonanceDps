// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class ProfileImageTable
    {
        public Dictionary<string, ProfileImage> Data = [];
    }

    /// <summary>プロフィールの絵の表の1行。種類は 1 がアイコン(ゲームの用意した顔の絵)、2 がアイコンの枠、3 が名刺のスタイルなど。</summary>
    public class ProfileImage
    {
        public int Id { get; set; }
        public int Type { get; set; }

        /// <summary>絵の全体のパス(表の値のまま)。</summary>
        public string Image { get; set; } = string.Empty;
    }
}
