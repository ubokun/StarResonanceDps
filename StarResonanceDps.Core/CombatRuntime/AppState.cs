// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Serilog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class AppState
    {
        public static long PlayerUUID { get; set; }
        public static long PlayerUID { get; set; }
        public static string AccountId { get; set; } = null!;
        public static string PlayerName { get; set; } = null!;
        public static int ProfessionId { get; set; }

        public static int PlayerMeterPlacement { get; set; }

        public static ulong PlayerTotalMeterValue { get; set; }

        public static bool MousePassthrough { get; set; } = false;

        public static bool IsUpdateAvailable { get; set; } = false;

        public static bool IsChatEnabled = true;

        public static long PartyTeamId = 0;

        public static Encounter? OpenedHistoricalEncounter = null;

        public static void LoadDataTables()
        {
            System.Diagnostics.Stopwatch loadTime = new();
            loadTime.Start();

            // 先に全部のファイルを同時に解き始める。組み立ては下の今までの順番で、それぞれ解き終わるのを待って受け取る。
            var reads = new JsonDataFileReads();
            reads.Start<Dictionary<string, Monster>>(RawTablePath("MonsterTable.json"));
            reads.Start<Dictionary<string, Skill>>(RawTablePath("SkillTable.json"));
            reads.Start<Dictionary<string, SkillFightLevel>>(RawTablePath("SkillFightLevelTable.json"));
            reads.Start<Dictionary<string, Buff>>(RawTablePath("BuffTable.json"));
            reads.Start<Dictionary<string, ProfessionSystem>>(RawTablePath("ProfessionSystemTable.json"));
            reads.Start<Dictionary<string, SkillAoyi>>(RawTablePath("SkillAoyiTable.json"));
            reads.Start<Dictionary<string, SkillAoyiStar>>(RawTablePath("SkillAoyiStarTable.json"));
            reads.Start<Dictionary<string, Buff>>(Path.Combine(CombatRuntimePaths.DataDirectory, BuffOverridesRelativePath));
            reads.Start<Dictionary<string, SkillLevelGroupOverride>>(Path.Combine(CombatRuntimePaths.OverridesDirectory, "SkillOverrides.json"));
            CombatDataCatalog.StartReads(reads);
            reads.Start<Dictionary<string, SceneEventDungeonConfig>>(RawTablePath("SceneEventDuneonConfigTable.json"));
            reads.Start<Dictionary<string, TargetRow>>(RawTablePath("TargetTable.json"));
            reads.Start<Dictionary<string, TempAttr>>(RawTablePath("TempAttrTable.json"));
            reads.Start<Dictionary<string, ChatSticker>>(RawTablePath("ChatStickersTable.json"));
            reads.Start<Dictionary<string, ProfileImage>>(RawTablePath("ProfileImageTable.json"));
            reads.Start<Dictionary<string, CookCuisine>>(RawTablePath("CookCuisineTable.json"));
            reads.Start<Dictionary<string, FightAttr>>(RawTablePath("FightAttrTable.json"));
            reads.Start<Dictionary<string, DamageAttr>>(RawTablePath("DamageAttrTable.json"));

            LoadRawTable<Monster>(reads, "MonsterTable.json", table => HelperMethods.DataTables.Monsters.Data = table);
            LoadRawTable<Skill>(reads, "SkillTable.json", table => HelperMethods.DataTables.Skills.Data = table);
            LoadRawTable<SkillFightLevel>(reads, "SkillFightLevelTable.json", table => HelperMethods.DataTables.SkillFightLevels.Data = table);
            LoadRawTable<Buff>(reads, "BuffTable.json", table => HelperMethods.DataTables.Buffs.Data = table);
            // CombatDataCatalog.Load が技のアイコンの枠を作るのに使う(変身クラスの技の番号)。
            LoadRawTable<ProfessionSystem>(reads, "ProfessionSystemTable.json", table => HelperMethods.DataTables.ProfessionSystems.Data = table);
            // CombatDataCatalog.Load がイマジンのパッシブの根のバフを引けるようにするのに使う(バフの由来の名前)。
            LoadRawTable<SkillAoyi>(reads, "SkillAoyiTable.json", table => HelperMethods.DataTables.SkillAoyis.Data = table);
            LoadRawTable<SkillAoyiStar>(reads, "SkillAoyiStarTable.json", table => HelperMethods.DataTables.SkillAoyiStars.Data = table);

            LoadBuffOverridesTable(reads);
            LoadSkillOverridesTable(reads);
            CombatDataCatalog.Load(reads);

            LoadRawTable<SceneEventDungeonConfig>(reads, "SceneEventDuneonConfigTable.json", table => HelperMethods.DataTables.SceneEventDungeonConfigs.Data = table);
            LoadRawTable<TargetRow>(reads, "TargetTable.json", table => HelperMethods.DataTables.Targets.Data = table);
            LoadRawTable<TempAttr>(reads, "TempAttrTable.json", table => HelperMethods.DataTables.TempAttrs.Data = table);
            LoadRawTable<ChatSticker>(reads, "ChatStickersTable.json", table => HelperMethods.DataTables.ChatStickers.Data = table);
            LoadRawTable<ProfileImage>(reads, "ProfileImageTable.json", table => HelperMethods.DataTables.ProfileImages.Data = table);
            LoadRawTable<DamageAttr>(reads, "DamageAttrTable.json", table => HelperMethods.DataTables.DamageAttrs.Load(table));

            LoadCookCuisineTable(reads);
            LoadFightAttrTable(reads);
            reads.LogUntaken();

            var startupTime = loadTime.Elapsed.TotalSeconds;
            Serilog.Log.Debug($"Took {Math.Round(startupTime, 4)}s to load DataTables.");

            loadTime.Stop();
        }

        private const string BuffOverridesRelativePath = @"Overrides\BuffOverrides.json";

        private static string RawTablePath(string fileName) => Path.Combine(CombatRuntimePaths.RawTableDirectory, fileName);

        /// <summary>
        /// <c>Data/Raw</c> の生テーブルを1つ受け取り、<paramref name="assign"/> で置き場へ入れる。
        /// 無ければ、その表を引く機能が空のまま動くので、黙って飛ばさずエラーを出す。
        /// </summary>
        private static void LoadRawTable<T>(JsonDataFileReads reads, string fileName, Action<Dictionary<string, T>> assign)
        {
            string path = RawTablePath(fileName);
            if (!reads.TryTake<Dictionary<string, T>>(path, out var table))
            {
                Log.Error("{FileName} is missing. Lookups that use this table will be empty path={Path}", fileName, path);
                return;
            }

            assign(table!);
            Log.Information("Loaded {FileName}", fileName);
        }

        /// <summary>
        /// 料理の表を読み、回復の料理が付けるバフと1回の回復量を引けるようにする。
        /// 表が無いと料理の回復を HPS に数えられないので、黙って空にせずエラーを出す。
        /// </summary>
        private static void LoadCookCuisineTable(JsonDataFileReads reads)
        {
            string cookCuisineTableFile = RawTablePath("CookCuisineTable.json");
            if (!reads.TryTake<Dictionary<string, CookCuisine>>(cookCuisineTableFile, out var loadedCookCuisines))
            {
                Log.Error("CookCuisineTable.json is missing. Cuisine healing cannot be counted in HPS path={Path}", cookCuisineTableFile);
                return;
            }

            var cookCuisines = loadedCookCuisines!;
            HelperMethods.DataTables.CookCuisines.Data = cookCuisines;

            var amounts = new Dictionary<int, int>();
            var conflicts = new HashSet<int>();
            foreach (var cuisine in cookCuisines.Values)
            {
                if (cuisine.Description != CookCuisineTable.HealthRegenDescriptionId)
                {
                    continue;
                }

                foreach (var buffPar in cuisine.BuffPar)
                {
                    if (buffPar.Count < 2)
                    {
                        Log.Error("CookCuisineTable.json: healing cuisine {Id} has too few BuffPar entries ({Count})", cuisine.Id, buffPar.Count);
                        continue;
                    }

                    if (amounts.TryGetValue(buffPar[0], out var existing) && existing != buffPar[1])
                    {
                        conflicts.Add(buffPar[0]);
                    }

                    amounts[buffPar[0]] = buffPar[1];
                }
            }

            foreach (var buffId in conflicts)
            {
                Log.Error("CookCuisineTable.json: healing buff {BuffId} has rows with different amounts. This buff is not counted", buffId);
                amounts.Remove(buffId);
            }

            HelperMethods.DataTables.CookCuisines.RegenAmountsByBuffId = amounts.ToFrozenDictionary();
            Log.Information("Loaded CookCuisineTable.json");
        }

        /// <summary>
        /// 能力値の表を読み、能力値の属性番号を引けるようにする。
        /// 表が無いと入場のときに残った能力値を 0 に戻せないので、黙って空にせずエラーを出す。
        /// </summary>
        private static void LoadFightAttrTable(JsonDataFileReads reads)
        {
            string fightAttrTableFile = RawTablePath("FightAttrTable.json");
            if (!reads.TryTake<Dictionary<string, FightAttr>>(fightAttrTableFile, out var loadedFightAttrs))
            {
                Log.Error("FightAttrTable.json is missing. Stats left over from the previous scene are not reset on EnterScene path={Path}", fightAttrTableFile);
                return;
            }

            var fightAttrs = loadedFightAttrs!;
            HelperMethods.DataTables.FightAttrs.Data = fightAttrs;

            var statAttrIds = new HashSet<int>();
            foreach (var fightAttr in fightAttrs.Values)
            {
                if (!fightAttr.IsClass)
                {
                    continue;
                }

                foreach (var attrId in new[]
                {
                    fightAttr.Id,
                    fightAttr.AttrFinal,
                    fightAttr.AttrTotal,
                    fightAttr.AttrAdd,
                    fightAttr.AttrExAdd,
                    fightAttr.AttrPer,
                    fightAttr.AttrExPer,
                })
                {
                    if (attrId != 0)
                    {
                        statAttrIds.Add(attrId);
                    }
                }
            }

            HelperMethods.DataTables.FightAttrs.StatAttrIds = statAttrIds.ToFrozenSet();
            Log.Information("Loaded FightAttrTable.json");
        }

        /// <summary>
        /// 生の <c>SkillTable</c> が持たない <c>SkillLevelGroup</c> を補う。スキル枠のバッジの帰属が、
        /// 付与元の技から装備中のイマジン・ロールスキルへ辿るのに使う。
        /// 推移グラフの技のアイコンの枠(<c>CombatDataCatalog.GetSkillIconFrame</c>)も、この寄せ先で決まる
        /// (枠の番号を持たない技は <c>SkillLevelGroup</c> の先の技の枠になる)。
        ///
        /// <para>
        /// 書くのは <c>SkillLevelGroup</c> だけ。表に無いキーは、<c>BuffOverrides</c> と同じく行を作ってから当てる。
        /// 作る行は ID と <c>SkillLevelGroup</c> だけを持ち、文字列の項目は空にする。
        /// ファイルが無ければエラーログを出して上書き無しのまま続け、<c>SkillLevelGroup</c> が書かれていない項目は当てない。
        /// </para>
        /// </summary>
        private static void LoadSkillOverridesTable(JsonDataFileReads reads)
        {
            const string relativePath = "Overrides/SkillOverrides.json";
            var overridePath = Path.Combine(CombatRuntimePaths.OverridesDirectory, "SkillOverrides.json");
            if (!reads.TryTake<Dictionary<string, SkillLevelGroupOverride>>(overridePath, out var loadedOverrides))
            {
                Log.Error("Failed to load {OverridePath}", relativePath);
                return;
            }

            var overrides = loadedOverrides
                ?? throw new InvalidDataException($"{relativePath} is empty.");

            foreach (var (key, value) in overrides)
            {
                if (value.SkillLevelGroup is not > 0)
                {
                    continue;
                }

                if (!HelperMethods.DataTables.Skills.Data.TryGetValue(key, out var skill))
                {
                    skill = new Skill
                    {
                        Id = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture),
                        Icon = string.Empty,
                        Name = string.Empty,
                        Desc = string.Empty,
                        NameDesign = string.Empty,
                    };
                    HelperMethods.DataTables.Skills.Data.Add(key, skill);
                }

                skill.SkillLevelGroup = value.SkillLevelGroup.Value;
            }

            Log.Information("Loaded {OverridePath}", relativePath);
        }

        private sealed class SkillLevelGroupOverride
        {
            public int? SkillLevelGroup { get; set; }
        }

        private static void LoadBuffOverridesTable(JsonDataFileReads reads)
        {
            LoadBuffOverrideFile(reads, BuffOverridesRelativePath);
        }

        private static void LoadBuffOverrideFile(JsonDataFileReads reads, string relativePath)
        {
            var overridePath = Path.Combine(CombatRuntimePaths.DataDirectory, relativePath);
            if (!reads.TryTake<Dictionary<string, Buff>>(overridePath, out var loadedOverrides))
            {
                Log.Error("Failed to load {OverridePath}", relativePath);
                return;
            }

            var overrides = loadedOverrides
                ?? new Dictionary<string, Buff>();
            foreach (var item in overrides)
            {
                if (!HelperMethods.DataTables.Buffs.Data.TryGetValue(item.Key, out var buff))
                {
                    buff = new Buff
                    {
                        Id = item.Key,
                        Icon = string.Empty,
                        ShowHUDIcon = string.Empty,
                        Name = string.Empty,
                        Desc = string.Empty,
                        NameDesign = string.Empty
                    };
                    HelperMethods.DataTables.Buffs.Data.Add(item.Key, buff);
                }

                if (!string.IsNullOrWhiteSpace(item.Value.Icon))
                {
                    buff.Icon = item.Value.Icon == "-" ? string.Empty : item.Value.Icon;
                }

                if (!string.IsNullOrWhiteSpace(item.Value.ShowHUDIcon))
                {
                    buff.ShowHUDIcon = item.Value.ShowHUDIcon == "-" ? string.Empty : item.Value.ShowHUDIcon;
                }

                if (item.Value.BuffType.HasValue)
                {
                    buff.BuffType = item.Value.BuffType.Value;
                }

                if (item.Value.BuffPriority.HasValue)
                {
                    buff.BuffPriority = item.Value.BuffPriority.Value;
                }

                if (item.Value.FoldInto.HasValue)
                {
                    buff.FoldInto = item.Value.FoldInto.Value;
                }
            }

            ApplyBuffFolds(relativePath);
            Log.Information("Loaded {OverridePath}", relativePath);
        }

        /// <summary>
        /// 畳む手修正(<see cref="Buff.FoldInto"/>)を当てる。全部の手修正を重ねた後の畳み先の側・優先度・絵を、畳む側の行へ写す。
        /// 名前は <see cref="CombatDataCatalog"/> が写す。
        /// 畳み先が表に無い・自分自身・さらに畳まれている行は、エラーログを出して畳まない(静かに効かないので)。
        /// </summary>
        private static void ApplyBuffFolds(string relativePath)
        {
            var buffs = HelperMethods.DataTables.Buffs.Data;
            var folds = new List<(string Key, Buff Buff, Buff Target)>();
            foreach (var (key, buff) in buffs)
            {
                if (buff.FoldInto is not { } targetId)
                {
                    continue;
                }

                var targetKey = targetId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (targetKey == key || !buffs.TryGetValue(targetKey, out var target) || target.FoldInto.HasValue)
                {
                    Log.Error(
                        "{OverridePath}: skipping FoldInto of {Key} because the target {TargetId} is missing, itself, or folded",
                        relativePath, key, targetId);
                    continue;
                }

                folds.Add((key, buff, target));
            }

            foreach (var (key, buff, target) in folds)
            {
                buff.BuffType = target.BuffType;
                buff.BuffPriority = target.BuffPriority;
                buff.Icon = target.Icon;
                buff.ShowHUDIcon = target.ShowHUDIcon;
            }

            // 畳めなかった行は畳まない(番号で引く側が畳み先へ行かないように、印を消す)。
            var foldedKeys = folds.Select(fold => fold.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var (key, buff) in buffs)
            {
                if (buff.FoldInto.HasValue && !foldedKeys.Contains(key))
                {
                    buff.FoldInto = null;
                }
            }
        }
    }
}
