using StarResonanceDps.App.Config;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// エンティティの行(<see cref="EntityListEntry"/>)を組む設定。エンティティリストとエンティティ情報が、それぞれの設定から作って渡す。
/// 設定が変わったときだけ作り直し、同じ設定の間は同じものを渡す(ヘイトの枠のクラスの色の控えは、これが替わったときに作り直す)。
/// </summary>
public sealed class EntityRowSettings
{
    private EntityRowSettings(
        string nameFormatString,
        int healthValueDisplayModeIndex,
        bool healthAsPercentage,
        bool showsCombatState,
        bool showsTargetedPlayer,
        int hateRankCount,
        IReadOnlyDictionary<string, int> classColorIndexes,
        IReadOnlyDictionary<string, List<string>> classColorPalettes)
    {
        NameFormatString = nameFormatString;
        HealthValueDisplayModeIndex = healthValueDisplayModeIndex;
        HealthAsPercentage = healthAsPercentage;
        ShowsCombatState = showsCombatState;
        ShowsTargetedPlayer = showsTargetedPlayer;
        HateRankCount = hateRankCount;
        ClassColorIndexes = classColorIndexes;
        ClassColorPalettes = classColorPalettes;
    }

    /// <summary>名前の書式(差し込む項目は <c>{EntityName}</c> と <c>{Level}</c>)。</summary>
    public string NameFormatString { get; }

    /// <summary>HP 数値の見せ方(<see cref="WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex"/> ならバリア量を括弧で分ける)。</summary>
    public int HealthValueDisplayModeIndex { get; }
    public bool HealthAsPercentage { get; }

    /// <summary>戦闘状態の実体の名前の左に印を出すか。</summary>
    public bool ShowsCombatState { get; }

    /// <summary>ヘイトの枠の左端に今狙っている相手を出すか。</summary>
    public bool ShowsTargetedPlayer { get; }

    /// <summary>ヘイトを何位まで並べるか。全員なら <see cref="int.MaxValue"/>。</summary>
    public int HateRankCount { get; }

    /// <summary>アイコンカラーで選んでいる枠(鍵は <see cref="WidgetConfigDefaults.EntityClassColorKeys"/>)。</summary>
    public IReadOnlyDictionary<string, int> ClassColorIndexes { get; }

    /// <summary>アイコンカラーの色見本(鍵は <see cref="WidgetConfigDefaults.EntityClassColorKeys"/>)。</summary>
    public IReadOnlyDictionary<string, List<string>> ClassColorPalettes { get; }

    public static EntityRowSettings FromEntityList(MeterWidgetSettingsConfig settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new EntityRowSettings(
            settings.PlayerInfoFormatString ?? WidgetConfigDefaults.DefaultEntityInfoFormatString,
            settings.HealthValueDisplayModeIndex,
            settings.EntityHpAsPercentage,
            settings.CombatStateDisplayModeIndex == WidgetConfigDefaults.CombatStateDisplayShowIndex,
            settings.TargetedPlayerDisplayModeIndex == WidgetConfigDefaults.TargetedPlayerDisplayShowIndex,
            WidgetConfigDefaults.GetHateRankDisplayCount(settings.HateRankDisplayModeIndex),
            settings.ClassColorIndexes,
            settings.ClassColorPalettes);
    }

    public static EntityRowSettings FromEntityInfo(EntityInfoWidgetSettingsConfig settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new EntityRowSettings(
            settings.EntityInfoFormatString ?? WidgetConfigDefaults.DefaultEntityInfoFormatString,
            settings.HealthValueDisplayModeIndex,
            false,
            settings.CombatStateDisplayModeIndex == WidgetConfigDefaults.CombatStateDisplayShowIndex,
            settings.TargetedPlayerDisplayModeIndex == WidgetConfigDefaults.TargetedPlayerDisplayShowIndex,
            WidgetConfigDefaults.GetEntityInfoHateRankDisplayCount(settings.HateRankDisplayModeIndex),
            settings.ClassColorIndexes,
            settings.ClassColorPalettes);
    }
}
