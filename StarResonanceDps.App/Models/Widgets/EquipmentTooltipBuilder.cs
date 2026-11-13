// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows.Media;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>装備の TIPS の1行。色が <c>null</c> の行は TIPS の文字のふつうの色。</summary>
/// <param name="IconMask">
/// アイコンの形。属性の行はステータス詳細と同じもの、バフ・一時属性の行はバフの表・一時属性の表の絵(ゲームの TIPS と同じ)。
/// 絵が無ければ <c>null</c>。
/// </param>
public sealed record EquipmentTooltipLine(string Text, Brush? TextBrush, Brush? IconMask = null)
{
    public bool HasTextBrush => TextBrush is not null;

    public bool HasIcon => IconMask is not null;
}

/// <summary>
/// 装備の TIPS の行を作る。基礎の行を全部、続けて進化の行を全部並べる。
///
/// <para>
/// 他人の突破段階と完成度は届かないので、値は範囲で出す。
/// 段階ごとの値を束ね、最小の段階の下限 〜 最大の段階の上限にする。
/// </para>
///
/// <para>
/// 型2 の庫は特化ごとに行が分かれている。持ち主の特化が候補にあればそれ、
/// 無ければ番号の一番小さい候補の行を出す(ゲームの装備の TIPS と同じ選び方)。
/// </para>
///
/// <para>
/// 自分の装備は通信で付いている行と r が届くので、<see cref="BuildExact"/> が具体値で作る。
/// </para>
/// </summary>
public static class EquipmentTooltipBuilder
{
    /// <summary>庫の型。1 は特化で分かれない庫、2 は特化ごとに行が分かれる庫。</summary>
    private const int PlainAttrLibType = 1;
    private const int SchoolAttrLibType = 2;

    /// <summary>効果の種類。1 = 属性、3 = バフ、5 = 一時属性。</summary>
    private const int AttrEffectKind = 1;
    private const int BuffEffectKind = 3;
    private const int TempAttrEffectKind = 5;

    /// <summary>値の書式。1 は万分率(÷100 して %)。</summary>
    private const int PercentFormat = 1;

    /// <summary>部位の指定で「全部位」を表す番号。</summary>
    private const int AnyPart = 0;

    /// <summary>シーズン強度のアイコンを引く、ステータス詳細の行の番号。</summary>
    private const int SeasonStrengthAttrId = 11440;

    private const string SeasonStrengthNameKey = "PlayerStatus_11440";

    /// <summary>今のシーズンより前のシーズン強度の行の色。</summary>
    private static readonly Brush ExpiredSeasonStrengthBrush = CreateFrozenBrush(0xB5, 0xB5, 0xB4);

    /// <param name="equipment">装備の定義。</param>
    /// <param name="player">持ち主。特化を決めるのに使う。</param>
    /// <param name="currentSeasonId">今のシーズン。分からなければ 0。</param>
    public static IReadOnlyList<EquipmentTooltipLine> Build(
        EquipmentDefinition equipment,
        PlayerRosterEntry player,
        int currentSeasonId)
    {
        var school = ResolveSchool(equipment, player);
        var lines = new List<EquipmentTooltipLine>();

        AppendSection(lines, equipment, school, stage => stage.Basic, currentSeasonId);
        AppendSection(lines, equipment, school, stage => stage.Advanced, currentSeasonId);

        return lines;
    }

    /// <summary>
    /// 自分の装備の TIPS。行は通信で届いた「属性庫の行ID → r」で決まり、値は
    /// <c>floor(r × (最大 − 最小) / 100 + 最小)</c>(ゲームの TIPS と同じ切り捨て)。
    /// 並びは 基礎 → 進化 → 改鋳 → レア で、区分の中は行ID の順。
    /// </summary>
    /// <param name="equipment">装備の定義。<paramref name="detail"/> と同じ装備であること(呼び出し側が確かめる)。</param>
    /// <param name="detail">自分の装備の値。</param>
    /// <param name="currentSeasonId">今のシーズン。分からなければ 0。</param>
    public static IReadOnlyList<EquipmentTooltipLine> BuildExact(
        EquipmentDefinition equipment,
        SelfEquipmentSlot detail,
        int currentSeasonId)
    {
        var stage = equipment.Stages[0];
        var lines = new List<EquipmentTooltipLine>();

        AppendExactSection(lines, detail.Set.Basic, detail.Plain.Basic, LibTypeOf(stage.Basic), currentSeasonId);
        AppendExactSection(lines, detail.Set.Advance, detail.Plain.Advance, LibTypeOf(stage.Advanced), currentSeasonId);
        AppendExactSection(lines, detail.Set.Recast, detail.Plain.Recast, LibTypeOf(equipment.Recast), currentSeasonId);
        AppendExactSection(lines, detail.Set.Rare, detail.Plain.Rare, LibTypeOf(equipment.Rare), currentSeasonId);

        return lines;
    }

    /// <summary>
    /// 素の項目で届いた行を引く庫の型。装備の表のその区分の庫の配列の型で、配列が空なら型1(ゲームと同じ)。
    /// 改造でしか付かない区分は装備の表の配列が空になる。
    /// </summary>
    private static int LibTypeOf(IReadOnlyList<int> libs)
    {
        return libs.Count > 0 ? libs[0] : PlainAttrLibType;
    }

    /// <summary>
    /// 区分1つぶんの行を足す。特化ごとの行(<paramref name="setRows"/>)があれば型2 の表で、
    /// 無ければ素の項目を装備の表のその区分の属性庫の型で引く(ゲームと同じ)。表に無い行は <c>(行ID)</c>。
    /// </summary>
    private static void AppendExactSection(
        List<EquipmentTooltipLine> lines,
        IReadOnlyDictionary<int, int> setRows,
        IReadOnlyDictionary<int, int> plainRows,
        int plainLibType,
        int currentSeasonId)
    {
        var (rows, libType) = setRows.Count > 0
            ? (setRows, SchoolAttrLibType)
            : (plainRows, plainLibType);

        foreach (var (rowId, r) in rows.OrderBy(pair => pair.Key))
        {
            var row = CombatDataCatalog.GetEquipmentAttrRow(libType, rowId);
            if (row is null)
            {
                lines.Add(new EquipmentTooltipLine($"({rowId})", null));
                continue;
            }

            foreach (var effect in row.Effects)
            {
                var value = CalculateValue(r, effect);
                lines.Add(CreateLine(new EffectRange(effect.Kind, effect.Id, effect.Format, value, value), currentSeasonId));
            }
        }
    }

    /// <summary>r のときの値。<c>floor(r × (最大 − 最小) / 100 + 最小)</c>。</summary>
    private static long CalculateValue(int r, EquipmentAttrEffect effect)
    {
        return (long)Math.Floor(r * (effect.Max - effect.Min) / 100d) + effect.Min;
    }

    /// <summary>
    /// 型2 の庫の行を選ぶ特化。候補は装備の全段階の型2 の庫に出る特化の全部。
    /// 候補が無ければ 0(型2 の庫を持たない)。
    /// </summary>
    private static int ResolveSchool(EquipmentDefinition equipment, PlayerRosterEntry player)
    {
        var candidates = new SortedSet<int>();
        foreach (var stage in equipment.Stages)
        {
            CollectSchools(stage.Basic, candidates);
            CollectSchools(stage.Advanced, candidates);
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var ownerSchool = player.ClassSpec == PlayerClassSpec.Rank1
            ? CombatDataCatalog.GetEquipmentRank1School(player.ProfessionId)
            : player.SubProfessionId != 0
                ? CombatDataCatalog.GetEquipmentSpecSchool(player.SubProfessionId)
                : 0;

        return candidates.Contains(ownerSchool) ? ownerSchool : candidates.Min;
    }

    private static void CollectSchools(IReadOnlyList<int> libs, SortedSet<int> candidates)
    {
        if (libs.Count == 0 || libs[0] != SchoolAttrLibType)
        {
            return;
        }

        for (var index = 1; index < libs.Count; index++)
        {
            foreach (var row in CombatDataCatalog.GetEquipmentAttrLibRows(SchoolAttrLibType, libs[index]))
            {
                candidates.UnionWith(row.Specs);
            }
        }
    }

    /// <summary>
    /// 区分(基礎か進化)1つぶんの行を足す。同じ効果は段階をまたいで1行に束ねる。
    /// 束ねる鍵は (種類, 番号, その段階の区分の中で何回目か)、並びは最初に出てきた順。
    /// </summary>
    private static void AppendSection(
        List<EquipmentTooltipLine> lines,
        EquipmentDefinition equipment,
        int school,
        Func<EquipmentStage, IReadOnlyList<int>> selectLibs,
        int currentSeasonId)
    {
        var ranges = new List<EffectRange>();
        var rangeIndexes = new Dictionary<(int Kind, int Id, int Occurrence), int>();

        foreach (var stage in equipment.Stages)
        {
            var occurrences = new Dictionary<(int Kind, int Id), int>();
            foreach (var effect in EnumerateEffects(selectLibs(stage), equipment.Part, school))
            {
                var occurrence = occurrences.GetValueOrDefault((effect.Kind, effect.Id));
                occurrences[(effect.Kind, effect.Id)] = occurrence + 1;

                var lower = effect.Min;
                var upper = CalculateUpper(equipment.PerfectUpperLimit, effect);
                var key = (effect.Kind, effect.Id, occurrence);

                if (rangeIndexes.TryGetValue(key, out var index))
                {
                    var range = ranges[index];
                    ranges[index] = range with
                    {
                        Lower = Math.Min(range.Lower, lower),
                        Upper = Math.Max(range.Upper, upper)
                    };
                    continue;
                }

                rangeIndexes[key] = ranges.Count;
                ranges.Add(new EffectRange(effect.Kind, effect.Id, effect.Format, lower, upper));
            }
        }

        foreach (var range in ranges)
        {
            lines.Add(CreateLine(range, currentSeasonId));
        }
    }

    /// <summary>
    /// 庫の並び(先頭が型、以降が庫の番号)から、部位と特化に合う行の効果を表の順に出す。
    /// 合う行がちょうど1つの庫だけを出し、候補が複数ある庫(どれが付くかは個体ごとの抽選)は出さない。
    /// </summary>
    private static IEnumerable<EquipmentAttrEffect> EnumerateEffects(IReadOnlyList<int> libs, int part, int school)
    {
        if (libs.Count == 0)
        {
            yield break;
        }

        var libType = libs[0];
        for (var index = 1; index < libs.Count; index++)
        {
            var rows = CombatDataCatalog.GetEquipmentAttrLibRows(libType, libs[index])
                .Where(row => row.Parts.Contains(part) || row.Parts.Contains(AnyPart))
                .Where(row => libType != SchoolAttrLibType || row.Specs.Contains(school))
                .ToArray();
            if (rows.Length != 1)
            {
                continue;
            }

            foreach (var effect in rows[0].Effects)
            {
                yield return effect;
            }
        }
    }

    /// <summary>完成度が上限のときの値。<c>floor(上限 × (最大 − 最小) / 100 + 最小)</c>。</summary>
    private static long CalculateUpper(int perfectUpperLimit, EquipmentAttrEffect effect)
    {
        return (long)Math.Floor(perfectUpperLimit * (effect.Max - effect.Min) / 100d) + effect.Min;
    }

    private static EquipmentTooltipLine CreateLine(EffectRange range, int currentSeasonId)
    {
        var valueText = FormatRange(range);

        if (range.Kind is AttrEffectKind or TempAttrEffectKind)
        {
            var season = CombatDataCatalog.GetEquipmentStrengthSeason(range.Kind, range.Id);
            if (season != 0)
            {
                return CreateSeasonStrengthLine(season, valueText, currentSeasonId);
            }
        }

        if (range.Kind == AttrEffectKind)
        {
            var nameAttrId = range.Id - range.Id % 10;
            var nameKey = $"PlayerStatus_{nameAttrId}";
            var name = LocalizationManager.Instance.GetString(nameKey);
            if (string.Equals(name, nameKey, StringComparison.Ordinal))
            {
                name = $"({range.Id})";
            }

            return new EquipmentTooltipLine($"{name} {valueText}", null, PlayerStatusEntry.FindRowIconMask(nameAttrId));
        }

        // 一時属性とバフは説明文の {0} にそのままの値、{1} に % の値(値 ÷ 100)を入れる。言語でどちらが入るかが違う。
        // 絵はゲームの TIPS と同じく、バフの表・一時属性の表の絵。
        var text = CombatDataCatalog.GetEquipEffectText(range.Id);
        var iconMask = PlayerStatusEntry.FindIconMaskByName(range.Kind switch
        {
            BuffEffectKind => CombatDataCatalog.GetBuffTableIconName(range.Id),
            TempAttrEffectKind => CombatDataCatalog.GetTempAttrIconName(range.Id),
            _ => string.Empty
        });

        return string.IsNullOrEmpty(text)
            ? new EquipmentTooltipLine($"({range.Id}) {valueText}", null, iconMask)
            : new EquipmentTooltipLine(
                text.Replace("{0}", valueText, StringComparison.Ordinal)
                    .Replace("{1}", FormatRange(range with { Format = PercentFormat }), StringComparison.Ordinal),
                null,
                iconMask);
    }

    /// <summary>
    /// シーズン強度。名前は「シーズン強度」に統一する。
    /// 装備のシーズンが今のシーズンより前なら無効の文言にして灰色で出す(今のシーズンが分からなければ無効にしない)。
    /// </summary>
    private static EquipmentTooltipLine CreateSeasonStrengthLine(int season, string valueText, int currentSeasonId)
    {
        var localization = LocalizationManager.Instance;
        var name = localization.GetString(SeasonStrengthNameKey);
        var iconMask = PlayerStatusEntry.FindRowIconMask(SeasonStrengthAttrId);

        return currentSeasonId > 0 && season < currentSeasonId
            ? new EquipmentTooltipLine(
                localization.Format("PlayerEquipment_ExpiredSeasonStrength", name, valueText),
                ExpiredSeasonStrengthBrush,
                iconMask)
            : new EquipmentTooltipLine($"{name} {valueText}", null, iconMask);
    }

    /// <summary>下限と上限が同じなら1つ、違えば「下限~上限」。</summary>
    private static string FormatRange(EffectRange range)
    {
        var lower = FormatNumber(range.Lower, range.Format);
        return range.Lower == range.Upper
            ? lower
            : $"{lower}~{FormatNumber(range.Upper, range.Format)}";
    }

    /// <summary>万分率は 100 で割って末尾の 0 を落とし % を付ける。桁区切りは入れない。</summary>
    private static string FormatNumber(long value, int format)
    {
        return format == PercentFormat
            ? (value / 100d).ToString("0.##", CultureInfo.CurrentCulture) + "%"
            : value.ToString(CultureInfo.CurrentCulture);
    }

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    /// <summary>段階をまたいで束ねた効果1つ。</summary>
    private readonly record struct EffectRange(int Kind, int Id, int Format, long Lower, long Upper);
}
