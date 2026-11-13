// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// 装備詳細の1行(部位1つ)。左に部位のアイコンと書式で組んだ文字、右寄せに装備レベル。
/// 装備名の文字の色は、設定のテキストカラーで品質ごとに選んだ色で、TIPS に基礎と進化のステータスを出す。
/// </summary>
public sealed class PlayerEquipmentSlotEntry
{
    /// <summary>何も装備していない部位の装備ID。</summary>
    private const int NoEquipmentId = 0;

    /// <summary>行を作る部位。装備の表の部位(200〜210)を番号順に全部。</summary>
    private static readonly int[] AllSlots = [.. Enumerable.Range(200, 11)];

    /// <summary>部位のアイコンの形。部位ごとに1つ作って使い回す。</summary>
    private static readonly Dictionary<int, ImageBrush> IconMasks = [];

    private PlayerEquipmentSlotEntry(
        int slot,
        ImageBrush iconMask,
        string text,
        string levelText,
        Brush? textBrush,
        IReadOnlyList<EquipmentTooltipLine> toolTipLines)
    {
        Slot = slot;
        IconMask = iconMask;
        Text = text;
        LevelText = levelText;
        TextBrush = textBrush;
        ToolTipLines = toolTipLines;
    }

    public int Slot { get; }

    /// <summary>部位のアイコンの形。白い塗りをこの形で抜く。</summary>
    public ImageBrush IconMask { get; }

    public string Text { get; }

    /// <summary>行の右寄せの装備レベル。表に無い装備・装備なし・不明は空。</summary>
    public string LevelText { get; }

    /// <summary>品質の色。<c>null</c> はふつうの色(表に無い装備・装備なし・不明)。</summary>
    public Brush? TextBrush { get; }

    public bool HasTextBrush => TextBrush is not null;

    public IReadOnlyList<EquipmentTooltipLine> ToolTipLines { get; }

    public bool HasToolTip => ToolTipLines.Count > 0;

    /// <summary>
    /// 部位 200〜210 の行を部位の番号順に全部作る。届いた一覧に無い部位は、行を出したまま「不明」にする。
    /// 同じ部位が2つ以上あれば最初の1つを使う。
    /// </summary>
    /// <param name="currentSeasonId">今のシーズン。シーズン強度の有効・無効を決める。</param>
    /// <param name="infoFormat">行の書式。</param>
    /// <param name="qualityBrushes">品質の番号ごとの色(設定のテキストカラー)。</param>
    public static IReadOnlyList<PlayerEquipmentSlotEntry> CreateAll(
        PlayerEquipmentData equipmentData,
        PlayerRosterEntry player,
        int currentSeasonId,
        string? infoFormat,
        IReadOnlyDictionary<int, Brush> qualityBrushes)
    {
        ArgumentNullException.ThrowIfNull(equipmentData);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(qualityBrushes);

        var itemsBySlot = new Dictionary<int, PlayerEquipmentItem>();
        foreach (var item in equipmentData.Items)
        {
            itemsBySlot.TryAdd(item.Slot, item);
        }

        return AllSlots
            .Select(slot => itemsBySlot.TryGetValue(slot, out var item)
                ? Create(item, player, currentSeasonId, infoFormat, qualityBrushes)
                : CreateUnknown(slot))
            .ToArray();
    }

    /// <summary>届いた一覧に無い部位の行。部位のアイコンと「不明」をふつうの色で出し、装備レベルと TIPS は付けない。</summary>
    private static PlayerEquipmentSlotEntry CreateUnknown(int slot)
    {
        return new PlayerEquipmentSlotEntry(
            slot,
            GetIconMask(slot),
            LocalizationManager.Instance.GetString("PlayerEquipment_Unknown"),
            string.Empty,
            null,
            Array.Empty<EquipmentTooltipLine>());
    }

    /// <summary>
    /// 届いた一覧にある部位の1行を作る(無い部位は <see cref="CreateUnknown"/>)。装備なしの部位は「装備なし」、
    /// 表に無い装備は <c>(装備ID)</c> をふつうの色で出し、どちらも装備レベルと TIPS は付けない。自分の装備は具体値、他人の装備は範囲で出す。
    /// </summary>
    private static PlayerEquipmentSlotEntry Create(
        PlayerEquipmentItem item,
        PlayerRosterEntry player,
        int currentSeasonId,
        string? infoFormat,
        IReadOnlyDictionary<int, Brush> qualityBrushes)
    {
        var iconMask = GetIconMask(item.Slot);

        if (item.EquipmentId == NoEquipmentId)
        {
            return new PlayerEquipmentSlotEntry(
                item.Slot,
                iconMask,
                LocalizationManager.Instance.GetString("PlayerEquipment_NoEquipment"),
                string.Empty,
                null,
                Array.Empty<EquipmentTooltipLine>());
        }

        var equipment = CombatDataCatalog.GetEquipment(item.EquipmentId);
        if (equipment is null)
        {
            return new PlayerEquipmentSlotEntry(
                item.Slot,
                iconMask,
                $"({item.EquipmentId})",
                string.Empty,
                null,
                Array.Empty<EquipmentTooltipLine>());
        }

        if (TryGetSelfDetail(item, equipment, player, out var detail))
        {
            return new PlayerEquipmentSlotEntry(
                item.Slot,
                iconMask,
                EquipmentInfoFormatFormatter.Format(equipment, infoFormat, detail.BreakThroughTime),
                EquipmentInfoFormatFormatter.FormatLevel(equipment, detail.BreakThroughTime),
                qualityBrushes.GetValueOrDefault(equipment.Quality),
                EquipmentTooltipBuilder.BuildExact(equipment, detail, currentSeasonId));
        }

        return new PlayerEquipmentSlotEntry(
            item.Slot,
            iconMask,
            EquipmentInfoFormatFormatter.Format(equipment, infoFormat),
            EquipmentInfoFormatFormatter.FormatLevel(equipment),
            qualityBrushes.GetValueOrDefault(equipment.Quality),
            EquipmentTooltipBuilder.Build(equipment, player, currentSeasonId));
    }

    /// <summary>
    /// 自分の装備の値(具体値の元)。自分の行で、置き場のその部位の装備ID が行の装備ID と同じで、
    /// 突破の段階が装備の段階に収まるときだけ使う。食い違うとき(途中起動で全体コンテナがまだ来ていない など)は
    /// 範囲で出す(範囲は「下限~上限」の形なので具体値と見分けがつく)。
    /// </summary>
    private static bool TryGetSelfDetail(
        PlayerEquipmentItem item,
        EquipmentDefinition equipment,
        PlayerRosterEntry player,
        out SelfEquipmentSlot detail)
    {
        if (player.IsSelf
            && SelfEquipmentStore.TryGetSlot(item.Slot, out detail)
            && detail.EquipmentId == item.EquipmentId
            && detail.BreakThroughTime >= 0
            && detail.BreakThroughTime < equipment.Stages.Count)
        {
            return true;
        }

        detail = null!;
        return false;
    }

    /// <summary>部位のアイコン(<c>Icon.EquipSlot.部位</c>)。部位 200〜210 は全部ある。</summary>
    private static ImageBrush GetIconMask(int slot)
    {
        if (IconMasks.TryGetValue(slot, out var mask))
        {
            return mask;
        }

        mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.EquipSlot.{slot}"))
        {
            Stretch = Stretch.Uniform
        };
        mask.Freeze();

        IconMasks[slot] = mask;
        return mask;
    }
}
