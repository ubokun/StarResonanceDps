// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static StarResonanceDps.Core.CombatRuntime.DataTypes.Enums.Professions;

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public static class Professions
    {
        public static int GetProfessionIdFromSubProfessionId(int subProfessionId) => subProfessionId switch
        {
            (int)SubProfessionId.SubProfession_Unknown => (int)EProfessionId.Profession_Unknown,
            (int)SubProfessionId.SubProfession_Iaido => (int)EProfessionId.Profession_Stormblade,
            (int)SubProfessionId.SubProfession_Moonstrike => (int)EProfessionId.Profession_Stormblade,
            (int)SubProfessionId.SubProfession_Icicle => (int)EProfessionId.Profession_FrostMage,
            (int)SubProfessionId.SubProfession_Frostbeam => (int)EProfessionId.Profession_FrostMage,
            (int)SubProfessionId.SubProfession_FormlessExpertise => (int)EProfessionId.Profession_TwinStriker,
            (int)SubProfessionId.SubProfession_CrimsonExpertise => (int)EProfessionId.Profession_TwinStriker,
            (int)SubProfessionId.SubProfession_Vanguard => (int)EProfessionId.Profession_WindKnight,
            (int)SubProfessionId.SubProfession_Skyward => (int)EProfessionId.Profession_WindKnight,
            (int)SubProfessionId.SubProfession_Smite => (int)EProfessionId.Profession_VerdantOracle,
            (int)SubProfessionId.SubProfession_Lifebind => (int)EProfessionId.Profession_VerdantOracle,
            (int)SubProfessionId.SubProfession_Earthfort => (int)EProfessionId.Profession_HeavyGuardian,
            (int)SubProfessionId.SubProfession_Block => (int)EProfessionId.Profession_HeavyGuardian,
            (int)SubProfessionId.SubProfession_Wildpack => (int)EProfessionId.Profession_Marksman,
            (int)SubProfessionId.SubProfession_Falconry => (int)EProfessionId.Profession_Marksman,
            (int)SubProfessionId.SubProfession_Recovery => (int)EProfessionId.Profession_ShieldKnight,
            (int)SubProfessionId.SubProfession_Shield => (int)EProfessionId.Profession_ShieldKnight,
            (int)SubProfessionId.SubProfession_Dissonance => (int)EProfessionId.Profession_BeatPerformer,
            (int)SubProfessionId.SubProfession_Concerto => (int)EProfessionId.Profession_BeatPerformer,
            _ => (int)EProfessionId.Profession_Unknown
        };

        public static int GetTalentIdFromSubProfessionId(int subProfessionId) => subProfessionId switch
        {
            (int)SubProfessionId.SubProfession_Unknown => (int)ETalentId.Unknown,
            (int)EProfessionId.Profession_Stormblade => (int)ETalentId.Profession_Stormblade,
            (int)SubProfessionId.SubProfession_Iaido => (int)ETalentId.SubProfession_Iaido,
            (int)SubProfessionId.SubProfession_Moonstrike => (int)ETalentId.SubProfession_Moonstrike,
            (int)EProfessionId.Profession_FrostMage => (int)ETalentId.Profession_FrostMage,
            (int)SubProfessionId.SubProfession_Icicle => (int)ETalentId.SubProfession_Icicle,
            (int)SubProfessionId.SubProfession_Frostbeam => (int)ETalentId.SubProfession_Frostbeam,
            (int)SubProfessionId.SubProfession_FormlessExpertise => (int)ETalentId.SubProfession_FormlessExpertise,
            (int)SubProfessionId.SubProfession_CrimsonExpertise => (int)ETalentId.SubProfession_CrimsonExpertise,
            (int)EProfessionId.Profession_WindKnight => (int)ETalentId.Profession_WindKnight,
            (int)SubProfessionId.SubProfession_Vanguard => (int)ETalentId.SubProfession_Vanguard,
            (int)SubProfessionId.SubProfession_Skyward => (int)ETalentId.SubProfession_Skyward,
            (int)EProfessionId.Profession_VerdantOracle => (int)ETalentId.Profession_VerdantOracle,
            (int)SubProfessionId.SubProfession_Smite => (int)ETalentId.SubProfession_Smite,
            (int)SubProfessionId.SubProfession_Lifebind => (int)ETalentId.SubProfession_Lifebind,
            (int)EProfessionId.Profession_HeavyGuardian => (int)ETalentId.Profession_HeavyGuardian,
            (int)SubProfessionId.SubProfession_Earthfort => (int)ETalentId.SubProfession_Earthfort,
            (int)SubProfessionId.SubProfession_Block => (int)ETalentId.SubProfession_Block,
            (int)EProfessionId.Profession_Marksman => (int)ETalentId.Profession_Marksman,
            (int)SubProfessionId.SubProfession_Wildpack => (int)ETalentId.SubProfession_Wildpack,
            (int)SubProfessionId.SubProfession_Falconry => (int)ETalentId.SubProfession_Falconry,
            (int)EProfessionId.Profession_ShieldKnight => (int)ETalentId.Profession_ShieldKnight,
            (int)SubProfessionId.SubProfession_Recovery => (int)ETalentId.SubProfession_Recovery,
            (int)SubProfessionId.SubProfession_Shield => (int)ETalentId.SubProfession_Shield,
            (int)EProfessionId.Profession_BeatPerformer => (int)ETalentId.Profession_BeatPerformer,
            (int)SubProfessionId.SubProfession_Dissonance => (int)ETalentId.SubProfession_Dissonance,
            (int)SubProfessionId.SubProfession_Concerto => (int)ETalentId.SubProfession_Concerto,
            _ => (int)ETalentId.Unknown
        };

        public static int GetBaseProfessionIdBySkillId(int skillId) => skillId switch
        {
            1701 or 1705 or 1713 or 1714 or 1715 or 1716 or 1717 or 1718 or 1719 or 1720 or 1724 or 1728 or 1730 or 1731 => 1,
            1201 or 1210 or 1211 or 1239 or 1240 or 1241 or 1242 or 1243 or 1244 or 1245 or 1246 or 1248 => 2,
            1601 or 1605 or 1606 or 1607 or 1608 or 1609 or 1610 or 1611 or 1612 or 1613 or 1614 or 1615 or 1616 or 1617 or 1618 or 1621 => 3,
            1401 or 1410 or 1418 or 1419 or 1420 or 1421 or 1422 or 1423 or 1424 or 1425 or 1426 or 1430 or 1431 => 4,
            1501 or 1507 or 1509 or 1518 or 1519 or 1520 or 1521 or 1522 or 1523 or 1524 or 1527 or 1528 or 1529 or 1531 => 5,
            1901 or 1907 or 1917 or 1922 or 1923 or 1924 or 1925 or 1926 or 1927 or 1930 or 1932 or 1936 or 1937 or 1938 or 1940 => 9,
            2201 or 2209 or 2220 or 2222 or 2230 or 2231 or 2232 or 2233 or 2234 or 2235 or 2237 or 2238 => 11,
            2401 or 2405 or 2406 or 2407 or 2408 or 2409 or 2410 or 2412 or 2414 or 2415 or 2419 or 2420 or 2421 => 12,
            2301 or 2306 or 2307 or 2308 or 2309 or 2310 or 2311 or 2312 or 2313 or 2314 or 2315 or 2316 or 2321 or 2335 or 2336 => 13,
            _ => 0
        };

        /// <summary>
        /// 特化マーカーバフから特化を引く。
        ///
        /// <para>
        /// 各特化のアビリティは、装着している間ずっと乗り続ける固有の常時バフを1つ付与する
        /// (TalentStageTable の RootId → TalentTable.TalentEffect の <c>[3, buffId, 1]</c>)。
        /// 持続時間は無期限(<c>DestroyParam: []</c>)なので、スキルを撃たなくても、
        /// 命中しなくても、戦闘していなくても特化が確定する。
        /// </para>
        ///
        /// <para>
        /// <b>スキルIDから特化を推定しないこと。</b> 各職とも特殊攻撃を置換するのは片方の特化だけで、
        /// もう片方(雷刃・氷牙・双炎・烈風・威咲・剛身・狼弓・光砕・狂音)は素のスキルIDのまま
        /// アビリティ未装着と区別がつかない。マーカーバフは「装着しているか」そのものを表す。
        /// </para>
        ///
        /// <para>シーン切替で一度削除され、新シーンで再付与される(<c>DeleteChangeScene: true</c>)。</para>
        /// </summary>
        public static SubProfessionId GetSubProfessionIdBySpecMarkerBuffId(int buffId) => buffId switch
        {
            2200320 => SubProfessionId.SubProfession_Iaido,              // 雷刃型
            2200590 => SubProfessionId.SubProfession_Moonstrike,         // 月影型
            2204300 => SubProfessionId.SubProfession_Icicle,             // 氷牙型
            2204120 => SubProfessionId.SubProfession_Frostbeam,          // 霜天型
            2208130 => SubProfessionId.SubProfession_FormlessExpertise,  // 双炎型
            2208430 => SubProfessionId.SubProfession_CrimsonExpertise,   // 炎舞型
            2205300 => SubProfessionId.SubProfession_Vanguard,           // 烈風型
            2205290 => SubProfessionId.SubProfession_Skyward,            // 乱風型
            2202110 => SubProfessionId.SubProfession_Smite,              // 威咲型
            2202340 => SubProfessionId.SubProfession_Lifebind,           // 森癒型
            2201330 => SubProfessionId.SubProfession_Earthfort,          // 剛身型
            2201320 => SubProfessionId.SubProfession_Block,              // 剛守型
            2203260 => SubProfessionId.SubProfession_Wildpack,           // 狼弓型
            2203290 => SubProfessionId.SubProfession_Falconry,           // 鷹弓型
            2206090 => SubProfessionId.SubProfession_Recovery,           // 光砕型
            2206190 => SubProfessionId.SubProfession_Shield,             // 光盾型
            2207090 => SubProfessionId.SubProfession_Dissonance,         // 狂音型
            2207180 => SubProfessionId.SubProfession_Concerto,           // 響奏型
            _ => SubProfessionId.SubProfession_Unknown
        };

        public static string GetBaseProfessionMainStatName(int professionId)
        {
            switch (professionId)
            {
                case (int)EProfessionId.Profession_Stormblade:
                case (int)EProfessionId.Profession_Marksman:
                    {
                        return "Agility";
                    }
                case (int)EProfessionId.Profession_FrostMage:
                case (int)EProfessionId.Profession_VerdantOracle:
                case (int)EProfessionId.Profession_BeatPerformer:
                    {
                        return "Intellect";
                    }
                case (int)EProfessionId.Profession_TwinStriker:
                case (int)EProfessionId.Profession_WindKnight:
                case (int)EProfessionId.Profession_HeavyGuardian:
                case (int)EProfessionId.Profession_ShieldKnight:
                    {
                        return "Strength";
                    }
                default:
                    return "";
            }
        }

        public static ERoleType GetRoleFromBaseProfessionId(int professionId)
        {
            switch (professionId)
            {
                case (int)EProfessionId.Profession_Stormblade:
                case (int)EProfessionId.Profession_FrostMage:
                case (int)EProfessionId.Profession_TwinStriker:
                case (int)EProfessionId.Profession_WindKnight:
                case (int)EProfessionId.Profession_Marksman:
                    return ERoleType.DPS;
                case (int)EProfessionId.Profession_HeavyGuardian:
                case (int)EProfessionId.Profession_ShieldKnight:
                    return ERoleType.Tank;
                case (int)EProfessionId.Profession_VerdantOracle:
                case (int)EProfessionId.Profession_BeatPerformer:
                    return ERoleType.Healer;
                default:
                    return ERoleType.None;
            }
        }
    }
}
