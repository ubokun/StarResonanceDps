// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

public static class PlayerProfession
{
    /// <param name="spec">
    /// 変身している間の特化なら、職業IDに関係なく変身のキーにする。
    /// ミーンの変身は職業IDが 0 で届くので、職業IDだけでは変身と分からない。
    /// </param>
    public static string GetKey(int professionId, PlayerClassSpec spec = PlayerClassSpec.Unknown)
    {
        if (PlayerClassSpecResolver.IsTransformation(spec))
        {
            return "Transformation";
        }

        return professionId switch
        {
            1 => "Stormblade",
            2 => "FrostMage",
            3 => "FlameBerserker",
            4 => "WindKnight",
            5 => "VerdantOracle",
            9 => "HeavyGuardian",
            11 => "Marksman",
            12 => "ShieldKnight",
            13 => "SoulMusician",
            // 変身クラス。ゲーム側の ProfessionTable.ProfessionIcon が
            // 8/14/15 とも profession_horizontal_hud00 で同じなので、1つのキーに束ねる。
            8 or 14 or 15 => "Transformation",
            _ => "Unknown"
        };
    }
}
