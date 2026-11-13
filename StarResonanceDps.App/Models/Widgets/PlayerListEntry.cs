// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// プレイヤーリストの1行。行は位置に結びつき、順が変わると別の人の値に替わる(行の部品は作り直さない)。
/// </summary>
public sealed partial class PlayerListEntry : ObservableObject
{
    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _imagineSkillEntries = [];
    private readonly ObservableCollection<PlayerImagineRoleSkillEntry> _roleSkillEntries = [];
    private const int SelfRoleSlotCount = 4;

    private readonly PlayerInfoFormatCache _displayNameFormat = new();

    /// <summary>他人のロールスキル表示設定。<b>自分には適用しない。</b></summary>
    private IReadOnlyDictionary<string, bool>? _otherRoleSkillVisibility;

    /// <summary>いま映している人の技の枠の値。</summary>
    private PlayerSkillDisplayState _skillState;

    // HPテキストは HP更新(Update)とデバフ更新(RefreshSkillDisplay)の
    // どちらからでも組み立て直す。呼ばれるタイミングが別なので値を控えておく。
    private long _currentHp;
    private long _maxHp;
    private long _currentShield;
    private int _healthValueDisplayModeIndex;

    private PlayerListEntry(long characterId, PlayerSkillDisplayState skillState)
    {
        CharacterId = characterId;
        _skillState = skillState;
        FillSkillSlots(_imagineSkillEntries, 2, isImagine: true);
        // 自分は4枠のまま(装備スロットが分かるので空欄を出せる)。
        // 他人は RefreshSkillDisplay で取得できた数に合わせる。
        FillSkillSlots(_roleSkillEntries, SelfRoleSlotCount, isImagine: false);
        ImagineSkillEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_imagineSkillEntries);
        RoleSkillEntries = new ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry>(_roleSkillEntries);
    }

    public long CharacterId { get; private set; }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> ImagineSkillEntries { get; }

    public ReadOnlyObservableCollection<PlayerImagineRoleSkillEntry> RoleSkillEntries { get; }

    [ObservableProperty]
    private string _professionKey = string.Empty;

    [ObservableProperty]
    private string _classSpecDisplayName = string.Empty;

    /// <summary>有効化しているシーズンタレントの型の根ノードのバフID。0 は不明か無効(<see cref="IsSeasonTalentInactive"/> で分ける)。</summary>
    [ObservableProperty]
    private int _seasonTalentBuffId;

    /// <summary>シーズンタレントの型が無効(どの型も有効化していない)と確定しているか。</summary>
    [ObservableProperty]
    private bool _isSeasonTalentInactive;

    /// <summary>有効化しているシーズンタレントの型の名前(根ノードの名前)。不明か無効なら空。</summary>
    [ObservableProperty]
    private string _seasonTalentName = string.Empty;

    /// <summary>潜在心相晶の絵のツールチップの文字。書式の <c>{Psych}</c> と同じ(型の名前 / 無効 / 不明)。</summary>
    [ObservableProperty]
    private string _seasonTalentDisplayText = string.Empty;

    /// <summary>
    /// 潜在心相晶の枠の中の絵の形。型が分かれば型の絵、不明と表に無い型(絵が無い型)はクラスの不明と同じ絵、無効なら無し(枠だけ)。
    /// どの型がどの絵かは <see cref="SeasonTalentIcons"/> の表が持つ。
    /// </summary>
    [ObservableProperty]
    private Brush? _seasonTalentIconMask;

    /// <summary>潜在心相晶の枠と絵の色。アイコンカラーの設定の、型の絵の行の色(不明と無効は不明の行の色)。</summary>
    [ObservableProperty]
    private SolidColorBrush _seasonTalentBrush = CreateBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));

    /// <summary>潜在心相晶の絵を出すか。設定「潜在心相晶を表示」。オフなら絵も、絵のために空けた幅も無くす。</summary>
    [ObservableProperty]
    private bool _showsSeasonTalent = true;

    /// <summary>
    /// 名前の左に戦闘状態の印を出すか。いま戦闘状態の人のうち、設定「戦闘状態」が表示なら全員、自分のみ表示なら自分だけ。
    /// 出さない行は印の幅も空けない(戦闘中でない行は印の無い今までの見た目)。
    /// </summary>
    [ObservableProperty]
    private bool _showsCombatState;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private double _healthRatio;

    [ObservableProperty]
    private double _shieldVisibleRatio;

    [ObservableProperty]
    private double _shieldOverflowRatio;

    [ObservableProperty]
    private double _shieldOverflowStartRatio = 1d;

    [ObservableProperty]
    private string _healthText = string.Empty;

    [ObservableProperty]
    private bool _isNpc;

    /// <summary>
    /// いま値が供給されているか。false は「キャッシュしか無い」状態で、HPバーを
    /// NPCと同じ灰色にする。AOIかパーティから届き直せば緑へ戻る。
    /// </summary>
    [ObservableProperty]
    private bool _isLive = true;

    [ObservableProperty]
    private bool _isPartyMember;

    [ObservableProperty]
    private string _partyNumberText = string.Empty;

    [ObservableProperty]
    private SolidColorBrush _classBrush = CreateBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

    [ObservableProperty]
    private bool _isPlayerSelectionMenuOpen;

    /// <summary>自分の行か。</summary>
    [ObservableProperty]
    private bool _isSelf;

    /// <summary>自分の行を強調表示するか。設定「自分の表示」が強調表示のときだけ true。</summary>
    [ObservableProperty]
    private bool _isSelfHighlighted;

    public bool IsHealthFull => HealthRatio >= 1d;

    /// <summary>潜在心相晶の枠の中に絵があるか。無ければ枠だけを出す。</summary>
    public bool HasSeasonTalentIcon => SeasonTalentIconMask is not null;

    /// <param name="skillStates">キャラIDからその人の技の枠の値を引く。</param>
    public static PlayerListEntry Create(
        PlayerRosterEntry player,
        MeterWidgetSettingsConfig settings,
        PlayerNameDisplayMode playerNameDisplayMode,
        Func<long, PlayerSkillDisplayState> skillStates)
    {
        var entry = new PlayerListEntry(player.CharacterId, skillStates(player.CharacterId));
        entry.Update(player, settings, playerNameDisplayMode, skillStates);
        entry.ShowPlayerSkills();
        return entry;
    }

    /// <param name="skillStates">キャラIDからその人の技の枠の値を引く。人が替わったときだけ呼ぶ。</param>
    public void Update(
        PlayerRosterEntry player,
        MeterWidgetSettingsConfig settings,
        PlayerNameDisplayMode playerNameDisplayMode,
        Func<long, PlayerSkillDisplayState> skillStates)
    {
        var playerChanged = player.CharacterId != CharacterId;
        if (playerChanged)
        {
            CharacterId = player.CharacterId;
            _skillState = skillStates(player.CharacterId);
        }

        IsSelf = player.IsSelf;
        IsSelfHighlighted = player.IsSelf
            && settings.SelfDisplayModeIndex == WidgetConfigDefaults.DefaultSelfDisplayModeIndex;
        _otherRoleSkillVisibility = settings.OtherRoleSkillVisibility;
        ProfessionKey = PlayerProfession.GetKey(player.ProfessionId, player.ClassSpec);
        ClassSpecDisplayName = PlayerInfoFormatFormatter.GetClassSpecText(player.ClassSpec);
        SeasonTalentBuffId = player.SeasonTalentBuffId;
        IsSeasonTalentInactive = player.IsSeasonTalentInactive;
        SeasonTalentName = player.SeasonTalentBuffId > 0
            ? CombatDataCatalog.GetSeasonTalentName(player.SeasonTalentBuffId)
            : string.Empty;
        SeasonTalentDisplayText = PlayerInfoFormatFormatter.GetSeasonTalentText(
            player.SeasonTalentBuffId, player.IsSeasonTalentInactive);
        SeasonTalentIconMask = SeasonTalentIcons.GetIconMask(player.SeasonTalentBuffId, player.IsSeasonTalentInactive);
        IsNpc = player.IsNpc;
        IsLive = player.IsLive;
        IsPartyMember = player.IsPartyMember;
        PartyNumberText = player.IsPartyMember
            ? player.PartyNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "？"
            : string.Empty;

        DisplayName = PlayerInfoFormatFormatter.Format(
            player,
            settings.PlayerInfoFormatString,
            playerNameDisplayMode,
            _displayNameFormat);

        HealthRatio = GetRatio(player.CurrentHp, player.MaxHp);
        UpdateShieldGeometry(player.CurrentHp, player.MaxHp, player.CurrentShield);
        _currentHp = player.CurrentHp;
        _maxHp = player.MaxHp;
        _currentShield = player.CurrentShield;
        _healthValueDisplayModeIndex = settings.HealthValueDisplayModeIndex;
        ShowsSeasonTalent = settings.ShowSeasonTalent;
        ShowsCombatState = player.IsInCombat
            && (settings.CombatStateDisplayModeIndex == WidgetConfigDefaults.CombatStateDisplayShowIndex
                || (settings.CombatStateDisplayModeIndex == WidgetConfigDefaults.CombatStateDisplaySelfOnlyIndex && player.IsSelf));
        UpdateHealthText();

        var classColor = GetClassColor(settings, ProfessionKey);
        if (ClassBrush.Color != classColor)
        {
            ClassBrush = CreateBrush(classColor);
        }

        var seasonTalentColor = GetClassColor(settings, SeasonTalentIcons.GetColorKey(player.SeasonTalentBuffId));
        if (SeasonTalentBrush.Color != seasonTalentColor)
        {
            SeasonTalentBrush = CreateBrush(seasonTalentColor);
        }

        // 前の人の技の枠を次の描画に残さない。
        if (playerChanged)
        {
            ShowPlayerSkills();
        }
    }

    /// <summary>定期の更新。今の人の値を Core から読んで枠へ書く。</summary>
    public void RefreshSkillDisplay(bool refreshEffects)
    {
        if (_skillState.RefreshSkills(IsSelf, _otherRoleSkillVisibility))
        {
            WriteSkillSlots();
        }

        _skillState.RefreshCooldowns();
        WriteSkillCooldowns();

        if (!refreshEffects)
        {
            return;
        }

        _skillState.RefreshEffects();
        WriteSkillEffects();
    }

    /// <summary>次の <see cref="RefreshSkillDisplay"/> で技の一覧を読み直させる(<see cref="PlayerSkillDisplayState.InvalidateLoadout"/>)。</summary>
    public void InvalidateSkillLoadout()
    {
        _skillState.InvalidateLoadout();
    }

    /// <summary>
    /// 行が映す人が替わった。枠を今の人の値で書き直す。読み済みの人は Core を読まずに控えの値を写し
    /// (値は最後の定期の更新のもの)、まだ読んでいない人だけ読む。
    /// </summary>
    private void ShowPlayerSkills()
    {
        if (_skillState.HasRead)
        {
            _skillState.ApplyRoleSkillVisibilityIfChanged(IsSelf, _otherRoleSkillVisibility);
        }
        else
        {
            _skillState.RefreshSkills(IsSelf, _otherRoleSkillVisibility);
            _skillState.RefreshCooldowns();
            _skillState.RefreshEffects();
        }

        WriteSkillSlots();
        WriteSkillCooldowns();
        WriteSkillEffects();
    }

    private void WriteSkillSlots()
    {
        SyncSkillSlotCount(
            _roleSkillEntries,
            IsSelf ? SelfRoleSlotCount : _skillState.RoleSkills.Count,
            isImagine: false);
        UpdateSkillMetadata(_imagineSkillEntries, _skillState.ImagineSkills);
        UpdateSkillMetadata(_roleSkillEntries, _skillState.RoleSkills);
    }

    private void WriteSkillCooldowns()
    {
        UpdateSkillCooldowns(_imagineSkillEntries, _skillState.ImagineSkills, _skillState.ImagineCooldowns);
        UpdateSkillCooldowns(_roleSkillEntries, _skillState.RoleSkills, _skillState.RoleCooldowns);
    }

    private void WriteSkillEffects()
    {
        UpdateSkillEffects(_imagineSkillEntries, _skillState.Effects);
        UpdateSkillEffects(_roleSkillEntries, _skillState.Effects);
        UpdateHealthText();
    }

    private static void FillSkillSlots(
        ObservableCollection<PlayerImagineRoleSkillEntry> entries,
        int count,
        bool isImagine)
    {
        for (var index = 0; index < count; index++)
        {
            entries.Add(new PlayerImagineRoleSkillEntry(isImagine));
        }
    }

    /// <summary>
    /// 出す枠の数を <paramref name="count"/> に合わせる。足りなければ末尾に足し、余った枠は外さずに隠す
    /// (人が替わるたびに枠の部品を作り直さない)。
    /// </summary>
    private static void SyncSkillSlotCount(
        ObservableCollection<PlayerImagineRoleSkillEntry> entries,
        int count,
        bool isImagine)
    {
        while (entries.Count < count)
        {
            entries.Add(new PlayerImagineRoleSkillEntry(isImagine));
        }

        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].IsSlotInUse = index < count;
        }
    }

    private static void UpdateSkillMetadata(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyList<PlayerCooldownSkillSnapshot?> snapshots)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].UpdateSkill(index < snapshots.Count ? snapshots[index] : null);
            // 空枠は null で届くので、一覧の長さより後ろの枠だけが不明。
            entries[index].IsSlotKnown = index < snapshots.Count;
        }
    }

    private static void UpdateSkillCooldowns(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyList<PlayerCooldownSkillSnapshot?> snapshots,
        IReadOnlyList<SkillCooldownDisplayState> cooldowns)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].UpdateCooldown(
                index < snapshots.Count ? snapshots[index] : null,
                index < cooldowns.Count ? cooldowns[index] : default);
        }
    }

    private static void UpdateSkillEffects(
        IReadOnlyList<PlayerImagineRoleSkillEntry> entries,
        IReadOnlyDictionary<int, PlayerSkillEffectSnapshot> effectsBySkillId)
    {
        foreach (var entry in entries)
        {
            if (entry.SkillId > 0
                && effectsBySkillId.TryGetValue(entry.SkillId, out var effects))
            {
                entry.UpdateEffects(effects.Buff, effects.Debuff);
            }
            else
            {
                entry.UpdateEffects(null, null);
            }
        }
    }

    partial void OnHealthRatioChanged(double value)
    {
        OnPropertyChanged(nameof(IsHealthFull));
    }

    partial void OnSeasonTalentIconMaskChanged(Brush? value)
    {
        OnPropertyChanged(nameof(HasSeasonTalentIcon));
    }

    private void UpdateShieldGeometry(long currentHp, long maxHp, long currentShield)
    {
        if (maxHp <= 0)
        {
            ShieldVisibleRatio = 0d;
            ShieldOverflowRatio = 0d;
            ShieldOverflowStartRatio = 1d;
            return;
        }

        var displayedHp = Math.Clamp(currentHp, 0L, maxHp);
        var shield = Math.Max(currentShield, 0L);
        var shieldGaugeCapacity = (double)maxHp * 2d;
        var availableShieldCapacity = (maxHp - displayedHp) * 2d;
        var foldsEntireShield = shield > availableShieldCapacity;

        var visibleShield = foldsEntireShield
            ? 0d
            : shield;

        var overflowShield = foldsEntireShield
            ? Math.Min(shield, shieldGaugeCapacity)
            : 0d;

        ShieldVisibleRatio = visibleShield / shieldGaugeCapacity;
        ShieldOverflowRatio = overflowShield / shieldGaugeCapacity;
        ShieldOverflowStartRatio = 1d - ShieldOverflowRatio;
    }

    private static double GetRatio(long currentValue, long maxValue)
    {
        if (maxValue <= 0)
        {
            return 0d;
        }

        return Math.Clamp(currentValue / (double)maxValue, 0d, 1d);
    }

    /// <summary>
    /// HPバーのテキスト。<b>HPが優先。</b>
    ///
    /// <para>
    /// 死んでいて、かつ蘇生不可デバフの残り秒が取れているときだけ差し替える。
    /// 残り秒が取れないのはデバフの持続が分からない状態なので、
    /// そのまま出すと切れた後も表示が残る。その場合はHPのまま。
    /// </para>
    /// </summary>
    private void UpdateHealthText()
    {
        if (_currentHp <= 0 && _skillState.ReviveBlockSeconds is { } seconds)
        {
            var roundedSeconds = Math.Max(0, (int)Math.Ceiling(seconds));
            HealthText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                LocalizationManager.Instance.GetString("PlayerList_ReviveBlocked"),
                roundedSeconds);
            return;
        }

        HealthText = FormatHealthText(
            _currentHp,
            _maxHp,
            _currentShield,
            _healthValueDisplayModeIndex);
    }

    private static string FormatHealthText(
        long currentHp,
        long maxHp,
        long currentShield,
        int displayModeIndex)
    {
        var shield = Math.Max(currentShield, 0L);
        // バリア量個別表示でも、バリアが無いときは括弧を出さない。
        return displayModeIndex == WidgetConfigDefaults.SeparateShieldHealthValueDisplayModeIndex
            ? shield > 0
                ? $"{currentHp}({shield})/{maxHp}"
                : $"{currentHp}/{maxHp}"
            : $"{AddSaturating(currentHp, shield)}/{maxHp}";
    }

    private static long AddSaturating(long value, long nonNegativeAddition)
    {
        return nonNegativeAddition > 0 && value > long.MaxValue - nonNegativeAddition
            ? long.MaxValue
            : value + nonNegativeAddition;
    }

    /// <summary>プレイヤーリストのクラス色。</summary>
    internal static Color GetClassColor(MeterWidgetSettingsConfig classColors, string professionKey)
    {
        var palette = classColors.ClassColorPalettes.TryGetValue(professionKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultClassColors(WidgetKind.PlayerList, professionKey);
        var selectedIndex = classColors.ClassColorIndexes.TryGetValue(professionKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        var selectedColor = palette.Count == 0
            ? "#A8A8A8"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        return ColorUtilities.TryParseHex(selectedColor, out var color)
            ? color
            : Color.FromRgb(0xA8, 0xA8, 0xA8);
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
